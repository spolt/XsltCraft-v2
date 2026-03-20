import { useState } from 'react'
import { Link, useLocation } from 'react-router-dom'
import {
  LayoutDashboard,
  BookOpen,
  FileText,
  Truck,
  FilePlus,
  FolderOpen,
  Shield,
  ChevronDown,
  ChevronRight,
} from 'lucide-react'
import { useAuthStore } from '../../store/authStore'

interface SidebarProps {
  collapsed: boolean
}

export default function Sidebar({ collapsed }: SidebarProps) {
  const { user } = useAuthStore()
  const location = useLocation()
  const [libraryOpen, setLibraryOpen] = useState(true)

  const isActive = (path: string) => location.pathname + location.search === path || location.pathname === path

  const itemBase =
    'flex items-center gap-3 px-3 py-2 rounded-lg text-sm transition-colors cursor-pointer w-full text-left'
  const itemActive = 'bg-blue-50 text-blue-700 font-medium'
  const itemInactive = 'text-gray-600 hover:bg-gray-100 hover:text-gray-900'

  return (
    <aside
      className={`flex-shrink-0 bg-white border-r border-gray-200 flex flex-col overflow-hidden transition-all duration-200 ${
        collapsed ? 'w-14' : 'w-56'
      }`}
    >
      <nav className="flex-1 p-2 flex flex-col gap-0.5 overflow-y-auto overflow-x-hidden">
        {/* Dashboard */}
        <Link
          to="/dashboard"
          className={`${itemBase} ${isActive('/dashboard') ? itemActive : itemInactive}`}
          title="Dashboard"
        >
          <LayoutDashboard size={18} className="flex-shrink-0" />
          {!collapsed && <span>Dashboard</span>}
        </Link>

        {/* Tema Kütüphanesi */}
        <button
          className={`${itemBase} ${
            location.pathname === '/templates' ? itemActive : itemInactive
          }`}
          onClick={() => !collapsed && setLibraryOpen((o) => !o)}
          title="Tema Kütüphanesi"
        >
          <BookOpen size={18} className="flex-shrink-0" />
          {!collapsed && (
            <>
              <span className="flex-1">Tema Kütüphanesi</span>
              {libraryOpen ? <ChevronDown size={14} /> : <ChevronRight size={14} />}
            </>
          )}
        </button>

        {/* Sub-menu */}
        {!collapsed && libraryOpen && (
          <div className="ml-4 flex flex-col gap-0.5">
            <Link
              to="/templates?type=Invoice"
              className={`${itemBase} ${
                isActive('/templates?type=Invoice') ? itemActive : itemInactive
              }`}
            >
              <FileText size={16} className="flex-shrink-0" />
              <span>e-Fatura / e-Arşiv</span>
            </Link>
            <Link
              to="/templates?type=Despatch"
              className={`${itemBase} ${
                isActive('/templates?type=Despatch') ? itemActive : itemInactive
              }`}
            >
              <Truck size={16} className="flex-shrink-0" />
              <span>e-İrsaliye</span>
            </Link>
          </div>
        )}

        {/* Yeni Şablon */}
        <Link
          to="/editor/new"
          className={`${itemBase} ${isActive('/editor/new') ? itemActive : itemInactive}`}
          title="Yeni Şablon Oluştur"
        >
          <FilePlus size={18} className="flex-shrink-0" />
          {!collapsed && <span>Yeni Şablon</span>}
        </Link>

        {/* Taslaklarım */}
        <Link
          to="/drafts"
          className={`${itemBase} ${isActive('/drafts') ? itemActive : itemInactive}`}
          title="Taslaklarım"
        >
          <FolderOpen size={18} className="flex-shrink-0" />
          {!collapsed && <span>Taslaklarım</span>}
        </Link>

        {/* Admin - sadece admin kullanıcı */}
        {user?.role === 'Admin' && (
          <Link
            to="/admin/themes"
            className={`${itemBase} ${isActive('/admin/themes') ? itemActive : itemInactive}`}
            title="Admin Paneli"
          >
            <Shield size={18} className="flex-shrink-0" />
            {!collapsed && <span>Admin Paneli</span>}
          </Link>
        )}
      </nav>

      {/* Versiyon bilgisi (sadece genişletilmiş halde) */}
      {!collapsed && (
        <div className="p-2 border-t border-gray-100">
          <p className="text-xs text-gray-400 text-center px-2">v0.3.0</p>
        </div>
      )}
    </aside>
  )
}
