import { useEffect } from 'react'
import { Navigate, Outlet } from 'react-router-dom'
import { useAuthStore } from '../store/authStore'
import { useEntitlementStore } from '../store/entitlementStore'

export default function PrivateRoute() {
  const accessToken = useAuthStore((s) => s.accessToken)
  const refresh = useEntitlementStore((s) => s.refresh)

  // Oturum açık olan tüm rotalar için (editör sayfaları AppLayout dışında olsa da) yetkileri yükle.
  useEffect(() => {
    if (accessToken) refresh()
  }, [accessToken, refresh])

  return accessToken ? <Outlet /> : <Navigate to="/auth/login" replace />
}
