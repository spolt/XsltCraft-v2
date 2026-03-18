import { useNavigate } from 'react-router-dom'
import api from '../services/apiService'
import { useAuthStore } from '../store/authStore'

export default function DashboardPage() {
  const navigate = useNavigate()
  const { user, logout } = useAuthStore()

  const handleLogout = async () => {
    await api.post('/api/auth/logout').catch(() => {})
    logout()
    navigate('/auth/login')
  }

  return (
    <div className="min-h-screen bg-gray-950 text-white flex flex-col items-center justify-center gap-4">
      <h1 className="text-3xl font-bold">Dashboard</h1>
      {user && (
        <p className="text-gray-400">
          Hoş geldin, <span className="text-white">{user.displayName ?? user.email}</span>
        </p>
      )}
      <button
        onClick={handleLogout}
        className="px-4 py-2 bg-gray-800 hover:bg-gray-700 rounded-lg text-sm transition"
      >
        Çıkış Yap
      </button>
    </div>
  )
}
