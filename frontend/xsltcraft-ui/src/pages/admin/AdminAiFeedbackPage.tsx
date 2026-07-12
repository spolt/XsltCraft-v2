import { useEffect, useState, useCallback, Fragment } from 'react'
import { Link } from 'react-router-dom'
import { Sparkles, Search, Globe, Trash2, ThumbsUp, ThumbsDown, Check, AlertCircle } from 'lucide-react'
import {
  getAdminAiFeedback,
  setAiFeedbackGlobal,
  deleteAiFeedback,
  type AdminAiFeedbackItem,
} from '../../services/aiAssistantService'

type RatingFilter = 'positive' | 'negative' | 'all'

export default function AdminAiFeedbackPage() {
  const [items, setItems] = useState<AdminAiFeedbackItem[]>([])
  const [total, setTotal] = useState(0)
  const [rating, setRating] = useState<RatingFilter>('positive')
  const [q, setQ] = useState('')
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)
  const [expanded, setExpanded] = useState<string | null>(null)
  const [busyId, setBusyId] = useState<string | null>(null)

  const load = useCallback(async () => {
    setLoading(true)
    setError(null)
    try {
      const data = await getAdminAiFeedback({ rating, q: q.trim() || undefined, pageSize: 100 })
      setItems(data.items)
      setTotal(data.total)
    } catch {
      setError('Geri bildirimler yüklenemedi.')
    } finally {
      setLoading(false)
    }
  }, [rating, q])

  // Filtre değişince hemen; arama için kısa debounce.
  useEffect(() => {
    const t = setTimeout(load, 300)
    return () => clearTimeout(t)
  }, [load])

  async function toggleGlobal(item: AdminAiFeedbackItem) {
    setBusyId(item.id)
    try {
      await setAiFeedbackGlobal(item.id, !item.isGlobal)
      setItems(prev => prev.map(i => (i.id === item.id ? { ...i, isGlobal: !i.isGlobal } : i)))
    } catch {
      setError('Terfi işlemi başarısız.')
    } finally {
      setBusyId(null)
    }
  }

  async function remove(id: string) {
    if (!confirm('Bu geri bildirim kalıcı olarak silinsin mi?')) return
    setBusyId(id)
    try {
      await deleteAiFeedback(id)
      setItems(prev => prev.filter(i => i.id !== id))
      setTotal(t => t - 1)
    } catch {
      setError('Silme başarısız.')
    } finally {
      setBusyId(null)
    }
  }

  const TABS: { key: RatingFilter; label: string }[] = [
    { key: 'positive', label: 'İşe yaradı' },
    { key: 'negative', label: 'İşe yaramadı' },
    { key: 'all', label: 'Tümü' },
  ]

  return (
    <div className="max-w-6xl mx-auto px-6 py-10">
      <div className="flex items-center justify-between mb-2">
        <div>
          <h1 className="text-2xl font-semibold text-gray-900 flex items-center gap-2">
            <Sparkles size={22} className="text-violet-600" />
            AI Geri Bildirim Havuzu
          </h1>
          <p className="text-gray-500 text-sm mt-1">
            Kaliteli pozitif örnekleri <strong>global havuza</strong> terfi edin — global örnekler
            tüm kullanıcıların prompt'una few-shot olarak eklenir.
          </p>
        </div>
        <Link
          to="/admin/themes"
          className="text-sm text-gray-600 hover:text-gray-900 border border-gray-300 px-3 py-1.5 rounded-lg transition"
        >
          ← Admin Paneli
        </Link>
      </div>

      {/* Gizlilik uyarısı */}
      <div className="flex items-start gap-2 text-xs text-amber-800 bg-amber-50 border border-amber-200 rounded-lg px-3 py-2 my-4">
        <AlertCircle size={14} className="flex-shrink-0 mt-0.5" />
        <span>
          Global yapmadan önce içeriği kontrol edin: soru/cevap metni kişisel veri veya gerçek
          fatura bilgisi içermemeli. Global örnekler diğer kullanıcıların modeline gönderilir.
        </span>
      </div>

      {/* Filtre + arama */}
      <div className="flex items-center gap-3 mb-4">
        <div className="flex bg-gray-100 rounded-lg p-0.5">
          {TABS.map(t => (
            <button
              key={t.key}
              onClick={() => setRating(t.key)}
              className={`px-3 py-1.5 text-sm rounded-md transition ${
                rating === t.key ? 'bg-white shadow-sm text-gray-900 font-medium' : 'text-gray-500 hover:text-gray-700'
              }`}
            >
              {t.label}
            </button>
          ))}
        </div>
        <div className="relative flex-1 max-w-sm">
          <Search size={15} className="absolute left-3 top-1/2 -translate-y-1/2 text-gray-400" />
          <input
            value={q}
            onChange={e => setQ(e.target.value)}
            placeholder="Soru metninde ara…"
            className="w-full border border-gray-300 rounded-lg pl-9 pr-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-violet-500"
          />
        </div>
        <span className="text-xs text-gray-400">{total} kayıt</span>
      </div>

      {error && <p className="text-sm text-red-600 mb-3">{error}</p>}
      {loading && <p className="text-sm text-gray-500">Yükleniyor…</p>}
      {!loading && items.length === 0 && !error && (
        <p className="text-sm text-gray-400">Kayıt yok.</p>
      )}

      {!loading && items.length > 0 && (
        <div className="border border-gray-200 rounded-xl overflow-hidden bg-white shadow-sm">
          <table className="w-full text-sm">
            <thead className="bg-gray-50 border-b border-gray-200">
              <tr>
                <th className="text-left px-4 py-3 font-medium text-gray-600 w-10" />
                <th className="text-left px-4 py-3 font-medium text-gray-600">Soru</th>
                <th className="text-left px-4 py-3 font-medium text-gray-600 w-40">Kullanıcı</th>
                <th className="text-left px-4 py-3 font-medium text-gray-600 w-24">Uygulandı</th>
                <th className="px-4 py-3 w-44" />
              </tr>
            </thead>
            <tbody className="divide-y divide-gray-100">
              {items.map(item => (
                <Fragment key={item.id}>
                  <tr className="hover:bg-gray-50 align-top">
                    <td className="px-4 py-3">
                      {item.rating === 'Positive'
                        ? <ThumbsUp size={14} className="text-emerald-600" />
                        : <ThumbsDown size={14} className="text-rose-600" />}
                    </td>
                    <td className="px-4 py-3">
                      <button
                        onClick={() => setExpanded(expanded === item.id ? null : item.id)}
                        className="text-left text-gray-800 hover:text-violet-700 truncate max-w-[380px] block"
                        title="Detayı aç/kapat"
                      >
                        {item.userMessage}
                      </button>
                      {item.isGlobal && (
                        <span className="inline-flex items-center gap-1 mt-1 text-[11px] text-emerald-700 bg-emerald-50 border border-emerald-200 rounded px-1.5 py-0.5">
                          <Globe size={10} /> Global
                        </span>
                      )}
                    </td>
                    <td className="px-4 py-3 text-gray-500 text-xs">
                      {item.username || item.email}
                    </td>
                    <td className="px-4 py-3">
                      {item.applied
                        ? <Check size={14} className="text-emerald-600" />
                        : <span className="text-gray-300">—</span>}
                    </td>
                    <td className="px-4 py-3">
                      <div className="flex gap-2 justify-end">
                        {item.rating === 'Positive' && (
                          <button
                            onClick={() => toggleGlobal(item)}
                            disabled={busyId === item.id}
                            className={`flex items-center gap-1 px-2.5 py-1.5 text-xs font-medium rounded-lg border transition disabled:opacity-40 ${
                              item.isGlobal
                                ? 'text-emerald-700 border-emerald-300 bg-emerald-50 hover:bg-emerald-100'
                                : 'text-gray-700 border-gray-300 hover:bg-gray-50'
                            }`}
                          >
                            <Globe size={12} />
                            {item.isGlobal ? 'Global (kaldır)' : 'Global yap'}
                          </button>
                        )}
                        <button
                          onClick={() => remove(item.id)}
                          disabled={busyId === item.id}
                          className="flex items-center gap-1 px-2.5 py-1.5 text-xs font-medium text-red-700 border border-red-200 hover:bg-red-50 rounded-lg transition disabled:opacity-40"
                        >
                          <Trash2 size={12} />
                          Sil
                        </button>
                      </div>
                    </td>
                  </tr>
                  {expanded === item.id && (
                    <tr className="bg-gray-50/60">
                      <td />
                      <td colSpan={4} className="px-4 py-3">
                        <div className="text-xs text-gray-500 mb-1 font-medium">Cevap:</div>
                        <pre className="text-xs text-gray-700 whitespace-pre-wrap break-words bg-white border border-gray-200 rounded-lg p-3 max-h-64 overflow-auto">
                          {item.assistantAnswer}
                        </pre>
                      </td>
                    </tr>
                  )}
                </Fragment>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </div>
  )
}
