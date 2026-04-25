import api from './apiService'

export interface UserListItem {
  id: string
  username: string
  email: string
  displayName: string | null
  role: 'User' | 'Editor' | 'Admin'
  isActive: boolean
  saveCount: number
  downloadCount: number
  lastActivityAt: string | null
  lastLoginAt: string | null
  createdAt: string
}

export interface UserListResponse {
  items: UserListItem[]
  totalCount: number
  page: number
  pageSize: number
  activeCount: number
  inactiveCount: number
  totalActivityCount: number
}

export interface ListUsersParams {
  query?: string
  role?: string
  isActive?: boolean
  page?: number
  pageSize?: number
}

export interface CreateUserRequest {
  username: string
  email: string
  displayName?: string
  role: string
  password: string
}

export async function listUsers(params: ListUsersParams = {}): Promise<UserListResponse> {
  const { data } = await api.get<UserListResponse>('/api/admin/users', { params })
  return data
}

export async function getUser(id: string): Promise<UserListItem> {
  const { data } = await api.get<UserListItem>(`/api/admin/users/${id}`)
  return data
}

export async function createUser(request: CreateUserRequest): Promise<void> {
  await api.post('/api/admin/users', request)
}

export async function updateRole(userId: string, role: string): Promise<void> {
  await api.patch(`/api/admin/users/${userId}/role`, { role })
}

export async function setActive(userId: string, isActive: boolean): Promise<void> {
  await api.patch(`/api/admin/users/${userId}/active`, { isActive })
}

export async function resetPassword(userId: string, newPassword: string): Promise<void> {
  await api.post(`/api/admin/users/${userId}/reset-password`, { newPassword })
}

export async function deleteUser(userId: string): Promise<void> {
  await api.delete(`/api/admin/users/${userId}`)
}
