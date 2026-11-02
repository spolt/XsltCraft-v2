import { useState } from 'react'
import { DiffEditor, Editor } from '@monaco-editor/react'
import { Sparkles, X, Check, Loader2, Copy, ClipboardCheck, AlertTriangle, MousePointerClick } from 'lucide-react'
import { useUiPrefsStore } from '../../store/uiPrefsStore'
import { defineMonacoThemes, monacoThemeId } from '../../theme/monacoThemes'
import type { ApplyTarget } from '../../utils/xsltApply'
import api from '../../services/apiService'
import { toXsltError } from '../../services/xsltErrors'

interface Props {
  target: ApplyTarget
  /** Kullanıcı kabul edince, uygulanacak TAM doküman ile çağrılır. */
  onAccept: (newDoc: string) => void
  onClose: () => void
}

const KIND_LABEL: Record<ApplyTarget['kind'], string> = {
  'whole-doc': 'Tüm belge değiştirilecek',
  'selection': 'Seçili bölge değiştirilecek',
  'element': 'Eşleşen blok değiştirilecek',
  'no-match': 'Otomatik hedef bulunamadı',
}

export default function AiApplyDialog({ target, onAccept, onClose }: Props) {
  const [validating, setValidating] = useState(false)
  const editorTheme = useUiPrefsStore(s => s.editorTheme)
  const [validateError, setValidateError] = useState<string | null>(null)
  const [copied, setCopied] = useState(false)

  const noMatch = target.kind === 'no-match'

  async function handleAccept() {
    if (!target.newDoc) return
    setValidating(true)
    setValidateError(null)
    try {
      const { data } = await api.post<{ valid: boolean; error?: string }>(
        '/api/preview/validate-xslt',
        { xslt: target.newDoc },
      )
      if (!data.valid) {
        setValidateError(`Uygulanan XSLT sözdizimi hatalı: ${toXsltError(data, 'bilinmeyen hata').message}. Yine de uygulamak ister misin?`)
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
        className="bg-ed-bg rounded-xl border border-ed-border w-[90vw] max-w-6xl h-[80vh] flex flex-col text-ed-text shadow-2xl"
        onClick={(e) => e.stopPropagation()}
      >
        {/* Header */}
        <div className="flex items-center gap-2 px-4 py-3 border-b border-ed-border">
          <Sparkles size={16} className="text-ui-accent" />
          <h2 className="text-sm font-medium">Değişikliği Uygula — Önizleme</h2>
          <span className={`ml-2 text-[11px] px-2 py-0.5 rounded-full ${
            noMatch ? 'bg-fn-orange-tint text-fn-orange-ink' : 'bg-ed-active text-ed-text'
          }`}>
            {KIND_LABEL[target.kind]}
          </span>
          <div className="flex-1" />
          <button onClick={onClose} className="text-ed-muted hover:text-ed-text">
            <X size={16} />
          </button>
        </div>

        {/* Body */}
        <div className="flex-1 min-h-0 flex flex-col">
          {noMatch && (
            <div className="flex-shrink-0 mx-3 mt-3 rounded-lg border border-ed-warn bg-ed-panel px-4 py-3">
              <div className="flex items-start gap-3">
                <AlertTriangle size={20} className="text-ed-warn flex-shrink-0 mt-0.5" />
                <div className="min-w-0">
                  <div className="text-sm font-semibold text-ed-warn">
                    Otomatik uygulanamadı — bu bloğu elle yerleştirmen gerekiyor
                  </div>
                  {target.reason && (
                    <p className="mt-1 text-xs text-ed-text leading-relaxed">{target.reason}</p>
                  )}
                  <div className="mt-2.5 flex flex-col gap-1.5 text-xs text-ed-text">
                    <div className="flex items-start gap-2">
                      <MousePointerClick size={14} className="text-ed-warn flex-shrink-0 mt-0.5" />
                      <span>
                        <strong>Önerilen:</strong> Editörde değiştirmek istediğin bloğu <strong>seçip</strong> AI'ya
                        aynı soruyu tekrar sor — seçili bölge birebir değiştirilir, hedef kesinleşir.
                      </span>
                    </div>
                    <div className="flex items-start gap-2">
                      <Copy size={14} className="text-ed-warn flex-shrink-0 mt-0.5" />
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
                beforeMount={defineMonacoThemes}
                theme={monacoThemeId(editorTheme)}
                value={target.newText}
                options={{ readOnly: true, minimap: { enabled: false }, fontSize: 13, scrollBeyondLastLine: false }}
              />
            ) : (
              <DiffEditor
                height="100%"
                language="xml"
                beforeMount={defineMonacoThemes}
                theme={monacoThemeId(editorTheme)}
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
        <div className="px-4 py-3 border-t border-ed-border flex items-center gap-3">
          <span className="text-[11px] text-ed-muted max-w-[60%]">
            {noMatch ? 'AI önerisi otomatik yerleştirilemedi.' : 'Sol: mevcut · Sağ: AI önerisi.'}
          </span>
          {validateError && (
            <span className="text-[11px] text-ed-warn ml-auto">{validateError}</span>
          )}
          <div className={validateError ? '' : 'ml-auto flex items-center gap-2'}>
            <button
              onClick={onClose}
              className="px-3 py-1.5 text-xs rounded bg-ed-active hover:brightness-110 text-ed-text"
            >
              {noMatch ? 'Kapat' : 'Reddet'}
            </button>
            {noMatch ? (
              <button
                onClick={copyBlock}
                className="px-4 py-2 text-sm font-medium rounded bg-ui-accent hover:brightness-110 text-ui-accent-fg flex items-center gap-1.5"
              >
                {copied ? <ClipboardCheck size={14} /> : <Copy size={14} />}
                {copied ? 'Kopyalandı' : 'Panoya kopyala'}
              </button>
            ) : validateError ? (
              <button
                onClick={handleForceAccept}
                className="px-3 py-1.5 text-xs rounded bg-fn-orange hover:brightness-110 text-on-fn flex items-center gap-1"
              >
                Yine de uygula
              </button>
            ) : (
              <button
                onClick={handleAccept}
                disabled={validating}
                className="px-3 py-1.5 text-xs rounded bg-fn-green hover:brightness-110 text-on-fn flex items-center gap-1 disabled:opacity-30 disabled:cursor-not-allowed"
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
