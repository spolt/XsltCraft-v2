import { useEffect, useRef, useState } from 'react'
import {
  Sparkles, X, StopCircle, Loader2, ChevronDown,
  RotateCcw, AlertTriangle, ThumbsUp, ThumbsDown, Wand2, RefreshCcw, PencilLine,
} from 'lucide-react'
import {
  streamAi, submitAiFeedback, updateAiFeedback,
  type AiChunk, type AssistantMessage,
} from '../../services/aiAssistantService'
import { toast } from '../../store/toastStore'
import { useEntitlementStore } from '../../store/entitlementStore'
import { openUpgradeModal } from '../../store/upgradeModalStore'
import { extractApplicableBlock, computeApplyTarget, type ApplyTarget } from '../../utils/xsltApply'
import AiApplyDialog from './AiApplyDialog'
import MarkdownMessage from './MarkdownMessage'

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
}

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
      out.push({ role: 'user', content: q.content })
      out.push({ role: 'assistant', content: a.content })
      i++
    }
  }
  return out
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
  onClose,
}: Props) {
  const [messages, setMessages] = useState<ChatMessage[]>([])
  const [input, setInput] = useState('')
  const [streaming, setStreaming] = useState(false)
  const [lastMeta, setLastMeta] = useState<{ provider?: string; model?: string; ms?: number } | null>(null)
  // Açık "Uygula" diyaloğu (hangi mesaj + hesaplanmış hedef).
  const [applyState, setApplyState] = useState<{ messageId: number; target: ApplyTarget } | null>(null)
  const [showJump, setShowJump] = useState(false)

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

  // Bir asistan mesajının hemen öncesindeki kullanıcı sorusunu bulur (feedback bağlamı).
  function precedingUserMessage(assistantId: number): string {
    const idx = messages.findIndex(m => m.id === assistantId)
    for (let i = idx - 1; i >= 0; i--) {
      if (messages[i].role === 'user') return messages[i].content
    }
    return ''
  }

  function patchMessage(id: number, patch: Partial<ChatMessage>) {
    setMessages(prev => prev.map(m => (m.id === id ? { ...m, ...patch } : m)))
  }

  // ── Uygula ──────────────────────────────────────────────────────────────────
  function openApply(msg: ChatMessage) {
    const block = extractApplicableBlock(msg.content)
    if (!block) return
    const target = computeApplyTarget(xslt, block, xsltSelection)
    setApplyState({ messageId: msg.id, target })
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
  function retryDifferent(assistantId: number) {
    const question = precedingUserMessage(assistantId)
    if (!question) return
    runChat(`Önceki yanıt işe yaramadı, aynı çözümü tekrarlama. Farklı bir yaklaşım dene: ${question}`)
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

  async function runChat(message: string, opts?: { interrupt?: boolean }) {
    if (!message.trim()) return
    if (streamingRef.current) {
      // Manuel gönderim yolunda input/düğme zaten disabled; bu guard savunma amaçlı.
      if (!opts?.interrupt) return
      // "AI'ya sor" kasıtlı bir kullanıcı eylemi: mevcut akışı iptal et. İptal edilen
      // yanıt 'cancelled' olarak ekranda kalır, hiçbir şey kaybolmaz.
      cancel()
    }
    streamingRef.current = true

    const userMsg: ChatMessage = { id: ++msgIdCounter, role: 'user', content: message, status: 'done' }
    const assistantId = ++msgIdCounter

    setMessages(prev => [...prev, userMsg])
    setInput('')
    setStreaming(true)
    setLastMeta(null)

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
            if (chunk.code === 'http_402') {
              // Free kullanıcı günlük 1 soru hakkını doldurdu → Pro'ya yönlendir.
              openUpgradeModal({ title: 'AI soru hakkınız doldu', message: msg })
            } else if (chunk.code === 'http_429') {
              toast.warning(msg, { title: 'Günlük AI limiti' })
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
    setMessages([])
    setInput('')
    setLastMeta(null)
    // Sıfırlama sonrası son prompt yeniden oynatılmasın.
    lastPromptNonceRef.current = prompt?.nonce ?? 0
  }

  function handleSend() {
    runChat(input)
  }

  return (
    <div className="h-full flex flex-col bg-gray-900 text-gray-100">
      {/* Header */}
      <div className="h-9 px-3 flex items-center gap-2 border-b border-gray-700 bg-gray-800 flex-shrink-0">
        <Sparkles size={14} className="text-violet-400" />
        <span className="text-xs font-medium uppercase tracking-wide text-gray-300">AI Asistan</span>
        {lastMeta?.provider && (
          <span className="text-[10px] text-gray-500 font-mono ml-2">
            {lastMeta.provider}{lastMeta.model ? ` · ${lastMeta.model}` : ''}{lastMeta.ms ? ` · ${lastMeta.ms}ms` : ''}
          </span>
        )}
        <div className="flex-1" />
        {streaming && (
          <button
            onClick={cancel}
            className="text-xs text-amber-400 hover:text-amber-300 flex items-center gap-1"
          >
            <StopCircle size={13} /> İptal
          </button>
        )}
        {messages.length > 0 && !streaming && (
          <button
            onClick={handleNewChat}
            className="text-gray-500 hover:text-gray-300 transition-colors"
            title="Yeni sohbet"
          >
            <RotateCcw size={13} />
          </button>
        )}
        <button onClick={onClose} className="text-gray-400 hover:text-white" title="Paneli kapat">
          <X size={14} />
        </button>
      </div>

      {/* İlk satırda XML bildirimi eksik uyarısı */}
      {xmlDeclarationMissing && (
        <div className="px-3 py-1.5 border-b border-amber-700/40 bg-amber-900/20 flex items-start gap-1.5 text-[11px] text-amber-300 flex-shrink-0">
          <AlertTriangle size={12} className="flex-shrink-0 mt-0.5" />
          <span>
            XSLT'nin ilk satırında{' '}
            <code className="font-mono text-amber-200">{'<?xml version="1.0" encoding="UTF-8"?>'}</code>{' '}
            bildirimi eksik. İlk satıra eklemeniz gerekir.
          </span>
        </div>
      )}

      {/* XML seçimi bilgi bandı */}
      {xmlSelection?.trim() && (
        <div className="px-3 py-1 border-b border-gray-700 bg-gray-850 flex items-center gap-1.5 text-[10px] text-violet-400 flex-shrink-0">
          <Sparkles size={10} />
          XML seçimi bağlam olarak kullanılıyor ({xmlSelection.split('\n').length} satır)
        </div>
      )}

      {/* XSLT seçimi bilgi bandı */}
      {xsltSelection?.trim() && (
        <div className="px-3 py-1 border-b border-gray-700 bg-gray-850 flex items-center gap-1.5 text-[10px] text-violet-400 flex-shrink-0">
          <Sparkles size={10} />
          XSLT seçimi bağlam olarak kullanılıyor ({xsltSelection.split('\n').length} satır)
        </div>
      )}

      {/* Mesaj listesi */}
      <div className="flex-1 relative min-h-0">
      <div ref={listRef} onScroll={handleScroll} className="h-full overflow-y-auto px-3 py-3">
        <div ref={contentRef} className="space-y-3">
        {messages.length === 0 && !streaming && (
          <div className="text-sm text-gray-500 italic text-center mt-8">
            XSLT şablonunu doğal dille düzenlemek için mesaj yaz.<br />
            <span className="text-gray-600 text-xs">Örn: "PartyName altındaki cbc:Note alanını kaldır"</span>
          </div>
        )}

        {messages.map((msg) => (
          <div key={msg.id} className={`flex flex-col gap-1 ${msg.role === 'user' ? 'items-end' : 'items-start'}`}>
            <div
              className={`max-w-[90%] rounded-lg px-3 py-2 text-sm leading-relaxed ${
                msg.role === 'user'
                  ? 'bg-violet-700 text-white'
                  : 'bg-gray-800 text-gray-100 border border-gray-700'
              }`}
            >
              {msg.role === 'user' ? (
                <span className="whitespace-pre-wrap break-words">{msg.content}</span>
              ) : msg.content ? (
                <>
                  <MarkdownMessage text={msg.content} />
                  {/* Yazıyor göstergesi: içerik akarken de görünür kalsın. */}
                  {msg.status === 'streaming' && (
                    <span className="inline-block w-2 h-3 bg-violet-400 animate-pulse align-text-bottom ml-0.5" />
                  )}
                </>
              ) : msg.status === 'streaming' ? (
                <span className="flex items-center gap-1 text-gray-500">
                  <Loader2 size={12} className="animate-spin" /> Yanıt hazırlanıyor…
                </span>
              ) : msg.status === 'cancelled' ? (
                <span className="flex items-center gap-1 text-gray-500">
                  <StopCircle size={12} /> İptal edildi
                </span>
              ) : (
                <span className="text-gray-500">—</span>
              )}

              {/* İptal edilmiş kısmi yanıt: içeriğin altında etiketle. */}
              {msg.status === 'cancelled' && msg.content && (
                <div className="mt-1.5 flex items-center gap-1 text-[10px] text-gray-500">
                  <StopCircle size={10} /> İptal edildi — yanıt yarım
                </div>
              )}

              {/* Hata ayrı blokta; kısmi yanıt yukarıda korunur. */}
              {msg.status === 'error' && msg.errorMessage && (
                <div className="mt-1.5 flex items-start gap-1.5 rounded border border-rose-800/60 bg-rose-950/40 px-2 py-1 text-[11px] text-rose-300">
                  <AlertTriangle size={11} className="flex-shrink-0 mt-0.5" />
                  <span className="break-words">{msg.errorMessage}</span>
                </div>
              )}
            </div>
            {msg.role === 'assistant' && msg.meta?.provider && (
              <span className="text-[9px] text-gray-600 font-mono px-1">
                {msg.meta.provider}{msg.meta.model ? ` · ${msg.meta.model}` : ''}{msg.meta.ms ? ` · ${msg.meta.ms}ms` : ''}
              </span>
            )}

            {/* Aksiyon satırı: yalnız TAMAMLANMIŞ ve dolu yanıtlarda. İptal edilmiş
                yarım yanıtta gösterilmez — thumbs-up eğitim verisini kirletir,
                "Uygula" yarım XSLT uygular. */}
            {msg.role === 'assistant' && msg.status === 'done' && msg.content.trim() !== '' && (
              <div className="flex items-center gap-1.5 px-1 flex-wrap">
                {onApplyXslt && extractApplicableBlock(msg.content) && (
                  <button
                    onClick={() => openApply(msg)}
                    className="h-6 px-2 flex items-center gap-1 rounded border border-emerald-700 text-emerald-300 hover:bg-emerald-900/40 text-[11px] transition-colors"
                    title="Önerilen değişikliği editöre uygula (diff önizlemeli)"
                  >
                    <Wand2 size={12} /> {msg.applied ? 'Tekrar Uygula' : 'Uygula'}
                  </button>
                )}
                <button
                  onClick={() => sendFeedback(msg.id, 'up')}
                  disabled={msg.feedback === 'up'}
                  className={`h-6 w-6 flex items-center justify-center rounded border text-[11px] transition-colors ${
                    msg.feedback === 'up'
                      ? 'border-emerald-600 bg-emerald-900/40 text-emerald-300'
                      : 'border-gray-700 text-gray-400 hover:bg-gray-700 hover:text-gray-200'
                  }`}
                  title="İşe yaradı"
                >
                  <ThumbsUp size={12} />
                </button>
                <button
                  onClick={() => sendFeedback(msg.id, 'down')}
                  disabled={msg.feedback === 'down'}
                  className={`h-6 w-6 flex items-center justify-center rounded border text-[11px] transition-colors ${
                    msg.feedback === 'down'
                      ? 'border-rose-600 bg-rose-900/40 text-rose-300'
                      : 'border-gray-700 text-gray-400 hover:bg-gray-700 hover:text-gray-200'
                  }`}
                  title="İşe yaramadı"
                >
                  <ThumbsDown size={12} />
                </button>

                {msg.feedback === 'down' && (
                  <>
                    <button
                      onClick={() => retryDifferent(msg.id)}
                      disabled={streaming}
                      className="h-6 px-2 flex items-center gap-1 rounded border border-gray-700 text-gray-300 hover:bg-gray-700 text-[11px] transition-colors disabled:opacity-30"
                      title="Aynı soruyu farklı bir yaklaşımla yeniden sor"
                    >
                      <RefreshCcw size={11} /> Farklı yaklaşım dene
                    </button>
                    <button
                      onClick={askForDetail}
                      className="h-6 px-2 flex items-center gap-1 rounded border border-gray-700 text-gray-300 hover:bg-gray-700 text-[11px] transition-colors"
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
          onClick={() => scrollToBottom(true)}
          className="absolute bottom-3 right-3 h-8 w-8 flex items-center justify-center rounded-full bg-gray-800 border border-gray-600 text-gray-300 hover:bg-gray-700 hover:text-white shadow-lg transition-colors"
          title="En alta in"
        >
          <ChevronDown size={16} />
        </button>
      )}
      </div>

      {/* Input alanı */}
      <div className="px-3 py-2 border-t border-gray-700 flex-shrink-0">
        <div className="flex gap-2">
          <textarea
            ref={inputRef}
            value={input}
            onChange={e => setInput(e.target.value)}
            onKeyDown={e => {
              // Enter → gönder · Shift+Enter → yeni satır (IME kompozisyonu sürerken gönderme)
              if (e.key === 'Enter' && !e.shiftKey && !e.nativeEvent.isComposing) {
                e.preventDefault()
                handleSend()
              }
            }}
            rows={2}
            className="flex-1 bg-gray-800 border border-gray-700 rounded px-2 py-1.5 text-sm text-gray-100 placeholder-gray-500 focus:outline-none focus:border-violet-500 resize-none font-mono"
            placeholder="Buraya yaz… (Enter ile gönder)"
            disabled={streaming}
          />
          <button
            onClick={handleSend}
            disabled={streaming || !input.trim()}
            className="self-stretch px-3 rounded bg-violet-600 hover:bg-violet-500 disabled:opacity-30 disabled:cursor-not-allowed text-sm text-white font-medium"
          >
            Gönder
          </button>
        </div>
        <div className="mt-1 text-[10px] text-gray-500 select-none">
          <kbd className="font-mono text-gray-400">Enter</kbd> ile gönder ·{' '}
          <kbd className="font-mono text-gray-400">Shift+Enter</kbd> ile yeni satır
        </div>
      </div>

      {applyState && (
        <AiApplyDialog
          target={applyState.target}
          onAccept={handleApplyAccept}
          onClose={() => setApplyState(null)}
        />
      )}
    </div>
  )
}
