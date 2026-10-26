import { useEffect, useRef, useState } from 'react'
import { DiffEditor } from '@monaco-editor/react'
import { Sparkles, X, StopCircle, Check, RefreshCcw, Loader2 } from 'lucide-react'
import { useUiPrefsStore } from '../../store/uiPrefsStore'
import { defineMonacoThemes, monacoThemeId } from '../../theme/monacoThemes'
import { streamAi, type AiChunk } from '../../services/aiAssistantService'
import api from '../../services/apiService'
import { toXsltError } from '../../services/xsltErrors'

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
  const editorTheme = useUiPrefsStore(s => s.editorTheme)
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
      const { data } = await api.post<{ valid: boolean; error?: string }>(
        '/api/preview/validate-xslt',
        { xslt: replaced },
      )
      if (!data.valid) {
        setValidateError(`Refactor edilen XSLT sözdizimi hatalı: ${toXsltError(data, 'bilinmeyen hata').message}. Yine de uygulamak ister misin?`)
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
        className="bg-ed-bg rounded-xl border border-ed-border w-[90vw] max-w-6xl h-[80vh] flex flex-col text-ed-text shadow-2xl"
        onClick={(e) => e.stopPropagation()}
      >
        {/* Header */}
        <div className="flex items-center gap-2 px-4 py-3 border-b border-ed-border">
          <Sparkles size={16} className="text-ui-accent" />
          <h2 className="text-sm font-medium">AI Refactor — Önizleme</h2>
          <div className="flex-1" />
          {streaming && (
            <button onClick={cancel} className="text-xs text-ed-warn hover:brightness-110 flex items-center gap-1">
              <StopCircle size={13} /> İptal
            </button>
          )}
          <button onClick={onClose} className="text-ed-muted hover:text-ed-text">
            <X size={16} />
          </button>
        </div>

        {/* Goal input */}
        <div className="px-4 py-2 border-b border-ed-border flex gap-2 items-center">
          <input
            value={goal}
            onChange={e => setGoal(e.target.value)}
            disabled={streaming}
            className="flex-1 bg-ed-panel border border-ed-border rounded px-2 py-1.5 text-xs text-ed-text placeholder:text-ed-muted focus:outline-none focus:border-ui-accent"
            placeholder="Refactor amacı (örn: 'xsl:for-each yerine xsl:apply-templates kullan')"
          />
          <button
            onClick={runRefactor}
            disabled={streaming}
            className="px-3 py-1.5 text-xs rounded bg-ed-active hover:brightness-110 text-ed-text flex items-center gap-1 disabled:opacity-30"
          >
            <RefreshCcw size={12} /> Yeniden üret
          </button>
        </div>

        {/* Diff */}
        <div className="flex-1 min-h-0 relative">
          {streaming && !refactored && (
            <div className="absolute inset-0 flex items-center justify-center bg-ed-bg/80 z-10">
              <div className="flex items-center gap-2 text-sm text-ed-muted">
                <Loader2 size={16} className="animate-spin" /> AI cevap üretiyor…
              </div>
            </div>
          )}
          {error && (
            <div className="absolute inset-0 flex items-center justify-center text-fn-red text-sm">
              {error}
            </div>
          )}
          {!error && (
            <DiffEditor
              height="100%"
              language="xml"
              beforeMount={defineMonacoThemes}
                theme={monacoThemeId(editorTheme)}
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
        <div className="px-4 py-3 border-t border-ed-border flex items-center gap-3">
          <span className="text-[11px] text-ed-muted">
            {refactored
              ? 'Sol: orijinal · Sağ: AI önerisi.'
              : streaming ? 'Akış sürüyor…' : 'AI henüz kullanılabilir bir kod bloğu döndürmedi.'}
          </span>
          {validateError && (
            <span className="text-[11px] text-ed-warn ml-auto">{validateError}</span>
          )}
          <div className={validateError ? '' : 'ml-auto flex items-center gap-2'}>
            <button
              onClick={onClose}
              className="px-3 py-1.5 text-xs rounded bg-ed-active hover:brightness-110 text-ed-text"
            >
              Reddet
            </button>
            {validateError ? (
              <button
                onClick={handleForceAccept}
                className="px-3 py-1.5 text-xs rounded bg-fn-orange hover:brightness-110 text-white flex items-center gap-1"
              >
                Yine de uygula
              </button>
            ) : (
              <button
                onClick={handleAccept}
                disabled={!refactored || streaming || validating}
                className="px-3 py-1.5 text-xs rounded bg-fn-green hover:brightness-110 text-white flex items-center gap-1 disabled:opacity-30 disabled:cursor-not-allowed"
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
