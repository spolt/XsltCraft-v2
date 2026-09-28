import { useEffect, useState } from 'react'
import { Navigate, Outlet } from 'react-router-dom'
import { useAuthStore } from '../store/authStore'
import { useEntitlementStore } from '../store/entitlementStore'
import { ensureSession } from '../services/authSession'

export default function PrivateRoute() {
  const accessToken = useAuthStore((s) => s.accessToken)
  const refresh = useEntitlementStore((s) => s.refresh)
  // Sayfa yenilendiğinde access token bellekte yoktur; saklı kullanıcı varsa önce oturumu geri yükle.
  const [restoring, setRestoring] = useState(() => !accessToken && !!useAuthStore.getState().user)

  useEffect(() => {
    if (!restoring) return
    ensureSession().finally(() => setRestoring(false))
  }, [restoring])

  // Oturum açık olan tüm rotalar için (editör sayfaları AppLayout dışında olsa da) yetkileri yükle.
  useEffect(() => {
    if (accessToken) refresh()
  }, [accessToken, refresh])

  if (restoring) {
    return (
      <div className="h-screen flex items-center justify-center bg-gray-50 text-gray-500 text-sm">
        Oturum doğrulanıyor…
      </div>
    )
  }

  return accessToken ? <Outlet /> : <Navigate to="/auth/login" replace />
}
