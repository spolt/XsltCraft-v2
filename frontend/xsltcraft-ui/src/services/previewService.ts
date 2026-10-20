import api from './apiService'
import type { GridBlock } from '../types/blocks'
import type { DocumentType } from '../types/template'

export interface PreviewTimings {
  parseMs: number
  compileMs: number
  transformMs: number
  serializeMs: number
}

export interface PreviewResponse {
  html: string
  generationTimeMs: number
  timings?: PreviewTimings
}

export async function previewFromBlockTree(
  blocks: Record<string, GridBlock>,
  xmlContent: string,
  documentType: DocumentType = 'Invoice',
): Promise<PreviewResponse> {
  const { data } = await api.post<PreviewResponse>('/api/preview', {
    version: 2,
    documentType,
    blocks,
    xmlContent,
  })
  return data
}

export async function generateXslt(
  blocks: Record<string, GridBlock>,
  documentType: DocumentType = 'Invoice',
): Promise<string> {
  const { data } = await api.post<string>(
    '/api/preview/xslt',
    { version: 2, documentType, blocks, xmlContent: '' },
    { responseType: 'text' },
  )
  return data
}

export interface BankInfoItem {
  bankName: string
  iban: string
}

export type Alignment = 'left' | 'center' | 'right'

/** Geliştirici mod: theme'in ham XSLT içeriğini döndür */
export async function fetchThemeXslt(templateId: string): Promise<string> {
  const { data } = await api.get<string>(
    `/api/preview/theme/${templateId}/xslt-content`,
    { responseType: 'text' },
  )
  return data
}

/** Geliştirici mod: ham XSLT + XML ile anlık önizleme */
export async function previewFromRawXslt(xslt: string, xmlContent: string): Promise<PreviewResponse> {
  const { data } = await api.post<PreviewResponse>('/api/preview/raw', { xslt, xmlContent })
  return data
}

/** Kullanıcının blok ağacı tabanlı şablonundan önizleme (V1/V2 her ikisi de desteklenir) */
export async function previewFromUserTemplate(
  templateId: string,
  xmlContent: string,
): Promise<PreviewResponse> {
  const { data } = await api.post<PreviewResponse>(
    `/api/preview/user-template/${templateId}`,
    { xmlContent },
  )
  return data
}

/**
 * Storage'daki XSLT dosyasıyla (free theme klonu vb.) önizleme. Kullanıcı logo/imza/IBAN eklenmiş önizleme
 * için `themeComposeService.composeTheme`.
 */
export async function previewFromStoredXslt(templateId: string, xmlContent: string): Promise<PreviewResponse> {
  const { data } = await api.post<PreviewResponse>(`/api/preview/theme/${templateId}`, { xmlContent })
  return data
}
