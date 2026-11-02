import axios from 'axios'

/** Backend `XsltFailureKind` (2.0.0) + hız sınırlayıcının `RateLimited` değeri. */
export type XsltErrorKind =
  | 'Xml'
  | 'Compile'
  | 'Runtime'
  | 'Security'
  | 'Unsupported'
  | 'Timeout'
  | 'MemoryLimit'
  | 'OutputLimit'
  | 'LimitExceeded'
  | 'EngineCrashed'
  | 'Busy'
  | 'UserBusy'
  | 'EngineUnavailable'
  | 'EngineInternal'
  | 'RateLimited'

/** Önizleme/doğrulama hatasının UI'dan bağımsız, tek biçimli hali. */
export interface XsltError {
  message: string
  /** 1 tabanlı satır; bilinmiyorsa 0. */
  line: number
  column: number
  kind?: XsltErrorKind
  retryAfterSeconds?: number
  /** Şablondan değil sistemden kaynaklı (meşgul, hız sınırı, ağ, oturum, sunucu) — satır işaretlenmez. */
  isOperational: boolean
}

const KINDS: readonly XsltErrorKind[] = [
  'Xml', 'Compile', 'Runtime', 'Security', 'Unsupported', 'Timeout', 'MemoryLimit', 'OutputLimit',
  'LimitExceeded', 'EngineCrashed', 'Busy', 'UserBusy', 'EngineUnavailable', 'EngineInternal', 'RateLimited',
]

const OPERATIONAL_KINDS: ReadonlySet<XsltErrorKind> = new Set<XsltErrorKind>([
  'Busy', 'UserBusy', 'EngineUnavailable', 'EngineInternal', 'RateLimited',
])

const MSG_BUSY = 'Önizleme motoru şu an meşgul, birkaç saniye sonra tekrar deneyin.'
const MSG_RATE = 'Çok sık istek gönderildi, lütfen biraz bekleyin.'
const MSG_TIMEOUT = 'Dönüşüm zaman sınırını aştı.'
const MSG_RESOURCE = 'Dönüşüm kaynak sınırını aştı; şablonu sadeleştirin.'
const MSG_INTERNAL = 'Beklenmeyen bir hata oluştu.'
const MSG_NETWORK = 'Sunucuya ulaşılamadı; bağlantınızı kontrol edin.'
const MSG_AUTH = 'Oturumunuz sona erdi; lütfen yeniden giriş yapın.'

interface ErrorBody {
  error?: unknown
  line?: unknown
  column?: unknown
  kind?: unknown
  code?: unknown
}

function asKind(value: unknown): XsltErrorKind | undefined {
  return typeof value === 'string' && (KINDS as readonly string[]).includes(value)
    ? (value as XsltErrorKind)
    : undefined
}

function asNumber(value: unknown): number {
  return typeof value === 'number' && Number.isFinite(value) && value > 0 ? value : 0
}

function parseRetryAfter(raw: unknown): number | undefined {
  if (typeof raw !== 'string' && typeof raw !== 'number') return undefined
  const seconds = Number(raw)
  if (Number.isFinite(seconds)) return seconds > 0 ? Math.ceil(seconds) : undefined
  const date = Date.parse(String(raw))
  if (Number.isNaN(date)) return undefined
  const diff = Math.ceil((date - Date.now()) / 1000)
  return diff > 0 ? diff : undefined
}

function parseBody(data: unknown): ErrorBody | undefined {
  if (typeof data === 'string') {
    try { data = JSON.parse(data) } catch { return undefined }
  }
  return data && typeof data === 'object' ? (data as ErrorBody) : undefined
}

function messageFor(
  kind: XsltErrorKind | undefined,
  backendMessage: string | undefined,
  code: string | undefined,
  retryAfterSeconds: number | undefined,
  fallback: string,
): string {
  switch (kind) {
    case 'EngineUnavailable':
      // Kalıcı neden (ör. worker kapalı) backend mesajıyla gösterilir; geçici yoklukta sabit metin.
      if (backendMessage && code === 'WorkerDisabled') return backendMessage
      return retryAfterSeconds ? `${MSG_BUSY} (~${retryAfterSeconds} sn)` : MSG_BUSY
    case 'Busy':
      return retryAfterSeconds ? `${MSG_BUSY} (~${retryAfterSeconds} sn)` : MSG_BUSY
    case 'UserBusy':
    case 'RateLimited':
      return MSG_RATE
    case 'Timeout':
      return MSG_TIMEOUT
    case 'MemoryLimit':
    case 'EngineCrashed':
      return MSG_RESOURCE
    case 'EngineInternal':
      return MSG_INTERNAL
    default:
      return backendMessage || fallback
  }
}

/**
 * Önizleme/doğrulama hatasını tek biçime çevirir. Girdi: axios hatası (gövde + durum + `Retry-After`),
 * `validate-xslt`'in `200 { valid:false, ... }` gövdesi ya da başka bir istisna.
 */
export function toXsltError(err: unknown, fallback = 'Önizleme alınamadı.'): XsltError {
  let status: number | undefined
  let body: ErrorBody | undefined
  let retryAfterSeconds: number | undefined

  if (axios.isAxiosError(err)) {
    if (!err.response) {
      return { message: MSG_NETWORK, line: 0, column: 0, isOperational: true }
    }
    status = err.response.status
    body = parseBody(err.response.data)
    const headers = err.response.headers as Record<string, unknown> | undefined
    retryAfterSeconds = parseRetryAfter(headers?.['retry-after'])
  } else {
    body = parseBody(err)
    if (err instanceof Error || !body) {
      return { message: fallback, line: 0, column: 0, isOperational: true }
    }
  }

  if (status === 401) {
    return { message: MSG_AUTH, line: 0, column: 0, isOperational: true }
  }

  let kind = asKind(body?.kind)
  if (!kind && status === 429) kind = 'RateLimited'
  if (!kind && status !== undefined && status >= 500) kind = status === 503 ? 'EngineUnavailable' : 'EngineInternal'

  const isOperational = kind ? OPERATIONAL_KINDS.has(kind) : status !== undefined && status >= 500
  const backendMessage = typeof body?.error === 'string' && body.error.trim() ? body.error : undefined

  return {
    message: messageFor(kind, backendMessage, typeof body?.code === 'string' ? body.code : undefined, retryAfterSeconds, fallback),
    line: isOperational ? 0 : asNumber(body?.line),
    column: isOperational ? 0 : asNumber(body?.column),
    kind,
    retryAfterSeconds,
    isOperational,
  }
}
