import { useLayoutEffect } from 'react'
import { Outlet, useParams } from 'react-router-dom'
import { releaseEditingLock } from '../../services/userXsltService'
import { useAuthStore } from '../../store/authStore'
import { useThemeDraftStore } from '../../store/themeDraftStore'

/**
 * Tema Kullan ve Geliştirici Modu'nu saran layout route: taslağın ömrü bu iki ekranla sınırlıdır.
 * Taslak URL'deki temaya bağlanana kadar sayfa render edilmez; böylece temadan temaya atlamada sayfa
 * yeniden mount olur ve eski taslağı görmez.
 */
export default function ThemeDraftScope() {
  const { templateId } = useParams<{ templateId: string }>()
  const boundThemeId = useThemeDraftStore((s) => s.themeId)

  useLayoutEffect(() => {
    if (!templateId) return
    useThemeDraftStore.getState().bind(templateId)
    return () => {
      const { saved, reset } = useThemeDraftStore.getState()
      // Çıkışta token gitmiştir: istek refresh akışını tetikleyip oturumu canlandırmasın. Kilit kendiliğinden düşer.
      if (saved && useAuthStore.getState().accessToken) releaseEditingLock(saved.id).catch(() => {})
      reset()
    }
  }, [templateId])

  if (boundThemeId !== templateId) return null
  return <Outlet />
}
