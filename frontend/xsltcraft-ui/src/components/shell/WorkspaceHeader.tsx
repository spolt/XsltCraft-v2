import { useCallback, useId, useRef, useState, type CSSProperties, type MouseEvent, type ReactNode } from 'react'
import { useNavigate } from 'react-router-dom'
import { LayoutDashboard, LogOut, UserRound } from 'lucide-react'
import { useAuthStore } from '../../store/authStore'
import MenuPopover, { type MenuPopoverItem } from './MenuPopover'
import { useLogout } from './useLogout'
import { userDisplayName, userInitials } from './userDisplay'

export type WorkspaceModule = 'design' | 'xslt' | 'themes' | 'templates'

/** Modül sekmeleri ve hedef rotaları. */
const WORKSPACE_MODULES: readonly { id: WorkspaceModule; label: string; to: string }[] = [
  { id: 'design', label: 'Tasarım Alanı', to: '/editor/new' },
  { id: 'xslt', label: 'XSLT Editör', to: '/xslt-editor' },
  { id: 'themes', label: 'Tema Kütüphanesi', to: '/templates?type=Invoice' },
  { id: 'templates', label: 'Şablonlarım', to: '/my-xslt-templates' },
]

interface WorkspaceHeaderProps {
  /** Vurgulanan modül sekmesi (aria-current="page"). */
  active?: WorkspaceModule | null
  /**
   * Header içinden yapılan tüm gezinmeler (logo, modül sekmeleri, avatar menüsü) buradan geçer —
   * kaydedilmemiş değişiklik koruması için. Verilmezse doğrudan `navigate(path)`.
   */
  onNavigate?: (path: string) => void
  /**
   * Çıkış yap öncesi sorulur (kaydedilmemiş değişiklik koruması); `false` dönerse çıkış iptal edilir.
   * Verilmezse doğrudan çıkış yapılır.
   */
  confirmLeave?: () => boolean
  /** Logonun solunda (ör. sol paneli daraltma butonu). */
  leading?: ReactNode
  /** Sekmelerin sağında, esnek boşluktan sonra: dosya adı, çipler, kayıt durumu. */
  center?: ReactNode
  /** En sağda avatarın solunda: HeaderButton'lar. */
  actions?: ReactNode
}

/**
 * Çalışma alanı sayfalarının (Tasarım Alanı, XSLT Editör, Tema Kullan, DevMode) üst barı.
 * Bar tokenları (--ui-bar*) ile çizilir; avatar menüsü: Dashboard, Profilim, Çıkış.
 */
export default function WorkspaceHeader({ active = null, onNavigate, confirmLeave, leading, center, actions }: WorkspaceHeaderProps) {
  const navigate = useNavigate()
  const user = useAuthStore((s) => s.user)
  const logout = useLogout()
  const [menuOpen, setMenuOpen] = useState(false)
  const avatarRef = useRef<HTMLButtonElement>(null)
  const menuId = useId()

  const go = useCallback((path: string) => (onNavigate ? onNavigate(path) : navigate(path)), [onNavigate, navigate])
  const closeMenu = useCallback(() => setMenuOpen(false), [])

  // Normal tıklamada SPA gezinmesi (koruma dahil); Ctrl/⌘/orta tık yeni sekme davranışını korur.
  const onLinkClick = (path: string) => (e: MouseEvent<HTMLAnchorElement>) => {
    if (e.button !== 0 || e.metaKey || e.ctrlKey || e.shiftKey || e.altKey) return
    e.preventDefault()
    go(path)
  }

  const name = userDisplayName(user)
  const menuItems: MenuPopoverItem[] = [
    { id: 'dashboard', label: 'Dashboard', icon: LayoutDashboard, to: '/dashboard' },
    { id: 'profile', label: 'Profilim', icon: UserRound, to: '/profile' },
    {
      id: 'logout',
      label: 'Çıkış yap',
      icon: LogOut,
      tone: 'danger',
      onSelect: () => {
        if (confirmLeave && !confirmLeave()) return
        void logout()
      },
    },
  ]

  return (
    <header
      style={{ '--xc-focus-ring': 'var(--ui-bar-fg)' } as CSSProperties}
      className="relative z-20 flex min-h-14 flex-none flex-wrap items-center gap-x-5 gap-y-1 border-b border-ui-bar-border bg-ui-bar px-4 text-ui-bar-fg"
    >
      {leading}
      <a
        href="/dashboard"
        onClick={onLinkClick('/dashboard')}
        className="xc-focus flex items-center gap-2.5 rounded-md text-ui-bar-fg no-underline"
        aria-label="XsltCraft — Dashboard"
      >
        <span aria-hidden="true" className="flex h-7 w-7 items-center justify-center rounded-md bg-ui-accent text-[14px] font-bold text-ui-accent-fg">
          X
        </span>
        <span className="text-[15px] font-semibold">XsltCraft</span>
      </a>

      <nav aria-label="Modüller" className="flex gap-1 self-stretch">
        {WORKSPACE_MODULES.map((m) => {
          const on = m.id === active
          return (
            <a
              key={m.id}
              href={m.to}
              onClick={onLinkClick(m.to)}
              aria-current={on ? 'page' : undefined}
              className={`xc-focus flex items-center px-3 text-[13px] no-underline transition-colors ${
                on
                  ? 'font-semibold text-ui-bar-fg shadow-[inset_0_-2px_0_var(--ui-accent)]'
                  : 'font-normal text-ui-bar-muted hover:text-ui-bar-fg'
              }`}
            >
              {m.label}
            </a>
          )
        })}
      </nav>

      <div className="flex-[1_1_40px]" />
      {center && <div className="flex min-w-0 items-center gap-2.5 text-[12px]">{center}</div>}

      <div className="flex items-center gap-1.5">
        {actions}
        <button
          ref={avatarRef}
          type="button"
          aria-label={`Hesap: ${name}`}
          aria-haspopup="menu"
          aria-expanded={menuOpen}
          aria-controls={menuOpen ? menuId : undefined}
          onClick={() => setMenuOpen((o) => !o)}
          className="xc-focus ml-1 flex h-8 w-8 items-center justify-center rounded-full bg-ui-avatar text-[12px] font-semibold text-ui-avatar-fg"
        >
          {userInitials(user)}
        </button>
        <MenuPopover
          id={menuId}
          open={menuOpen}
          onClose={closeMenu}
          anchorRef={avatarRef}
          items={menuItems}
          label="Hesap menüsü"
          placement="bottom-end"
          onNavigate={go}
        />
      </div>
    </header>
  )
}
