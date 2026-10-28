import { useEffect, useRef, useState, type ReactNode } from 'react'
import { Link, useNavigate } from 'react-router-dom'
import { Crown, Download, Trash2 } from 'lucide-react'
import { useAuthStore } from '../store/authStore'
import { useEntitlementStore } from '../store/entitlementStore'
import PageHeader from '../components/ui/PageHeader'
import Card from '../components/ui/Card'
import IconTile from '../components/ui/IconTile'
import { userInitials } from '../components/shell/userDisplay'
import { fnStyle } from '../theme/uiThemes'
import { DOC_TYPE_LABEL } from '../utils/workRoutes'
import { updateProfile, changePassword, deleteAccount } from '../services/profileService'
import { getMyTemplates, downloadTemplate, type TemplateDetail } from '../services/templateService'


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
    <div className="flex flex-col gap-[18px] px-8 pt-7 pb-9">
      <PageHeader title="Profilim" description="Hesap bilgilerin, planın ve güvenlik ayarların." />

      <div className="flex flex-wrap items-start gap-[18px]">
        {/* ── Sol sütun ── */}
        <div className="flex min-w-0 flex-[2_1_460px] flex-col gap-[18px]">
          {/* Profil bilgileri */}
          <Card as="section" aria-labelledby="profile-info-title" padding="none" className="flex flex-col gap-4 p-[22px]">
            <div className="flex items-center gap-3.5">
              <span aria-hidden="true" className="flex h-14 w-14 flex-none items-center justify-center rounded-full bg-ui-accent text-[19px] font-semibold text-ui-accent-fg">
                {userInitials(user)}
              </span>
              <div className="min-w-0">
                <h2 id="profile-info-title" className="m-0 text-base font-semibold text-tx">Kişisel Bilgiler</h2>
                <span className="block truncate text-[12.5px] text-tx-muted">{user?.email}</span>
              </div>
            </div>
            <form onSubmit={handleSaveProfile} className="flex flex-col gap-3.5">
              <div className="grid grid-cols-[repeat(auto-fit,minmax(220px,1fr))] gap-3.5">
                <Field label="Görünen Ad" htmlFor="profile-display-name">
                  <input
                    id="profile-display-name"
                    type="text"
                    value={displayName}
                    onChange={(e) => setDisplayName(e.target.value)}
                    placeholder="Adınız"
                    maxLength={100}
                    autoComplete="name"
                    className={INPUT}
                  />
                </Field>
                <Field label="E-posta" htmlFor="profile-email">
                  <input
                    id="profile-email"
                    type="email"
                    value={email}
                    onChange={(e) => setEmail(e.target.value)}
                    placeholder="eposta@ornek.com"
                    autoComplete="email"
                    className={INPUT}
                  />
                </Field>
              </div>
              <div className="flex flex-wrap items-center justify-end gap-3">
                {profileMsg && <FormMessage msg={profileMsg} />}
                <button type="submit" disabled={profileSaving} className={PRIMARY_BTN}>
                  {profileSaving ? 'Kaydediliyor…' : 'Kaydet'}
                </button>
              </div>
            </form>
          </Card>

          {/* Şifre değiştirme */}
          <Card as="section" aria-labelledby="profile-pw-title" padding="none" className="flex flex-col gap-3.5 p-[22px]">
            <h2 id="profile-pw-title" className="m-0 text-base font-semibold text-tx">Şifre Değiştir</h2>
            <form onSubmit={handleChangePassword} className="flex flex-col gap-3.5">
              <div className="grid grid-cols-[repeat(auto-fit,minmax(200px,1fr))] gap-3.5">
                <Field label="Mevcut Şifre" htmlFor="profile-pw-current">
                  <input
                    id="profile-pw-current"
                    type="password"
                    required
                    autoComplete="current-password"
                    value={currentPassword}
                    onChange={(e) => setCurrentPassword(e.target.value)}
                    placeholder="••••••••"
                    className={INPUT}
                  />
                </Field>
                <Field label="Yeni Şifre" htmlFor="profile-pw-new">
                  <input
                    id="profile-pw-new"
                    type="password"
                    required
                    autoComplete="new-password"
                    aria-describedby="profile-pw-hint"
                    value={newPassword}
                    onChange={(e) => setNewPassword(e.target.value)}
                    placeholder="••••••••"
                    className={INPUT}
                  />
                </Field>
                <Field label="Yeni Şifre (Tekrar)" htmlFor="profile-pw-confirm">
                  <input
                    id="profile-pw-confirm"
                    type="password"
                    required
                    autoComplete="new-password"
                    value={confirmPassword}
                    onChange={(e) => setConfirmPassword(e.target.value)}
                    placeholder="••••••••"
                    className={INPUT}
                  />
                </Field>
              </div>
              {passwordMsg && <FormMessage msg={passwordMsg} />}
              <div className="flex flex-wrap items-center justify-between gap-3">
                <span id="profile-pw-hint" className="text-[12.5px] text-tx-muted">En az 8 karakter, 1 büyük harf, 1 rakam</span>
                <button
                  type="submit"
                  disabled={passwordSaving}
                  className="xc-focus h-10 rounded-lg border-[1.5px] border-ui-accent bg-ui-card px-[18px] text-[13.5px] font-semibold text-ui-ink transition-colors hover:bg-ui-soft disabled:cursor-not-allowed disabled:opacity-50"
                >
                  {passwordSaving ? 'Güncelleniyor…' : 'Şifreyi Değiştir'}
                </button>
              </div>
            </form>
          </Card>

          {/* Şablonlarım (taslak XSLT indirme) */}
          <Card as="section" aria-labelledby="profile-tpl-title" padding="none" className="flex flex-col gap-3 p-[22px]">
            <h2 id="profile-tpl-title" className="m-0 text-base font-semibold text-tx">Şablonlarım</h2>
            {templatesLoading ? (
              <p role="status" className="m-0 text-xs text-tx-subtle">Yükleniyor…</p>
            ) : templates.length === 0 ? (
              <p className="m-0 text-xs text-tx-subtle">Henüz bir şablonunuz yok.</p>
            ) : (
              <ul className="m-0 flex list-none flex-col gap-1.5 p-0">
                {templates.map((tpl) => (
                  <li
                    key={tpl.id}
                    className="flex items-center justify-between gap-3 rounded-lg border border-ui-line bg-ui-side px-3 py-2"
                  >
                    <div className="flex min-w-0 items-center gap-2">
                      <span className="truncate text-[13px] font-medium text-tx">{tpl.name}</span>
                      <span className="flex-none rounded-full bg-fn-blue-tint px-2 py-0.5 text-[11px] font-medium text-fn-blue-ink">
                        {DOC_TYPE_LABEL[tpl.documentType] ?? tpl.documentType}
                      </span>
                    </div>
                    <button
                      type="button"
                      onClick={() => handleDownload(tpl)}
                      disabled={downloadingId === tpl.id}
                      aria-label={`${tpl.name} XSLT indir`}
                      title="XSLT İndir"
                      className="xc-focus flex flex-none items-center gap-1 rounded px-1.5 py-1 text-xs text-tx-muted transition-colors hover:bg-ui-hover hover:text-ui-ink disabled:opacity-40"
                    >
                      <Download size={14} aria-hidden="true" />
                      {downloadingId === tpl.id ? 'İndiriliyor…' : 'İndir'}
                    </button>
                  </li>
                ))}
              </ul>
            )}
          </Card>
        </div>

        {/* ── Sağ sütun ── */}
        <div className="flex min-w-0 flex-[1_1_300px] flex-col gap-[18px]">
          <PlanCard />

          {/* Hesap silme */}
          <section aria-labelledby="profile-danger-title" className="flex flex-col gap-2.5 rounded-xl border border-fn-red-line bg-fn-red-tint px-[22px] py-[18px]">
            <h2 id="profile-danger-title" className="m-0 text-sm font-semibold text-fn-red-ink">Tehlikeli Alan</h2>
            {!showDeleteConfirm ? (
              <>
                <p className="m-0 text-[12.5px] leading-normal text-tx-2">
                  Hesabınızı kalıcı olarak silin. Bu işlem geri alınamaz.
                </p>
                <button
                  type="button"
                  onClick={() => {
                    setShowDeleteConfirm(true)
                    setTimeout(() => deleteInputRef.current?.focus(), 50)
                  }}
                  className="xc-focus inline-flex h-[38px] items-center gap-1.5 self-start rounded-lg border border-fn-red bg-ui-card px-3.5 text-[13px] font-semibold text-fn-red-ink transition-colors hover:bg-fn-red hover:text-on-fn"
                >
                  <Trash2 size={14} aria-hidden="true" />
                  Hesabı Sil
                </button>
              </>
            ) : (
              <div className="flex flex-col gap-3">
                <label htmlFor="profile-delete-confirm" className="text-[12.5px] text-tx-2">
                  Onaylamak için aşağıya{' '}
                  <span className="font-mono font-semibold text-fn-red-ink">{DELETE_PHRASE}</span> yazın:
                </label>
                <input
                  id="profile-delete-confirm"
                  ref={deleteInputRef}
                  type="text"
                  value={deleteInput}
                  onChange={(e) => setDeleteInput(e.target.value)}
                  placeholder={DELETE_PHRASE}
                  className="h-10 rounded-lg border border-fn-red-line bg-ui-card px-3 text-[13.5px] text-tx outline-none focus:border-fn-red focus:ring-2 focus:ring-fn-red-line"
                />
                <div className="flex flex-wrap gap-2">
                  <button
                    type="button"
                    onClick={handleDeleteAccount}
                    disabled={deleteInput !== DELETE_PHRASE || deleting}
                    className="xc-focus h-[38px] rounded-lg bg-fn-red px-4 text-[13px] font-semibold text-on-fn transition-colors hover:bg-fn-red-ink disabled:cursor-not-allowed disabled:opacity-40"
                  >
                    {deleting ? 'Siliniyor…' : 'Hesabı Kalıcı Olarak Sil'}
                  </button>
                  <button
                    type="button"
                    onClick={() => {
                      setShowDeleteConfirm(false)
                      setDeleteInput('')
                    }}
                    className="xc-focus h-[38px] rounded-lg border border-ui-line bg-ui-card px-4 text-[13px] text-tx-2 transition-colors hover:bg-ui-hover"
                  >
                    İptal
                  </button>
                </div>
              </div>
            )}
          </section>
        </div>
      </div>
    </div>
  )
}

// ── Parçalar ────────────────────────────────────────────────────────────────

const INPUT =
  'h-10 rounded-lg border border-field-line bg-ui-card px-3 text-[13.5px] font-normal text-tx outline-none focus:border-ui-accent focus:ring-2 focus:ring-ui-soft-line'
const PRIMARY_BTN =
  'xc-focus h-10 rounded-lg bg-ui-accent px-[18px] text-[13.5px] font-semibold text-ui-accent-fg transition-[filter] hover:brightness-110 disabled:cursor-not-allowed disabled:opacity-50'

function Field({ label, htmlFor, children }: { label: string; htmlFor: string; children: ReactNode }) {
  return (
    <div className="flex flex-col gap-1.5">
      <label htmlFor={htmlFor} className="text-[12.5px] font-medium text-tx-2">{label}</label>
      {children}
    </div>
  )
}

function FormMessage({ msg }: { msg: { type: 'ok' | 'err'; text: string } }) {
  return (
    <p role={msg.type === 'err' ? 'alert' : 'status'} className={`m-0 text-xs ${msg.type === 'ok' ? 'text-fn-green-ink' : 'text-fn-red-ink'}`}>
      {msg.text}
    </p>
  )
}

const NUM = new Intl.NumberFormat('tr-TR')

interface Meter {
  label: string
  used: number
  /** 0 → sınırsız */
  limit: number
  unit: string
  color: 'purple' | 'blue'
}

function UsageMeter({ m }: { m: Meter }) {
  const unlimited = m.limit <= 0
  const pct = unlimited ? 0 : Math.min(100, Math.round((m.used / m.limit) * 100))
  const value = unlimited ? 'Sınırsız' : `${NUM.format(m.used)} / ${NUM.format(m.limit)}${m.unit ? ` ${m.unit}` : ''}`
  return (
    <div className="flex flex-col gap-1.5">
      <div className="flex justify-between gap-2 text-[12.5px]">
        <span className="text-tx-2">{m.label}</span>
        <span className="font-semibold text-tx">{value}</span>
      </div>
      {!unlimited && (
        <div
          role="img"
          aria-label={`${m.label}: ${NUM.format(m.limit)} hakkın ${NUM.format(m.used)} kullanıldı`}
          style={fnStyle(m.color)}
          className="h-2 overflow-hidden rounded bg-ui-hover"
        >
          <div className="h-full rounded bg-(--fn)" style={{ width: `${pct}%` }} />
        </div>
      )}
    </div>
  )
}

/** Plan + bugünkü kullanım (entitlementStore). Limit 0 → "Sınırsız". */
function PlanCard() {
  const entitlements = useEntitlementStore((s) => s.entitlements)
  const refresh = useEntitlementStore((s) => s.refresh)

  // Bugünkü kullanım güncel görünsün (salt okunur istek).
  useEffect(() => {
    void refresh()
  }, [refresh])

  const isPro = entitlements?.plan === 'Pro'
  const privileged = !!entitlements?.isPrivileged
  const title = isPro ? 'XsltCraft Pro' : 'Standart'
  const status = !entitlements
    ? 'Plan bilgisi yükleniyor…'
    : privileged
      ? 'Yönetici erişimi · tüm kotalar sınırsız'
      : isPro
        ? 'Pro üyeliğin aktif'
        : 'Ücretsiz plan'

  const meters: Meter[] = []
  if (entitlements) {
    const u = entitlements.usage
    const lim = (n: number) => (privileged ? 0 : n)
    if (entitlements.canDownloadGridXslt || privileged) {
      meters.push({ label: 'Bugünkü XSLT indirme', used: u.templateExportCount, limit: lim(entitlements.dailyTemplateExportLimit), unit: '', color: 'purple' })
    }
    if (entitlements.dailyAiTokenLimit > 0 && !privileged) {
      meters.push({ label: 'Bugünkü AI kullanımı', used: u.aiTokensUsed, limit: entitlements.dailyAiTokenLimit, unit: 'token', color: 'blue' })
    } else {
      meters.push({ label: 'Bugünkü AI soruları', used: u.aiRequestCount, limit: lim(entitlements.dailyAiRequestLimit), unit: 'soru', color: 'blue' })
    }
  }

  return (
    <section
      aria-labelledby="profile-plan-title"
      style={fnStyle('purple')}
      className="flex flex-col gap-3.5 rounded-xl border-[1.5px] border-(--fn-line) bg-ui-card p-[22px] shadow-[var(--ui-shadow-rest)]"
    >
      <div className="flex items-center gap-3">
        <IconTile icon={Crown} size="lg" hoverFill={false} />
        <div className="min-w-0">
          <h2 id="profile-plan-title" className="m-0 text-base font-semibold text-tx">{title}</h2>
          <span className={`text-[12.5px] font-semibold ${isPro || privileged ? 'text-fn-green-ink' : 'text-tx-muted'}`}>{status}</span>
        </div>
      </div>
      {meters.map((m) => <UsageMeter key={m.label} m={m} />)}
      <Link
        to="/pricing"
        className="xc-focus flex h-10 items-center justify-center rounded-lg bg-(--fn-tint) text-[13.5px] font-semibold text-(--fn-ink) no-underline transition-colors hover:bg-(--fn) hover:text-(--fn-fg)"
      >
        {isPro || privileged ? 'Planları karşılaştır' : "Pro'ya geç"}
      </Link>
    </section>
  )
}
