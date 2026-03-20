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
  xsltStoragePath: string | null
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

export async function getMyTemplates(): Promise<TemplateDetail[]> {
  const { data } = await api.get<TemplateDetail[]>('/api/templates/my')
  return data
}

export async function cloneTemplate(id: string): Promise<TemplateDetail> {
  const { data } = await api.post<TemplateDetail>(`/api/templates/${id}/clone`)
  return data
}

export async function deleteTemplate(id: string): Promise<void> {
  await api.delete(`/api/templates/${id}`)
}

export async function downloadTemplate(id: string, fileName: string): Promise<void> {
  const { data } = await api.get<Blob>(`/api/templates/${id}/download`, {
    responseType: 'blob',
  })
  const url = URL.createObjectURL(data)
  const a = document.createElement('a')
  a.href = url
  a.download = `${fileName.replace(/[^a-zA-Z0-9_-]/g, '_') || 'template'}.xslt`
  document.body.appendChild(a)
  a.click()
  document.body.removeChild(a)
  URL.revokeObjectURL(url)
}
