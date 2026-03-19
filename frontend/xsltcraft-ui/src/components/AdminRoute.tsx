import { Navigate, Outlet } from 'react-router-dom'
import { useAuthStore } from '../store/authStore'

export default function AdminRoute() {
  const user = useAuthStore((s) => s.user)

  if (!user) return <Navigate to="/auth/login" replace />
  if (user.role !== 'Admin') return <Navigate to="/dashboard" replace />

  return <Outlet />
}
