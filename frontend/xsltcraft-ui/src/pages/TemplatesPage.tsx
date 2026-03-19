import { useEffect, useState } from 'react'
import { getFreeThemes, getDownloadUrl, type FreeTheme } from '../services/templateService'

const DOC_TYPE_LABEL: Record<string, string> = {
  Invoice: 'e-Fatura',
  Despatch: 'İrsaliye',
}

export default function TemplatesPage() {
  const [themes, setThemes] = useState<FreeTheme[]>([])
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    getFreeThemes()
      .then(setThemes)
      .catch(() => setError('Temalar yüklenirken bir hata oluştu.'))
      .finally(() => setLoading(false))
  }, [])

  if (loading) {
    return (
      <div className="flex items-center justify-center min-h-screen">
        <p className="text-gray-500">Yükleniyor...</p>
      </div>
    )
  }

  if (error) {
    return (
      <div className="flex items-center justify-center min-h-screen">
        <p className="text-red-500">{error}</p>
      </div>
    )
  }

  return (
    <div className="max-w-5xl mx-auto px-6 py-10">
      <h1 className="text-2xl font-semibold text-gray-900 mb-2">Ücretsiz Temalar</h1>
      <p className="text-gray-500 mb-8">
        Hazır XSLT şablonlarından birini seçerek başlayın.
      </p>

      {themes.length === 0 ? (
        <p className="text-gray-400">Henüz yayınlanmış tema bulunmuyor.</p>
      ) : (
        <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-6">
          {themes.map((theme) => (
            <ThemeCard key={theme.id} theme={theme} />
          ))}
        </div>
      )}
    </div>
  )
}

function ThemeCard({ theme }: { theme: FreeTheme }) {
  return (
    <div className="border border-gray-200 rounded-xl p-5 flex flex-col gap-4 bg-white shadow-sm hover:shadow-md transition-shadow">
      <div className="flex-1">
        <span className="inline-block text-xs font-medium text-blue-600 bg-blue-50 rounded-full px-2 py-0.5 mb-2">
          {DOC_TYPE_LABEL[theme.documentType] ?? theme.documentType}
        </span>
        <h2 className="text-base font-semibold text-gray-800">{theme.name}</h2>
      </div>

      <div className="flex gap-2">
        {/* Faz 3'te editor'a bağlanacak */}
        <button
          disabled
          className="flex-1 text-sm font-medium text-white bg-blue-600 hover:bg-blue-700 disabled:opacity-40 disabled:cursor-not-allowed rounded-lg px-3 py-2 transition-colors"
        >
          Bu temayı kullan
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
