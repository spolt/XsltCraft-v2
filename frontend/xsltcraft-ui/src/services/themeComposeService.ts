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

/**
 * Dosyanın kendi görseli (analizdeki `images[].index`) için statik düzenleme. Yalnız `xslt` dalında gönderilir.
 * `remove` true ise diğer alanlar yok sayılır; `slot` null → görsel yerinde kalır; `alignment` null → değişmez.
 */
export interface ComposeStaticImage {
  index: number
  slot?: string | null
  remove: boolean
  width?: number | null
  height?: number | null
  alignment?: Alignment | null
}

/**
 * Yalnız `xslt` dalında: belgede boş bir yuvaya eklenen serbest görsel (en fazla 1). `slot` zorunludur ve temadaki
 * görselin yeri (`existing-*`) olamaz.
 */
export interface ComposeFreeImage {
  slot: string
  dataUrl: string
  width: number | null
  height: number | null
  alignment: Alignment
}

/**
 * Yalnız `xslt` dalında: belgenin kendi IBAN satırları için kümülatif düzenleme. `index` analizdeki
 * `documentBank.rows[].index`; null → yeni satır. `remove` true ise ad/IBAN yok sayılır.
 */
export interface ComposeDocumentBankEdit {
  index: number | null
  bankName: string
  iban: string
  remove: boolean
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
  /** Yalnız `xslt` dalında: her faturada çıkmayabilecek yuvalar (en fazla 32); yalnız uyarı için. */
  conditionalSlots?: string[] | null
  /** Yalnız `xslt` dalında: dosyadaki görsellerin taşınması/silinmesi (en fazla 20, her görsel bir kez). */
  staticImages?: ComposeStaticImage[] | null
  /** Yalnız `xslt` dalında: serbest görseller (en fazla 1). */
  freeImages?: ComposeFreeImage[] | null
  /** Yalnız `xslt` dalında: belgenin kendi IBAN satırlarındaki düzenlemeler. */
  documentBankEdits?: ComposeDocumentBankEdit[] | null
  includeXslt: boolean
  includeHtml: boolean
}

/**
 * Statik görsel düzenlemesine özgü uyarı kodları; diğer kodlar (tema akışı) da gelebilir.
 */
export type StaticImageWarningCode =
  | 'static-image-not-found'
  | 'slot-not-found'
  | 'static-image-ignored'
  | 'static-image-conflict'
  | 'static-images-skipped'

export interface ComposeWarning {
  /** Bilinen kodlar: `StaticImageWarningCode` ve tema akışının kodları. */
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
