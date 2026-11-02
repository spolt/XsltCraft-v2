import { NavLink, Outlet } from 'react-router-dom'
import { BarChart3, BookOpen, Library, MessageSquareText, Shield, Sparkles, Users, type LucideIcon } from 'lucide-react'
import IconTile from '../ui/IconTile'
import { fnStyle, type FnColor } from '../../theme/uiThemes'

const ADMIN_TABS: readonly { to: string; label: string; icon: LucideIcon; color: FnColor }[] = [
  { to: '/admin/themes', label: 'Hazır Şablonlar', icon: BookOpen, color: 'green' },
  { to: '/admin/snippets', label: 'Snippet Kütüphanesi', icon: Library, color: 'purple' },
  { to: '/admin/users', label: 'Kullanıcılar', icon: Users, color: 'blue' },
  { to: '/admin/usage', label: 'Kullanım Raporu', icon: BarChart3, color: 'orange' },
  { to: '/admin/ai', label: 'AI Asistan', icon: Sparkles, color: 'violet' },
  { to: '/admin/ai-feedback', label: 'AI Geri Bildirim', icon: MessageSquareText, color: 'slate' },
]

/** Admin sayfalarının ortak başlığı + bölüm sekmeleri (AppLayout içinde). Sayfa içeriği <Outlet/>. */
export default function AdminLayout() {
  return (
    <div className="flex flex-col">
      <div className="flex flex-col gap-3.5 px-8 pt-[22px]">
        <div className="flex items-center gap-2.5 text-[12.5px]">
          <IconTile icon={Shield} color="slate" size="sm" hoverFill={false} />
          <span className="font-semibold text-fn-slate">Admin Paneli</span>
        </div>
        <nav
          aria-label="Admin bölümleri"
          className="flex flex-wrap gap-1.5 self-start rounded-[10px] border border-ui-line bg-ui-card p-1"
        >
          {ADMIN_TABS.map((tab) => (
            <NavLink
              key={tab.to}
              to={tab.to}
              style={fnStyle(tab.color)}
              className={({ isActive }) =>
                `group xc-focus flex h-9 items-center gap-2 rounded-[7px] px-3 text-[13px] no-underline transition-colors ${
                  isActive ? 'bg-(--fn-tint) font-semibold text-(--fn-ink)' : 'font-medium text-tx-2 hover:bg-ui-hover'
                }`
              }
            >
              {({ isActive }) => (
                <>
                  <IconTile icon={tab.icon} size="xs" filled={isActive} />
                  {tab.label}
                </>
              )}
            </NavLink>
          ))}
        </nav>
      </div>
      <Outlet />
    </div>
  )
}
