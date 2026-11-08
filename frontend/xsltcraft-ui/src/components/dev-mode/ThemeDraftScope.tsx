import { useLayoutEffect } from 'react'
import { Outlet, useParams } from 'react-router-dom'
import { releaseEditingLock } from '../../services/userXsltService'
import { useAuthStore } from '../../store/authStore'
import { draftKey, useThemeDraftStore } from '../../store/themeDraftStore'

/**
 * Tema Kullan ve Geliştirici Modu'nu saran layout route: taslağın ömrü bu iki ekranla sınırlıdır.
 * Taslak URL'deki temaya bağlanana kadar sayfa render edilmez; böylece temadan temaya atlamada sayfa
 * yeniden mount olur ve eski taslağı görmez.
 */
export default function ThemeDraftScope() {
  const { templateId } = useParams<{ templateId: string }>()
  const boundKey = useThemeDraftStore((s) => s.boundKey)

  useLayoutEffect(() => {
    if (!templateId) return
    useThemeDraftStore.getState().bind(draftKey.theme(templateId))
    return () => {
      const { saved, reset } = useThemeDraftStore.getState()
      // Çıkışta token gitmiştir: istek refresh akışını tetikleyip oturumu canlandırmasın. Kilit kendiliğinden düşer.
      if (saved && useAuthStore.getState().accessToken) releaseEditingLock(saved.id).catch(() => {})
      reset()
    }
  }, [templateId])

  if (!templateId || boundKey !== draftKey.theme(templateId)) return null
  return <Outlet />
}
