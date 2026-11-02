import { AtSign, Calendar, Clock, Crown, Download, KeyRound, Save, Shield, Trash2, X, type LucideIcon } from 'lucide-react'
import type { UserListItem } from '../../../services/adminUserService'
import { BTN_DANGER_SOFT, BTN_ICON, BTN_SECONDARY, Pill } from '../adminUi'
import { PlanPill, RolePill, UserAvatar } from './userDisplay'
import { formatDate } from './userFormat'

interface UserDetailAsideProps {
  user: UserListItem
  isSelf: boolean
  onClose: () => void
  onRoleChange: () => void
  onPlanChange: () => void
  onResetPassword: () => void
  onDelete: () => void
}

function InfoRow({ icon: Icon, label, children }: { icon: LucideIcon; label: string; children: React.ReactNode }) {
  return (
    <div className="flex items-start gap-2.5 text-[13px] text-tx-2">
      <Icon size={14} className="mt-0.5 flex-none text-tx-subtle" aria-hidden="true" />
      <div className="min-w-0">
        <div className="text-[11.5px] text-tx-muted">{label}</div>
        <div className="break-all">{children}</div>
      </div>
    </div>
  )
}

/** Seçili kullanıcının detay paneli (tablonun sağında). */
export default function UserDetailAside({
  user, isSelf, onClose, onRoleChange, onPlanChange, onResetPassword, onDelete,
}: UserDetailAsideProps) {
  return (
    <aside
      aria-labelledby="user-detail-title"
      className="flex min-w-0 flex-[1_1_300px] flex-col gap-4 rounded-xl border border-ui-line bg-ui-card p-[18px] shadow-[var(--ui-shadow-rest)] lg:sticky lg:top-4"
    >
      <div className="flex items-start gap-3">
        <UserAvatar user={user} size="lg" />
        <div className="flex min-w-0 flex-1 flex-col">
          <h2 id="user-detail-title" className="m-0 truncate text-[15px] font-semibold text-tx">
            @{user.username}
            {isSelf && <span className="ml-1.5 text-[12px] font-normal text-tx-muted">(sen)</span>}
          </h2>
          {user.displayName && <span className="truncate text-[12.5px] text-tx-muted">{user.displayName}</span>}
          <div className="mt-1.5 flex flex-wrap gap-1.5">
            <RolePill role={user.role} />
            <PlanPill plan={user.plan} />
            <Pill tone={user.isActive ? 'green' : 'red'}>{user.isActive ? 'Aktif' : 'Pasif'}</Pill>
          </div>
        </div>
        <button type="button" onClick={onClose} aria-label="Detayı kapat" className={BTN_ICON}>
          <X size={16} aria-hidden="true" />
        </button>
      </div>

      <div className="flex flex-col gap-2.5">
        <InfoRow icon={AtSign} label="E-posta">{user.email}</InfoRow>
        <InfoRow icon={Calendar} label="Kayıt Tarihi">{formatDate(user.createdAt)}</InfoRow>
        <InfoRow icon={Clock} label="Son Giriş">
          {user.lastLoginAt ? formatDate(user.lastLoginAt) : <span className="italic text-tx-subtle">Henüz giriş yapmadı</span>}
        </InfoRow>
        {user.lastActivityAt && <InfoRow icon={Clock} label="Son Aktivite">{formatDate(user.lastActivityAt)}</InfoRow>}
        {user.plan === 'Pro' && (
          <InfoRow icon={Crown} label="Pro bitişi">{user.planExpiresAt ? formatDate(user.planExpiresAt) : 'Süresiz'}</InfoRow>
        )}
      </div>

      <div className="grid grid-cols-2 gap-2 text-[12px]">
        <div className="rounded-lg bg-ui-hover p-2.5">
          <div className="flex items-center gap-1.5 text-tx-muted"><Save size={12} aria-hidden="true" />Kaydetme</div>
          <div className="text-[15px] font-bold text-tx tabular-nums">{user.saveCount}</div>
        </div>
        <div className="rounded-lg bg-ui-hover p-2.5">
          <div className="flex items-center gap-1.5 text-tx-muted"><Download size={12} aria-hidden="true" />İndirme</div>
          <div className="text-[15px] font-bold text-tx tabular-nums">{user.downloadCount}</div>
        </div>
      </div>

      {!isSelf && (
        <div className="flex flex-col gap-2 border-t border-ui-line pt-4">
          <button type="button" onClick={onRoleChange} className={`${BTN_SECONDARY} justify-start!`}>
            <Shield size={14} aria-hidden="true" />Rol Değiştir
          </button>
          <button type="button" onClick={onPlanChange} className={`${BTN_SECONDARY} justify-start!`}>
            <Crown size={14} aria-hidden="true" />Plan / Üyelik
          </button>
          <button type="button" onClick={onResetPassword} className={`${BTN_SECONDARY} justify-start!`}>
            <KeyRound size={14} aria-hidden="true" />Şifre Sıfırla
          </button>
          <button type="button" onClick={onDelete} className={`${BTN_DANGER_SOFT} justify-start!`}>
            <Trash2 size={14} aria-hidden="true" />Kullanıcıyı Sil
          </button>
        </div>
      )}
    </aside>
  )
}
