import api from './apiService'

export interface FreeTheme {
  id: string
  name: string
  documentType: 'Invoice' | 'Despatch'
  thumbnailUrl: string | null
  createdAt: string
  updatedAt: string
}

export async function getFreeThemes(): Promise<FreeTheme[]> {
  const { data } = await api.get<FreeTheme[]>('/api/templates')
  return data
}

export function getDownloadUrl(id: string): string {
  const base = import.meta.env.VITE_API_URL ?? 'http://localhost:5000'
  return `${base}/api/templates/${id}/download`
}
