import { useEffect, useRef, useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { Download, Trash2 } from 'lucide-react'
import { useAuthStore } from '../store/authStore'
import { updateProfile, changePassword, deleteAccount } from '../services/profileService'
import { getMyTemplates, downloadTemplate, type TemplateDetail } from '../services/templateService'

const DOC_TYPE_LABEL: Record<string, string> = {
  Invoice: 'e-Fatura / e-Arşiv',
  Despatch: 'e-İrsaliye',
}

const PASSWORD_RULES = /^(?=.*[A-Z])(?=.*\d).{8,}$/

export default function ProfilePage() {
  const { user, updateUser, logout, setAccessToken } = useAuthStore()
  const navigate = useNavigate()

  // ── Profil formu ─────────────────────────────────────────────────────────────
  const [displayName, setDisplayName] = useState(user?.displayName ?? '')
  const [email, setEmail] = useState(user?.email ?? '')
  const [profileSaving, setProfileSaving] = useState(false)
  const [profileMsg, setProfileMsg] = useState<{ type: 'ok' | 'err'; text: string } | null>(null)

  async function handleSaveProfile(e: React.FormEvent) {
    e.preventDefault()
    setProfileSaving(true)
    setProfileMsg(null)
    try {
      const updated = await updateProfile({
        displayName: displayName.trim() || undefined,
        email: email.trim() !== user?.email ? email.trim() : undefined,
      })
      updateUser({ displayName: updated.displayName, email: updated.email })
      setProfileMsg({ type: 'ok', text: 'Profil güncellendi.' })
    } catch (err: unknown) {
      const msg =
        (err as { response?: { data?: { message?: string } } })?.response?.data?.message ??
        'Güncelleme başarısız.'
      setProfileMsg({ type: 'err', text: msg })
    } finally {
      setProfileSaving(false)
    }
  }

  // ── Şifre değiştirme ─────────────────────────────────────────────────────────
  const [currentPassword, setCurrentPassword] = useState('')
  const [newPassword, setNewPassword] = useState('')
  const [confirmPassword, setConfirmPassword] = useState('')
  const [passwordSaving, setPasswordSaving] = useState(false)
  const [passwordMsg, setPasswordMsg] = useState<{ type: 'ok' | 'err'; text: string } | null>(null)

  async function handleChangePassword(e: React.FormEvent) {
    e.preventDefault()
    setPasswordMsg(null)

    if (!PASSWORD_RULES.test(newPassword)) {
      setPasswordMsg({
        type: 'err',
        text: 'Yeni şifre en az 8 karakter, 1 büyük harf ve 1 rakam içermelidir.',
      })
      return
    }
    if (newPassword !== confirmPassword) {
      setPasswordMsg({ type: 'err', text: 'Yeni şifreler eşleşmiyor.' })
      return
    }

    setPasswordSaving(true)
    try {
      const { accessToken } = await changePassword({ currentPassword, newPassword })
      setAccessToken(accessToken)
      setCurrentPassword('')
      setNewPassword('')
      setConfirmPassword('')
      setPasswordMsg({ type: 'ok', text: 'Şifreniz güncellendi.' })
    } catch (err: unknown) {
      const msg =
        (err as { response?: { data?: { message?: string } } })?.response?.data?.message ??
        'Şifre değiştirilemedi.'
      setPasswordMsg({ type: 'err', text: msg })
    } finally {
      setPasswordSaving(false)
    }
  }

  // ── Şablonlarım ──────────────────────────────────────────────────────────────
  const [templates, setTemplates] = useState<TemplateDetail[]>([])
  const [templatesLoading, setTemplatesLoading] = useState(true)
  const [downloadingId, setDownloadingId] = useState<string | null>(null)

  useEffect(() => {
    getMyTemplates()
      .then(setTemplates)
      .catch(() => {})
      .finally(() => setTemplatesLoading(false))
  }, [])

  async function handleDownload(tpl: TemplateDetail) {
    setDownloadingId(tpl.id)
    try {
      await downloadTemplate(tpl.id, tpl.name)
    } catch {
      alert('İndirme başarısız.')
    } finally {
      setDownloadingId(null)
    }
  }

  // ── Hesap silme ───────────────────────────────────────────────────────────────
  const [showDeleteConfirm, setShowDeleteConfirm] = useState(false)
  const [deleteInput, setDeleteInput] = useState('')
  const [deleting, setDeleting] = useState(false)
  const deleteInputRef = useRef<HTMLInputElement>(null)

  const DELETE_PHRASE = 'hesabımı sil'

  async function handleDeleteAccount() {
    if (deleteInput !== DELETE_PHRASE) return
    setDeleting(true)
    try {
      await deleteAccount()
      logout()
      navigate('/auth/login')
    } catch {
      alert('Hesap silinemedi.')
      setDeleting(false)
    }
  }

  return (
    <div className="max-w-2xl mx-auto px-6 py-8 flex flex-col gap-8">
      <h1 className="text-xl font-semibold text-gray-800">Profilim</h1>

      {/* ── Profil bilgileri ── */}
      <section className="bg-white border border-gray-200 rounded-xl p-6 flex flex-col gap-4">
        <h2 className="text-sm font-semibold text-gray-700">Kişisel Bilgiler</h2>
        <form onSubmit={handleSaveProfile} className="flex flex-col gap-3">
          <div className="flex flex-col gap-1">
            <label className="text-xs font-medium text-gray-500 uppercase tracking-wide">
              Görünen Ad
            </label>
            <input
              type="text"
              value={displayName}
              onChange={(e) => setDisplayName(e.target.value)}
              placeholder="Adınız"
              maxLength={100}
              className="text-sm border border-gray-200 rounded-lg px-3 py-2 outline-none focus:border-blue-400 focus:ring-1 focus:ring-blue-100"
            />
          </div>
          <div className="flex flex-col gap-1">
            <label className="text-xs font-medium text-gray-500 uppercase tracking-wide">
              E-posta
            </label>
            <input
              type="email"
              value={email}
              onChange={(e) => setEmail(e.target.value)}
              placeholder="eposta@ornek.com"
              className="text-sm border border-gray-200 rounded-lg px-3 py-2 outline-none focus:border-blue-400 focus:ring-1 focus:ring-blue-100"
            />
          </div>
          {profileMsg && (
            <p
              className={`text-xs ${profileMsg.type === 'ok' ? 'text-emerald-600' : 'text-red-500'}`}
            >
              {profileMsg.text}
            </p>
          )}
          <button
            type="submit"
            disabled={profileSaving}
            className="self-start px-4 py-2 text-sm font-medium bg-blue-600 text-white rounded-lg hover:bg-blue-700 disabled:opacity-50 transition-colors"
          >
            {profileSaving ? 'Kaydediliyor…' : 'Kaydet'}
          </button>
        </form>
      </section>

      {/* ── Şifre değiştirme ── */}
      <section className="bg-white border border-gray-200 rounded-xl p-6 flex flex-col gap-4">
        <h2 className="text-sm font-semibold text-gray-700">Şifre Değiştir</h2>
        <form onSubmit={handleChangePassword} className="flex flex-col gap-3">
          <div className="flex flex-col gap-1">
            <label className="text-xs font-medium text-gray-500 uppercase tracking-wide">
              Mevcut Şifre
            </label>
            <input
              type="password"
              required
              autoComplete="current-password"
              value={currentPassword}
              onChange={(e) => setCurrentPassword(e.target.value)}
              placeholder="••••••••"
              className="text-sm border border-gray-200 rounded-lg px-3 py-2 outline-none focus:border-blue-400 focus:ring-1 focus:ring-blue-100"
            />
          </div>
          <div className="flex flex-col gap-1">
            <label className="text-xs font-medium text-gray-500 uppercase tracking-wide">
              Yeni Şifre
            </label>
            <input
              type="password"
              required
              autoComplete="new-password"
              value={newPassword}
              onChange={(e) => setNewPassword(e.target.value)}
              placeholder="••••••••"
              className="text-sm border border-gray-200 rounded-lg px-3 py-2 outline-none focus:border-blue-400 focus:ring-1 focus:ring-blue-100"
            />
            <p className="text-xs text-gray-400">En az 8 karakter, 1 büyük harf, 1 rakam</p>
          </div>
          <div className="flex flex-col gap-1">
            <label className="text-xs font-medium text-gray-500 uppercase tracking-wide">
              Yeni Şifre (Tekrar)
            </label>
            <input
              type="password"
              required
              autoComplete="new-password"
              value={confirmPassword}
              onChange={(e) => setConfirmPassword(e.target.value)}
              placeholder="••••••••"
              className="text-sm border border-gray-200 rounded-lg px-3 py-2 outline-none focus:border-blue-400 focus:ring-1 focus:ring-blue-100"
            />
          </div>
          {passwordMsg && (
            <p
              className={`text-xs ${passwordMsg.type === 'ok' ? 'text-emerald-600' : 'text-red-500'}`}
            >
              {passwordMsg.text}
            </p>
          )}
          <button
            type="submit"
            disabled={passwordSaving}
            className="self-start px-4 py-2 text-sm font-medium bg-blue-600 text-white rounded-lg hover:bg-blue-700 disabled:opacity-50 transition-colors"
          >
            {passwordSaving ? 'Güncelleniyor…' : 'Şifreyi Değiştir'}
          </button>
        </form>
      </section>

      {/* ── Şablonlarım ── */}
      <section className="bg-white border border-gray-200 rounded-xl p-6 flex flex-col gap-4">
        <h2 className="text-sm font-semibold text-gray-700">Şablonlarım</h2>
        {templatesLoading ? (
          <p className="text-xs text-gray-400">Yükleniyor…</p>
        ) : templates.length === 0 ? (
          <p className="text-xs text-gray-400">Henüz bir şablonunuz yok.</p>
        ) : (
          <div className="flex flex-col gap-2">
            {templates.map((tpl) => (
              <div
                key={tpl.id}
                className="flex items-center justify-between gap-3 px-3 py-2.5 border border-gray-100 rounded-lg bg-gray-50"
              >
                <div className="flex items-center gap-2 min-w-0">
                  <span className="text-sm font-medium text-gray-800 truncate">{tpl.name}</span>
                  <span className="flex-shrink-0 text-xs text-blue-500 bg-blue-50 rounded-full px-2 py-0.5">
                    {DOC_TYPE_LABEL[tpl.documentType] ?? tpl.documentType}
                  </span>
                </div>
                <button
                  onClick={() => handleDownload(tpl)}
                  disabled={downloadingId === tpl.id}
                  title="XSLT İndir"
                  className="flex items-center gap-1 text-xs text-gray-500 hover:text-blue-600 disabled:opacity-40 flex-shrink-0 transition-colors"
                >
                  <Download size={14} />
                  {downloadingId === tpl.id ? 'İndiriliyor…' : 'İndir'}
                </button>
              </div>
            ))}
          </div>
        )}
      </section>

      {/* ── Hesap silme ── */}
      <section className="bg-white border border-red-100 rounded-xl p-6 flex flex-col gap-4">
        <h2 className="text-sm font-semibold text-red-600">Tehlikeli Alan</h2>
        {!showDeleteConfirm ? (
          <div className="flex items-center justify-between">
            <p className="text-xs text-gray-500">
              Hesabınızı kalıcı olarak silin. Bu işlem geri alınamaz.
            </p>
            <button
              onClick={() => {
                setShowDeleteConfirm(true)
                setTimeout(() => deleteInputRef.current?.focus(), 50)
              }}
              className="flex items-center gap-1.5 text-sm text-red-600 border border-red-200 hover:bg-red-50 rounded-lg px-3 py-2 transition-colors flex-shrink-0 ml-4"
            >
              <Trash2 size={14} />
              Hesabı Sil
            </button>
          </div>
        ) : (
          <div className="flex flex-col gap-3">
            <p className="text-xs text-gray-600">
              Onaylamak için aşağıya{' '}
              <span className="font-mono font-semibold text-red-600">{DELETE_PHRASE}</span> yazın:
            </p>
            <input
              ref={deleteInputRef}
              type="text"
              value={deleteInput}
              onChange={(e) => setDeleteInput(e.target.value)}
              placeholder={DELETE_PHRASE}
              className="text-sm border border-red-200 rounded-lg px-3 py-2 outline-none focus:border-red-400 focus:ring-1 focus:ring-red-100"
            />
            <div className="flex gap-2">
              <button
                onClick={handleDeleteAccount}
                disabled={deleteInput !== DELETE_PHRASE || deleting}
                className="px-4 py-2 text-sm font-medium bg-red-600 text-white rounded-lg hover:bg-red-700 disabled:opacity-40 disabled:cursor-not-allowed transition-colors"
              >
                {deleting ? 'Siliniyor…' : 'Hesabı Kalıcı Olarak Sil'}
              </button>
              <button
                onClick={() => {
                  setShowDeleteConfirm(false)
                  setDeleteInput('')
                }}
                className="px-4 py-2 text-sm text-gray-600 border border-gray-200 rounded-lg hover:bg-gray-50 transition-colors"
              >
                İptal
              </button>
            </div>
          </div>
        )}
      </section>
    </div>
  )
}
