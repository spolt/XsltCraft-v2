import api from './apiService'
import type { Alignment, BankInfoItem } from './previewService'

export type AssetKind = 'logo' | 'signature'

export interface ComposeAsset {
  kind: AssetKind
  /** Yuva kimliği; null → temanın varsayılan konumu. */
  slot: string | null
  dataUrl: string
  width: number | null
  height: number | null
  alignment: Alignment
}

export interface ComposeRequest {
  /** Tema Kullan: tema kimliği. Geliştirici Modu: editördeki XSLT (`xslt`). Yalnız biri verilir. */
  themeId?: string
  xslt?: string
  xmlContent?: string
  assets: ComposeAsset[]
  bankInfo: BankInfoItem[]
  /** Kendi görseli eklenmeden kaldırılan temadaki görseller (yalnız admin'in izin verdikleri gizlenir). */
  hideThemeImages?: AssetKind[]
  includeXslt: boolean
  includeHtml: boolean
}

export interface ComposeWarning {
  code: string
  message: string
  kind: AssetKind | null
}

export interface ComposeResponse {
  xslt: string | null
  html: string | null
  generationTimeMs: number
  warnings: ComposeWarning[]
  appliedSlots: Partial<Record<AssetKind, string | null>>
}

/**
 * Hazır temaya kullanıcı logo/imza/IBAN bilgisini ekler. Önizleme (`includeHtml`) ve indirme/kaydetme
 * (`includeXslt`) sunucuda aynı birleştirme kodundan geçer.
 */
export async function composeTheme(req: ComposeRequest, signal?: AbortSignal): Promise<ComposeResponse> {
  const { data } = await api.post<ComposeResponse>('/api/preview/compose', req, { signal })
  return data
}
