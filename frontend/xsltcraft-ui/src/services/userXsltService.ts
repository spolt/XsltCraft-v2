import api from './apiService'

/** Şablonun en son düzenlendiği mod. */
export type XsltEditMode = 'editor' | 'easyEdit'

export interface UserXsltTemplateSummary {
  id: string
  name: string
  createdAt: string
  updatedAt: string
  isOwner: boolean
  isShared: boolean
  ownerId: string
  ownerName: string
  folderId: string | null
  isFavorite: boolean
  lastEditMode: XsltEditMode
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
  lastEditMode?: XsltEditMode
}): Promise<UserXsltTemplateDetail> {
  const { data } = await api.post<UserXsltTemplateDetail>('/api/user-xslt-templates', params)
  return data
}

export async function updateUserXsltTemplate(
  id: string,
  params: { name?: string; xsltContent?: string; xmlContent?: string; expectedUpdatedAt?: string; lastEditMode?: XsltEditMode },
): Promise<UserXsltTemplateDetail> {
  const { data } = await api.put<UserXsltTemplateDetail>(`/api/user-xslt-templates/${id}`, params)
  return data
}

export async function deleteUserXsltTemplate(id: string): Promise<void> {
  await api.delete(`/api/user-xslt-templates/${id}`)
}

export async function moveUserXsltToFolder(id: string, folderId: string | null): Promise<void> {
  await api.patch(`/api/user-xslt-templates/${id}/folder`, { folderId })
}

export async function setUserXsltFavorite(id: string, isFavorite: boolean): Promise<void> {
  await api.patch(`/api/user-xslt-templates/${id}/favorite`, { isFavorite })
}

/** Sunucudaki `TemplateLimits.MaxTemplatesPerFolder` ile aynı (yalnız XSLT klasörleri). */
export const MAX_TEMPLATES_PER_FOLDER = 500
/** Sunucudaki `DownloadLimits.MaxDownloadItems` ile aynı. */
export const MAX_DOWNLOAD_ITEMS = 500

/** Tek istekte toplu taşıma; sunucu tek kapasite kontrolü yapar (409 folder_full → hiçbiri taşınmaz). */
export async function bulkMoveUserXslt(ids: string[], folderId: string | null): Promise<{ movedIds: string[] }> {
  const { data } = await api.post<{ movedIds: string[] }>('/api/user-xslt-templates/bulk-move', { ids, folderId })
  return data
}

// --- İndirme (ZIP) — JWT bellekte olduğundan <a href> yerine Blob olarak alınır ---

/** İndirme ilerlemesi: alınan bayt ve (sunucu Content-Length verdiyse) toplam bayt. */
export type DownloadProgressFn = (loaded: number, total?: number) => void

export async function downloadUserXsltZip(ids: string[], onProgress?: DownloadProgressFn): Promise<Blob> {
  const { data } = await api.post<Blob>('/api/user-xslt-templates/download', { ids }, {
    responseType: 'blob',
    onDownloadProgress: (e) => onProgress?.(e.loaded, e.total),
  })
  return data
}

export async function downloadFolderZip(folderId: string, onProgress?: DownloadProgressFn): Promise<Blob> {
  const { data } = await api.get<Blob>(`/api/user-xslt-templates/folders/${folderId}/download`, {
    responseType: 'blob',
    onDownloadProgress: (e) => onProgress?.(e.loaded, e.total),
  })
  return data
}

// --- Toplu yükleme (klasör olarak) ---

export interface BulkUploadItem {
  name: string
  xsltContent: string
}

export interface BulkUploadResult {
  created: { id: string; name: string }[]
  skipped: { name: string; reason: string }[]
}

export async function bulkUploadUserXslt(
  folderId: string | null,
  items: BulkUploadItem[],
): Promise<BulkUploadResult> {
  const { data } = await api.post<BulkUploadResult>('/api/user-xslt-templates/bulk', {
    folderId,
    items,
  })
  return data
}

// --- Toplu sabit not ekleme ---

export type FixedNoteMode = 'replace' | 'append'
export type FixedNoteItemStatus = 'updated' | 'no_notes' | 'ambiguous' | 'locked' | 'failed'

export interface BulkAddFixedNoteResult {
  /** `copies`: notun eklendiği kopya sayısı (A5 çift kopyada 2). */
  results: { id: string; name: string; status: FixedNoteItemStatus; copies?: number }[]
}

/**
 * `resolveAmbiguous: 'first'` — birden fazla not alanı bulunan şablonlarda kullanıcı onayıyla
 * belgede önce gelen not alanını kullan (yalnız `ambiguous` dönen şablonlar için gönderilir).
 */
export async function bulkAddFixedNote(
  ids: string[],
  noteText: string,
  mode: FixedNoteMode,
  resolveAmbiguous?: 'first',
): Promise<BulkAddFixedNoteResult> {
  const { data } = await api.post<BulkAddFixedNoteResult>('/api/user-xslt-templates/bulk-add-note', {
    ids,
    noteText,
    mode,
    ...(resolveAmbiguous ? { resolveAmbiguous } : {}),
  })
  return data
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
