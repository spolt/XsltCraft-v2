import type { CSSProperties } from 'react'
import { Link } from 'react-router-dom'
import { useUiPrefsStore } from '../../store/uiPrefsStore'
import QuickSearch from './QuickSearch'

/** AppShell üst barı: logo (menü genişliğiyle hizalı) + ortada hızlı arama. Sağda buton yok. */
export default function AppHeader() {
  const collapsed = useUiPrefsStore((s) => s.navCollapsed)
  const sideW = collapsed ? 'w-[72px]' : 'w-[240px]'

  return (
    <header
      style={{ '--xc-focus-ring': 'var(--ui-bar-fg)' } as CSSProperties}
      className="relative z-20 flex min-h-14 flex-none flex-wrap items-center gap-4 border-b border-ui-bar-border bg-ui-bar px-4 text-ui-bar-fg"
    >
      <div className={`flex flex-none items-center ${sideW}`}>
        <Link to="/dashboard" aria-label="XsltCraft — Dashboard" className="xc-focus flex items-center gap-2.5 rounded-md text-ui-bar-fg no-underline">
          <span aria-hidden="true" className="flex h-[30px] w-[30px] items-center justify-center rounded-[7px] bg-ui-accent text-[15px] font-bold text-white">
            X
          </span>
          {!collapsed && <span className="text-[15px] font-semibold">XsltCraft</span>}
        </Link>
      </div>
      <div className="flex flex-[1_1_360px] justify-center">
        <QuickSearch />
      </div>
      <div aria-hidden="true" className={`hidden flex-none md:block ${sideW}`} />
    </header>
  )
}
