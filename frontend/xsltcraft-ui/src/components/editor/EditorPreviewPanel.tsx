import { useEffect, useRef, useState } from 'react'
import { useEditorStore } from '../../store/editorStore'
import { useXmlStore } from '../../store/xmlStore'
import { previewFromBlockTree, previewFromStoredXslt } from '../../services/previewService'
import { toXsltError } from '../../services/xsltErrors'
import { LoaderCircle, Maximize2, Minimize2, Printer } from 'lucide-react'

const DEBOUNCE_MS = 1500
const API_BASE = import.meta.env.VITE_API_URL ?? 'http://localhost:5000'

export default function EditorPreviewPanel() {
  const blocks = useEditorStore((s) => s.blocks)
  const templateId = useEditorStore((s) => s.templateId)
  const hasStoredXslt = useEditorStore((s) => s.hasStoredXslt)
  const documentType = useEditorStore((s) => s.documentType)
  const xmlFiles = useXmlStore((s) => s.xmlFiles)
  const activeXmlId = useXmlStore((s) => s.activeXmlId)
  const activeXml = xmlFiles.find((f) => f.id === activeXmlId)

  // Storage XSLT modu: block tree boş ama free theme XSLT'si var (klon senaryosu)
  const useStoredXslt = hasStoredXslt && Object.keys(blocks).length === 0 && !!templateId

  const iframeRef = useRef<HTMLIFrameElement>(null)

  const [html, setHtml] = useState<string>('')
  const [loading, setLoading] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [lastMs, setLastMs] = useState<number | null>(null)
  const [expanded, setExpanded] = useState(false)

  // Debounced preview fetch
  useEffect(() => {
    if (!activeXml) return

    const t = setTimeout(async () => {
      setLoading(true)
      setError(null)
      try {
        const result = useStoredXslt
          ? await previewFromStoredXslt(templateId!, activeXml.content)
          : await previewFromBlockTree(blocks, activeXml.content, documentType)
        // srcDoc iframe'lerinin base URL'i about:srcdoc olduğu için
        // relative asset URL'leri (/api/assets/...) çalışmaz.
        // <base> tag ekleyerek API sunucusuna yönlendiriyoruz.
        const baseTag = `<base href="${API_BASE}"/>`
        const patched = result.html.replace(/<head>/i, `<head>${baseTag}`)
        setHtml(patched)
        setLastMs(result.generationTimeMs)
      } catch (e: unknown) {
        setError(toXsltError(e).message)
      } finally {
        setLoading(false)
      }
    }, DEBOUNCE_MS)

    return () => clearTimeout(t)
  }, [blocks, activeXml?.content, useStoredXslt, documentType]) // eslint-disable-line react-hooks/exhaustive-deps

  return (
    <section
      aria-label="Canlı önizleme"
      className="flex flex-col border-l border-ui-line bg-ui-card"
      style={{ width: expanded ? 860 : 420, flexShrink: 0 }}
    >
      {/* Header */}
      <div className="flex h-11 flex-none items-center gap-2 border-b border-ui-line px-3">
        <span className="flex-1 text-[13px] font-semibold text-tx">Canlı Önizleme</span>
        <span aria-live="polite" className="flex items-center">
          {loading && (
            <span className="flex items-center gap-1 text-[12px] text-ui-ink">
              <LoaderCircle size={13} aria-hidden="true" className="animate-spin motion-reduce:animate-none" />
              Yükleniyor
            </span>
          )}
          {!loading && lastMs !== null && <span className="text-[12px] tabular-nums text-tx-subtle">{lastMs} ms</span>}
        </span>
        {!activeXml && <span className="text-[12px] text-fn-orange-ink">XML yüklenmedi</span>}
        <button
          type="button"
          onClick={() => setExpanded((v) => !v)}
          aria-label={expanded ? 'Önizlemeyi daralt' : 'Önizlemeyi genişlet'}
          aria-pressed={expanded}
          title={expanded ? 'Daralt' : 'Genişlet'}
          className="xc-focus flex h-7 w-7 flex-none items-center justify-center rounded-md border border-field-line text-tx-muted hover:bg-ui-hover hover:text-tx"
        >
          {expanded ? <Minimize2 size={14} aria-hidden="true" /> : <Maximize2 size={14} aria-hidden="true" />}
        </button>
        <button
          type="button"
          onClick={() => iframeRef.current?.contentWindow?.print()}
          disabled={!html}
          aria-label="Yazdır / PDF olarak kaydet"
          title="Yazdır / PDF olarak kaydet"
          className="xc-focus flex h-7 w-7 flex-none items-center justify-center rounded-md border border-field-line text-tx-muted hover:bg-ui-hover hover:text-tx disabled:cursor-not-allowed disabled:opacity-40"
        >
          <Printer size={14} aria-hidden="true" />
        </button>
      </div>

      {/* Error */}
      {error && (
        <div role="alert" className="flex-none border-b border-fn-red-line bg-fn-red-tint px-3 py-2 text-[12px] text-fn-red-ink">
          {error}
        </div>
      )}

      {/* Preview iframe — srcdoc is the reliable way to inject HTML in React; zemin her zaman beyaz */}
      {activeXml ? (
        <div className="flex-1 overflow-auto bg-ui-bg">
          <iframe
            ref={iframeRef}
            srcDoc={html || '<html><body style="background:white;margin:0"></body></html>'}
            sandbox="allow-same-origin allow-scripts allow-modals"
            className="w-full border-none bg-white"
            style={{ height: 'max(100%, 1200px)' }}
            title="Canlı Önizleme"
          />
        </div>
      ) : (
        <div className="flex flex-1 items-center justify-center">
          <p className="px-4 text-center text-[12px] text-tx-subtle">
            Önizleme için sol paneldeki XML Ağacı sekmesinden bir XML dosyası yükleyin.
          </p>
        </div>
      )}
    </section>
  )
}
