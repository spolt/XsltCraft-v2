import { useState } from 'react'
import { Crown, TriangleAlert } from 'lucide-react'
import {
  createUser,
  deleteUser,
  resetPassword,
  setPlan,
  updateRole,
  type UserListItem,
} from '../../../services/adminUserService'
import {
  Banner, BTN_DANGER, BTN_PRIMARY, BTN_SECONDARY, INPUT, LABEL, Modal, SegmentGroup,
} from '../adminUi'
import { apiMessage } from './userFormat'

interface ModalProps {
  user: UserListItem
  onClose: () => void
  onDone: () => void
}

const ROLES = ['User', 'Editor', 'Admin'] as const
const who = (u: UserListItem) => u.displayName || u.email

function Footer({ onClose, children }: { onClose: () => void; children: React.ReactNode }) {
  return (
    <>
      <button type="button" onClick={onClose} className={`${BTN_SECONDARY} h-10! px-4!`}>İptal</button>
      {children}
    </>
  )
}

// ─── Rol değiştir ─────────────────────────────────────────────────────────────
export function RoleModal({ user, onClose, onDone }: ModalProps) {
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
    <Modal
      titleId="role-modal-title"
      title="Rol Değiştir"
      description={`${who(user)} için yeni rol seçin.`}
      onClose={onClose}
      footer={
        <Footer onClose={onClose}>
          <button type="button" onClick={handleSave} disabled={saving} className={BTN_PRIMARY}>
            {saving ? 'Kaydediliyor…' : 'Kaydet'}
          </button>
        </Footer>
      }
    >
      {error && <Banner>{error}</Banner>}
      <SegmentGroup label="Rol" options={ROLES.map(r => ({ value: r, label: r }))} value={role} onChange={setRole} />
    </Modal>
  )
}

// ─── Plan / üyelik ────────────────────────────────────────────────────────────
export function PlanModal({ user, onClose, onDone }: ModalProps) {
  const [plan, setPlanValue] = useState<'Free' | 'Pro'>(user.plan)
  const [expiresAt, setExpiresAt] = useState<string>(user.planExpiresAt ? user.planExpiresAt.slice(0, 10) : '')
  const [saving, setSaving] = useState(false)
  const [error, setError] = useState<string | null>(null)

  const handleSave = async () => {
    setSaving(true)
    setError(null)
    try {
      // Pro + tarih verilmişse gün sonunu (UTC) gönder; boşsa süresiz.
      const iso = plan === 'Pro' && expiresAt ? new Date(`${expiresAt}T23:59:59Z`).toISOString() : null
      await setPlan(user.id, plan, iso)
      onDone()
    } catch (err: unknown) {
      setError(apiMessage(err) ?? 'Plan değiştirilemedi.')
    } finally {
      setSaving(false)
    }
  }

  return (
    <Modal
      titleId="plan-modal-title"
      title="Üyelik Planı"
      description={`${who(user)} için Pro üyelik tanımlayın veya kaldırın.`}
      onClose={onClose}
      footer={
        <Footer onClose={onClose}>
          <button type="button" onClick={handleSave} disabled={saving} className={BTN_PRIMARY}>
            {saving ? 'Kaydediliyor…' : 'Kaydet'}
          </button>
        </Footer>
      }
    >
      {error && <Banner>{error}</Banner>}
      <SegmentGroup
        label="Plan"
        options={[
          { value: 'Free', label: 'Free' },
          { value: 'Pro', label: <><Crown size={13} aria-hidden="true" />Pro</> },
        ]}
        value={plan}
        onChange={setPlanValue}
      />
      {plan === 'Pro' && (
        <label className={LABEL}>
          Bitiş tarihi (boş = süresiz)
          <input type="date" value={expiresAt} onChange={e => setExpiresAt(e.target.value)} className={INPUT} />
        </label>
      )}
    </Modal>
  )
}

// ─── Şifre sıfırlama ──────────────────────────────────────────────────────────
export function PasswordModal({ user, onClose, onDone }: ModalProps) {
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
    <Modal
      titleId="pwd-modal-title"
      title="Şifre Sıfırla"
      description={`${who(user)} için yeni şifre belirleyin.`}
      onClose={onClose}
      footer={
        <Footer onClose={onClose}>
          <button type="button" onClick={handleSave} disabled={saving} className={BTN_PRIMARY}>
            {saving ? 'Güncelleniyor…' : 'Şifreyi Güncelle'}
          </button>
        </Footer>
      }
    >
      {error && <Banner>{error}</Banner>}
      <label className={LABEL}>
        Yeni Şifre
        <input type="password" autoComplete="new-password" value={pwd} onChange={e => setPwd(e.target.value)} placeholder="••••••••" className={INPUT} />
      </label>
      <label className={LABEL}>
        Şifre Tekrar
        <input type="password" autoComplete="new-password" value={pwd2} onChange={e => setPwd2(e.target.value)} placeholder="••••••••" className={INPUT} />
      </label>
      <p className="m-0 flex items-start gap-1.5 text-[12px] text-fn-orange-ink">
        <TriangleAlert size={13} className="mt-px flex-none" aria-hidden="true" />
        Mevcut oturumlar geçersiz kılınacak.
      </p>
    </Modal>
  )
}

// ─── Silme onayı ──────────────────────────────────────────────────────────────
export function DeleteUserModal({ user, onClose, onDone }: ModalProps) {
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
    <Modal
      titleId="delete-user-title"
      title="Kullanıcıyı Sil"
      onClose={onClose}
      footer={
        <Footer onClose={onClose}>
          <button type="button" onClick={handleDelete} disabled={deleting} className={BTN_DANGER}>
            {deleting ? 'Siliniyor…' : 'Evet, Sil'}
          </button>
        </Footer>
      }
    >
      <p className="m-0 text-[13px] text-tx-2">
        <span className="font-semibold text-tx">{who(user)}</span> hesabı ve tüm verileri{' '}
        <span className="font-semibold text-fn-red-ink">kalıcı olarak silinecek</span>.
      </p>
      <Banner>Şablonlar, snippet'lar ve aktivite geçmişi dahil tüm veriler geri alınamaz şekilde silinir.</Banner>
      {error && <Banner>{error}</Banner>}
    </Modal>
  )
}

// ─── Yeni kullanıcı ───────────────────────────────────────────────────────────
export function CreateUserModal({ onClose, onDone }: { onClose: () => void; onDone: () => void }) {
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
      setError(apiMessage(err) ?? 'Kullanıcı oluşturulamadı.')
    } finally {
      setSaving(false)
    }
  }

  return (
    <Modal
      titleId="create-user-title"
      title="Yeni Kullanıcı Oluştur"
      onClose={onClose}
      maxWidth="max-w-md"
      footer={
        <Footer onClose={onClose}>
          <button type="button" onClick={handleCreate} disabled={saving} className={BTN_PRIMARY}>
            {saving ? 'Oluşturuluyor…' : 'Oluştur'}
          </button>
        </Footer>
      }
    >
      {error && <Banner>{error}</Banner>}
      <label className={LABEL}>
        Kullanıcı Adı *
        <input type="text" value={form.username} onChange={e => setForm(f => ({ ...f, username: e.target.value }))} autoFocus placeholder="kullanici_adi" className={INPUT} />
        <span className="text-[11.5px] font-normal text-tx-muted">3-30 karakter, harf/rakam/alt çizgi</span>
      </label>
      <label className={LABEL}>
        E-posta *
        <input type="email" value={form.email} onChange={e => setForm(f => ({ ...f, email: e.target.value }))} placeholder="kullanici@ornek.com" className={INPUT} />
      </label>
      <label className={LABEL}>
        Ad Soyad
        <input type="text" value={form.displayName} onChange={e => setForm(f => ({ ...f, displayName: e.target.value }))} placeholder="Ad Soyad" className={INPUT} />
      </label>
      <div className={LABEL}>
        <span id="create-user-role">Rol</span>
        <SegmentGroup
          label="Rol"
          options={ROLES.map(r => ({ value: r, label: r }))}
          value={form.role as (typeof ROLES)[number]}
          onChange={r => setForm(f => ({ ...f, role: r }))}
        />
      </div>
      <label className={LABEL}>
        Şifre *
        <input type="password" autoComplete="new-password" value={form.password} onChange={e => setForm(f => ({ ...f, password: e.target.value }))} placeholder="En az 6 karakter" className={INPUT} />
      </label>
    </Modal>
  )
}
