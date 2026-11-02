import { useEffect, useMemo, useRef, useState, type DragEvent } from 'react'
import {
  Sparkles, X, StopCircle, Loader2, ChevronDown,
  RotateCcw, AlertTriangle, ThumbsUp, ThumbsDown, Wand2, RefreshCcw, PencilLine, ImagePlus,
} from 'lucide-react'
import {
  streamAi, submitAiFeedback, updateAiFeedback,
  type AiChunk, type AssistantMessage,
} from '../../services/aiAssistantService'
import { toast } from '../../store/toastStore'
import { useEntitlementStore } from '../../store/entitlementStore'
import { useAiStore } from '../../store/aiStore'
import { openUpgradeModal } from '../../store/upgradeModalStore'
import { aiImagesPerMessage } from '../../services/entitlementService'
import { useImageAttachments } from '../../hooks/useImageAttachments'
import { MAX_IMAGES_TECHNICAL, filesFrom, type PreparedImage } from '../../utils/imageAttachment'
import { extractApplicableBlock, computeApplyTarget, type ApplyTarget } from '../../utils/xsltApply'
import AiApplyDialog from './AiApplyDialog'
import MarkdownMessage from './MarkdownMessage'
import ChatComposer from './ChatComposer'
import ChatImageGrid from './ChatImageGrid'

/** Dışarıdan tetiklenen soru (ör. Problems panelinden "AI'ya sor"). */
export interface AiPrompt {
  /**
   * Monotonik artan. Aynı metin tekrar sorulsa bile nonce değiştiği için yeni istek
   * tetiklenir; metin/nesne karşılaştırması ikinci tıklamayı yutardı.
   */
  nonce: number
  text: string
}

interface Props {
  xslt: string
  xml: string | null
  xmlSelection?: string
  xmlCursorLine?: number
  xsltSelection?: string
  xsltCursorLine?: number
  /** Dışarıdan gelen soru. Panel REMOUNT EDİLMEZ → mevcut sohbet korunur. */
  prompt?: AiPrompt | null
  xmlDeclarationMissing?: boolean
  /** AI önerisini editördeki XSLT'ye uygular (TAM doküman). Yoksa "Uygula" gösterilmez. */
  onApplyXslt?: (newDoc: string) => void
  /** Yükseltme modalındaki "Pro'ya Geç" sayfadan çıkarır; kaydedilmemiş iş varsa çağıran sorar. */
  confirmLeave?: () => boolean
  onClose: () => void
}

// ─── Chat mesaj tipi ─────────────────────────────────────────────────────────

type ChatStatus = 'streaming' | 'done' | 'cancelled' | 'error'

interface ChatMessage {
  id: number
  role: 'user' | 'assistant'
  content: string
  /** Balonun yaşam döngüsü. Kullanıcı mesajları daima 'done'. */
  status: ChatStatus
  /** Hata metni — içeriğe '⚠️' önekiyle gömülmez, kısmi yanıt korunur. */
  errorMessage?: string
  meta?: { provider?: string; model?: string; ms?: number }
  /** Geri bildirim durumu (per-mesaj). */
  feedback?: 'up' | 'down'
  feedbackId?: string
  applied?: boolean
  /** Kullanıcı mesajına eklenen ekran görüntüleri (yalnız o turda modele gider). */
  images?: PreparedImage[]
}

/**
 * Görselli sorunun geri bildirim kaydına eklenen işaret (backend AiFeedbackMarkers.ImageAttached ile AYNI).
 * Boş metinli soruda yer tutucu görevi görür; sunucu bu kayıtların global örneğe terfisini engeller.
 */
const IMAGE_MARKER = '[ekran görüntüsü ekli]'

/**
 * Modele yalnız TAMAMLANMIŞ ve dolu turlar gider. Hata/iptal/boş balonlar ve cevabı
 * hataya düşmüş "öksüz" kullanıcı soruları dışlanır — aksi halde hem bağlam kirlenir
 * hem de arka arkaya iki `user` turu bazı sağlayıcılarda 400 üretir.
 */
function toHistory(msgs: ChatMessage[]): AssistantMessage[] {
  const out: AssistantMessage[] = []
  for (let i = 0; i < msgs.length; i++) {
    const q = msgs[i]
    if (q.role !== 'user') continue
    const a = msgs[i + 1]
    if (a?.role === 'assistant' && a.status === 'done' && a.content.trim()) {
      out.push({ role: 'user', content: q.content, imageCount: q.images?.length || undefined })
      out.push({ role: 'assistant', content: a.content })
      i++
    }
  }
  return out
}

/** Mesajlara devredilmiş görsel URL'lerini serbest bırakır ("Tekrar ekle" kopyaları aynı URL'i paylaşır). */
function revokeMessageImages(msgs: ChatMessage[]) {
  const urls = new Set(msgs.flatMap(m => m.images?.map(i => i.previewUrl) ?? []))
  urls.forEach(url => URL.revokeObjectURL(url))
}

// ─── XML bağlam kırpma ────────────────────────────────────────────────────────

const CURSOR_WINDOW_LINES = 100

function getEffectiveXml(
  xml: string | null,
  xmlSelection?: string,
  xmlCursorLine?: number,
): string | null {
  if (!xml) return null
  if (xmlSelection?.trim()) return xmlSelection

  if (xmlCursorLine !== undefined) {
    const lines = xml.split('\n')
    if (lines.length <= CURSOR_WINDOW_LINES * 2) return xml
    const start = Math.max(0, xmlCursorLine - 1 - CURSOR_WINDOW_LINES)
    const end = Math.min(lines.length, xmlCursorLine - 1 + CURSOR_WINDOW_LINES)
    const pre = start > 0 ? `<!-- ... ${start} satır kırpıldı ... -->\n` : ''
    const post = end < lines.length ? `\n<!-- ... ${lines.length - end} satır kırpıldı ... -->` : ''
    return pre + lines.slice(start, end).join('\n') + post
  }

  return xml
}

// ─── Hazır komutlar ───────────────────────────────────────────────────────────

const PROMPT_EXPLAIN_SELECTION = 'Seçili kodu açıkla: ne yaptığını ve UBL-TR belgesindeki hangi alanlara dokunduğunu kısaca anlat.'

const chipCls =
  'xc-focus h-[26px] whitespace-nowrap rounded-full border border-field-line bg-ui-card px-2.5 text-[11.5px] text-tx-2 transition-colors enabled:hover:border-ui-soft-line enabled:hover:bg-ui-soft enabled:hover:text-ui-ink disabled:opacity-45'

// ─── Ana bileşen ──────────────────────────────────────────────────────────────

let msgIdCounter = 0

/** Dibe bu mesafeden yakınsa "takip" modunda sayılır. */
const NEAR_BOTTOM_PX = 64

export default function AiAssistantPanel({
  xslt, xml, xmlSelection, xmlCursorLine,
  xsltSelection, xsltCursorLine,
  prompt,
  xmlDeclarationMissing,
  onApplyXslt,
  confirmLeave,
  onClose,
}: Props) {
  const [messages, setMessages] = useState<ChatMessage[]>([])
  const [input, setInput] = useState('')
  const [streaming, setStreaming] = useState(false)
  const [lastMeta, setLastMeta] = useState<{ provider?: string; model?: string; ms?: number } | null>(null)
  // Açık "Uygula" diyaloğu (hangi mesaj + uygulanacak blok).
  const [applyState, setApplyState] = useState<{ messageId: number; block: string } | null>(null)
  // Hedef güncel metinden hesaplanır: diyalog açıkken metin değişirse (compose) kabul eski metni geri yazmaz.
  const applyTarget = useMemo<ApplyTarget | null>(
    () => (applyState ? computeApplyTarget(xslt, applyState.block, xsltSelection) : null),
    [applyState, xslt, xsltSelection],
  )
  const [showJump, setShowJump] = useState(false)
  const [dragOver, setDragOver] = useState(false)

  const visionEnabled = useAiStore(s => s.vision)
  const entitlements = useEntitlementStore(s => s.entitlements)
  const maxImages = aiImagesPerMessage(entitlements, MAX_IMAGES_TECHNICAL)
  const attach = useImageAttachments(maxImages)

  const abortRef = useRef<AbortController | null>(null)
  const inputRef = useRef<HTMLTextAreaElement>(null)
  /**
   * `streaming` state'i async closure'da bayat kalır; aynı tick'te iki istek gelirse
   * ikisi de `false` görüp paralel akış başlatırdı. Bu ref senkron kilittir.
   */
  const streamingRef = useRef(false)
  /** İptali `finally`'ye bildirir (status'ü orada tek yerden yazıyoruz). */
  const cancelledRef = useRef(false)
  const lastPromptNonceRef = useRef(0)

  // Scroll takibi
  const listRef = useRef<HTMLDivElement>(null)
  const contentRef = useRef<HTMLDivElement>(null)
  const stickRef = useRef(true)
  const prevLenRef = useRef(0)
  const dragDepthRef = useRef(0)
  // Mesajlara devredilen görsel URL'leri — yeni sohbette/unmount'ta revoke edilir.
  const messagesRef = useRef<ChatMessage[]>([])
  messagesRef.current = messages
  // Akış başladıktan sonra metin kirlenmiş olabilir: modal son durumu sorar.
  const confirmLeaveRef = useRef(confirmLeave)
  confirmLeaveRef.current = confirmLeave
  const guardedLeave = () => confirmLeaveRef.current?.() ?? true

  useEffect(() => () => revokeMessageImages(messagesRef.current), [])

  // Bir asistan mesajının hemen öncesindeki kullanıcı sorusunu bulur (feedback bağlamı).
  function precedingUser(assistantId: number): ChatMessage | null {
    const idx = messages.findIndex(m => m.id === assistantId)
    for (let i = idx - 1; i >= 0; i--) {
      if (messages[i].role === 'user') return messages[i]
    }
    return null
  }

  function precedingUserMessage(assistantId: number): string {
    const q = precedingUser(assistantId)
    if (!q) return ''
    if (!q.images?.length) return q.content
    return q.content.trim() ? `${q.content.trim()} ${IMAGE_MARKER}` : IMAGE_MARKER
  }

  function patchMessage(id: number, patch: Partial<ChatMessage>) {
    setMessages(prev => prev.map(m => (m.id === id ? { ...m, ...patch } : m)))
  }

  // ── Uygula ──────────────────────────────────────────────────────────────────
  function openApply(msg: ChatMessage) {
    const block = extractApplicableBlock(msg.content)
    if (!block) return
    setApplyState({ messageId: msg.id, block })
  }

  function handleApplyAccept(newDoc: string) {
    if (!applyState) return
    const msg = messages.find(m => m.id === applyState.messageId)
    onApplyXslt?.(newDoc)
    // "Uygula" örtük pozitif geri bildirimdir.
    if (msg && msg.feedback !== 'up') {
      void sendFeedback(applyState.messageId, 'up', true)
    } else if (msg) {
      patchMessage(msg.id, { applied: true })
    }
    setApplyState(null)
    toast.success('Değişiklik editöre uygulandı.', { durationMs: 2500 })
  }

  // ── Geri bildirim ─────────────────────────────────────────────────────────────
  async function sendFeedback(messageId: number, rating: 'up' | 'down', applied?: boolean) {
    const msg = messages.find(m => m.id === messageId)
    if (!msg) return
    const prevFeedback = msg.feedback
    const nextApplied = applied ?? msg.applied ?? false
    // İyimser UI güncellemesi.
    patchMessage(messageId, { feedback: rating, applied: nextApplied })
    try {
      const apiRating = rating === 'up' ? 'positive' : 'negative'
      if (msg.feedbackId) {
        await updateAiFeedback(msg.feedbackId, { rating: apiRating, applied: nextApplied })
      } else {
        const { id } = await submitAiFeedback({
          rating: apiRating,
          userMessage: precedingUserMessage(messageId),
          assistantAnswer: msg.content,
          applied: nextApplied,
        })
        patchMessage(messageId, { feedbackId: id })
      }
    } catch (e) {
      // Başarısızsa görsel durumu geri al, kullanıcıyı bilgilendir.
      patchMessage(messageId, { feedback: prevFeedback })
      const status = (e as { response?: { status?: number } })?.response?.status
      toast.error(status === 401 ? 'Oturum gerekiyor.' : 'Geri bildirim gönderilemedi.')
    }
  }

  // "İşe yaramadı" → farklı yaklaşım iste (başarısız cevap zaten history'de).
  // Soru görselliyse görseller de yeniden gönderilir (geçmişte yalnız yer tutucu kalır).
  function retryDifferent(assistantId: number) {
    const q = precedingUser(assistantId)
    if (!q || (!q.content.trim() && !q.images?.length)) return
    const base = 'Önceki yanıt işe yaramadı, aynı çözümü tekrarlama. Farklı bir yaklaşım dene'
    runChat(q.content.trim() ? `${base}: ${q.content}` : `${base}.`, { images: q.images })
  }

  // "İşe yaramadı" → detay ver: input'a ön-metin koy ve odaklan.
  function askForDetail() {
    setInput('Şu yüzden işe yaramadı: ')
    setTimeout(() => inputRef.current?.focus(), 0)
  }

  // ── Scroll takibi ───────────────────────────────────────────────────────────
  // Kullanıcı dibe yakınsa takip et; yukarı kaydırdıysa DOKUNMA. Önceki sürümde her
  // token'da scrollIntoView({behavior:'smooth'}) çağrılıyordu — hem atalarını da
  // kaydırıyor (panel resizable panel içinde) hem de token başına bir animasyon
  // kuyruğa alıyordu; streaming sırasında yukarı kaydırmak imkânsızdı.
  function handleScroll() {
    const el = listRef.current
    if (!el) return
    const near = el.scrollHeight - el.scrollTop - el.clientHeight <= NEAR_BOTTOM_PX
    stickRef.current = near
    setShowJump(!near)
  }

  function scrollToBottom(smooth = false) {
    const el = listRef.current
    if (!el) return
    stickRef.current = true
    setShowJump(false)
    if (smooth) el.scrollTo({ top: el.scrollHeight, behavior: 'smooth' })
    else el.scrollTop = el.scrollHeight
  }

  useEffect(() => {
    const el = listRef.current
    if (!el || !stickRef.current) return
    const isNewMessage = messages.length !== prevLenRef.current
    prevLenRef.current = messages.length
    // Yeni mesaj: yumuşak. Akış sırasında: anlık — animasyonlar üst üste binmesin.
    if (isNewMessage && !streamingRef.current) el.scrollTo({ top: el.scrollHeight, behavior: 'smooth' })
    else el.scrollTop = el.scrollHeight
  }, [messages])

  // Monaco kod bloğu mount olduktan sonra yükseklik değiştirir (loading placeholder →
  // editör); yukarıdaki efekt o büyümeden önce çalıştığı için dip kaçardı.
  useEffect(() => {
    const content = contentRef.current
    const el = listRef.current
    if (!content || !el) return
    let raf = 0
    const ro = new ResizeObserver(() => {
      if (!stickRef.current) return
      cancelAnimationFrame(raf)
      raf = requestAnimationFrame(() => { el.scrollTop = el.scrollHeight })
    })
    ro.observe(content)
    return () => { cancelAnimationFrame(raf); ro.disconnect() }
  }, [])

  // ── Dışarıdan gelen soru (Problems panelinden "AI'ya sor") ──────────────────
  useEffect(() => {
    if (!prompt || prompt.nonce <= lastPromptNonceRef.current) return
    lastPromptNonceRef.current = prompt.nonce
    void runChat(prompt.text, { interrupt: true })
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [prompt])

  function cancel() {
    // Aktif akış yoksa BAYRAĞI KİRLETME: handleNewChat() de cancel() çağırıyor ve
    // takılı kalan bayrak, `done` chunk'ı göndermeden biten bir sonraki yanıtı
    // yanlışlıkla "iptal edildi" damgalayıp aksiyon satırını gizlerdi.
    if (!abortRef.current) return
    // Status'ü `finally` yazar; burada yalnız niyeti işaretliyoruz.
    cancelledRef.current = true
    abortRef.current.abort()
  }

  async function runChat(message: string, opts?: { interrupt?: boolean; images?: PreparedImage[] }) {
    const images = opts?.images?.length ? opts.images : undefined
    if (!message.trim() && !images) return
    if (streamingRef.current) {
      // Manuel gönderim yolunda input/düğme zaten disabled; bu guard savunma amaçlı.
      if (!opts?.interrupt) return
      // "AI'ya sor" kasıtlı bir kullanıcı eylemi: mevcut akışı iptal et. İptal edilen
      // yanıt 'cancelled' olarak ekranda kalır, hiçbir şey kaybolmaz.
      cancel()
    }
    streamingRef.current = true

    const userMsg: ChatMessage = { id: ++msgIdCounter, role: 'user', content: message, status: 'done', images }
    const assistantId = ++msgIdCounter

    setMessages(prev => [...prev, userMsg])
    setInput('')
    setStreaming(true)

    const effectiveXml = getEffectiveXml(xml, xmlSelection, xmlCursorLine)

    // Yalnız tamamlanmış turlar; hata/iptal balonları ve öksüz sorular dışlanır.
    const historyForRequest = toHistory(messages)

    const ac = new AbortController()
    abortRef.current = ac

    // Boş assistant mesajı ekle (streaming için)
    setMessages(prev => [...prev, { id: assistantId, role: 'assistant', content: '', status: 'streaming' }])

    let finalMeta: { provider?: string; model?: string; ms?: number } | null = null

    try {
      await streamAi(
        'assistant',
        {
          xslt,
          xml: effectiveXml,
          xmlSelection: xmlSelection?.trim() || undefined,
          xsltSelection: xsltSelection?.trim() || undefined,
          xsltCursorLine,
          history: historyForRequest,
          message,
          images: images?.map(i => ({ mimeType: i.mimeType, data: i.base64 })),
        },
        (chunk: AiChunk) => {
          if (chunk.type === 'delta' && chunk.text) {
            setMessages(prev => prev.map(m =>
              m.id === assistantId ? { ...m, content: m.content + chunk.text } : m
            ))
          } else if (chunk.type === 'done') {
            finalMeta = { provider: chunk.provider, model: chunk.model, ms: chunk.ms }
            setLastMeta(finalMeta)
            setMessages(prev => prev.map(m =>
              m.id === assistantId ? { ...m, meta: finalMeta ?? undefined, status: 'done' } : m
            ))
          } else if (chunk.type === 'error') {
            const msg = chunk.message ?? 'Bilinmeyen hata.'
            // `content`'e DOKUNMA: o ana kadar akmış kısmi yanıt korunur, hata ayrı
            // bir alanda taşınır (eski kod içeriği '⚠️ …' ile eziyordu).
            setMessages(prev => prev.map(m =>
              m.id === assistantId ? { ...m, status: 'error', errorMessage: msg } : m
            ))
            if (chunk.code === 'http_402' && chunk.reason === 'image_count') {
              // Free: mesaj başına 1 ekran görüntüsü → Pro'ya yönlendir.
              openUpgradeModal({ title: 'Ekran görüntüsü limiti', message: msg, confirmLeave: guardedLeave })
            } else if (chunk.code === 'http_402') {
              // Free kullanıcı günlük 1 soru hakkını doldurdu → Pro'ya yönlendir.
              openUpgradeModal({ title: 'AI soru hakkınız doldu', message: msg, confirmLeave: guardedLeave })
            } else if (chunk.code === 'http_429' && chunk.reason === 'vision_rate_limited') {
              toast.warning(msg, { title: 'Ekran görüntüsü' })
            } else if (chunk.code === 'http_429') {
              toast.warning(msg, { title: 'Günlük AI limiti' })
            } else if (chunk.reason?.startsWith('image_') || chunk.reason === 'vision_unavailable') {
              toast.error(msg, { title: 'Ekran görüntüsü gönderilemedi' })
              // Admin kapattıysa ataç butonu da kapansın.
              if (chunk.reason === 'vision_unavailable') useAiStore.getState().refresh()
            } else if (
              chunk.code === 'provider_unavailable' ||
              chunk.code?.startsWith('ollama_') ||
              chunk.code?.startsWith('gemini_')
            ) {
              toast.error(msg, { title: 'AI asistan kullanılamıyor' })
            }
          }
        },
        ac.signal,
      )
    } catch (e) {
      if ((e as { name?: string }).name !== 'AbortError') {
        const msg = (e as Error).message ?? 'AI isteği başarısız.'
        setMessages(prev => prev.map(m =>
          m.id === assistantId ? { ...m, status: 'error', errorMessage: msg } : m
        ))
      }
      // AbortError: status'ü aşağıdaki emniyet ağı 'cancelled' yapar.
    } finally {
      const wasCancelled = cancelledRef.current
      // YARIŞ KORUMASI: `cancel()` ile yeni bir akış başlatıldığında bu `finally`
      // asenkron çalışır ve yeni akışın setStreaming(true)'sunu ezebilirdi. Temizliği
      // yalnız hâlâ AKTİF akış bizsek yap.
      if (abortRef.current === ac) {
        abortRef.current = null
        streamingRef.current = false
        setStreaming(false)
        cancelledRef.current = false
      }
      // Emniyet ağı: yalnız kullanıcı iptalini değil, ağ kopmasını ve sunucunun `done`
      // göndermeden akışı kapatmasını da kapsar — aksi halde balon sonsuza dek
      // "Yanıt hazırlanıyor…" spinner'ında kalırdı.
      setMessages(prev => prev.map(m =>
        m.id === assistantId && m.status === 'streaming'
          ? {
              ...m,
              status: wasCancelled ? 'cancelled' : (m.content ? 'done' : 'error'),
              errorMessage: !wasCancelled && !m.content ? 'Yanıt alınamadı.' : m.errorMessage,
            }
          : m
      ))
      // AI kotası (Free 1 soru/gün, Pro token) güncel kalsın.
      useEntitlementStore.getState().refresh()
    }
  }

  function handleNewChat() {
    cancel()
    revokeMessageImages(messages)
    setMessages([])
    setInput('')
    setLastMeta(null)
    // Sıfırlama sonrası son prompt yeniden oynatılmasın.
    lastPromptNonceRef.current = prompt?.nonce ?? 0
  }

  function handleSend() {
    if (streamingRef.current) return
    runChat(input, { images: attach.take() })
  }

  // ── Sürükle-bırak (yalnız dosya sürüklenirken overlay) ─────────────────────
  const isFileDrag = (e: DragEvent) => e.dataTransfer.types.includes('Files')

  function handleDragEnter(e: DragEvent) {
    if (!visionEnabled || !isFileDrag(e)) return
    e.preventDefault()
    dragDepthRef.current++
    setDragOver(true)
  }

  function handleDragLeave(e: DragEvent) {
    if (!visionEnabled || !isFileDrag(e)) return
    dragDepthRef.current = Math.max(0, dragDepthRef.current - 1)
    if (dragDepthRef.current === 0) setDragOver(false)
  }

  function handleDrop(e: DragEvent) {
    if (!visionEnabled || !isFileDrag(e)) return
    e.preventDefault()
    dragDepthRef.current = 0
    setDragOver(false)
    if (!streaming) void attach.add(filesFrom(e.dataTransfer.files))
  }

  return (
    <div
      className="relative h-full flex flex-col bg-ui-card text-tx"
      onDragEnter={handleDragEnter}
      onDragOver={e => { if (visionEnabled && isFileDrag(e)) e.preventDefault() }}
      onDragLeave={handleDragLeave}
      onDrop={handleDrop}
    >
      {dragOver && (
        <div className="pointer-events-none absolute inset-2 z-20 rounded-lg border-2 border-dashed border-ui-accent bg-ui-soft/90 flex flex-col items-center justify-center gap-2 text-ui-ink">
          <ImagePlus size={28} />
          <span className="text-sm font-medium">Ekran görüntüsünü buraya bırakın</span>
          <span className="text-[11px] text-tx-muted">PNG, JPG · en fazla {maxImages} görsel</span>
        </div>
      )}
      {/* Header — model adı yalnız gerçek yanıt meta verisinden (ilk yanıttan sonra) */}
      <div className="h-[36px] pl-3 pr-1.5 flex items-center gap-2 border-b border-ui-line flex-shrink-0">
        <Sparkles size={15} className="flex-none text-ui-accent" aria-hidden="true" />
        <h2 className="m-0 flex-none whitespace-nowrap text-[13px] font-semibold text-tx">AI Asistan</h2>
        {(lastMeta?.model || lastMeta?.provider) && (
          <span
            className="min-w-0 truncate font-mono text-[11px] text-tx-muted"
            title={[lastMeta.provider, lastMeta.model].filter(Boolean).join(' · ')}
          >
            {lastMeta.model || lastMeta.provider}
          </span>
        )}
        <div className="flex-1" />
        {streaming && (
          <button
            type="button"
            onClick={cancel}
            className="xc-focus rounded px-1.5 h-7 text-xs text-fn-orange-ink hover:bg-fn-orange-tint flex items-center gap-1"
          >
            <StopCircle size={13} aria-hidden="true" /> İptal
          </button>
        )}
        {messages.length > 0 && !streaming && (
          <button
            type="button"
            onClick={handleNewChat}
            className="xc-focus h-7 w-7 flex items-center justify-center rounded-md text-tx-muted hover:bg-ui-hover hover:text-tx transition-colors"
            title="Yeni sohbet"
            aria-label="Yeni sohbet"
          >
            <RotateCcw size={13} aria-hidden="true" />
          </button>
        )}
        <button
          type="button"
          onClick={onClose}
          className="xc-focus h-7 w-7 flex items-center justify-center rounded-md text-tx-muted hover:bg-ui-hover hover:text-tx"
          title="Paneli kapat"
          aria-label="AI Asistanı kapat"
        >
          <X size={15} aria-hidden="true" />
        </button>
      </div>

      {/* İlk satırda XML bildirimi eksik uyarısı */}
      {xmlDeclarationMissing && (
        <div className="px-3 py-1.5 border-b border-fn-orange-line bg-fn-orange-tint flex items-start gap-1.5 text-[11px] text-fn-orange-ink flex-shrink-0">
          <AlertTriangle size={12} className="flex-shrink-0 mt-0.5" aria-hidden="true" />
          <span>
            XSLT'nin ilk satırında{' '}
            <code className="font-mono font-semibold">{'<?xml version="1.0" encoding="UTF-8"?>'}</code>{' '}
            bildirimi eksik. İlk satıra eklemeniz gerekir.
          </span>
        </div>
      )}

      {/* XML seçimi bilgi bandı */}
      {xmlSelection?.trim() && (
        <div className="px-3 py-1 border-b border-ui-line bg-ui-side flex items-center gap-1.5 text-[10.5px] text-ui-ink flex-shrink-0">
          <Sparkles size={10} aria-hidden="true" />
          XML seçimi bağlam olarak kullanılıyor ({xmlSelection.split('\n').length} satır)
        </div>
      )}

      {/* XSLT seçimi bilgi bandı */}
      {xsltSelection?.trim() && (
        <div className="px-3 py-1 border-b border-ui-line bg-ui-side flex items-center gap-1.5 text-[10.5px] text-ui-ink flex-shrink-0">
          <Sparkles size={10} aria-hidden="true" />
          XSLT seçimi bağlam olarak kullanılıyor ({xsltSelection.split('\n').length} satır)
        </div>
      )}

      {/* Mesaj listesi */}
      <div className="flex-1 relative min-h-0">
      <div ref={listRef} onScroll={handleScroll} className="h-full overflow-y-auto px-3.5 py-3.5">
        <div ref={contentRef} className="space-y-3">
        {messages.length === 0 && !streaming && (
          <div className="mt-8 text-center text-[12.5px] text-tx-muted">
            XSLT şablonunu doğal dille düzenlemek için mesaj yaz.<br />
            <span className="text-[11.5px] text-tx-subtle">Örn: "PartyName altındaki cbc:Note alanını kaldır"</span>
            {visionEnabled && (
              <span className="mt-2 block text-[11.5px] text-tx-subtle">
                Önizlemenin ya da hedef tasarımın ekran görüntüsünü de ekleyebilirsin (ataç, Ctrl+V ya da sürükle-bırak).
              </span>
            )}
          </div>
        )}

        {messages.map((msg) => (
          <div key={msg.id} className={`flex flex-col gap-1 ${msg.role === 'user' ? 'items-end' : 'items-start'}`}>
            <div
              className={`text-[12.5px] leading-[1.55] ${
                msg.role === 'user'
                  ? 'max-w-[85%] rounded-[10px_10px_2px_10px] bg-ui-soft px-3 py-2 text-tx'
                  : 'w-full max-w-full text-tx'
              }`}
            >
              {msg.role === 'user' ? (
                <>
                  {msg.images?.length ? (
                    <ChatImageGrid
                      images={msg.images}
                      onReattach={visionEnabled && !streaming ? () => attach.addPrepared(msg.images!) : undefined}
                    />
                  ) : null}
                  {msg.content && <span className="whitespace-pre-wrap break-words">{msg.content}</span>}
                </>
              ) : msg.content ? (
                <>
                  <MarkdownMessage text={msg.content} />
                  {/* Yazıyor göstergesi: içerik akarken de görünür kalsın. */}
                  {msg.status === 'streaming' && (
                    <span aria-hidden="true" className="inline-block w-2 h-3 bg-ui-accent animate-pulse motion-reduce:animate-none align-text-bottom ml-0.5" />
                  )}
                </>
              ) : msg.status === 'streaming' ? (
                <span className="flex items-center gap-1 text-tx-muted">
                  <Loader2 size={12} className="animate-spin" aria-hidden="true" /> Yanıt hazırlanıyor…
                </span>
              ) : msg.status === 'cancelled' ? (
                <span className="flex items-center gap-1 text-tx-muted">
                  <StopCircle size={12} aria-hidden="true" /> İptal edildi
                </span>
              ) : (
                <span className="text-tx-subtle">—</span>
              )}

              {/* İptal edilmiş kısmi yanıt: içeriğin altında etiketle. */}
              {msg.status === 'cancelled' && msg.content && (
                <div className="mt-1.5 flex items-center gap-1 text-[10.5px] text-tx-muted">
                  <StopCircle size={10} /> İptal edildi — yanıt yarım
                </div>
              )}

              {/* Hata ayrı blokta; kısmi yanıt yukarıda korunur. */}
              {msg.status === 'error' && msg.errorMessage && (
                <div className="mt-1.5 flex items-start gap-1.5 rounded border border-fn-red-line bg-fn-red-tint px-2 py-1 text-[11px] text-fn-red-ink">
                  <AlertTriangle size={11} className="flex-shrink-0 mt-0.5" />
                  <span className="break-words">{msg.errorMessage}</span>
                </div>
              )}
            </div>
            {msg.role === 'assistant' && msg.meta?.provider && (
              <span className="font-mono text-[10.5px] text-tx-subtle">
                {msg.meta.provider}{msg.meta.model ? ` · ${msg.meta.model}` : ''}{msg.meta.ms ? ` · ${msg.meta.ms}ms` : ''}
              </span>
            )}

            {/* Aksiyon satırı: yalnız TAMAMLANMIŞ ve dolu yanıtlarda. İptal edilmiş
                yarım yanıtta gösterilmez — thumbs-up eğitim verisini kirletir,
                "Uygula" yarım XSLT uygular. */}
            {msg.role === 'assistant' && msg.status === 'done' && msg.content.trim() !== '' && (
              <div className="flex flex-wrap items-center gap-1.5">
                {onApplyXslt && extractApplicableBlock(msg.content) && (
                  <button
                    type="button"
                    onClick={() => openApply(msg)}
                    className="xc-focus flex h-[28px] items-center gap-1.5 rounded-md bg-ui-accent px-3 text-[12px] font-semibold text-ui-accent-fg transition-[filter] hover:brightness-110"
                    title="Önerilen değişikliği farkıyla göster, onaylayınca editöre uygula"
                  >
                    <Wand2 size={13} aria-hidden="true" /> {msg.applied ? 'Tekrar uygula' : 'Editöre uygula'}
                  </button>
                )}
                <button
                  type="button"
                  onClick={() => sendFeedback(msg.id, 'up')}
                  disabled={msg.feedback === 'up'}
                  aria-pressed={msg.feedback === 'up'}
                  className={`xc-focus h-7 w-7 flex items-center justify-center rounded-md border text-[11px] transition-colors ${
                    msg.feedback === 'up'
                      ? 'border-fn-green-line bg-fn-green-tint text-fn-green-ink'
                      : 'border-field-line text-tx-muted hover:bg-ui-hover hover:text-tx'
                  }`}
                  title="İşe yaradı"
                  aria-label="İşe yaradı"
                >
                  <ThumbsUp size={12} />
                </button>
                <button
                  type="button"
                  onClick={() => sendFeedback(msg.id, 'down')}
                  disabled={msg.feedback === 'down'}
                  aria-pressed={msg.feedback === 'down'}
                  className={`xc-focus h-7 w-7 flex items-center justify-center rounded-md border text-[11px] transition-colors ${
                    msg.feedback === 'down'
                      ? 'border-fn-red-line bg-fn-red-tint text-fn-red-ink'
                      : 'border-field-line text-tx-muted hover:bg-ui-hover hover:text-tx'
                  }`}
                  title="İşe yaramadı"
                  aria-label="İşe yaramadı"
                >
                  <ThumbsDown size={12} />
                </button>

                {msg.feedback === 'down' && (
                  <>
                    <button
                      type="button"
                      onClick={() => retryDifferent(msg.id)}
                      disabled={streaming}
                      className="xc-focus h-7 px-2 flex items-center gap-1 rounded-md border border-field-line bg-ui-card text-tx-2 hover:bg-ui-hover text-[11px] transition-colors disabled:opacity-40"
                      title="Aynı soruyu farklı bir yaklaşımla yeniden sor"
                    >
                      <RefreshCcw size={11} /> Farklı yaklaşım dene
                    </button>
                    <button
                      type="button"
                      onClick={askForDetail}
                      className="xc-focus h-7 px-2 flex items-center gap-1 rounded-md border border-field-line bg-ui-card text-tx-2 hover:bg-ui-hover text-[11px] transition-colors"
                      title="Neyin çalışmadığını yaz"
                    >
                      <PencilLine size={11} /> Detay vereyim
                    </button>
                  </>
                )}
              </div>
            )}
          </div>
        ))}

        </div>
      </div>

      {/* Kullanıcı yukarı kaydırdıysa dibe dönüş kısayolu */}
      {showJump && (
        <button
          type="button"
          onClick={() => scrollToBottom(true)}
          className="xc-focus absolute bottom-3 right-3 h-8 w-8 flex items-center justify-center rounded-full bg-ui-card border border-ui-line text-tx-2 hover:bg-ui-hover shadow-[var(--ui-shadow-lift)] transition-colors"
          title="En alta in"
          aria-label="En alta in"
        >
          <ChevronDown size={16} />
        </button>
      )}
      </div>

      {/* Input alanı (+ hazır komutlar) */}
      <ChatComposer
        above={(
          <div role="group" aria-label="Hazır komutlar" className="flex flex-wrap gap-1.5">
            <button
              type="button"
              className={chipCls}
              disabled={streaming || !xsltSelection?.trim()}
              title={xsltSelection?.trim() ? 'Seçili XSLT kodunu açıklat' : 'Önce editörde kod seçin'}
              onClick={() => void runChat(PROMPT_EXPLAIN_SELECTION)}
            >
              Seçili kodu açıkla
            </button>
          </div>
        )}
        value={input}
        onChange={setInput}
        onSend={handleSend}
        streaming={streaming}
        inputRef={inputRef}
        visionEnabled={visionEnabled}
        maxImages={maxImages}
        attachments={attach.attachments}
        processing={attach.processing}
        onAddFiles={files => void attach.add(files)}
        onRemoveAttachment={attach.remove}
      />

      {applyTarget && (
        <AiApplyDialog
          target={applyTarget}
          onAccept={handleApplyAccept}
          onClose={() => setApplyState(null)}
        />
      )}
    </div>
  )
}
