import { useEffect, useRef, useState } from 'react'
import { DiffEditor } from '@monaco-editor/react'
import { Sparkles, X, StopCircle, Check, RefreshCcw, Loader2 } from 'lucide-react'
import { streamAi, type AiChunk } from '../../services/aiAssistantService'

interface Props {
  /** Refactor edilecek orijinal seçim. */
  originalSelection: string
  /** Editördeki tüm XSLT — modele bağlam için. */
  fullXslt: string
  /** Kullanıcının istediği refactor amacı (boş geçilebilir). */
  initialGoal?: string
  /** Kullanıcı kabul ettiğinde çağrılır. */
  onAccept: (newText: string) => void
  onClose: () => void
}

export default function AiRefactorDialog({
  originalSelection, fullXslt, initialGoal, onAccept, onClose,
}: Props) {
  const [goal, setGoal] = useState(initialGoal ?? 'Daha okunabilir ve doğru hâle getir.')
  const [output, setOutput] = useState('')
  const [streaming, setStreaming] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [validating, setValidating] = useState(false)
  const [validateError, setValidateError] = useState<string | null>(null)
  const abortRef = useRef<AbortController | null>(null)

  useEffect(() => {
    runRefactor()
    return () => abortRef.current?.abort()
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [])

  function cancel() {
    abortRef.current?.abort()
    abortRef.current = null
    setStreaming(false)
  }

  async function runRefactor() {
    cancel()
    setOutput('')
    setError(null)
    setValidateError(null)
    setStreaming(true)
    const ac = new AbortController()
    abortRef.current = ac
    try {
      await streamAi('refactor-selection', {
        xslt: fullXslt,
        selection: originalSelection,
        goal,
      }, (chunk: AiChunk) => {
        if (chunk.type === 'delta' && chunk.text) setOutput(prev => prev + chunk.text)
        else if (chunk.type === 'error') setError(chunk.message ?? 'Bilinmeyen hata.')
      }, ac.signal)
    } catch (e) {
      if ((e as { name?: string }).name === 'AbortError') return
      setError((e as Error).message ?? 'AI isteği başarısız.')
    } finally {
      setStreaming(false)
      abortRef.current = null
    }
  }

  const refactored = extractFirstCodeBlock(output) ?? ''

  async function handleAccept() {
    if (!refactored) return
    setValidating(true)
    setValidateError(null)
    try {
      // Önce sözdizimi kontrolü — orijinal stylesheet'in seçim yerine refactored konursa parse edilebiliyor mu?
      // Backend validate-xslt endpoint'i tek başına bir XSLT parçası beklediği için tam dosya gönderiyoruz.
      const replaced = fullXslt.replace(originalSelection, refactored)
      const res = await fetch(`${import.meta.env.VITE_API_URL ?? 'http://localhost:5000'}/api/preview/validate-xslt`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ xslt: replaced }),
        credentials: 'include',
      })
      const data = await res.json() as { valid: boolean; error?: string }
      if (!data.valid) {
        setValidateError(`Refactor edilen XSLT sözdizimi hatalı: ${data.error ?? 'bilinmeyen hata'}. Yine de uygulamak ister misin?`)
        setValidating(false)
        return
      }
      onAccept(refactored)
    } catch {
      setValidateError('Doğrulama yapılamadı; yine de uygulayabilirsin.')
    } finally {
      setValidating(false)
    }
  }

  function handleForceAccept() {
    if (refactored) onAccept(refactored)
  }

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/60" onClick={onClose}>
      <div
        className="bg-gray-900 rounded-xl border border-gray-700 w-[90vw] max-w-6xl h-[80vh] flex flex-col text-gray-100 shadow-2xl"
        onClick={(e) => e.stopPropagation()}
      >
        {/* Header */}
        <div className="flex items-center gap-2 px-4 py-3 border-b border-gray-700">
          <Sparkles size={16} className="text-violet-400" />
          <h2 className="text-sm font-medium">AI Refactor — Önizleme</h2>
          <div className="flex-1" />
          {streaming && (
            <button onClick={cancel} className="text-xs text-amber-400 hover:text-amber-300 flex items-center gap-1">
              <StopCircle size={13} /> İptal
            </button>
          )}
          <button onClick={onClose} className="text-gray-400 hover:text-white">
            <X size={16} />
          </button>
        </div>

        {/* Goal input */}
        <div className="px-4 py-2 border-b border-gray-700 flex gap-2 items-center">
          <input
            value={goal}
            onChange={e => setGoal(e.target.value)}
            disabled={streaming}
            className="flex-1 bg-gray-800 border border-gray-700 rounded px-2 py-1.5 text-xs text-gray-100 placeholder-gray-500 focus:outline-none focus:border-violet-500"
            placeholder="Refactor amacı (örn: 'xsl:for-each yerine xsl:apply-templates kullan')"
          />
          <button
            onClick={runRefactor}
            disabled={streaming}
            className="px-3 py-1.5 text-xs rounded bg-gray-700 hover:bg-gray-600 text-gray-100 flex items-center gap-1 disabled:opacity-30"
          >
            <RefreshCcw size={12} /> Yeniden üret
          </button>
        </div>

        {/* Diff */}
        <div className="flex-1 min-h-0 relative">
          {streaming && !refactored && (
            <div className="absolute inset-0 flex items-center justify-center bg-gray-900/80 z-10">
              <div className="flex items-center gap-2 text-sm text-gray-400">
                <Loader2 size={16} className="animate-spin" /> AI cevap üretiyor…
              </div>
            </div>
          )}
          {error && (
            <div className="absolute inset-0 flex items-center justify-center text-red-400 text-sm">
              {error}
            </div>
          )}
          {!error && (
            <DiffEditor
              height="100%"
              language="xml"
              theme="vs-dark"
              original={originalSelection}
              modified={refactored || (streaming ? '' : output)}
              options={{
                readOnly: true,
                renderSideBySide: true,
                minimap: { enabled: false },
                fontSize: 13,
                originalEditable: false,
                scrollBeyondLastLine: false,
              }}
            />
          )}
        </div>

        {/* Footer */}
        <div className="px-4 py-3 border-t border-gray-700 flex items-center gap-3">
          <span className="text-[11px] text-gray-500">
            {refactored
              ? 'Sol: orijinal · Sağ: AI önerisi.'
              : streaming ? 'Akış sürüyor…' : 'AI henüz kullanılabilir bir kod bloğu döndürmedi.'}
          </span>
          {validateError && (
            <span className="text-[11px] text-amber-400 ml-auto">{validateError}</span>
          )}
          <div className={validateError ? '' : 'ml-auto flex items-center gap-2'}>
            <button
              onClick={onClose}
              className="px-3 py-1.5 text-xs rounded bg-gray-700 hover:bg-gray-600 text-gray-100"
            >
              Reddet
            </button>
            {validateError ? (
              <button
                onClick={handleForceAccept}
                className="px-3 py-1.5 text-xs rounded bg-amber-600 hover:bg-amber-500 text-white flex items-center gap-1"
              >
                Yine de uygula
              </button>
            ) : (
              <button
                onClick={handleAccept}
                disabled={!refactored || streaming || validating}
                className="px-3 py-1.5 text-xs rounded bg-emerald-600 hover:bg-emerald-500 text-white flex items-center gap-1 disabled:opacity-30 disabled:cursor-not-allowed"
              >
                {validating ? <Loader2 size={12} className="animate-spin" /> : <Check size={12} />}
                Kabul et ve uygula
              </button>
            )}
          </div>
        </div>
      </div>
    </div>
  )
}

function extractFirstCodeBlock(s: string): string | null {
  const m = /```(?:xslt|xml|xpath)?\s*\n([\s\S]*?)\n```/.exec(s)
  return m ? m[1].trim() : null
}
