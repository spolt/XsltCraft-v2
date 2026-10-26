import { useState } from 'react'
import { Link, useLocation } from 'react-router-dom'
import { ChevronsLeft, ChevronsRight, Info } from 'lucide-react'
import { useAuthStore } from '../../store/authStore'
import { useUiPrefsStore } from '../../store/uiPrefsStore'
import { NAV_ACTIVE_CLASS, NAV_ITEMS } from './navItems'
import SideNavItem from './SideNavItem'
import NewTemplateMenu from './NewTemplateMenu'
import ProfileCard from './ProfileCard'
import SettingsSection from './SettingsSection'

/** Ana menü: daraltılmış 88px / açık 256px (uiPrefsStore.navCollapsed). */
export default function SideNav() {
  const { pathname } = useLocation()
  const role = useAuthStore((s) => s.user?.role)
  const collapsed = useUiPrefsStore((s) => s.navCollapsed)
  const setNavCollapsed = useUiPrefsStore((s) => s.setNavCollapsed)
  const [settingsOpen, setSettingsOpen] = useState(false)

  const items = NAV_ITEMS.filter((i) => !i.adminOnly || role === 'Admin')
  const aboutActive = pathname === '/about'

  return (
    <nav
      aria-label="Ana menü"
      className={`flex flex-none flex-col border-r border-ui-line bg-ui-side ${collapsed ? 'w-[88px] px-2 py-2.5' : 'w-64 p-2.5'}`}
    >
      <div className={`mb-1 flex ${collapsed ? 'justify-center' : 'justify-end'}`}>
        <button
          type="button"
          aria-label={collapsed ? 'Menüyü genişlet' : 'Menüyü daralt'}
          aria-expanded={!collapsed}
          onClick={() => setNavCollapsed(!collapsed)}
          className="xc-focus flex h-8 w-9 items-center justify-center rounded-md text-tx-muted transition-colors hover:bg-ui-hover"
        >
          {collapsed ? <ChevronsRight size={17} aria-hidden="true" /> : <ChevronsLeft size={17} aria-hidden="true" />}
        </button>
      </div>

      <div className={`flex min-h-0 flex-1 flex-col overflow-y-auto ${collapsed ? 'gap-1' : 'gap-0.5'}`}>
        {items.map((item) =>
          item.to ? (
            <SideNavItem key={item.id} item={item} collapsed={collapsed} active={item.match(pathname)} />
          ) : (
            <NewTemplateMenu key={item.id} item={item} collapsed={collapsed} active={item.match(pathname)} />
          ),
        )}
      </div>

      <div className={`flex flex-col gap-1 border-t border-ui-line pt-2.5 ${collapsed ? 'items-stretch' : ''}`}>
        <ProfileCard collapsed={collapsed} />
        <SettingsSection
          collapsed={collapsed}
          open={settingsOpen}
          onToggle={() => setSettingsOpen((o) => !o)}
          onExpandAndOpen={() => {
            setNavCollapsed(false)
            setSettingsOpen(true)
          }}
        />
        {collapsed ? (
          <Link
            to="/about"
            aria-label="Hakkında"
            title="Hakkında"
            aria-current={aboutActive ? 'page' : undefined}
            className={`xc-focus relative flex h-11 w-11 items-center justify-center self-center rounded-[10px] transition-colors ${aboutActive ? NAV_ACTIVE_CLASS : 'text-tx-muted hover:bg-ui-hover'}`}
          >
            <Info size={22} strokeWidth={1.7} aria-hidden="true" />
          </Link>
        ) : (
          <Link
            to="/about"
            aria-current={aboutActive ? 'page' : undefined}
            className={`xc-focus relative flex h-10 items-center gap-3 rounded-lg px-2.5 text-[13.5px] no-underline transition-colors ${aboutActive ? NAV_ACTIVE_CLASS : 'text-tx-2 hover:bg-ui-hover'}`}
          >
            <Info size={18} strokeWidth={1.7} aria-hidden="true" className="text-tx-muted" />
            Hakkında
          </Link>
        )}
        {collapsed ? (
          <span className="text-center text-[11px] text-tx-muted">v{__APP_VERSION__}</span>
        ) : (
          <div className="flex items-center justify-between px-2.5 pt-2 pb-0.5 text-[11.5px] text-tx-muted">
            <span>XsltCraft v{__APP_VERSION__}</span>
            <Link to="/about#yenilikler" className="xc-focus rounded font-semibold text-ui-ink no-underline hover:underline">
              Yenilikler
            </Link>
          </div>
        )}
      </div>
    </nav>
  )
}
