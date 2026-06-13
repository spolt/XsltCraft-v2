import api from './apiService'

export interface ProfileData {
  id: string
  email: string
  displayName: string | null
  role: string
}

export async function updateProfile(params: {
  displayName?: string
  email?: string
}): Promise<ProfileData> {
  const { data } = await api.put<ProfileData>('/api/auth/profile', params)
  return data
}

export async function changePassword(params: {
  currentPassword: string
  newPassword: string
}): Promise<{ accessToken: string }> {
  const { data } = await api.post<{ accessToken: string }>('/api/auth/change-password', params)
  return data
}

export async function deleteAccount(): Promise<void> {
  await api.delete('/api/auth/account')
}
