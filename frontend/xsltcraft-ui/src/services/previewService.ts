import api from './apiService'
import type { Section } from '../types/template'
import type { Block } from '../types/blocks'

export interface PreviewResponse {
  html: string
  generationTimeMs: number
}

export async function previewFromBlockTree(
  sections: Section[],
  blocks: Record<string, Block>,
  xmlContent: string,
): Promise<PreviewResponse> {
  const { data } = await api.post<PreviewResponse>('/api/preview', {
    sections,
    blocks,
    xmlContent,
  })
  return data
}

export async function generateXslt(
  sections: Section[],
  blocks: Record<string, Block>,
): Promise<string> {
  const { data } = await api.post<string>(
    '/api/preview/xslt',
    { sections, blocks, xmlContent: '' },
    { responseType: 'text' },
  )
  return data
}

export interface BankInfoItem {
  bankName: string
  iban: string
}

export type Alignment = 'left' | 'center' | 'right'

export interface ImageSettings {
  url: string
  width?: number
  height?: number
  alignment: Alignment
}

/** Geliştirici mod: theme'in ham XSLT içeriğini döndür */
export async function fetchThemeXslt(templateId: string): Promise<string> {
  const { data } = await api.get<string>(
    `/api/preview/theme/${templateId}/xslt-content`,
    { responseType: 'text' },
  )
  return data
}

/** Geliştirici mod: ham XSLT + XML ile anlık önizleme */
export async function previewFromRawXslt(
  xslt: string,
  xmlContent: string,
  logo?: ImageSettings,
  signature?: ImageSettings,
  bankInfo?: BankInfoItem[],
): Promise<PreviewResponse> {
  const { data } = await api.post<PreviewResponse>('/api/preview/raw', {
    xslt,
    xmlContent,
    logoUrl: logo?.url,
    logoWidth: logo?.width,
    logoHeight: logo?.height,
    logoAlignment: logo?.alignment,
    signatureUrl: signature?.url,
    signatureWidth: signature?.width,
    signatureHeight: signature?.height,
    signatureAlignment: signature?.alignment,
    bankInfo: bankInfo ?? [],
  })
  return data
}

/** Storage'daki XSLT dosyasıyla (free theme klonu vb.) önizleme */
export async function previewFromStoredXslt(
  templateId: string,
  xmlContent: string,
  logo?: ImageSettings,
  signature?: ImageSettings,
  bankInfo?: BankInfoItem[],
): Promise<PreviewResponse> {
  const { data } = await api.post<PreviewResponse>(`/api/preview/theme/${templateId}`, {
    xmlContent,
    logoUrl: logo?.url,
    logoWidth: logo?.width,
    logoHeight: logo?.height,
    logoAlignment: logo?.alignment,
    signatureUrl: signature?.url,
    signatureWidth: signature?.width,
    signatureHeight: signature?.height,
    signatureAlignment: signature?.alignment,
    bankInfo: bankInfo ?? [],
  })
  return data
}
