import { useEffect, useRef, useState } from 'react'
import {
  Sparkles, X, StopCircle, Loader2,
  RotateCcw, AlertTriangle, ThumbsUp, ThumbsDown, Wand2, RefreshCcw, PencilLine,
} from 'lucide-react'
import Editor from '@monaco-editor/react'
import {
  streamAi, submitAiFeedback, updateAiFeedback,
  type AiChunk, type AssistantMessage,
} from '../../services/aiAssistantService'
import { toast } from '../../store/toastStore'
import { useEntitlementStore } from '../../store/entitlementStore'
import { openUpgradeModal } from '../../store/upgradeModalStore'
import { extractApplicableBlock, computeApplyTarget, type ApplyTarget } from '../../utils/xsltApply'
import AiApplyDialog from './AiApplyDialog'

interface Props {
  xslt: string
  xml: string | null
  xmlSelection?: string
  xmlCursorLine?: number
  xsltSelection?: string
  xsltCursorLine?: number
  initialErrorMessage?: string | null
  xmlDeclarationMissing?: boolean
  /** AI önerisini editördeki XSLT'ye uygular (TAM doküman). Yoksa "Uygula" gösterilmez. */
  onApplyXslt?: (newDoc: string) => void
  onClose: () => void
}

// ─── Chat mesaj tipi ─────────────────────────────────────────────────────────

interface ChatMessage {
  id: number
  role: 'user' | 'assistant'
  content: string
  meta?: { provider?: string; model?: string; ms?: number }
  /** Geri bildirim durumu (per-mesaj). */
  feedback?: 'up' | 'down'
  feedbackId?: string
  applied?: boolean
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

// ─── Markdown / kod bloğu ayrıştırma ─────────────────────────────────────────

type Segment =
  | { kind: 'text'; content: string }
  | { kind: 'code'; lang: string; content: string }

function parseSegments(text: string): Segment[] {
  const segments: Segment[] = []
  const re = /```(\w*)\n([\s\S]*?)\n```/g
  let last = 0
  let m: RegExpExecArray | null
  while ((m = re.exec(text)) !== null) {
    if (m.index > last) {
      const txt = text.slice(last, m.index)
      if (txt.trim()) segments.push({ kind: 'text', content: txt })
    }
    const content = m[2].trimEnd()
    if (content) segments.push({ kind: 'code', lang: m[1] || 'plaintext', content })
    last = m.index + m[0].length
  }
  if (last < text.length) {
    const txt = text.slice(last)
    if (txt.trim()) segments.push({ kind: 'text', content: txt })
  }
  return segments.length > 0 ? segments : [{ kind: 'text', content: text }]
}

function toMonacoLang(lang: string): string {
  if (lang === 'xslt' || lang === 'xml' || lang === 'html') return 'xml'
  if (lang === 'json') return 'json'
  return 'plaintext'
}

// ─── Segment render bileşeni ──────────────────────────────────────────────────

function MarkdownOutput({ text }: { text: string }) {
  const segments = parseSegments(text)
  return (
    <div className="space-y-2">
      {segments.map((seg, i) => {
        if (seg.kind === 'text') {
          return (
            <div
              key={i}
              className="text-sm text-gray-200 whitespace-pre-wrap leading-relaxed break-words"
            >
              {seg.content}
            </div>
          )
        }
        const lang = toMonacoLang(seg.lang)
        const lineCount = seg.content.split('\n').length
        const height = Math.min(Math.max(lineCount * 19 + 18, 48), 320)
        return (
          <div key={i} className="rounded overflow-hidden border border-gray-700">
            {seg.lang && (
              <div className="px-2 py-0.5 bg-gray-800 border-b border-gray-700 text-[10px] text-gray-400 font-mono">
                {seg.lang}
              </div>
            )}
            <Editor
              height={height}
              language={lang}
              value={seg.content}
              theme="vs-dark"
              loading={<div className="bg-gray-900 animate-pulse" style={{ height }} />}
              options={{
                readOnly: true,
                minimap: { enabled: false },
                lineNumbers: 'off',
                folding: false,
                fontSize: 13,
                wordWrap: 'on',
                scrollBeyondLastLine: false,
                scrollbar: { vertical: 'auto', horizontal: 'hidden', alwaysConsumeMouseWheel: false },
                overviewRulerLanes: 0,
                renderLineHighlight: 'none',
                contextmenu: false,
                padding: { top: 8, bottom: 8 },
              }}
            />
          </div>
        )
      })}
    </div>
  )
}

// ─── Ana bileşen ──────────────────────────────────────────────────────────────

let msgIdCounter = 0

export default function AiAssistantPanel({
  xslt, xml, xmlSelection, xmlCursorLine,
  xsltSelection, xsltCursorLine,
  initialErrorMessage,
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

  const abortRef = useRef<AbortController | null>(null)
  const messagesEndRef = useRef<HTMLDivElement>(null)
  const initializedRef = useRef(false)
  const inputRef = useRef<HTMLTextAreaElement>(null)

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

  useEffect(() => {
    messagesEndRef.current?.scrollIntoView({ behavior: 'smooth' })
  }, [messages])

  useEffect(() => {
    if (initialErrorMessage && !initializedRef.current) {
      initializedRef.current = true
      runChat(`Şu hatanın sebebini ve nasıl düzeltileceğini açıkla:\n\n${initialErrorMessage}`)
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [initialErrorMessage])

  function cancel() {
    abortRef.current?.abort()
    abortRef.current = null
    setStreaming(false)
  }

  async function runChat(message: string) {
    if (!message.trim() || streaming) return

    const userMsg: ChatMessage = { id: ++msgIdCounter, role: 'user', content: message }
    const assistantId = ++msgIdCounter

    setMessages(prev => [...prev, userMsg])
    setInput('')
    setStreaming(true)
    setLastMeta(null)

    const effectiveXml = getEffectiveXml(xml, xmlSelection, xmlCursorLine)

    // History = all messages except the one we're about to stream
    const historyForRequest: AssistantMessage[] = [...messages, userMsg].map(m => ({
      role: m.role,
      content: m.content,
    }))

    const ac = new AbortController()
    abortRef.current = ac

    // Boş assistant mesajı ekle (streaming için)
    setMessages(prev => [...prev, { id: assistantId, role: 'assistant', content: '' }])

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
          history: historyForRequest.slice(0, -1), // son user mesajı zaten message param'ı
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
              m.id === assistantId ? { ...m, meta: finalMeta ?? undefined } : m
            ))
          } else if (chunk.type === 'error') {
            const msg = chunk.message ?? 'Bilinmeyen hata.'
            setMessages(prev => prev.map(m =>
              m.id === assistantId ? { ...m, content: `⚠️ ${msg}` } : m
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
      if ((e as { name?: string }).name === 'AbortError') return
      const msg = (e as Error).message ?? 'AI isteği başarısız.'
      setMessages(prev => prev.map(m =>
        m.id === assistantId ? { ...m, content: `⚠️ ${msg}` } : m
      ))
    } finally {
      setStreaming(false)
      abortRef.current = null
      // AI kotası (Free 1 soru/gün, Pro token) güncel kalsın.
      useEntitlementStore.getState().refresh()
    }
  }

  function handleNewChat() {
    cancel()
    setMessages([])
    setInput('')
    setLastMeta(null)
    initializedRef.current = false
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
      <div className="flex-1 overflow-y-auto px-3 py-3 space-y-3">
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
                <MarkdownOutput text={msg.content} />
              ) : (
                <span className="flex items-center gap-1 text-gray-500">
                  <Loader2 size={12} className="animate-spin" /> Yanıt hazırlanıyor…
                </span>
              )}
            </div>
            {msg.role === 'assistant' && msg.meta?.provider && (
              <span className="text-[9px] text-gray-600 font-mono px-1">
                {msg.meta.provider}{msg.meta.model ? ` · ${msg.meta.model}` : ''}{msg.meta.ms ? ` · ${msg.meta.ms}ms` : ''}
              </span>
            )}

            {/* Aksiyon satırı: yalnız tamamlanmış (meta set), dolu ve hatasız asistan yanıtlarında */}
            {msg.role === 'assistant' && msg.meta && msg.content && !msg.content.startsWith('⚠️') && (
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

        {/* Streaming cursor — son mesaj zaten güncellendiği için sadece boş içerik ise göster */}
        {streaming && messages[messages.length - 1]?.role === 'assistant' && messages[messages.length - 1]?.content === '' && (
          <div className="flex items-start">
            <div className="bg-gray-800 border border-gray-700 rounded-lg px-3 py-2">
              <span className="inline-block w-2 h-3 bg-violet-400 animate-pulse align-text-bottom" />
            </div>
          </div>
        )}

        <div ref={messagesEndRef} />
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
