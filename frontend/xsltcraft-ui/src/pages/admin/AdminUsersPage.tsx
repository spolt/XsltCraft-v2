import { useEffect, useRef, useState } from 'react'
import { AlertCircle, Calendar, Clock, Download, ExternalLink, Plus, Save, Search, Shield, Trash2, Users, X } from 'lucide-react'
import { useAuthStore } from '../../store/authStore'
import {
  createUser,
  deleteUser,
  listUsers,
  resetPassword,
  setActive,
  updateRole,
  type UserListItem,
} from '../../services/adminUserService'

const ROLE_COLORS: Record<string, string> = {
  Admin: 'bg-purple-100 text-purple-700',
  Editor: 'bg-blue-100 text-blue-700',
  User: 'bg-gray-100 text-gray-700',
}

function formatDate(iso: string | null | undefined): string {
  if (!iso) return '—'
  return new Date(iso).toLocaleString('tr-TR', {
    day: '2-digit', month: 'short', year: 'numeric',
    hour: '2-digit', minute: '2-digit',
  })
}

function formatDateShort(iso: string | null | undefined): string {
  if (!iso) return '—'
  return new Date(iso).toLocaleDateString('tr-TR', {
    day: '2-digit', month: 'short', year: 'numeric',
  })
}

function userInitials(user: UserListItem): string {
  if (user.displayName) {
    const parts = user.displayName.trim().split(' ')
    if (parts.length >= 2) return (parts[0][0] + parts[parts.length - 1][0]).toUpperCase()
    return parts[0].slice(0, 2).toUpperCase()
  }
  return user.username.slice(0, 2).toUpperCase()
}

const AVATAR_COLORS = [
  'bg-purple-100 text-purple-700',
  'bg-blue-100 text-blue-700',
  'bg-green-100 text-green-700',
  'bg-orange-100 text-orange-700',
  'bg-pink-100 text-pink-700',
  'bg-teal-100 text-teal-700',
]
function avatarColor(email: string): string {
  let hash = 0
  for (const c of email) hash = (hash * 31 + c.charCodeAt(0)) & 0xffff
  return AVATAR_COLORS[hash % AVATAR_COLORS.length]
}

// ─── Toggle switch ────────────────────────────────────────────────────────────
function ActiveToggle({ user, disabled, onChange }: {
  user: UserListItem
  disabled: boolean
  onChange: (isActive: boolean) => void
}) {
  return (
    <label className={`relative inline-flex items-center ${disabled ? 'cursor-not-allowed opacity-50' : 'cursor-pointer'}`}>
      <input
        type="checkbox"
        className="sr-only"
        checked={user.isActive}
        disabled={disabled}
        onChange={e => !disabled && onChange(e.target.checked)}
      />
      <div className={`w-9 h-5 rounded-full transition-colors ${user.isActive ? 'bg-green-500' : 'bg-gray-300'}`}>
        <div className={`absolute top-0.5 bg-white rounded-full h-4 w-4 transition-all ${user.isActive ? 'left-[18px]' : 'left-0.5'}`} />
      </div>
    </label>
  )
}

// ─── Action menu ─────────────────────────────────────────────────────────────
function ActionMenu({ onDetail, onRoleChange, onResetPassword, onDelete }: {
  onDetail: () => void
  onRoleChange: () => void
  onResetPassword: () => void
  onDelete: () => void
}) {
  const [open, setOpen] = useState(false)
  const [menuStyle, setMenuStyle] = useState<React.CSSProperties>({})
  const btnRef = useRef<HTMLButtonElement>(null)

  useEffect(() => {
    function handle(e: MouseEvent) {
      if (btnRef.current && !btnRef.current.contains(e.target as Node)) setOpen(false)
    }
    document.addEventListener('mousedown', handle)
    return () => document.removeEventListener('mousedown', handle)
  }, [])

  const handleToggle = () => {
    if (!open && btnRef.current) {
      const rect = btnRef.current.getBoundingClientRect()
      const dropdownH = 180
      const spaceBelow = window.innerHeight - rect.bottom
      const style: React.CSSProperties = {
        position: 'fixed',
        right: window.innerWidth - rect.right,
        width: '11rem',
        zIndex: 9999,
      }
      if (spaceBelow < dropdownH) {
        style.bottom = window.innerHeight - rect.top + 4
      } else {
        style.top = rect.bottom + 4
      }
      setMenuStyle(style)
    }
    setOpen(o => !o)
  }

  return (
    <div className="relative inline-block">
      <button
        ref={btnRef}
        onClick={handleToggle}
        className="text-gray-400 hover:text-gray-700 text-lg px-1 leading-none"
      >
        ⋯
      </button>
      {open && (
        <div style={menuStyle} className="bg-white border border-gray-200 rounded-lg shadow-lg py-1 text-sm">
          <button
            onClick={() => { setOpen(false); onDetail() }}
            className="w-full text-left px-4 py-2 hover:bg-gray-50 text-gray-700 flex items-center gap-2"
          >
            <ExternalLink size={13} className="text-gray-400" /> Detaylar
          </button>
          <div className="border-t border-gray-100 my-1" />
          <button
            onClick={() => { setOpen(false); onRoleChange() }}
            className="w-full text-left px-4 py-2 hover:bg-gray-50 text-gray-700"
          >
            Rol Değiştir
          </button>
          <button
            onClick={() => { setOpen(false); onResetPassword() }}
            className="w-full text-left px-4 py-2 hover:bg-gray-50 text-gray-700"
          >
            Şifre Sıfırla
          </button>
          <div className="border-t border-gray-100 my-1" />
          <button
            onClick={() => { setOpen(false); onDelete() }}
            className="w-full text-left px-4 py-2 hover:bg-red-50 text-red-600"
          >
            Sil
          </button>
        </div>
      )}
    </div>
  )
}

// ─── Detay paneli ─────────────────────────────────────────────────────────────
function DetailCard({ user, onClose, onRoleChange, onResetPassword, onDelete, isSelf }: {
  user: UserListItem
  onClose: () => void
  onRoleChange: () => void
  onResetPassword: () => void
  onDelete: () => void
  isSelf: boolean
}) {
  return (
    <>
      <div className="fixed inset-0 bg-black/30 z-30" onClick={onClose} />
      <div className="fixed right-0 top-0 h-full w-80 bg-white border-l border-gray-200 shadow-xl z-40 flex flex-col">
        {/* Header */}
        <div className="flex items-center justify-between px-5 py-4 border-b border-gray-100">
          <span className="text-sm font-semibold text-gray-800">Kullanıcı Detayı</span>
          <button onClick={onClose} className="text-gray-400 hover:text-gray-600 transition">
            <X size={18} />
          </button>
        </div>

        <div className="flex-1 overflow-y-auto">
          {/* Profil */}
          <div className="px-5 py-5 flex flex-col items-center text-center border-b border-gray-100">
            <div className={`w-14 h-14 rounded-full flex items-center justify-center text-lg font-bold mb-3 ${avatarColor(user.email)}`}>
              {userInitials(user)}
            </div>
            <div className="font-semibold text-gray-900 text-base">
              @{user.username}
              {isSelf && <span className="ml-1.5 text-xs text-gray-400 font-normal">(sen)</span>}
            </div>
            {user.displayName && (
              <div className="text-sm text-gray-500 mt-0.5">{user.displayName}</div>
            )}
            <div className="flex items-center gap-1.5 mt-2">
              <span className={`inline-flex items-center px-2 py-0.5 rounded-full text-xs font-medium ${ROLE_COLORS[user.role] ?? 'bg-gray-100 text-gray-700'}`}>
                <Shield size={10} className="mr-1" />{user.role}
              </span>
              <span className={`inline-flex items-center px-2 py-0.5 rounded-full text-xs font-medium ${user.isActive ? 'bg-green-100 text-green-700' : 'bg-red-100 text-red-600'}`}>
                {user.isActive ? 'Aktif' : 'Pasif'}
              </span>
            </div>
          </div>

          {/* Bilgiler */}
          <div className="px-5 py-4 flex flex-col gap-3 border-b border-gray-100 text-sm">
            <div className="flex items-start gap-3 text-gray-600">
              <span className="text-gray-400 mt-0.5">@</span>
              <div>
                <div className="text-xs text-gray-400 mb-0.5">E-posta</div>
                <div className="break-all">{user.email}</div>
              </div>
            </div>
            <div className="flex items-start gap-3 text-gray-600">
              <Calendar size={14} className="text-gray-400 mt-0.5 flex-shrink-0" />
              <div>
                <div className="text-xs text-gray-400 mb-0.5">Kayıt Tarihi</div>
                <div>{formatDate(user.createdAt)}</div>
              </div>
            </div>
            <div className="flex items-start gap-3 text-gray-600">
              <Clock size={14} className="text-gray-400 mt-0.5 flex-shrink-0" />
              <div>
                <div className="text-xs text-gray-400 mb-0.5">Son Giriş</div>
                <div>
                  {user.lastLoginAt
                    ? formatDate(user.lastLoginAt)
                    : <span className="text-gray-400 italic">Henüz giriş yapmadı</span>}
                </div>
              </div>
            </div>
            {user.lastActivityAt && (
              <div className="flex items-start gap-3 text-gray-600">
                <Clock size={14} className="text-gray-400 mt-0.5 flex-shrink-0" />
                <div>
                  <div className="text-xs text-gray-400 mb-0.5">Son Aktivite</div>
                  <div>{formatDate(user.lastActivityAt)}</div>
                </div>
              </div>
            )}
          </div>

          {/* İstatistikler */}
          <div className="px-5 py-4 border-b border-gray-100">
            <div className="text-xs text-gray-400 uppercase tracking-wide mb-3">Kullanım</div>
            <div className="grid grid-cols-2 gap-3">
              <div className="bg-gray-50 rounded-lg p-3 text-center">
                <Save size={14} className="mx-auto text-blue-500 mb-1" />
                <div className="text-xl font-semibold text-gray-900">{user.saveCount}</div>
                <div className="text-xs text-gray-400 mt-0.5">Kaydetme</div>
              </div>
              <div className="bg-gray-50 rounded-lg p-3 text-center">
                <Download size={14} className="mx-auto text-green-500 mb-1" />
                <div className="text-xl font-semibold text-gray-900">{user.downloadCount}</div>
                <div className="text-xs text-gray-400 mt-0.5">İndirme</div>
              </div>
            </div>
          </div>
        </div>

        {/* Footer aksiyonlar */}
        {!isSelf && (
          <div className="px-5 py-4 border-t border-gray-100 flex flex-col gap-2">
            <button
              onClick={onRoleChange}
              className="w-full px-3 py-2 text-sm text-gray-700 border border-gray-300 hover:bg-gray-50 rounded-lg transition text-left"
            >
              Rol Değiştir
            </button>
            <button
              onClick={onResetPassword}
              className="w-full px-3 py-2 text-sm text-gray-700 border border-gray-300 hover:bg-gray-50 rounded-lg transition text-left"
            >
              Şifre Sıfırla
            </button>
            <button
              onClick={onDelete}
              className="w-full px-3 py-2 text-sm text-red-600 border border-red-200 hover:bg-red-50 rounded-lg transition text-left"
            >
              Kullanıcıyı Sil
            </button>
          </div>
        )}
      </div>
    </>
  )
}

// ─── Rol değiştir modal ───────────────────────────────────────────────────────
function RoleModal({ user, onClose, onDone }: {
  user: UserListItem
  onClose: () => void
  onDone: () => void
}) {
  const [role, setRole] = useState(user.role)
  const [saving, setSaving] = useState(false)
  const [error, setError] = useState<string | null>(null)

  const handleSave = async () => {
    setSaving(true)
    setError(null)
    try {
      await updateRole(user.id, role)
      onDone()
    } catch {
      setError('Rol değiştirilemedi.')
    } finally {
      setSaving(false)
    }
  }

  return (
    <div className="fixed inset-0 bg-black/50 flex items-center justify-center z-50">
      <div className="bg-white rounded-xl shadow-xl p-6 w-full max-w-sm mx-4">
        <h3 className="text-base font-semibold text-gray-900 mb-1">Rol Değiştir</h3>
        <p className="text-sm text-gray-500 mb-4">
          {user.displayName || user.email} için yeni rol seçin.
        </p>
        {error && (
          <div className="flex items-center gap-2 text-sm text-red-600 bg-red-50 border border-red-200 rounded-lg px-3 py-2 mb-4">
            <AlertCircle size={14} /> {error}
          </div>
        )}
        <div className="flex flex-col gap-2">
          {(['User', 'Editor', 'Admin'] as const).map(r => (
            <label key={r} className={`flex items-center gap-3 px-3 py-2.5 border rounded-lg cursor-pointer ${role === r ? 'border-blue-300 bg-blue-50' : 'border-gray-200 hover:bg-gray-50'}`}>
              <input type="radio" name="role" value={r} checked={role === r} onChange={() => setRole(r)} className="accent-blue-600" />
              <div>
                <div className="text-sm font-medium text-gray-900">{r}</div>
              </div>
            </label>
          ))}
        </div>
        <div className="flex gap-3 justify-end mt-6">
          <button onClick={onClose} className="px-4 py-2 text-sm text-gray-700 border border-gray-300 hover:bg-gray-50 rounded-lg transition">İptal</button>
          <button onClick={handleSave} disabled={saving} className="px-4 py-2 text-sm text-white bg-blue-600 hover:bg-blue-700 disabled:opacity-50 rounded-lg transition">
            {saving ? 'Kaydediliyor…' : 'Kaydet'}
          </button>
        </div>
      </div>
    </div>
  )
}

// ─── Şifre sıfırlama modal ────────────────────────────────────────────────────
function PasswordModal({ user, onClose, onDone }: {
  user: UserListItem
  onClose: () => void
  onDone: () => void
}) {
  const [pwd, setPwd] = useState('')
  const [pwd2, setPwd2] = useState('')
  const [saving, setSaving] = useState(false)
  const [error, setError] = useState<string | null>(null)

  const handleSave = async () => {
    if (pwd.length < 6) { setError('Şifre en az 6 karakter olmalıdır.'); return }
    if (pwd !== pwd2) { setError('Şifreler eşleşmiyor.'); return }
    setSaving(true)
    setError(null)
    try {
      await resetPassword(user.id, pwd)
      onDone()
    } catch {
      setError('Şifre sıfırlanamadı.')
    } finally {
      setSaving(false)
    }
  }

  return (
    <div className="fixed inset-0 bg-black/50 flex items-center justify-center z-50">
      <div className="bg-white rounded-xl shadow-xl p-6 w-full max-w-sm mx-4">
        <h3 className="text-base font-semibold text-gray-900 mb-1">Şifre Sıfırla</h3>
        <p className="text-sm text-gray-500 mb-4">{user.displayName || user.email} için yeni şifre belirleyin.</p>
        {error && (
          <div className="flex items-center gap-2 text-sm text-red-600 bg-red-50 border border-red-200 rounded-lg px-3 py-2 mb-4">
            <AlertCircle size={14} /> {error}
          </div>
        )}
        <div className="flex flex-col gap-3">
          <div>
            <label className="text-xs font-medium text-gray-600 block mb-1">Yeni Şifre</label>
            <input type="password" value={pwd} onChange={e => setPwd(e.target.value)} placeholder="••••••••" className="w-full px-3 py-2 border border-gray-300 rounded-lg text-sm outline-none focus:ring-2 focus:ring-blue-500" />
          </div>
          <div>
            <label className="text-xs font-medium text-gray-600 block mb-1">Şifre Tekrar</label>
            <input type="password" value={pwd2} onChange={e => setPwd2(e.target.value)} placeholder="••••••••" className="w-full px-3 py-2 border border-gray-300 rounded-lg text-sm outline-none focus:ring-2 focus:ring-blue-500" />
          </div>
        </div>
        <p className="text-xs text-amber-600 mt-3 flex items-start gap-1">
          <span>⚠</span>
          <span>Mevcut oturumlar geçersiz kılınacak.</span>
        </p>
        <div className="flex gap-3 justify-end mt-5">
          <button onClick={onClose} className="px-4 py-2 text-sm text-gray-700 border border-gray-300 hover:bg-gray-50 rounded-lg transition">İptal</button>
          <button onClick={handleSave} disabled={saving} className="px-4 py-2 text-sm text-white bg-blue-600 hover:bg-blue-700 disabled:opacity-50 rounded-lg transition">
            {saving ? 'Güncelleniyor…' : 'Şifreyi Güncelle'}
          </button>
        </div>
      </div>
    </div>
  )
}

// ─── Silme onay modal ─────────────────────────────────────────────────────────
function DeleteModal({ user, onClose, onDone }: {
  user: UserListItem
  onClose: () => void
  onDone: () => void
}) {
  const [deleting, setDeleting] = useState(false)
  const [error, setError] = useState<string | null>(null)

  const handleDelete = async () => {
    setDeleting(true)
    setError(null)
    try {
      await deleteUser(user.id)
      onDone()
    } catch {
      setError('Kullanıcı silinemedi.')
      setDeleting(false)
    }
  }

  return (
    <div className="fixed inset-0 bg-black/50 flex items-center justify-center z-50">
      <div className="bg-white rounded-xl shadow-xl p-6 w-full max-w-sm mx-4">
        <h3 className="text-base font-semibold text-gray-900 mb-2">Kullanıcıyı Sil</h3>
        <p className="text-sm text-gray-600 mb-2">
          <span className="font-medium text-gray-900">{user.displayName || user.email}</span> hesabı ve tüm verileri{' '}
          <span className="text-red-600 font-medium">kalıcı olarak silinecek</span>.
        </p>
        <div className="bg-red-50 border border-red-200 rounded-lg px-3 py-2 text-xs text-red-700 mb-4">
          Şablonlar, snippet'lar ve aktivite geçmişi dahil tüm veriler geri alınamaz şekilde silinir.
        </div>
        {error && (
          <div className="flex items-center gap-2 text-sm text-red-600 bg-red-50 border border-red-200 rounded-lg px-3 py-2 mb-4">
            <AlertCircle size={14} /> {error}
          </div>
        )}
        <div className="flex gap-3 justify-end">
          <button onClick={onClose} className="px-4 py-2 text-sm text-gray-700 border border-gray-300 hover:bg-gray-50 rounded-lg transition">İptal</button>
          <button onClick={handleDelete} disabled={deleting} className="px-4 py-2 text-sm text-white bg-red-600 hover:bg-red-700 disabled:opacity-50 rounded-lg transition">
            {deleting ? 'Siliniyor…' : 'Evet, Sil'}
          </button>
        </div>
      </div>
    </div>
  )
}

// ─── Yeni kullanıcı modal ─────────────────────────────────────────────────────
function CreateUserModal({ onClose, onDone }: {
  onClose: () => void
  onDone: () => void
}) {
  const [form, setForm] = useState({ username: '', email: '', displayName: '', role: 'User', password: '' })
  const [saving, setSaving] = useState(false)
  const [error, setError] = useState<string | null>(null)

  const handleCreate = async () => {
    if (!form.username.trim()) { setError('Kullanıcı adı boş olamaz.'); return }
    if (!form.email.trim()) { setError('E-posta boş olamaz.'); return }
    if (!form.password || form.password.length < 6) { setError('Şifre en az 6 karakter olmalıdır.'); return }
    setSaving(true)
    setError(null)
    try {
      await createUser({ username: form.username.trim(), email: form.email.trim(), displayName: form.displayName.trim() || undefined, role: form.role, password: form.password })
      onDone()
    } catch (err: unknown) {
      const msg = (err as { response?: { data?: { message?: string } } })?.response?.data?.message
      setError(msg ?? 'Kullanıcı oluşturulamadı.')
    } finally {
      setSaving(false)
    }
  }

  return (
    <div className="fixed inset-0 bg-black/50 flex items-center justify-center z-50">
      <div className="bg-white rounded-xl shadow-xl p-6 w-full max-w-md mx-4">
        <h3 className="text-base font-semibold text-gray-900 mb-4">Yeni Kullanıcı Oluştur</h3>
        {error && (
          <div className="flex items-center gap-2 text-sm text-red-600 bg-red-50 border border-red-200 rounded-lg px-3 py-2 mb-4">
            <AlertCircle size={14} /> {error}
          </div>
        )}
        <div className="flex flex-col gap-3">
          <div>
            <label className="text-xs font-medium text-gray-600 block mb-1">Kullanıcı Adı *</label>
            <input type="text" value={form.username} onChange={e => setForm(f => ({ ...f, username: e.target.value }))} autoFocus placeholder="kullanici_adi" className="w-full px-3 py-2 border border-gray-300 rounded-lg text-sm outline-none focus:ring-2 focus:ring-blue-500" />
            <p className="text-xs text-gray-400 mt-1">3-30 karakter, harf/rakam/alt çizgi</p>
          </div>
          <div>
            <label className="text-xs font-medium text-gray-600 block mb-1">E-posta *</label>
            <input type="email" value={form.email} onChange={e => setForm(f => ({ ...f, email: e.target.value }))} placeholder="kullanici@ornek.com" className="w-full px-3 py-2 border border-gray-300 rounded-lg text-sm outline-none focus:ring-2 focus:ring-blue-500" />
          </div>
          <div>
            <label className="text-xs font-medium text-gray-600 block mb-1">Ad Soyad</label>
            <input type="text" value={form.displayName} onChange={e => setForm(f => ({ ...f, displayName: e.target.value }))} placeholder="Ad Soyad" className="w-full px-3 py-2 border border-gray-300 rounded-lg text-sm outline-none focus:ring-2 focus:ring-blue-500" />
          </div>
          <div>
            <label className="text-xs font-medium text-gray-600 block mb-1">Rol</label>
            <select value={form.role} onChange={e => setForm(f => ({ ...f, role: e.target.value }))} className="w-full px-3 py-2 border border-gray-300 rounded-lg text-sm bg-white">
              <option value="User">User</option>
              <option value="Editor">Editor</option>
              <option value="Admin">Admin</option>
            </select>
          </div>
          <div>
            <label className="text-xs font-medium text-gray-600 block mb-1">Şifre *</label>
            <input type="password" value={form.password} onChange={e => setForm(f => ({ ...f, password: e.target.value }))} placeholder="En az 6 karakter" className="w-full px-3 py-2 border border-gray-300 rounded-lg text-sm outline-none focus:ring-2 focus:ring-blue-500" />
          </div>
        </div>
        <div className="flex gap-3 justify-end mt-6">
          <button onClick={onClose} className="px-4 py-2 text-sm text-gray-700 border border-gray-300 hover:bg-gray-50 rounded-lg transition">İptal</button>
          <button onClick={handleCreate} disabled={saving} className="px-4 py-2 text-sm text-white bg-blue-600 hover:bg-blue-700 disabled:opacity-50 rounded-lg transition">
            {saving ? 'Oluşturuluyor…' : 'Oluştur'}
          </button>
        </div>
      </div>
    </div>
  )
}

// ─── Ana sayfa ────────────────────────────────────────────────────────────────
export default function AdminUsersPage() {
  const currentUser = useAuthStore(s => s.user)

  const [items, setItems] = useState<UserListItem[]>([])
  const [totalCount, setTotalCount] = useState(0)
  const [activeCount, setActiveCount] = useState(0)
  const [inactiveCount, setInactiveCount] = useState(0)
  const [totalActivityCount, setTotalActivityCount] = useState(0)
  const [page, setPage] = useState(1)
  const PAGE_SIZE = 20

  const [query, setQuery] = useState('')
  const [filterRole, setFilterRole] = useState('')
  const [filterActive, setFilterActive] = useState('')

  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)

  const [showCreate, setShowCreate] = useState(false)
  const [detailTarget, setDetailTarget] = useState<UserListItem | null>(null)
  const [roleTarget, setRoleTarget] = useState<UserListItem | null>(null)
  const [pwdTarget, setPwdTarget] = useState<UserListItem | null>(null)
  const [deleteTarget, setDeleteTarget] = useState<UserListItem | null>(null)

  const totalPages = Math.max(1, Math.ceil(totalCount / PAGE_SIZE))

  const load = async (p = page) => {
    setLoading(true)
    setError(null)
    try {
      const isActive = filterActive === 'true' ? true : filterActive === 'false' ? false : undefined
      const resp = await listUsers({ query: query || undefined, role: filterRole || undefined, isActive, page: p, pageSize: PAGE_SIZE })
      setItems(resp.items)
      setTotalCount(resp.totalCount)
      setActiveCount(resp.activeCount)
      setInactiveCount(resp.inactiveCount)
      setTotalActivityCount(resp.totalActivityCount)
    } catch {
      setError('Kullanıcılar yüklenemedi.')
    } finally {
      setLoading(false)
    }
  }

  useEffect(() => { load(1); setPage(1) }, [query, filterRole, filterActive]) // eslint-disable-line react-hooks/exhaustive-deps
  useEffect(() => { load(page) }, [page]) // eslint-disable-line react-hooks/exhaustive-deps

  const handleToggleActive = async (user: UserListItem, val: boolean) => {
    try {
      await setActive(user.id, val)
      setItems(prev => prev.map(u => u.id === user.id ? { ...u, isActive: val } : u))
    } catch {
      // silently ignore, revert via reload
      load()
    }
  }

  return (
    <div className="max-w-7xl mx-auto px-6 py-8">

      {/* Başlık */}
      <div className="flex items-center justify-between mb-6">
        <div>
          <h1 className="text-xl font-semibold text-gray-900 flex items-center gap-2">
            <Users size={20} className="text-blue-600" />
            Kullanıcıları Yönet
          </h1>
          <p className="text-sm text-gray-500 mt-1">Kullanıcı listesi, roller, aktivite ve hesap durumları.</p>
        </div>
        <button
          onClick={() => setShowCreate(true)}
          className="inline-flex items-center gap-2 px-4 py-2 bg-blue-600 hover:bg-blue-700 text-white text-sm font-medium rounded-lg shadow-sm transition"
        >
          <Plus size={15} />
          Yeni Kullanıcı
        </button>
      </div>

      {/* Özet kartları */}
      <div className="grid grid-cols-2 sm:grid-cols-4 gap-4 mb-6">
        <div className="bg-white rounded-lg border border-gray-200 p-4">
          <div className="text-xs text-gray-500 uppercase tracking-wide">Toplam Kullanıcı</div>
          <div className="text-2xl font-semibold text-gray-900 mt-1">{totalCount}</div>
        </div>
        <div className="bg-white rounded-lg border border-gray-200 p-4">
          <div className="text-xs text-gray-500 uppercase tracking-wide">Aktif</div>
          <div className="text-2xl font-semibold text-green-600 mt-1">{activeCount}</div>
        </div>
        <div className="bg-white rounded-lg border border-gray-200 p-4">
          <div className="text-xs text-gray-500 uppercase tracking-wide">Pasif</div>
          <div className="text-2xl font-semibold text-gray-400 mt-1">{inactiveCount}</div>
        </div>
        <div className="bg-white rounded-lg border border-gray-200 p-4">
          <div className="text-xs text-gray-500 uppercase tracking-wide">Toplam Aktivite</div>
          <div className="text-2xl font-semibold text-blue-600 mt-1">{totalActivityCount.toLocaleString('tr-TR')}</div>
        </div>
      </div>

      {/* Filtre barı */}
      <div className="bg-white border border-gray-200 rounded-lg p-4 mb-4 flex flex-wrap items-center gap-3">
        <div className="relative flex-1 min-w-[200px]">
          <Search size={14} className="absolute left-3 top-1/2 -translate-y-1/2 text-gray-400" />
          <input
            type="text"
            value={query}
            onChange={e => setQuery(e.target.value)}
            placeholder="Kullanıcı adı, email veya isim ara…"
            className="w-full pl-8 pr-3 py-2 border border-gray-300 rounded-md text-sm outline-none focus:ring-2 focus:ring-blue-500"
          />
        </div>
        <select value={filterRole} onChange={e => setFilterRole(e.target.value)} className="px-3 py-2 border border-gray-300 rounded-md text-sm bg-white">
          <option value="">Tüm Roller</option>
          <option value="Admin">Admin</option>
          <option value="Editor">Editor</option>
          <option value="User">User</option>
        </select>
        <select value={filterActive} onChange={e => setFilterActive(e.target.value)} className="px-3 py-2 border border-gray-300 rounded-md text-sm bg-white">
          <option value="">Tüm Durumlar</option>
          <option value="true">Aktif</option>
          <option value="false">Pasif</option>
        </select>
      </div>

      {/* Hata / yükleniyor */}
      {error && (
        <div className="flex items-center gap-2 text-sm text-red-600 bg-red-50 border border-red-200 rounded-lg px-4 py-3 mb-4">
          <AlertCircle size={15} /> {error}
        </div>
      )}
      {loading && <p className="text-sm text-gray-500 mb-4">Yükleniyor…</p>}

      {/* Tablo */}
      {!loading && (
        <div className="bg-white border border-gray-200 rounded-lg overflow-hidden">
          <table className="w-full text-sm">
            <thead className="bg-gray-50 border-b border-gray-200">
              <tr className="text-left text-xs uppercase text-gray-500 tracking-wide">
                <th className="px-4 py-3 font-medium">Kullanıcı</th>
                <th className="px-4 py-3 font-medium">Rol</th>
                <th className="px-4 py-3 font-medium">Durum</th>
                <th className="px-4 py-3 font-medium text-right">Kaydet</th>
                <th className="px-4 py-3 font-medium text-right">İndirme</th>
                <th className="px-4 py-3 font-medium">Son Login</th>
                <th className="px-4 py-3 font-medium">Kayıt</th>
                <th className="px-4 py-3 font-medium text-right">İşlem</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-gray-100">
              {items.length === 0 && (
                <tr>
                  <td colSpan={8} className="px-4 py-8 text-center text-gray-400 text-sm">
                    Kullanıcı bulunamadı.
                  </td>
                </tr>
              )}
              {items.map(u => {
                const isSelf = u.id === currentUser?.id
                return (
                  <tr key={u.id} className={`hover:bg-gray-50 ${!u.isActive ? 'opacity-60' : ''}`}>
                    <td className="px-4 py-3">
                      <div className="flex items-center gap-3">
                        <div className={`w-8 h-8 rounded-full flex items-center justify-center text-xs font-semibold flex-shrink-0 ${avatarColor(u.email)}`}>
                          {userInitials(u)}
                        </div>
                        <div>
                          <div className="font-medium text-gray-900">
                            @{u.username}
                            {isSelf && <span className="ml-1.5 text-xs text-gray-400">(sen)</span>}
                          </div>
                          <div className="text-xs text-gray-500">{u.email}</div>
                        </div>
                      </div>
                    </td>
                    <td className="px-4 py-3">
                      <span className={`inline-flex items-center px-2 py-0.5 rounded-full text-xs font-medium ${ROLE_COLORS[u.role] ?? 'bg-gray-100 text-gray-700'}`}>
                        {u.role}
                      </span>
                    </td>
                    <td className="px-4 py-3">
                      <div className="flex items-center gap-2">
                        <ActiveToggle
                          user={u}
                          disabled={isSelf}
                          onChange={val => handleToggleActive(u, val)}
                        />
                        {!u.isActive && <span className="text-xs text-red-500 font-medium">Pasif</span>}
                      </div>
                    </td>
                    <td className="px-4 py-3 text-right tabular-nums text-gray-700">{u.saveCount}</td>
                    <td className="px-4 py-3 text-right tabular-nums text-gray-700">{u.downloadCount}</td>
                    <td className="px-4 py-3 text-gray-600 text-xs whitespace-nowrap">
                      {u.lastLoginAt
                        ? formatDate(u.lastLoginAt)
                        : <span className="text-gray-400 italic">Hiç login olmadı</span>}
                    </td>
                    <td className="px-4 py-3 text-gray-600 text-xs whitespace-nowrap">{formatDateShort(u.createdAt)}</td>
                    <td className="px-4 py-3 text-right">
                      {isSelf
                        ? <Trash2 size={14} className="text-gray-200 ml-auto" />
                        : (
                          <ActionMenu
                            onDetail={() => setDetailTarget(u)}
                            onRoleChange={() => setRoleTarget(u)}
                            onResetPassword={() => setPwdTarget(u)}
                            onDelete={() => setDeleteTarget(u)}
                          />
                        )}
                    </td>
                  </tr>
                )
              })}
            </tbody>
          </table>

          {/* Sayfalama */}
          <div className="flex items-center justify-between px-4 py-3 border-t border-gray-200 bg-gray-50">
            <div className="text-xs text-gray-500">
              {totalCount} kullanıcıdan {(page - 1) * PAGE_SIZE + 1}–{Math.min(page * PAGE_SIZE, totalCount)} arası
            </div>
            <div className="flex items-center gap-1">
              <button
                disabled={page <= 1}
                onClick={() => setPage(p => p - 1)}
                className="px-3 py-1 text-sm border border-gray-300 rounded bg-white hover:bg-gray-50 disabled:opacity-40"
              >
                ‹ Önceki
              </button>
              {Array.from({ length: Math.min(5, totalPages) }, (_, i) => {
                const p = Math.max(1, Math.min(page - 2, totalPages - 4)) + i
                return (
                  <button
                    key={p}
                    onClick={() => setPage(p)}
                    className={`px-3 py-1 text-sm border rounded ${p === page ? 'border-blue-500 bg-blue-50 text-blue-700 font-medium' : 'border-gray-300 bg-white hover:bg-gray-50'}`}
                  >
                    {p}
                  </button>
                )
              })}
              <button
                disabled={page >= totalPages}
                onClick={() => setPage(p => p + 1)}
                className="px-3 py-1 text-sm border border-gray-300 rounded bg-white hover:bg-gray-50 disabled:opacity-40"
              >
                Sonraki ›
              </button>
            </div>
          </div>
        </div>
      )}

      {/* Detay paneli */}
      {detailTarget && (
        <DetailCard
          user={detailTarget}
          isSelf={detailTarget.id === currentUser?.id}
          onClose={() => setDetailTarget(null)}
          onRoleChange={() => { setDetailTarget(null); setRoleTarget(detailTarget) }}
          onResetPassword={() => { setDetailTarget(null); setPwdTarget(detailTarget) }}
          onDelete={() => { setDetailTarget(null); setDeleteTarget(detailTarget) }}
        />
      )}

      {/* Modaller */}
      {showCreate && (
        <CreateUserModal
          onClose={() => setShowCreate(false)}
          onDone={() => { setShowCreate(false); load(1); setPage(1) }}
        />
      )}
      {roleTarget && (
        <RoleModal
          user={roleTarget}
          onClose={() => setRoleTarget(null)}
          onDone={() => { setRoleTarget(null); load() }}
        />
      )}
      {pwdTarget && (
        <PasswordModal
          user={pwdTarget}
          onClose={() => setPwdTarget(null)}
          onDone={() => setPwdTarget(null)}
        />
      )}
      {deleteTarget && (
        <DeleteModal
          user={deleteTarget}
          onClose={() => setDeleteTarget(null)}
          onDone={() => { setDeleteTarget(null); load(1); setPage(1) }}
        />
      )}
    </div>
  )
}
