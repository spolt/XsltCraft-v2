import api from './apiService'

export type FolderKind = 'Draft' | 'XsltTemplate'

export interface Folder {
  id: string
  name: string
  kind: FolderKind
  color: string | null
  createdAt: string
}

/** Hem Taslaklarım hem Şablonlarım listelerinin ortak alanları (klasör/arama/sıralama için). */
export interface StorageItem {
  id: string
  name: string
  createdAt: string
  updatedAt: string
  folderId: string | null
  isFavorite: boolean
}

export async function getFolders(kind: FolderKind): Promise<Folder[]> {
  const { data } = await api.get<Folder[]>('/api/folders', { params: { kind } })
  return data
}

export async function createFolder(params: {
  name: string
  kind: FolderKind
  color?: string | null
}): Promise<Folder> {
  const { data } = await api.post<Folder>('/api/folders', params)
  return data
}

export async function updateFolder(
  id: string,
  params: { name?: string; color?: string | null },
): Promise<Folder> {
  const { data } = await api.put<Folder>(`/api/folders/${id}`, params)
  return data
}

export async function deleteFolder(id: string): Promise<void> {
  await api.delete(`/api/folders/${id}`)
}
