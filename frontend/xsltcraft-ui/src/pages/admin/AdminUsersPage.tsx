import { useEffect, useRef, useState } from 'react'
import {
  Activity, ChevronLeft, ChevronRight, Crown, ExternalLink, KeyRound, Loader2, MoreHorizontal, Plus, Search, Shield,
  Trash2, UserCheck, Users, UserX, type LucideIcon,
} from 'lucide-react'
import { useAuthStore } from '../../store/authStore'
import { listUsers, setActive, type UserListItem } from '../../services/adminUserService'
import PageHeader from '../../components/ui/PageHeader'
import Card from '../../components/ui/Card'
import IconTile from '../../components/ui/IconTile'
import Switch from '../../components/ui/Switch'
import MenuPopover from '../../components/shell/MenuPopover'
import type { FnColor } from '../../theme/uiThemes'
import { AdminPage, Banner, BTN_ICON, BTN_PRIMARY, BTN_SECONDARY, INPUT, TABLE, TD, TH } from '../../components/admin/adminUi'
import { PlanPill, RolePill, UserAvatar } from '../../components/admin/users/userDisplay'
import { formatDate, formatDateShort } from '../../components/admin/users/userFormat'
import UserDetailAside from '../../components/admin/users/UserDetailAside'
import {
  CreateUserModal, DeleteUserModal, PasswordModal, PlanModal, RoleModal,
} from '../../components/admin/users/UserModals'

function StatCard({ icon, color, label, value }: { icon: LucideIcon; color: FnColor; label: string; value: string }) {
  return (
    <Card padding="none" className="flex items-center gap-3 px-4 py-3.5">
      <IconTile icon={icon} color={color} size="lg" hoverFill={false} />
      <div className="flex flex-col">
        <span className="text-[12px] text-tx-muted">{label}</span>
        <span className="text-[20px] font-bold text-tx tabular-nums">{value}</span>
      </div>
    </Card>
  )
}

// ─── Satır işlem menüsü ───────────────────────────────────────────────────────
function ActionMenu({ user, onDetail, onRoleChange, onPlanChange, onResetPassword, onDelete }: {
  user: UserListItem
  onDetail: () => void
  onRoleChange: () => void
  onPlanChange: () => void
  onResetPassword: () => void
  onDelete: () => void
}) {
  const [open, setOpen] = useState(false)
  const btnRef = useRef<HTMLButtonElement>(null)
  const menuId = `user-actions-${user.id}`
  return (
    <>
      <button
        ref={btnRef}
        type="button"
        aria-label={`@${user.username} için işlemler`}
        aria-haspopup="menu"
        aria-expanded={open}
        aria-controls={open ? menuId : undefined}
        onClick={(e) => { e.stopPropagation(); setOpen(o => !o) }}
        className={BTN_ICON}
      >
        <MoreHorizontal size={16} aria-hidden="true" />
      </button>
      <MenuPopover
        open={open}
        onClose={() => setOpen(false)}
        anchorRef={btnRef}
        id={menuId}
        label="Kullanıcı işlemleri"
        items={[
          { id: 'detail', label: 'Detaylar', icon: ExternalLink, onSelect: onDetail },
          { id: 'role', label: 'Rol Değiştir', icon: Shield, onSelect: onRoleChange },
          { id: 'plan', label: 'Plan / Üyelik', icon: Crown, onSelect: onPlanChange },
          { id: 'pwd', label: 'Şifre Sıfırla', icon: KeyRound, onSelect: onResetPassword },
          { id: 'delete', label: 'Sil', icon: Trash2, onSelect: onDelete, tone: 'danger' },
        ]}
      />
    </>
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
  const [planTarget, setPlanTarget] = useState<UserListItem | null>(null)
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

  // Liste yenilenince detay paneli güncel veriyi gösterir.
  const detail = detailTarget ? items.find(u => u.id === detailTarget.id) ?? detailTarget : null
  const pageStart = Math.max(1, Math.min(page - 2, totalPages - 4))

  return (
    <AdminPage>
      <PageHeader
        title="Kullanıcıları Yönet"
        description="Kullanıcı listesi, roller, aktivite ve hesap durumları."
        actions={
          <button type="button" onClick={() => setShowCreate(true)} className={BTN_PRIMARY}>
            <Plus size={15} aria-hidden="true" />
            Yeni Kullanıcı Oluştur
          </button>
        }
      />

      <div className="grid grid-cols-[repeat(auto-fit,minmax(170px,1fr))] gap-3.5">
        <StatCard icon={Users} color="blue" label="Toplam Kullanıcı" value={totalCount.toLocaleString('tr-TR')} />
        <StatCard icon={UserCheck} color="green" label="Aktif" value={activeCount.toLocaleString('tr-TR')} />
        <StatCard icon={UserX} color="gray" label="Pasif" value={inactiveCount.toLocaleString('tr-TR')} />
        <StatCard icon={Activity} color="orange" label="Toplam Aktivite" value={totalActivityCount.toLocaleString('tr-TR')} />
      </div>

      {error && <Banner>{error}</Banner>}

      <div className="flex flex-wrap items-start gap-4">
        <Card as="section" padding="none" aria-label="Kullanıcı listesi" className="min-w-0 flex-[999_1_560px]">
          <div className="flex flex-wrap gap-2 border-b border-ui-line px-3.5 py-3">
            <label className="relative min-w-[200px] flex-[0_1_300px]">
              <span className="sr-only">Kullanıcı ara</span>
              <Search size={14} aria-hidden="true" className="pointer-events-none absolute top-1/2 left-3 -translate-y-1/2 text-tx-subtle" />
              <input
                type="search"
                value={query}
                onChange={e => setQuery(e.target.value)}
                placeholder="Kullanıcı adı, email veya isim ara…"
                className={`${INPUT} pl-8!`}
              />
            </label>
            <label>
              <span className="sr-only">Rol filtresi</span>
              <select value={filterRole} onChange={e => setFilterRole(e.target.value)} className={`${INPUT} w-auto! pr-8!`}>
                <option value="">Tüm Roller</option>
                <option value="Admin">Admin</option>
                <option value="Editor">Editor</option>
                <option value="User">User</option>
              </select>
            </label>
            <label>
              <span className="sr-only">Durum filtresi</span>
              <select value={filterActive} onChange={e => setFilterActive(e.target.value)} className={`${INPUT} w-auto! pr-8!`}>
                <option value="">Tüm Durumlar</option>
                <option value="true">Aktif</option>
                <option value="false">Pasif</option>
              </select>
            </label>
          </div>

          {loading ? (
            <div className="flex items-center justify-center gap-2 py-12 text-[13px] text-tx-muted">
              <Loader2 size={15} className="animate-spin" aria-hidden="true" /> Yükleniyor…
            </div>
          ) : (
            <>
              <div className="overflow-x-auto">
                <table className={TABLE}>
                  <thead>
                    <tr>
                      <th scope="col" className={TH}>Kullanıcı</th>
                      <th scope="col" className={TH}>Rol</th>
                      <th scope="col" className={TH}>Plan</th>
                      <th scope="col" className={TH}>Durum</th>
                      <th scope="col" className={`${TH} text-right!`}>Kaydet</th>
                      <th scope="col" className={`${TH} text-right!`}>İndirme</th>
                      <th scope="col" className={TH}>Son Giriş</th>
                      <th scope="col" className={TH}>Kayıt</th>
                      <th scope="col" className={`${TH} text-right!`}><span className="sr-only">İşlem</span></th>
                    </tr>
                  </thead>
                  <tbody>
                    {items.length === 0 && (
                      <tr className="border-t border-ui-line">
                        <td colSpan={9} className="px-4 py-8 text-center text-[13px] text-tx-muted">Kullanıcı bulunamadı.</td>
                      </tr>
                    )}
                    {items.map(u => {
                      const isSelf = u.id === currentUser?.id
                      const selected = detail?.id === u.id
                      return (
                        <tr
                          key={u.id}
                          onClick={() => setDetailTarget(u)}
                          className={`cursor-pointer border-t border-ui-line transition-colors ${selected ? 'bg-ui-soft' : 'hover:bg-ui-hover'}`}
                        >
                          <td className={TD}>
                            <button
                              type="button"
                              aria-pressed={selected}
                              onClick={(e) => { e.stopPropagation(); setDetailTarget(u) }}
                              className={`xc-focus flex items-center gap-2.5 rounded-md text-left ${u.isActive ? '' : 'opacity-60'}`}
                            >
                              <UserAvatar user={u} />
                              <span className="flex min-w-0 flex-col">
                                <span className="font-semibold text-tx">
                                  @{u.username}
                                  {isSelf && <span className="ml-1.5 text-[12px] font-normal text-tx-muted">(sen)</span>}
                                </span>
                                <span className="text-[12px] text-tx-muted">{u.email}</span>
                              </span>
                            </button>
                          </td>
                          <td className={TD}><RolePill role={u.role} /></td>
                          <td className={TD}><PlanPill plan={u.plan} /></td>
                          <td className={TD} onClick={e => e.stopPropagation()}>
                            <div className="flex items-center gap-2">
                              <Switch
                                checked={u.isActive}
                                disabled={isSelf}
                                hideLabel
                                label={`@${u.username} hesabı aktif`}
                                onChange={val => { if (!isSelf) void handleToggleActive(u, val) }}
                              />
                              <span className={`text-[12px] font-medium ${u.isActive ? 'text-fn-green-ink' : 'text-tx-subtle'}`}>
                                {u.isActive ? 'Aktif' : 'Pasif'}
                              </span>
                            </div>
                          </td>
                          <td className={`${TD} text-right text-tx-2 tabular-nums`}>{u.saveCount}</td>
                          <td className={`${TD} text-right text-tx-2 tabular-nums`}>{u.downloadCount}</td>
                          <td className={`${TD} text-[12px] whitespace-nowrap text-tx-muted`}>
                            {u.lastLoginAt ? formatDate(u.lastLoginAt) : <span className="italic text-tx-subtle">Hiç login olmadı</span>}
                          </td>
                          <td className={`${TD} text-[12px] whitespace-nowrap text-tx-muted`}>{formatDateShort(u.createdAt)}</td>
                          <td className={`${TD} text-right`} onClick={e => e.stopPropagation()}>
                            {!isSelf && (
                              <ActionMenu
                                user={u}
                                onDetail={() => setDetailTarget(u)}
                                onRoleChange={() => setRoleTarget(u)}
                                onPlanChange={() => setPlanTarget(u)}
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
              </div>

              <nav aria-label="Sayfalama" className="flex flex-wrap items-center justify-between gap-2 border-t border-ui-line px-3.5 py-3">
                <div className="text-[12px] text-tx-muted">
                  {totalCount} kullanıcıdan {totalCount === 0 ? 0 : (page - 1) * PAGE_SIZE + 1}–{Math.min(page * PAGE_SIZE, totalCount)} arası
                </div>
                <div className="flex items-center gap-1">
                  <button type="button" disabled={page <= 1} onClick={() => setPage(p => p - 1)} className={`${BTN_SECONDARY} h-8!`}>
                    <ChevronLeft size={14} aria-hidden="true" /> Önceki
                  </button>
                  {Array.from({ length: Math.min(5, totalPages) }, (_, i) => {
                    const p = pageStart + i
                    const on = p === page
                    return (
                      <button
                        key={p}
                        type="button"
                        aria-current={on ? 'page' : undefined}
                        onClick={() => setPage(p)}
                        className={`xc-focus h-8 min-w-8 rounded-lg px-2 text-[12.5px] transition-colors ${
                          on ? 'bg-ui-accent font-semibold text-white' : 'border border-ui-line bg-ui-card text-tx-2 hover:bg-ui-hover'
                        }`}
                      >
                        {p}
                      </button>
                    )
                  })}
                  <button type="button" disabled={page >= totalPages} onClick={() => setPage(p => p + 1)} className={`${BTN_SECONDARY} h-8!`}>
                    Sonraki <ChevronRight size={14} aria-hidden="true" />
                  </button>
                </div>
              </nav>
            </>
          )}
        </Card>

        {detail && (
          <UserDetailAside
            user={detail}
            isSelf={detail.id === currentUser?.id}
            onClose={() => setDetailTarget(null)}
            onRoleChange={() => setRoleTarget(detail)}
            onPlanChange={() => setPlanTarget(detail)}
            onResetPassword={() => setPwdTarget(detail)}
            onDelete={() => setDeleteTarget(detail)}
          />
        )}
      </div>

      {showCreate && (
        <CreateUserModal
          onClose={() => setShowCreate(false)}
          onDone={() => { setShowCreate(false); load(1); setPage(1) }}
        />
      )}
      {roleTarget && (
        <RoleModal user={roleTarget} onClose={() => setRoleTarget(null)} onDone={() => { setRoleTarget(null); load() }} />
      )}
      {planTarget && (
        <PlanModal user={planTarget} onClose={() => setPlanTarget(null)} onDone={() => { setPlanTarget(null); load() }} />
      )}
      {pwdTarget && (
        <PasswordModal user={pwdTarget} onClose={() => setPwdTarget(null)} onDone={() => setPwdTarget(null)} />
      )}
      {deleteTarget && (
        <DeleteUserModal
          user={deleteTarget}
          onClose={() => setDeleteTarget(null)}
          onDone={() => {
            if (detailTarget?.id === deleteTarget.id) setDetailTarget(null)
            setDeleteTarget(null); load(1); setPage(1)
          }}
        />
      )}
    </AdminPage>
  )
}
