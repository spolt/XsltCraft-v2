import api from './apiService'

export interface UserXsltTemplateSummary {
  id: string
  name: string
  createdAt: string
  updatedAt: string
  isOwner: boolean
  isShared: boolean
  ownerId: string
  ownerName: string
}

export interface UserXsltTemplateDetail extends UserXsltTemplateSummary {
  xsltContent: string
  xmlContent: string | null
}

export interface TemplateShare {
  userId: string
  username: string
  displayName: string | null
  email: string
}

export interface EditingLock {
  lockedByOther: boolean
  editingUserId: string | null
  editingUserName: string | null
  updatedAt: string
}

export interface UserSearchResult {
  id: string
  username: string
  displayName: string | null
  email: string
}

export async function getUserXsltTemplates(): Promise<UserXsltTemplateSummary[]> {
  const { data } = await api.get<UserXsltTemplateSummary[]>('/api/user-xslt-templates')
  return data
}

export async function getUserXsltTemplate(id: string): Promise<UserXsltTemplateDetail> {
  const { data } = await api.get<UserXsltTemplateDetail>(`/api/user-xslt-templates/${id}`)
  return data
}

export async function createUserXsltTemplate(params: {
  name: string
  xsltContent: string
  xmlContent?: string
}): Promise<UserXsltTemplateDetail> {
  const { data } = await api.post<UserXsltTemplateDetail>('/api/user-xslt-templates', params)
  return data
}

export async function updateUserXsltTemplate(
  id: string,
  params: { name?: string; xsltContent?: string; xmlContent?: string; expectedUpdatedAt?: string },
): Promise<UserXsltTemplateDetail> {
  const { data } = await api.put<UserXsltTemplateDetail>(`/api/user-xslt-templates/${id}`, params)
  return data
}

export async function deleteUserXsltTemplate(id: string): Promise<void> {
  await api.delete(`/api/user-xslt-templates/${id}`)
}

// --- Paylaşım ---

export async function getTemplateShares(id: string): Promise<TemplateShare[]> {
  const { data } = await api.get<TemplateShare[]>(`/api/user-xslt-templates/${id}/shares`)
  return data
}

export async function shareTemplate(id: string, userId: string): Promise<TemplateShare> {
  const { data } = await api.post<TemplateShare>(`/api/user-xslt-templates/${id}/shares`, { userId })
  return data
}

export async function unshareTemplate(id: string, userId: string): Promise<void> {
  await api.delete(`/api/user-xslt-templates/${id}/shares/${userId}`)
}

// --- Eşzamanlı düzenleme kilidi ---

export async function acquireEditingLock(id: string): Promise<EditingLock> {
  const { data } = await api.post<EditingLock>(`/api/user-xslt-templates/${id}/lock/acquire`)
  return data
}

export async function releaseEditingLock(id: string): Promise<void> {
  await api.post(`/api/user-xslt-templates/${id}/lock/release`)
}

// --- Kullanıcı arama (paylaşım için) ---

export async function searchUsers(query: string): Promise<UserSearchResult[]> {
  const { data } = await api.get<UserSearchResult[]>('/api/users/search', { params: { query } })
  return data
}
