import { useCallback, useId, useRef, useState } from 'react'
import { Link } from 'react-router-dom'
import { Crown, CreditCard, LogOut, UserRound } from 'lucide-react'
import { useAuthStore } from '../../store/authStore'
import { useEntitlementStore } from '../../store/entitlementStore'
import MenuPopover, { type MenuPopoverItem } from './MenuPopover'
import { useLogout } from './useLogout'
import { userDisplayName, userInitials } from './userDisplay'

/** Menünün alt bölümündeki profil kartı: ad, plan çipi / "Pro'ya geç", tıklayınca Profilim · Planlar · Çıkış. */
export default function ProfileCard({ collapsed }: { collapsed: boolean }) {
  const user = useAuthStore((s) => s.user)
  const entitlements = useEntitlementStore((s) => s.entitlements)
  const logout = useLogout()
  const [open, setOpen] = useState(false)
  const anchorRef = useRef<HTMLButtonElement>(null)
  const menuId = useId()
  const close = useCallback(() => setOpen(false), [])

  const isAdmin = user?.role === 'Admin'
  // Eski Navbar ile aynı: plan rozeti yalnız ayrıcalıksız kullanıcıda; Pro → rozet, Free → CTA.
  const showPlan = !!entitlements && !entitlements.isPrivileged
  const isPro = showPlan && entitlements.plan === 'Pro'
  const isFree = showPlan && !isPro

  const items: MenuPopoverItem[] = [
    { id: 'profile', label: 'Profilim', icon: UserRound, to: '/profile' },
    { id: 'pricing', label: 'Planlar', icon: CreditCard, to: '/pricing' },
    { id: 'logout', label: 'Çıkış yap', icon: LogOut, tone: 'danger', onSelect: () => void logout() },
  ]
  const menuProps = {
    'aria-haspopup': 'menu' as const,
    'aria-expanded': open,
    'aria-controls': open ? menuId : undefined,
    onClick: () => setOpen((o) => !o),
  }
  const initials = userInitials(user)
  const name = userDisplayName(user)

  const menu = (
    <MenuPopover
      id={menuId}
      open={open}
      onClose={close}
      anchorRef={anchorRef}
      items={items}
      label="Profil menüsü"
      placement={collapsed ? 'right-end' : 'top-start'}
    />
  )

  if (collapsed) {
    return (
      <div className="flex flex-col items-center gap-1.5">
        <button
          ref={anchorRef}
          type="button"
          aria-label={`Profil ve plan: ${name}`}
          title="Profil ve plan"
          {...menuProps}
          className="xc-focus flex h-10 w-10 items-center justify-center rounded-full bg-ui-accent text-[13px] font-semibold text-ui-accent-fg"
        >
          {initials}
        </button>
        {isFree && (
          <Link
            to="/pricing"
            title="XsltCraft Pro'ya geç"
            className="xc-focus flex h-6 items-center gap-0.5 whitespace-nowrap rounded-full bg-ui-soft px-1.5 text-[10px] font-bold text-ui-ink no-underline hover:bg-ui-soft-line"
          >
            <Crown size={10} aria-hidden="true" />Pro'ya geç
          </Link>
        )}
        {menu}
      </div>
    )
  }

  return (
    <div className="flex flex-wrap items-center gap-x-1 rounded-lg transition-colors hover:bg-ui-hover">
      <button
        ref={anchorRef}
        type="button"
        {...menuProps}
        className="xc-focus flex min-w-0 flex-[1_1_130px] items-center gap-2.5 rounded-lg p-2 text-left text-tx"
      >
        <span
          aria-hidden="true"
          className="flex h-[34px] w-[34px] flex-none items-center justify-center rounded-full bg-ui-accent text-[12.5px] font-semibold text-ui-accent-fg"
        >
          {initials}
        </span>
        <span className="flex min-w-0 flex-1 flex-col">
          <span className="truncate text-[13px] font-semibold">{name}</span>
          <span className="truncate text-[12px] text-tx-muted">Profil ve plan</span>
        </span>
      </button>
      <span className="ml-auto flex flex-none items-center gap-1 py-1 pr-2 empty:hidden">
        {isAdmin && (
          <span className="rounded-full bg-fn-slate-tint px-2 py-px text-[11px] font-bold text-fn-slate-ink">Admin</span>
        )}
        {isPro && (
          <span className="inline-flex items-center gap-1 rounded-full bg-ui-soft px-2 py-px text-[11px] font-bold text-ui-ink">
            <Crown size={11} aria-hidden="true" /> Pro
          </span>
        )}
        {isFree && (
          <Link
            to="/pricing"
            title="XsltCraft Pro'ya geç"
            className="xc-focus inline-flex items-center gap-1 rounded-full bg-ui-accent px-2 py-px text-[11px] font-bold text-ui-accent-fg no-underline hover:brightness-110"
          >
            <Crown size={11} aria-hidden="true" /> Pro'ya geç
          </Link>
        )}
      </span>
      {menu}
    </div>
  )
}
