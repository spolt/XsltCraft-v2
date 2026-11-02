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

export type FixedNotePosition = 'before' | 'after'
export type FixedNoteFontFamily = 'theme' | 'arial' | 'verdana' | 'tahoma' | 'times' | 'georgia' | 'courier'

/** Temanın Notlar kutusuna eklenen sabit metin (konum başına en fazla bir öğe). */
export interface ComposeFixedNote {
  position: FixedNotePosition
  text: string
  bold: boolean
  italic: boolean
  underline: boolean
  fontSize: number
  /** #RRGGBB */
  color: string
  fontFamily: FixedNoteFontFamily
  align: Alignment
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
  /** Sabit notlar; yok/null → XSLT'ye dokunulmaz. */
  fixedNotes?: ComposeFixedNote[] | null
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
  /** Temada Notlar alanı var mı (not gönderilmese de hesaplanır). */
  fixedNotesSupported?: boolean
}

/**
 * Hazır temaya kullanıcı logo/imza/IBAN bilgisini ekler. Önizleme (`includeHtml`) ve indirme/kaydetme
 * (`includeXslt`) sunucuda aynı birleştirme kodundan geçer.
 */
export async function composeTheme(req: ComposeRequest, signal?: AbortSignal): Promise<ComposeResponse> {
  const { data } = await api.post<ComposeResponse>('/api/preview/compose', req, { signal })
  return data
}
