import api from './apiService'
import type { FreeTheme } from './templateService'

export async function getAdminThemes(): Promise<FreeTheme[]> {
  const { data } = await api.get<FreeTheme[]>('/api/templates')
  return data
}

export async function createTheme(form: {
  name: string
  documentType: string
  file: File
}): Promise<FreeTheme> {
  const body = new FormData()
  body.append('name', form.name)
  body.append('documentType', form.documentType)
  body.append('file', form.file)
  const { data } = await api.post<FreeTheme>('/api/admin/themes', body)
  return data
}

export async function updateTheme(
  id: string,
  form: { name?: string; documentType?: string; file?: File }
): Promise<FreeTheme> {
  const body = new FormData()
  if (form.name) body.append('name', form.name)
  if (form.documentType) body.append('documentType', form.documentType)
  if (form.file) body.append('file', form.file)
  const { data } = await api.put<FreeTheme>(`/api/admin/themes/${id}`, body)
  return data
}

export async function deleteTheme(id: string): Promise<void> {
  await api.delete(`/api/admin/themes/${id}`)
}
