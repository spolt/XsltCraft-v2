import api from './apiService'

export interface UserSnippet {
  id: string
  prefix: string
  body: string
  description?: string
  scope: 'xslt' | 'xpath' | 'html'
  isPublic: boolean
  createdAt: string
  updatedAt: string
}

export interface CreateSnippetRequest {
  prefix: string
  body: string
  description?: string
  scope: 'xslt' | 'xpath' | 'html'
  isPublic?: boolean
}

export interface UpdateSnippetRequest {
  prefix?: string
  body?: string
  description?: string
  scope?: 'xslt' | 'xpath' | 'html'
  isPublic?: boolean
}

export async function listSnippets(): Promise<UserSnippet[]> {
  const { data } = await api.get<UserSnippet[]>('/api/user-snippets')
  return data
}

export async function createSnippet(req: CreateSnippetRequest): Promise<UserSnippet> {
  const { data } = await api.post<UserSnippet>('/api/user-snippets', req)
  return data
}

export async function updateSnippet(id: string, req: UpdateSnippetRequest): Promise<UserSnippet> {
  const { data } = await api.put<UserSnippet>(`/api/user-snippets/${id}`, req)
  return data
}

export async function deleteSnippet(id: string): Promise<void> {
  await api.delete(`/api/user-snippets/${id}`)
}

// ─── Admin (kütüphane) snippet API'leri ─────────────────────────────────────

export async function listAdminSnippets(): Promise<UserSnippet[]> {
  const { data } = await api.get<UserSnippet[]>('/api/admin/snippets')
  return data
}

export async function createAdminSnippet(req: CreateSnippetRequest): Promise<UserSnippet> {
  const { data } = await api.post<UserSnippet>('/api/admin/snippets', req)
  return data
}

export async function updateAdminSnippet(id: string, req: UpdateSnippetRequest): Promise<UserSnippet> {
  const { data } = await api.put<UserSnippet>(`/api/admin/snippets/${id}`, req)
  return data
}

export async function deleteAdminSnippet(id: string): Promise<void> {
  await api.delete(`/api/admin/snippets/${id}`)
}
