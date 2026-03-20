import api from './apiService'

export interface AssetResponse {
  id: string
  url: string
  storagePath: string
  type: string
  mimeType: string
  sizeBytes: number
}

export async function uploadAsset(file: File, assetType: string = 'Custom'): Promise<AssetResponse> {
  const form = new FormData()
  form.append('file', file)
  form.append('assetType', assetType)
  const { data } = await api.post<AssetResponse>('/api/assets/upload', form)
  return data
}

export function getAssetUrl(id: string): string {
  return `/api/assets/${id}/serve`
}

export async function deleteAsset(id: string): Promise<void> {
  await api.delete(`/api/assets/${id}`)
}
