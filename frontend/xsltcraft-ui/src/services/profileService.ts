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

export async function deleteAccount(): Promise<void> {
  await api.delete('/api/auth/account')
}
