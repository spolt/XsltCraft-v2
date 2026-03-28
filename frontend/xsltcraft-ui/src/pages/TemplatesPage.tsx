import { useEffect, useRef, useState } from 'react'
import { useNavigate, useSearchParams } from 'react-router-dom'
import { X } from 'lucide-react'
import { cloneTemplate, getFreeThemes, type FreeTheme } from '../services/templateService'
import { previewFromStoredXslt } from '../services/previewService'
import defaultInvoiceXml from '../assets/default-invoice.xml?raw'

const DOC_TYPE_LABEL: Record<string, string> = {
  Invoice: 'e-Fatura / e-Arşiv',
  Despatch: 'e-İrsaliye',
}

export default function TemplatesPage() {
  const [themes, setThemes] = useState<FreeTheme[]>([])
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)
  const [searchParams] = useSearchParams()
  const typeFilter = searchParams.get('type')

  const [selectedTheme, setSelectedTheme] = useState<FreeTheme | null>(null)
  const [previewHtml, setPreviewHtml] = useState('')
  const [previewLoading, setPreviewLoading] = useState(false)
  const [cloning, setCloning] = useState(false)
  const iframeRef = useRef<HTMLIFrameElement>(null)
  const navigate = useNavigate()

  useEffect(() => {
    getFreeThemes()
      .then(setThemes)
      .catch(() => setError('Temalar yüklenirken bir hata oluştu.'))
      .finally(() => setLoading(false))
  }, [])

  useEffect(() => {
    if (!selectedTheme) return
    setPreviewHtml('')
    setPreviewLoading(true)
    previewFromStoredXslt(selectedTheme.id, defaultInvoiceXml)
      .then((res) => setPreviewHtml(res.html))
      .catch(() => setPreviewHtml('<html><body style="display:flex;align-items:center;justify-content:center;height:100%;color:#ef4444;font-family:sans-serif;font-size:13px">Önizleme alınamadı.</body></html>'))
      .finally(() => setPreviewLoading(false))
  }, [selectedTheme])

  async function handleUse() {
    if (!selectedTheme || cloning) return
    setCloning(true)
    try {
      const clone = await cloneTemplate(selectedTheme.id)
      navigate(`/theme-use/${clone.id}`)
    } catch {
      alert('Şablon kopyalanamadı.')
      setCloning(false)
    }
  }

  const filtered = typeFilter
    ? themes.filter((t) => t.documentType === typeFilter)
    : themes

  const title = typeFilter ? DOC_TYPE_LABEL[typeFilter] ?? 'Temalar' : 'Tüm Temalar'

  if (loading) {
    return (
      <div className="flex items-center justify-center py-24">
        <p className="text-gray-400 text-sm">Yükleniyor...</p>
      </div>
    )
  }

  if (error) {
    return (
      <div className="flex items-center justify-center py-24">
        <p className="text-red-500 text-sm">{error}</p>
      </div>
    )
  }

  return (
    <div className="flex h-full overflow-hidden">
      {/* ── Sol panel: tema listesi ── */}
      <div
        className={`overflow-y-auto px-6 py-8 transition-all duration-300 ${selectedTheme ? 'border-r border-gray-200' : 'flex-1'}`}
        style={selectedTheme ? { width: 360, flexShrink: 0 } : {}}
      >
        <div className={selectedTheme ? undefined : 'max-w-5xl mx-auto'}>
        <h1 className="text-xl font-semibold text-gray-800 mb-1">{title}</h1>
        <p className="text-gray-500 text-sm mb-6">
          Hazır şablonlardan birini seçerek başlayın.
        </p>

        {filtered.length === 0 ? (
          <p className="text-gray-400 text-sm">Bu kategoride henüz şablon yok.</p>
        ) : (
          <div className={`grid gap-4 ${selectedTheme ? 'grid-cols-1' : 'grid-cols-1 sm:grid-cols-2 lg:grid-cols-3'}`}>
            {filtered.map((theme) => (
              <ThemeCard
                key={theme.id}
                theme={theme}
                selected={selectedTheme?.id === theme.id}
                compact={!!selectedTheme}
                onSelect={() => setSelectedTheme(theme)}
              />
            ))}
          </div>
        )}
        </div>
      </div>

      {/* ── Sağ panel: önizleme ── */}
      {selectedTheme && (
        <div className="flex-1 flex flex-col overflow-hidden">
          {/* Toolbar */}
          <div className="h-11 flex items-center gap-3 px-4 bg-white border-b border-gray-200 flex-shrink-0">
            <div className="flex-1 min-w-0">
              <span className="text-sm font-medium text-gray-800 truncate block">{selectedTheme.name}</span>
            </div>
            {previewLoading && (
              <span className="flex items-center gap-1.5 text-xs text-blue-500 flex-shrink-0">
                <span className="inline-block w-3 h-3 border-2 border-blue-400 border-t-transparent rounded-full animate-spin" />
                Yükleniyor
              </span>
            )}
            <button
              onClick={handleUse}
              disabled={cloning || previewLoading}
              className="flex-shrink-0 text-sm font-medium text-white bg-blue-600 hover:bg-blue-700 disabled:opacity-50 disabled:cursor-not-allowed rounded-lg px-4 py-1.5 transition-colors"
            >
              {cloning ? 'Kopyalanıyor…' : 'Bu temayı kullan'}
            </button>
            <button
              onClick={() => setSelectedTheme(null)}
              className="flex-shrink-0 text-gray-400 hover:text-gray-600 transition-colors"
              title="Kapat"
            >
              <X size={16} />
            </button>
          </div>

          {/* iframe */}
          <div className="flex-1 overflow-auto bg-gray-300 flex justify-center">
            <iframe
              ref={iframeRef}
              srcDoc={previewHtml || '<html><body style="background:#d1d5db;margin:0"></body></html>'}
              sandbox="allow-same-origin allow-scripts allow-modals"
              className="border-none bg-white shadow-lg"
              style={{ width: 860, minHeight: '100%', height: 'max(100%, 1200px)' }}
              title="Tema Önizleme"
            />
          </div>
        </div>
      )}
    </div>
  )
}

// ── Tema kartı ────────────────────────────────────────────────────────────────

function ThemeCard({
  theme,
  selected,
  compact,
  onSelect,
}: {
  theme: FreeTheme
  selected: boolean
  compact: boolean
  onSelect: () => void
}) {
  const [cloning, setCloning] = useState(false)
  const navigate = useNavigate()

  async function handleUse(e: React.MouseEvent) {
    e.stopPropagation()
    setCloning(true)
    try {
      const clone = await cloneTemplate(theme.id)
      navigate(`/theme-use/${clone.id}`)
    } catch {
      alert('Şablon kopyalanamadı.')
      setCloning(false)
    }
  }

  return (
    <div
      onClick={onSelect}
      className={`border rounded-xl p-4 flex flex-col gap-3 bg-white cursor-pointer transition-all ${
        selected
          ? 'border-blue-500 shadow-md ring-1 ring-blue-400'
          : 'border-gray-200 shadow-sm hover:shadow-md hover:border-blue-200'
      }`}
    >
      {compact ? (
        /* Önizleme açıkken: badge+isim sola, "Seç" sağa */
        <div className="flex items-center gap-2">
          <div className="flex-1 min-w-0">
            <span className="inline-block text-xs font-medium text-blue-600 bg-blue-50 rounded-full px-2 py-0.5 mb-1">
              {DOC_TYPE_LABEL[theme.documentType] ?? theme.documentType}
            </span>
            <h2 className="text-sm font-semibold text-gray-800 truncate">{theme.name}</h2>
          </div>
          <button
            onClick={handleUse}
            disabled={cloning}
            className="flex-shrink-0 text-xs font-medium text-blue-600 border border-blue-500 hover:bg-blue-600 hover:text-white disabled:opacity-50 disabled:cursor-not-allowed rounded-lg px-3 py-1.5 transition-colors"
          >
            {cloning ? '…' : 'Seç'}
          </button>
        </div>
      ) : (
        /* Normal mod: badge + isim üstte, "Ön İzleme" butonu altta */
        <>
          <div>
            <span className="inline-block text-xs font-medium text-blue-600 bg-blue-50 rounded-full px-2 py-0.5 mb-2">
              {DOC_TYPE_LABEL[theme.documentType] ?? theme.documentType}
            </span>
            <h2 className="text-sm font-semibold text-gray-800">{theme.name}</h2>
          </div>
          <button
            onClick={(e) => { e.stopPropagation(); onSelect() }}
            className="w-full text-sm font-medium text-blue-600 border border-blue-500 hover:bg-blue-600 hover:text-white rounded-lg px-3 py-1.5 transition-colors"
          >
            Ön İzleme
          </button>
        </>
      )}
    </div>
  )
}
