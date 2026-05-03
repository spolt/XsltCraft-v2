import { useEffect, useRef, useState } from 'react'
import {
  Sparkles, X, StopCircle, Loader2,
  RotateCcw,
} from 'lucide-react'
import Editor from '@monaco-editor/react'
import { streamAi, type AiChunk, type AssistantMessage } from '../../services/aiAssistantService'
import { toast } from '../../store/toastStore'

interface Props {
  xslt: string
  xml: string | null
  xmlSelection?: string
  xmlCursorLine?: number
  initialErrorMessage?: string | null
  onClose: () => void
}

// ─── Chat mesaj tipi ─────────────────────────────────────────────────────────

interface ChatMessage {
  id: number
  role: 'user' | 'assistant'
  content: string
  meta?: { provider?: string; model?: string; ms?: number }
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
              className="text-xs text-gray-200 whitespace-pre-wrap leading-relaxed break-words"
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
                fontSize: 12,
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
  initialErrorMessage,
  onClose,
}: Props) {
  const [messages, setMessages] = useState<ChatMessage[]>([])
  const [input, setInput] = useState('')
  const [streaming, setStreaming] = useState(false)
  const [lastMeta, setLastMeta] = useState<{ provider?: string; model?: string; ms?: number } | null>(null)

  const abortRef = useRef<AbortController | null>(null)
  const messagesEndRef = useRef<HTMLDivElement>(null)
  const initializedRef = useRef(false)

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
            if (
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

      {/* XML seçimi bilgi bandı */}
      {xmlSelection?.trim() && (
        <div className="px-3 py-1 border-b border-gray-700 bg-gray-850 flex items-center gap-1.5 text-[10px] text-violet-400 flex-shrink-0">
          <Sparkles size={10} />
          XML seçimi bağlam olarak kullanılıyor ({xmlSelection.split('\n').length} satır)
        </div>
      )}

      {/* Mesaj listesi */}
      <div className="flex-1 overflow-y-auto px-3 py-3 space-y-3">
        {messages.length === 0 && !streaming && (
          <div className="text-xs text-gray-500 italic text-center mt-8">
            XSLT şablonunu doğal dille düzenlemek için mesaj yaz.<br />
            <span className="text-gray-600">Örn: "PartyName altındaki cbc:Note alanını kaldır"</span>
          </div>
        )}

        {messages.map((msg) => (
          <div key={msg.id} className={`flex flex-col gap-1 ${msg.role === 'user' ? 'items-end' : 'items-start'}`}>
            <div
              className={`max-w-[90%] rounded-lg px-3 py-2 text-xs leading-relaxed ${
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
            value={input}
            onChange={e => setInput(e.target.value)}
            onKeyDown={e => {
              if ((e.ctrlKey || e.metaKey) && e.key === 'Enter') {
                e.preventDefault()
                handleSend()
              }
            }}
            rows={2}
            className="flex-1 bg-gray-800 border border-gray-700 rounded px-2 py-1.5 text-xs text-gray-100 placeholder-gray-500 focus:outline-none focus:border-violet-500 resize-none font-mono"
            placeholder="Buraya yaz… (Ctrl+Enter ile gönder)"
            disabled={streaming}
          />
          <button
            onClick={handleSend}
            disabled={streaming || !input.trim()}
            className="self-stretch px-3 rounded bg-violet-600 hover:bg-violet-500 disabled:opacity-30 disabled:cursor-not-allowed text-xs text-white font-medium"
          >
            Gönder
          </button>
        </div>
      </div>
    </div>
  )
}
