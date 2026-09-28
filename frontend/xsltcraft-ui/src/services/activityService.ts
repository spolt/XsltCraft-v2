import api from './apiService'
import { useAuthStore } from '../store/authStore'

export type DownloadEntityKind = 'Template' | 'Xslt'

/** Tarayıcıda üretilen indirmeyi sayaca bildirir; hata indirmeyi etkilemez. */
export function reportDownload(entityKind: DownloadEntityKind, entityId?: string | null): void {
  if (!useAuthStore.getState().user) return
  api.post('/api/activity/download', { entityKind, entityId: entityId ?? null }).catch(() => {})
}
