import api from './apiService'

export interface FreeTheme {
  id: string
  name: string
  documentType: 'Invoice' | 'Despatch'
  thumbnailUrl: string | null
  createdAt: string
  updatedAt: string
}

export interface TemplateDetail {
  id: string
  name: string
  documentType: 'Invoice' | 'Despatch'
  isFreeTheme: boolean
  blockTree: string | null
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

export async function getTemplate(id: string): Promise<TemplateDetail> {
  const { data } = await api.get<TemplateDetail>(`/api/templates/${id}`)
  return data
}

export async function createTemplate(params: {
  name: string
  documentType: 'Invoice' | 'Despatch'
  blockTree?: string
}): Promise<TemplateDetail> {
  const { data } = await api.post<TemplateDetail>('/api/templates', params)
  return data
}

export async function updateTemplate(id: string, params: {
  name?: string
  blockTree?: string
}): Promise<TemplateDetail> {
  const { data } = await api.put<TemplateDetail>(`/api/templates/${id}`, params)
  return data
}
