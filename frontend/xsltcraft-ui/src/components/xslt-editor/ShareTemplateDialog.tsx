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
  'bg-fn-purple-tint text-fn-purple-ink',
  'bg-fn-blue-tint text-fn-blue-ink',
  'bg-fn-green-tint text-fn-green-ink',
  'bg-fn-orange-tint text-fn-orange-ink',
  'bg-fn-red-tint text-fn-red-ink',
  'bg-fn-slate-tint text-fn-slate-ink',
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
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/50" onClick={onClose}>
      <div className="bg-ui-card rounded-xl shadow-2xl w-full max-w-md mx-4 p-6" onClick={(e) => e.stopPropagation()}>
        <div className="flex items-center justify-between mb-1">
          <h2 className="text-lg font-semibold text-tx">Şablonu Paylaş</h2>
          <button onClick={onClose} className="text-tx-subtle hover:text-tx-2 transition-colors">
            <X size={18} />
          </button>
        </div>
        <p className="text-sm text-tx-muted mb-4 truncate">{templateName}</p>

        {/* Kullanıcı arama */}
        <div className="relative">
          <Search size={16} className="absolute left-3 top-1/2 -translate-y-1/2 text-tx-subtle" />
          <input
            type="text"
            value={query}
            onChange={(e) => setQuery(e.target.value)}
            placeholder="Kullanıcı adı, e-posta veya isim ara…"
            autoFocus
            className="w-full border border-field-line rounded-lg pl-9 pr-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-ui-accent focus:border-ui-accent"
          />
        </div>

        {/* Arama sonuçları */}
        {query.trim().length >= 2 && (
          <div className="mt-2 border border-ui-line rounded-lg max-h-44 overflow-y-auto">
            {searching ? (
              <div className="px-3 py-3 text-sm text-tx-subtle">Aranıyor…</div>
            ) : filteredResults.length === 0 ? (
              <div className="px-3 py-3 text-sm text-tx-subtle">Sonuç bulunamadı.</div>
            ) : (
              filteredResults.map((u) => (
                <button
                  key={u.id}
                  onClick={() => handleAdd(u)}
                  disabled={busyUserId === u.id}
                  className="w-full flex items-center gap-3 px-3 py-2 hover:bg-ui-hover disabled:opacity-50 transition-colors text-left"
                >
                  <div className={`w-8 h-8 rounded-full flex items-center justify-center text-xs font-semibold ${avatarColor(u.email)}`}>
                    {initials(u.displayName, u.username)}
                  </div>
                  <div className="min-w-0 flex-1">
                    <div className="text-sm text-tx truncate">{u.displayName || u.username}</div>
                    <div className="text-xs text-tx-subtle truncate">{u.email}</div>
                  </div>
                  <UserPlus size={16} className="text-ui-accent shrink-0" />
                </button>
              ))
            )}
          </div>
        )}

        {/* Paylaşılan kullanıcılar */}
        <div className="mt-5">
          <h3 className="text-xs font-medium text-tx-muted uppercase tracking-wide mb-2">
            Paylaşılan kullanıcılar
          </h3>
          {loading ? (
            <div className="text-sm text-tx-subtle">Yükleniyor…</div>
          ) : shares.length === 0 ? (
            <div className="text-sm text-tx-subtle">Henüz kimseyle paylaşılmadı.</div>
          ) : (
            <div className="space-y-1">
              {shares.map((s) => (
                <div key={s.userId} className="flex items-center gap-3 px-2 py-1.5 rounded-lg hover:bg-ui-hover">
                  <div className={`w-8 h-8 rounded-full flex items-center justify-center text-xs font-semibold ${avatarColor(s.email)}`}>
                    {initials(s.displayName, s.username)}
                  </div>
                  <div className="min-w-0 flex-1">
                    <div className="text-sm text-tx truncate">{s.displayName || s.username}</div>
                    <div className="text-xs text-tx-subtle truncate">{s.email}</div>
                  </div>
                  <button
                    onClick={() => handleRemove(s.userId)}
                    disabled={busyUserId === s.userId}
                    title="Paylaşımı kaldır"
                    className="text-tx-subtle hover:text-fn-red-ink disabled:opacity-50 transition-colors p-1"
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
            className="px-4 py-2 text-sm font-medium text-ui-accent-fg bg-ui-accent hover:brightness-110 rounded-lg transition-colors"
          >
            Kapat
          </button>
        </div>
      </div>
    </div>
  )
}
