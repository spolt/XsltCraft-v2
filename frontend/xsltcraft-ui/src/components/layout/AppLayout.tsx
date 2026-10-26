import { Outlet } from 'react-router-dom'
import AppHeader from '../shell/AppHeader'
import SideNav from '../shell/SideNav'

/** AppShell: üst bar (logo + hızlı arama) + ana menü + sayfa içeriği. Menü durumu uiPrefsStore'da. */
export default function AppLayout() {
  return (
    <div className="flex h-screen flex-col bg-ui-bg text-tx">
      <AppHeader />
      <div className="flex min-h-0 flex-1 overflow-hidden">
        <SideNav />
        <main className="min-w-0 flex-1 overflow-y-auto">
          <Outlet />
        </main>
      </div>
    </div>
  )
}
