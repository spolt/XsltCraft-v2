import { useEffect, useRef, useState } from 'react'
import { useEditorStore } from '../../store/editorStore'
import { useXmlStore } from '../../store/xmlStore'
import { previewFromBlockTree, previewFromStoredXslt } from '../../services/previewService'

const DEBOUNCE_MS = 1500
const API_BASE = import.meta.env.VITE_API_URL ?? 'http://localhost:5000'

export default function EditorPreviewPanel() {
  const sections = useEditorStore((s) => s.sections)
  const blocks = useEditorStore((s) => s.blocks)
  const templateId = useEditorStore((s) => s.templateId)
  const xsltStoragePath = useEditorStore((s) => s.xsltStoragePath)
  const xmlFiles = useXmlStore((s) => s.xmlFiles)
  const activeXmlId = useXmlStore((s) => s.activeXmlId)
  const activeXml = xmlFiles.find((f) => f.id === activeXmlId)

  // Storage XSLT modu: block tree boş ama free theme XSLT'si var (klon senaryosu)
  const useStoredXslt = !!xsltStoragePath && sections.length === 0 && !!templateId

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
          : await previewFromBlockTree(sections, blocks, activeXml.content)
        // srcDoc iframe'lerinin base URL'i about:srcdoc olduğu için
        // relative asset URL'leri (/api/assets/...) çalışmaz.
        // <base> tag ekleyerek API sunucusuna yönlendiriyoruz.
        const baseTag = `<base href="${API_BASE}"/>`
        const patched = result.html.replace(/<head>/i, `<head>${baseTag}`)
        setHtml(patched)
        setLastMs(result.generationTimeMs)
      } catch (e: unknown) {
        const msg =
          e && typeof e === 'object' && 'response' in e
            ? (e as { response?: { data?: { error?: string } } }).response?.data?.error ?? 'Önizleme alınamadı.'
            : 'Önizleme alınamadı.'
        setError(msg)
      } finally {
        setLoading(false)
      }
    }, DEBOUNCE_MS)

    return () => clearTimeout(t)
  }, [sections, blocks, activeXml?.content, useStoredXslt]) // eslint-disable-line react-hooks/exhaustive-deps

  return (
    <div className="flex flex-col border-l border-gray-200 bg-white" style={{ width: expanded ? 860 : 420, flexShrink: 0 }}>
      {/* Header */}
      <div className="h-9 flex items-center gap-2 px-3 border-b border-gray-100 flex-shrink-0">
        <span className="text-xs font-medium text-gray-500 flex-1">Canlı Önizleme</span>
        {loading && (
          <span className="text-xs text-blue-500 flex items-center gap-1">
            <span className="inline-block w-3 h-3 border-2 border-blue-400 border-t-transparent rounded-full animate-spin" />
            Yükleniyor
          </span>
        )}
        {!loading && lastMs !== null && (
          <span className="text-xs text-gray-400">{lastMs} ms</span>
        )}
        {!activeXml && (
          <span className="text-xs text-amber-500">XML yüklenmedi</span>
        )}
        <button
          onClick={() => setExpanded(v => !v)}
          className="px-2 py-0.5 text-xs rounded border border-gray-200 text-gray-500 hover:bg-gray-50 flex-shrink-0"
          title={expanded ? 'Daralt' : 'Genişlet'}
        >
          {expanded ? '⊟' : '⊞'}
        </button>
        <button
          onClick={() => iframeRef.current?.contentWindow?.print()}
          disabled={!html}
          className="px-2 py-0.5 text-xs rounded border border-gray-200 text-gray-500 hover:bg-gray-50 disabled:opacity-30 disabled:cursor-not-allowed flex-shrink-0"
          title="Yazdır / PDF olarak kaydet"
        >
          🖨
        </button>
      </div>

      {/* Error */}
      {error && (
        <div className="px-3 py-2 text-xs text-red-600 bg-red-50 border-b border-red-100 flex-shrink-0">
          {error}
        </div>
      )}

      {/* Preview iframe — srcdoc is the reliable way to inject HTML in React */}
      {activeXml ? (
        <div className="flex-1 overflow-auto bg-gray-300">
          <iframe
            ref={iframeRef}
            srcDoc={html || '<html><body style="background:#d1d5db;margin:0"></body></html>'}
            sandbox="allow-same-origin allow-scripts allow-modals"
            className="w-full border-none"
            style={{ height: 'max(100%, 1200px)' }}
            title="Canlı Önizleme"
          />
        </div>
      ) : (
        <div className="flex-1 flex items-center justify-center">
          <p className="text-xs text-gray-400 text-center px-4">
            Önizleme için toolbar'dan bir XML dosyası yükleyin.
          </p>
        </div>
      )}
    </div>
  )
}
