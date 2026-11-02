import { useCallback } from 'react'
import { useNavigate } from 'react-router-dom'
import api from '../../services/apiService'
import { useAuthStore } from '../../store/authStore'

/**
 * Çıkış işlemi — eski Navbar.handleLogout ile birebir: sunucu oturumunu kapat (hata yutulur),
 * yerel oturumu temizle, giriş sayfasına git.
 */
export function useLogout(): () => Promise<void> {
  const logout = useAuthStore((s) => s.logout)
  const navigate = useNavigate()
  return useCallback(async () => {
    await api.post('/api/auth/logout').catch(() => {})
    logout()
    navigate('/auth/login')
  }, [logout, navigate])
}
