import axios from 'axios'
import api from './apiService'
import { parseGateError } from './entitlementService'
import type { ThemeSlotOption } from './templateService'
import type { ComposeFixedNote } from './themeComposeService'
import type { Alignment } from './previewService'
import { parseRetryAfter } from './xsltErrors'

export type EasyEditVerdict = 'suitable' | 'partial' | 'unsuitable' | 'invalid' | 'rejected' | 'unavailable'
export type EasyEditDocumentType = 'Invoice' | 'Despatch'
export type EasyEditImageRole = 'logo' | 'signature' | 'design'
export type EasyEditImageSource = 'client' | 'marker' | 'suggested'
/** Dosya görselinin neden taşınamadığı: gömülü data URL değil, biçim desteklenmiyor ya da `src` XML'den üretiliyor. */
export type EasyEditImageBlocker = 'no-data-url' | 'format-unsupported' | 'dynamic-attribute'

/** Dosyada bulunan görsel. `previewDataUrl` yalnız `<img src>` içinde, `safeImageSrc` ile kullanılır. */
export interface EasyEditImage {
  index: number
  role: EasyEditImageRole
  source: EasyEditImageSource
  reason: string
  line: number
  previewDataUrl: string | null
  movable: boolean
  /** Taşınamıyorsa nedeni (`movable` false iken); taşınabilirse null. */
  blocker: EasyEditImageBlocker | null
  slotId: string | null
}

/** Önceki Kolay düzenle oturumunun metne işlenmiş görseli. */
export interface EasyEditPreviousImage {
  /** Tanınmayan görselde null. */
  kind: 'logo' | 'signature' | null
  slot: string | null
  dataUrl: string | null
  width: number | null
  height: number | null
  alignment: Alignment
  valid: boolean
  problem: string | null
}

/** Önceki Kolay düzenle oturumunun serbest görseli (belgede boş bir yuvaya eklenmiş). */
export interface EasyEditPreviousFreeImage {
  slot: string
  dataUrl: string | null
  width: number | null
  height: number | null
  alignment: Alignment
  valid: boolean
  problem: string | null
}

export interface EasyEditBankRow {
  bankName: string
  iban: string
}

export interface EasyEditPrevious {
  recognized: boolean
  problem: string | null
  images: EasyEditPreviousImage[]
  hide: ('logo' | 'signature')[]
  bank: EasyEditBankRow[]
  /** Eski sunucu yanıtında olmayabilir. */
  freeImages?: EasyEditPreviousFreeImage[]
}

/** Belgenin kendi (literal) IBAN satırı; `index` düzenleme isteğindeki `documentBankEdits[].index`. */
export interface EasyEditDocumentBankRow {
  index: number
  bankName: string
  iban: string
  bankNameEditable: boolean
  deletable: boolean
  line: number
}

/** Belgedeki IBAN tablosu: `found` false ise belge IBAN'ı bulunamadı; `canAdd` false ise satır eklenemez. */
export interface EasyEditDocumentBank {
  found: boolean
  canAdd: boolean
  rows: EasyEditDocumentBankRow[]
}

/** Konum grubu ve o gruptaki konum sayısı. */
export interface EasyEditFeature {
  group: string
  count: number
}

export interface EasyEditWarning {
  code: string
  message: string
  slotId: string | null
}

export interface EasyEditAnalyzeResponse {
  verdict: EasyEditVerdict
  reasonCode: string | null
  message: string | null
  documentType: EasyEditDocumentType
  slots: ThemeSlotOption[]
  conditionalSlots: string[]
  images: EasyEditImage[]
  previous: EasyEditPrevious | null
  features: EasyEditFeature[]
  warnings: EasyEditWarning[]
  notes: ComposeFixedNote[]
  notesSupported: boolean
  /** Yalnız kullanılabilir kararda dolu; eski sunucu yanıtında olmayabilir. */
  documentBank?: EasyEditDocumentBank | null
  /** Yalnız metin olarak kullanılır (editör, indirme, compose isteği); HTML olarak render edilmez. */
  xslt: string | null
  /** Yalnız `invalid` kararında. */
  error: { line: number | null } | null
}

export type EasyEditErrorKind = 'upgrade' | 'rateLimited' | 'badRequest' | 'canceled' | 'network' | 'unknown'

export interface EasyEditError {
  kind: EasyEditErrorKind
  status?: number
  message?: string
  /** 400'de hatalı alan (`xslt` | `imageRoles`). */
  field?: string
  /** 429'da `Retry-After` (saniye); başlık okunamazsa undefined. */
  retryAfterSeconds?: number
}

/** Kullanıcının yüklediği XSLT'yi Kolay düzenle uygunluğu için analiz eder (Pro). */
export async function analyzeXslt(
  xslt: string,
  imageRoles?: EasyEditImageRole[],
  signal?: AbortSignal,
): Promise<EasyEditAnalyzeResponse> {
  const body = imageRoles ? { xslt, imageRoles } : { xslt }
  const { data } = await api.post<EasyEditAnalyzeResponse>('/api/easy-edit/analyze', body, { signal })
  return data
}

/** `analyzeXslt` hatasını sınıflandırır: 402 yükseltme, 429 meşgul/limit (`Retry-After`), 400 alan hatası. */
export async function parseEasyEditError(err: unknown): Promise<EasyEditError> {
  if (axios.isCancel(err)) return { kind: 'canceled' }
  if (axios.isAxiosError(err) && !err.response) return { kind: 'network' }

  const gate = await parseGateError(err)
  const { status } = gate
  if (status === 402) return { kind: 'upgrade', status, message: gate.message }

  const response = axios.isAxiosError(err) ? err.response : undefined
  if (status === 429) {
    const headers = response?.headers as Record<string, unknown> | undefined
    return {
      kind: 'rateLimited',
      status,
      message: gate.code,
      retryAfterSeconds: parseRetryAfter(headers?.['retry-after']),
    }
  }
  if (status === 400) {
    const data = response?.data as { field?: unknown } | undefined
    const field = typeof data?.field === 'string' ? data.field : undefined
    return { kind: 'badRequest', status, message: gate.code, field }
  }
  return { kind: 'unknown', status, message: gate.message ?? gate.code }
}
