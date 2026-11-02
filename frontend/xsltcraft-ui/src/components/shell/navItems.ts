import { BookOpen, Code2, FilePlus2, FileCode2, FolderOpen, LayoutDashboard, Shield, type LucideIcon } from 'lucide-react'
import type { FnColor } from '../../theme/uiThemes'

export interface NavItem {
  id: string
  /** Açık menüdeki etiket */
  label: string
  /** Daraltılmış menüdeki kısa etiket */
  short: string
  icon: LucideIcon
  color: FnColor
  /** Doğrudan bağlantı; yoksa öğe bir menü açar (Yeni Şablon). */
  to?: string
  /** Aktiflik: pathname eşleşmesi. */
  match: (pathname: string) => boolean
  adminOnly?: boolean
}

/** Ana menü öğeleri (plan T1.1). Eski Sidebar'daki tüm rotalar bunlardan ya da alt bölümden erişilir. */
export const NAV_ITEMS: readonly NavItem[] = [
  { id: 'dashboard', label: 'Dashboard', short: 'Ana sayfa', icon: LayoutDashboard, color: 'accent', to: '/dashboard', match: (p) => p === '/dashboard' },
  { id: 'xslt', label: 'XSLT Editör', short: 'XSLT', icon: Code2, color: 'purple', to: '/xslt-editor', match: (p) => p.startsWith('/xslt-editor') },
  { id: 'templates', label: 'Şablonlarım', short: 'Şablonlar', icon: FileCode2, color: 'violet', to: '/my-xslt-templates', match: (p) => p === '/my-xslt-templates' },
  { id: 'drafts', label: 'Taslaklarım', short: 'Taslaklar', icon: FolderOpen, color: 'gray', to: '/drafts', match: (p) => p === '/drafts' },
  { id: 'new', label: 'Yeni Şablon', short: 'Yeni', icon: FilePlus2, color: 'blue', match: (p) => p === '/editor/new' },
  { id: 'themes', label: 'Tema Kütüphanesi', short: 'Temalar', icon: BookOpen, color: 'green', to: '/templates?type=Invoice', match: (p) => p === '/templates' },
  { id: 'admin', label: 'Admin Paneli', short: 'Admin', icon: Shield, color: 'slate', to: '/admin/themes', match: (p) => p.startsWith('/admin'), adminOnly: true },
]

/** "Yeni Şablon" menüsünün hedefleri. */
export const NEW_TEMPLATE_LINKS: readonly { id: string; label: string; to: string }[] = [
  { id: 'invoice', label: 'e-Fatura / e-Arşiv', to: '/editor/new' },
  { id: 'despatch', label: 'e-İrsaliye', to: '/editor/new?type=despatch' },
]

/** Aktif menü öğesi: açık vurgu zemini + 3px vurgu rengi sol çizgi (öğede `relative` gerekir). */
export const NAV_ACTIVE_CLASS =
  "bg-ui-soft text-ui-ink font-semibold before:absolute before:inset-y-1.5 before:left-0 before:w-[3px] before:rounded-r-full before:bg-ui-accent before:content-['']"
