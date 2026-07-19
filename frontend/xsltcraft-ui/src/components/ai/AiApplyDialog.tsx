import { useState } from 'react'
import { DiffEditor, Editor } from '@monaco-editor/react'
import { Sparkles, X, Check, Loader2, Copy, ClipboardCheck, AlertTriangle, MousePointerClick } from 'lucide-react'
import type { ApplyTarget } from '../../utils/xsltApply'

interface Props {
  target: ApplyTarget
  /** Kullanıcı kabul edince, uygulanacak TAM doküman ile çağrılır. */
  onAccept: (newDoc: string) => void
  onClose: () => void
}

const API_BASE = import.meta.env.VITE_API_URL ?? 'http://localhost:5000'

const KIND_LABEL: Record<ApplyTarget['kind'], string> = {
  'whole-doc': 'Tüm belge değiştirilecek',
  'selection': 'Seçili bölge değiştirilecek',
  'element': 'Eşleşen blok değiştirilecek',
  'no-match': 'Otomatik hedef bulunamadı',
}

export default function AiApplyDialog({ target, onAccept, onClose }: Props) {
  const [validating, setValidating] = useState(false)
  const [validateError, setValidateError] = useState<string | null>(null)
  const [copied, setCopied] = useState(false)

  const noMatch = target.kind === 'no-match'

  async function handleAccept() {
    if (!target.newDoc) return
    setValidating(true)
    setValidateError(null)
    try {
      const res = await fetch(`${API_BASE}/api/preview/validate-xslt`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ xslt: target.newDoc }),
        credentials: 'include',
      })
      const data = await res.json() as { valid: boolean; error?: string }
      if (!data.valid) {
        setValidateError(`Uygulanan XSLT sözdizimi hatalı: ${data.error ?? 'bilinmeyen hata'}. Yine de uygulamak ister misin?`)
        setValidating(false)
        return
      }
      onAccept(target.newDoc)
    } catch {
      setValidateError('Doğrulama yapılamadı; yine de uygulayabilirsin.')
    } finally {
      setValidating(false)
    }
  }

  function handleForceAccept() {
    if (target.newDoc) onAccept(target.newDoc)
  }

  async function copyBlock() {
    try {
      await navigator.clipboard.writeText(target.newText)
      setCopied(true)
      setTimeout(() => setCopied(false), 1500)
    } catch { /* ignore */ }
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
          <h2 className="text-sm font-medium">Değişikliği Uygula — Önizleme</h2>
          <span className={`ml-2 text-[11px] px-2 py-0.5 rounded-full ${
            noMatch ? 'bg-amber-900/40 text-amber-300' : 'bg-gray-700 text-gray-300'
          }`}>
            {KIND_LABEL[target.kind]}
          </span>
          <div className="flex-1" />
          <button onClick={onClose} className="text-gray-400 hover:text-white">
            <X size={16} />
          </button>
        </div>

        {/* Body */}
        <div className="flex-1 min-h-0 flex flex-col">
          {noMatch && (
            <div className="flex-shrink-0 mx-3 mt-3 rounded-lg border border-amber-500/60 bg-amber-950/50 px-4 py-3">
              <div className="flex items-start gap-3">
                <AlertTriangle size={20} className="text-amber-400 flex-shrink-0 mt-0.5" />
                <div className="min-w-0">
                  <div className="text-sm font-semibold text-amber-200">
                    Otomatik uygulanamadı — bu bloğu elle yerleştirmen gerekiyor
                  </div>
                  {target.reason && (
                    <p className="mt-1 text-xs text-amber-100/80 leading-relaxed">{target.reason}</p>
                  )}
                  <div className="mt-2.5 flex flex-col gap-1.5 text-xs text-amber-50">
                    <div className="flex items-start gap-2">
                      <MousePointerClick size={14} className="text-amber-300 flex-shrink-0 mt-0.5" />
                      <span>
                        <strong>Önerilen:</strong> Editörde değiştirmek istediğin bloğu <strong>seçip</strong> AI'ya
                        aynı soruyu tekrar sor — seçili bölge birebir değiştirilir, hedef kesinleşir.
                      </span>
                    </div>
                    <div className="flex items-start gap-2">
                      <Copy size={14} className="text-amber-300 flex-shrink-0 mt-0.5" />
                      <span>
                        <strong>Ya da:</strong> Aşağıdaki kodu <strong>"Panoya kopyala"</strong> ile alıp editörde ilgili
                        yere elle yapıştır.
                      </span>
                    </div>
                  </div>
                </div>
              </div>
            </div>
          )}
          <div className="flex-1 min-h-0">
            {noMatch ? (
              <Editor
                height="100%"
                language="xml"
                theme="vs-dark"
                value={target.newText}
                options={{ readOnly: true, minimap: { enabled: false }, fontSize: 13, scrollBeyondLastLine: false }}
              />
            ) : (
              <DiffEditor
                height="100%"
                language="xml"
                theme="vs-dark"
                original={target.oldText}
                modified={target.newText}
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
        </div>

        {/* Footer */}
        <div className="px-4 py-3 border-t border-gray-700 flex items-center gap-3">
          <span className="text-[11px] text-gray-500 max-w-[60%]">
            {noMatch ? 'AI önerisi otomatik yerleştirilemedi.' : 'Sol: mevcut · Sağ: AI önerisi.'}
          </span>
          {validateError && (
            <span className="text-[11px] text-amber-400 ml-auto">{validateError}</span>
          )}
          <div className={validateError ? '' : 'ml-auto flex items-center gap-2'}>
            <button
              onClick={onClose}
              className="px-3 py-1.5 text-xs rounded bg-gray-700 hover:bg-gray-600 text-gray-100"
            >
              {noMatch ? 'Kapat' : 'Reddet'}
            </button>
            {noMatch ? (
              <button
                onClick={copyBlock}
                className="px-4 py-2 text-sm font-medium rounded bg-violet-600 hover:bg-violet-500 text-white flex items-center gap-1.5"
              >
                {copied ? <ClipboardCheck size={14} /> : <Copy size={14} />}
                {copied ? 'Kopyalandı' : 'Panoya kopyala'}
              </button>
            ) : validateError ? (
              <button
                onClick={handleForceAccept}
                className="px-3 py-1.5 text-xs rounded bg-amber-600 hover:bg-amber-500 text-white flex items-center gap-1"
              >
                Yine de uygula
              </button>
            ) : (
              <button
                onClick={handleAccept}
                disabled={validating}
                className="px-3 py-1.5 text-xs rounded bg-emerald-600 hover:bg-emerald-500 text-white flex items-center gap-1 disabled:opacity-30 disabled:cursor-not-allowed"
              >
                {validating ? <Loader2 size={12} className="animate-spin" /> : <Check size={12} />}
                Uygula
              </button>
            )}
          </div>
        </div>
      </div>
    </div>
  )
}
