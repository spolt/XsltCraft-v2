import { useEffect, useState } from 'react'
import { useNavigate, useSearchParams } from 'react-router-dom'
import { cloneTemplate, getFreeThemes, getDownloadUrl, type FreeTheme } from '../services/templateService'

const DOC_TYPE_LABEL: Record<string, string> = {
  Invoice: 'e-Fatura / e-Arşiv',
  Despatch: 'e-İrsaliye',
}

export default function TemplatesPage() {
  const [themes, setThemes] = useState<FreeTheme[]>([])
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)
  const [searchParams] = useSearchParams()
  const typeFilter = searchParams.get('type') // 'Invoice' | 'Despatch' | null

  useEffect(() => {
    getFreeThemes()
      .then(setThemes)
      .catch(() => setError('Temalar yüklenirken bir hata oluştu.'))
      .finally(() => setLoading(false))
  }, [])

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
    <div className="max-w-5xl mx-auto px-6 py-8">
      <h1 className="text-xl font-semibold text-gray-800 mb-1">{title}</h1>
      <p className="text-gray-500 text-sm mb-6">
        Hazır şablonlardan birini seçerek başlayın.
      </p>

      {filtered.length === 0 ? (
        <p className="text-gray-400 text-sm">Bu kategoride henüz şablon yok.</p>
      ) : (
        <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-5">
          {filtered.map((theme) => (
            <ThemeCard key={theme.id} theme={theme} />
          ))}
        </div>
      )}
    </div>
  )
}

function ThemeCard({ theme }: { theme: FreeTheme }) {
  const navigate = useNavigate()
  const [cloning, setCloning] = useState(false)

  async function handleUse() {
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
    <div className="border border-gray-200 rounded-xl p-5 flex flex-col gap-4 bg-white shadow-sm hover:shadow-md transition-shadow">
      <div className="flex-1">
        <span className="inline-block text-xs font-medium text-blue-600 bg-blue-50 rounded-full px-2 py-0.5 mb-2">
          {DOC_TYPE_LABEL[theme.documentType] ?? theme.documentType}
        </span>
        <h2 className="text-base font-semibold text-gray-800">{theme.name}</h2>
      </div>
      <div className="flex gap-2">
        <button
          onClick={handleUse}
          disabled={cloning}
          className="flex-1 text-sm font-medium text-white bg-blue-600 hover:bg-blue-700 disabled:opacity-50 disabled:cursor-not-allowed rounded-lg px-3 py-2 transition-colors"
        >
          {cloning ? 'Kopyalanıyor…' : 'Bu temayı kullan'}
        </button>
        <a
          href={getDownloadUrl(theme.id)}
          download
          className="text-sm font-medium text-gray-700 border border-gray-300 hover:bg-gray-50 rounded-lg px-3 py-2 transition-colors"
        >
          İndir
        </a>
      </div>
    </div>
  )
}
