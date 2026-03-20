import { LogOut, Menu } from 'lucide-react'
import { Link, useNavigate } from 'react-router-dom'
import api from '../../services/apiService'
import { useAuthStore } from '../../store/authStore'

interface NavbarProps {
  onToggleSidebar: () => void
}

export default function Navbar({ onToggleSidebar }: NavbarProps) {
  const { user, logout } = useAuthStore()
  const navigate = useNavigate()

  const handleLogout = async () => {
    await api.post('/api/auth/logout').catch(() => {})
    logout()
    navigate('/auth/login')
  }

  return (
    <header className="h-14 flex items-center justify-between px-4 bg-white border-b border-gray-200 flex-shrink-0 z-20">
      <div className="flex items-center gap-3">
        <button
          onClick={onToggleSidebar}
          className="p-1.5 rounded-md text-gray-500 hover:bg-gray-100 transition-colors"
        >
          <Menu size={20} />
        </button>
        <span className="text-lg font-bold text-blue-600 tracking-tight select-none">XsltCraft</span>
      </div>

      <div className="flex items-center gap-3">
        {user?.role === 'Admin' && (
          <span className="text-xs font-semibold px-2 py-0.5 rounded-full bg-amber-100 text-amber-700">
            Admin
          </span>
        )}
        <Link
          to="/profile"
          className="text-sm text-gray-600 hover:text-blue-600 transition-colors hidden sm:block"
          title="Profilim"
        >
          {user?.displayName ?? user?.email}
        </Link>
        <button
          onClick={handleLogout}
          className="flex items-center gap-1.5 text-sm text-gray-500 hover:text-red-600 transition-colors"
          title="Çıkış yap"
        >
          <LogOut size={16} />
          <span className="hidden sm:block">Çıkış</span>
        </button>
      </div>
    </header>
  )
}
