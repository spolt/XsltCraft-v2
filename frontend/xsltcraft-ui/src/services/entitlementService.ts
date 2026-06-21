import api from './apiService'

export interface DailyUsage {
  aiRequestCount: number
  aiTokensUsed: number
  templateExportCount: number
}

/** /api/me/entitlements yanıtı. Limitlerde 0 = sınırsız. */
export interface Entitlements {
  role: string // 'User' | 'Editor' | 'Admin'
  plan: string // 'Free' | 'Pro'
  isPrivileged: boolean // Editör/Admin → tüm kotalar bypass
  canDownloadGridXslt: boolean
  canSaveRawXslt: boolean
  canAccessRawXslt: boolean
  canUsePremiumThemes: boolean
  dailyAiRequestLimit: number
  dailyAiTokenLimit: number
  dailyTemplateExportLimit: number
  usage: DailyUsage
}

export async function getEntitlements(): Promise<Entitlements> {
  const { data } = await api.get<Entitlements>('/api/me/entitlements')
  return data
}

export interface CheckoutResult {
  available: boolean
  plan: string
  message: string
}

/** Faz 1 stub: gerçek ödeme yok; "yakında" mesajı döner. */
export async function startCheckout(plan = 'Pro'): Promise<CheckoutResult> {
  const { data } = await api.post<CheckoutResult>('/api/billing/checkout', { plan })
  return data
}

export interface GateError {
  status?: number
  code?: string
  message?: string
  upgrade?: boolean
}

/**
 * Gate'li bir uçtan dönen 402/429 hatasını çözer. Yanıt gövdesi Blob (responseType:'blob' indirmelerde)
 * olabileceğinden metne çevirip JSON parse eder.
 */
export async function parseGateError(err: unknown): Promise<GateError> {
  const ax = err as { response?: { status?: number; data?: unknown } }
  const status = ax.response?.status
  let body = ax.response?.data as { error?: string; message?: string; upgrade?: boolean } | Blob | undefined
  if (body instanceof Blob) {
    try {
      body = JSON.parse(await body.text())
    } catch {
      body = undefined
    }
  }
  const parsed = body as { error?: string; message?: string; upgrade?: boolean } | undefined
  return { status, code: parsed?.error, message: parsed?.message, upgrade: parsed?.upgrade }
}

/** Pro'nun günlük indirme hakkından kalan; sınırsızsa (privileged) null. */
export function exportsRemaining(e: Entitlements | null): number | null {
  if (!e || e.isPrivileged || e.dailyTemplateExportLimit === 0) return null
  return Math.max(0, e.dailyTemplateExportLimit - e.usage.templateExportCount)
}

/** Free'nin günlük AI soru hakkından kalan; sınırsızsa null. */
export function aiRequestsRemaining(e: Entitlements | null): number | null {
  if (!e || e.isPrivileged || e.dailyAiRequestLimit === 0) return null
  return Math.max(0, e.dailyAiRequestLimit - e.usage.aiRequestCount)
}
