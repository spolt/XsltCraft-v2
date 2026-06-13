import { useEffect, useRef, useState } from 'react'
import { X, Search, Trash2, UserPlus } from 'lucide-react'

import {
  getTemplateShares,
  shareTemplate,
  unshareTemplate,
  searchUsers,
  type TemplateShare,
  type UserSearchResult,
} from '../../services/userXsltService'
import { toast } from '../../store/toastStore'

interface Props {
  templateId: string
  templateName: string
  onClose: () => void
}

const AVATAR_COLORS = [
  'bg-purple-100 text-purple-700',
  'bg-blue-100 text-blue-700',
  'bg-emerald-100 text-emerald-700',
  'bg-amber-100 text-amber-700',
  'bg-rose-100 text-rose-700',
  'bg-cyan-100 text-cyan-700',
]

function initials(name: string | null, fallback: string): string {
  const source = name?.trim() || fallback
  const parts = source.split(' ').filter(Boolean)
  if (parts.length >= 2) return (parts[0][0] + parts[parts.length - 1][0]).toUpperCase()
  return source.slice(0, 2).toUpperCase()
}

function avatarColor(email: string): string {
  let hash = 0
  for (const c of email) hash = (hash * 31 + c.charCodeAt(0)) & 0xffff
  return AVATAR_COLORS[hash % AVATAR_COLORS.length]
}

export default function ShareTemplateDialog({ templateId, templateName, onClose }: Props) {
  const [shares, setShares] = useState<TemplateShare[]>([])
  const [loading, setLoading] = useState(true)
  const [query, setQuery] = useState('')
  const [results, setResults] = useState<UserSearchResult[]>([])
  const [searching, setSearching] = useState(false)
  const [busyUserId, setBusyUserId] = useState<string | null>(null)
  const debounceRef = useRef<ReturnType<typeof setTimeout> | null>(null)

  useEffect(() => {
    getTemplateShares(templateId)
      .then(setShares)
      .catch(() => toast.error('Paylaşım listesi yüklenemedi.', { durationMs: 4000 }))
      .finally(() => setLoading(false))
  }, [templateId])

  useEffect(() => {
    if (debounceRef.current) clearTimeout(debounceRef.current)
    const term = query.trim()
    if (term.length < 2) {
      setResults([])
      setSearching(false)
      return
    }
    setSearching(true)
    debounceRef.current = setTimeout(() => {
      searchUsers(term)
        .then(setResults)
        .catch(() => setResults([]))
        .finally(() => setSearching(false))
    }, 300)
    return () => {
      if (debounceRef.current) clearTimeout(debounceRef.current)
    }
  }, [query])

  async function handleAdd(user: UserSearchResult) {
    setBusyUserId(user.id)
    try {
      const added = await shareTemplate(templateId, user.id)
      setShares((prev) => [...prev, added])
      setQuery('')
      setResults([])
      toast.success(`${added.displayName || added.username} ile paylaşıldı.`, { durationMs: 3000 })
    } catch (err: unknown) {
      const status = (err as { response?: { status?: number } })?.response?.status
      toast.error(status === 409 ? 'Bu kullanıcıyla zaten paylaşıldı.' : 'Paylaşım başarısız.', { durationMs: 4000 })
    } finally {
      setBusyUserId(null)
    }
  }

  async function handleRemove(userId: string) {
    setBusyUserId(userId)
    try {
      await unshareTemplate(templateId, userId)
      setShares((prev) => prev.filter((s) => s.userId !== userId))
    } catch {
      toast.error('Paylaşım kaldırılamadı.', { durationMs: 4000 })
    } finally {
      setBusyUserId(null)
    }
  }

  const sharedIds = new Set(shares.map((s) => s.userId))
  const filteredResults = results.filter((u) => !sharedIds.has(u.id))

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/60" onClick={onClose}>
      <div className="bg-white rounded-xl shadow-2xl w-full max-w-md mx-4 p-6" onClick={(e) => e.stopPropagation()}>
        <div className="flex items-center justify-between mb-1">
          <h2 className="text-lg font-semibold text-gray-800">Şablonu Paylaş</h2>
          <button onClick={onClose} className="text-gray-400 hover:text-gray-600 transition-colors">
            <X size={18} />
          </button>
        </div>
        <p className="text-sm text-gray-500 mb-4 truncate">{templateName}</p>

        {/* Kullanıcı arama */}
        <div className="relative">
          <Search size={16} className="absolute left-3 top-1/2 -translate-y-1/2 text-gray-400" />
          <input
            type="text"
            value={query}
            onChange={(e) => setQuery(e.target.value)}
            placeholder="Kullanıcı adı, e-posta veya isim ara…"
            autoFocus
            className="w-full border border-gray-300 rounded-lg pl-9 pr-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500 focus:border-blue-500"
          />
        </div>

        {/* Arama sonuçları */}
        {query.trim().length >= 2 && (
          <div className="mt-2 border border-gray-100 rounded-lg max-h-44 overflow-y-auto">
            {searching ? (
              <div className="px-3 py-3 text-sm text-gray-400">Aranıyor…</div>
            ) : filteredResults.length === 0 ? (
              <div className="px-3 py-3 text-sm text-gray-400">Sonuç bulunamadı.</div>
            ) : (
              filteredResults.map((u) => (
                <button
                  key={u.id}
                  onClick={() => handleAdd(u)}
                  disabled={busyUserId === u.id}
                  className="w-full flex items-center gap-3 px-3 py-2 hover:bg-gray-50 disabled:opacity-50 transition-colors text-left"
                >
                  <div className={`w-8 h-8 rounded-full flex items-center justify-center text-xs font-semibold ${avatarColor(u.email)}`}>
                    {initials(u.displayName, u.username)}
                  </div>
                  <div className="min-w-0 flex-1">
                    <div className="text-sm text-gray-800 truncate">{u.displayName || u.username}</div>
                    <div className="text-xs text-gray-400 truncate">{u.email}</div>
                  </div>
                  <UserPlus size={16} className="text-blue-600 shrink-0" />
                </button>
              ))
            )}
          </div>
        )}

        {/* Paylaşılan kullanıcılar */}
        <div className="mt-5">
          <h3 className="text-xs font-medium text-gray-500 uppercase tracking-wide mb-2">
            Paylaşılan kullanıcılar
          </h3>
          {loading ? (
            <div className="text-sm text-gray-400">Yükleniyor…</div>
          ) : shares.length === 0 ? (
            <div className="text-sm text-gray-400">Henüz kimseyle paylaşılmadı.</div>
          ) : (
            <div className="space-y-1">
              {shares.map((s) => (
                <div key={s.userId} className="flex items-center gap-3 px-2 py-1.5 rounded-lg hover:bg-gray-50">
                  <div className={`w-8 h-8 rounded-full flex items-center justify-center text-xs font-semibold ${avatarColor(s.email)}`}>
                    {initials(s.displayName, s.username)}
                  </div>
                  <div className="min-w-0 flex-1">
                    <div className="text-sm text-gray-800 truncate">{s.displayName || s.username}</div>
                    <div className="text-xs text-gray-400 truncate">{s.email}</div>
                  </div>
                  <button
                    onClick={() => handleRemove(s.userId)}
                    disabled={busyUserId === s.userId}
                    title="Paylaşımı kaldır"
                    className="text-gray-400 hover:text-red-600 disabled:opacity-50 transition-colors p-1"
                  >
                    <Trash2 size={16} />
                  </button>
                </div>
              ))}
            </div>
          )}
        </div>

        <div className="flex justify-end mt-6">
          <button
            onClick={onClose}
            className="px-4 py-2 text-sm font-medium text-white bg-blue-600 hover:bg-blue-700 rounded-lg transition-colors"
          >
            Kapat
          </button>
        </div>
      </div>
    </div>
  )
}
