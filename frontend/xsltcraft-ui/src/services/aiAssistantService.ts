import api from './apiService'
import { useAuthStore } from '../store/authStore'
import { refreshAccessToken } from './authSession'

const API_BASE = import.meta.env.VITE_API_URL ?? 'http://localhost:5000'

export type AiTaskKind =
  | 'refactor-selection'
  | 'assistant'

export interface AiChunk {
  type: 'delta' | 'done' | 'error'
  text?: string
  provider?: string
  model?: string
  ms?: number
  code?: string
  message?: string
  /** HTTP hata yanıtındaki sunucu hata kodu (ör. image_count, vision_unavailable). */
  reason?: string
}

export interface AiStatus {
  enabled: boolean
  /** Ekran görüntüsü gönderilebilir mi (vision açık + uygun sağlayıcı var). */
  vision?: boolean
}

export async function getAiStatus(): Promise<AiStatus> {
  const { data } = await api.get<AiStatus>('/api/ai/status')
  return data
}

export interface RefactorSelectionBody { xslt?: string; selection: string; goal?: string }

export interface AssistantMessage {
  role: 'user' | 'assistant'
  content: string
  /** Bu geçmiş mesaja eklenmiş görsel sayısı — görseller yeniden gönderilmez, yalnız varlığı bildirilir. */
  imageCount?: number
}

/** Ekran görüntüsü: base64 (data: öneki yok). Yalnız mevcut turda gönderilir. */
export interface AssistantImage { mimeType: 'image/png' | 'image/jpeg'; data: string }

export interface AssistantBody {
  xslt: string
  xml: string | null
  xmlSelection?: string
  /** XSLT editöründe seçili metin — varsa o template tam bağlam olarak gönderilir. */
  xsltSelection?: string
  /** XSLT editöründe imlecin satırı (1-tabanlı) — bakılan template alaka skorunda öne çıkar. */
  xsltCursorLine?: number
  history: AssistantMessage[]
  message: string
  images?: AssistantImage[]
}

type Body = RefactorSelectionBody | AssistantBody

/**
 * NDJSON streaming. Her chunk için onChunk çağrılır.
 * AbortSignal ile iptal edilebilir.
 */
export async function streamAi(
  task: AiTaskKind,
  body: Body,
  onChunk: (chunk: AiChunk) => void,
  signal: AbortSignal,
): Promise<void> {
  const send = (token: string | null) => fetch(`${API_BASE}/api/ai/${task}`, {
    method: 'POST',
    headers: {
      'Content-Type': 'application/json',
      ...(token ? { Authorization: `Bearer ${token}` } : {}),
    },
    body: JSON.stringify(body),
    signal,
    credentials: 'include',
  })

  // fetch axios interceptor'ından geçmez: süresi dolan access token'ı burada yenile.
  let res = await send(useAuthStore.getState().accessToken)
  if (res.status === 401) {
    const fresh = await refreshAccessToken().catch(() => null)
    if (fresh) res = await send(fresh)
  }

  if (!res.ok) {
    let message = `AI isteği başarısız (${res.status}).`
    let reason: string | undefined
    try {
      const data = await res.json()
      message = data?.message ?? data?.error ?? message
      reason = data?.error
    } catch { /* ignore */ }
    onChunk({ type: 'error', code: `http_${res.status}`, message, reason })
    return
  }

  if (!res.body) {
    onChunk({ type: 'error', code: 'no_body', message: 'Sunucudan akış alınamadı.' })
    return
  }

  const reader = res.body.pipeThrough(new TextDecoderStream()).getReader()
  let buffer = ''
  while (true) {
    const { value, done } = await reader.read()
    if (done) break
    buffer += value
    let idx: number
    while ((idx = buffer.indexOf('\n')) >= 0) {
      const line = buffer.slice(0, idx).trim()
      buffer = buffer.slice(idx + 1)
      if (!line) continue
      try {
        onChunk(JSON.parse(line) as AiChunk)
      } catch {
        // satır parse edilemezse atla
      }
    }
  }
  if (buffer.trim()) {
    try { onChunk(JSON.parse(buffer.trim()) as AiChunk) } catch { /* ignore */ }
  }
}

// ── Geri bildirim (feedback) ─────────────────────────────────────────────────

export interface AiFeedbackBody {
  rating: 'positive' | 'negative'
  userMessage: string
  assistantAnswer: string
  applied?: boolean
}

export async function submitAiFeedback(body: AiFeedbackBody): Promise<{ id: string }> {
  const { data } = await api.post<{ id: string }>('/api/ai/feedback', body)
  return data
}

export async function updateAiFeedback(
  id: string,
  body: { rating: 'positive' | 'negative'; applied: boolean },
): Promise<void> {
  await api.put(`/api/ai/feedback/${id}`, body)
}

// ── Admin — geri bildirim havuzu ─────────────────────────────────────────────

export interface AdminAiFeedbackItem {
  id: string
  userId: string
  username: string | null
  email: string
  rating: 'Positive' | 'Negative'
  userMessage: string
  assistantAnswer: string
  applied: boolean
  isGlobal: boolean
  createdAt: string
}

export interface AdminAiFeedbackPage {
  items: AdminAiFeedbackItem[]
  total: number
}

export async function getAdminAiFeedback(params: {
  rating?: 'positive' | 'negative' | 'all'
  q?: string
  page?: number
  pageSize?: number
}): Promise<AdminAiFeedbackPage> {
  const { data } = await api.get<AdminAiFeedbackPage>('/api/admin/ai-feedback', { params })
  return data
}

export async function setAiFeedbackGlobal(id: string, isGlobal: boolean): Promise<void> {
  await api.put(`/api/admin/ai-feedback/${id}/global`, { isGlobal })
}

export async function deleteAiFeedback(id: string): Promise<void> {
  await api.delete(`/api/admin/ai-feedback/${id}`)
}

// Admin
export async function getAdminAiFlag(): Promise<{ enabled: boolean }> {
  const { data } = await api.get<{ enabled: boolean }>('/api/admin/feature-flags/ai')
  return data
}

export async function setAdminAiFlag(enabled: boolean): Promise<void> {
  await api.put('/api/admin/feature-flags/ai', { enabled })
}

export interface AiProviderHealth {
  name: string
  configured: boolean
  available: boolean
  model?: string | null
  latencyMs?: number | null
  error?: string | null
}

export async function getAiProviderHealth(): Promise<{ providers: AiProviderHealth[] }> {
  const { data } = await api.get<{ providers: AiProviderHealth[] }>('/api/admin/feature-flags/ai/health')
  return data
}

// Admin — provider preference
export async function getAiProvider(): Promise<{ provider: string }> {
  const { data } = await api.get<{ provider: string }>('/api/admin/feature-flags/ai/provider')
  return data
}

export async function setAiProvider(provider: string): Promise<void> {
  await api.put('/api/admin/feature-flags/ai/provider', { provider })
}

// Admin — ekran görüntüsü (vision)
export interface AiVisionStatus {
  enabled: boolean
  /** Tercih "ollama" iken yerel vision modeli yoksa/hata verirse Gemini'ye izin. */
  geminiFallback: boolean
  /** appsettings Ai:Ollama:VisionModel; null = tanımlı değil. */
  ollamaVisionModel: string | null
  /** Görselli isteğin etkin sağlayıcı sırası; boş = kullanılamıyor. */
  providers: string[]
}

export async function getAiVision(): Promise<AiVisionStatus> {
  const { data } = await api.get<AiVisionStatus>('/api/admin/feature-flags/ai/vision')
  return data
}

export async function setAiVision(patch: { enabled?: boolean; geminiFallback?: boolean }): Promise<AiVisionStatus> {
  const { data } = await api.put<AiVisionStatus>('/api/admin/feature-flags/ai/vision', patch)
  return data
}

// Admin — daily token usage
export interface AiUsageEntry {
  userId: string
  username: string | null
  email: string
  tokensUsed: number
}

export interface AiDailyUsage {
  date: string
  limit: number
  users: AiUsageEntry[]
}

export async function getAiUsage(date?: string): Promise<AiDailyUsage> {
  const q = date ? `?date=${encodeURIComponent(date)}` : ''
  const { data } = await api.get<AiDailyUsage>(`/api/admin/feature-flags/ai/usage${q}`)
  return data
}
