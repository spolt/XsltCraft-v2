import api from './apiService'
import { useAuthStore } from '../store/authStore'

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
}

export interface AiStatus {
  enabled: boolean
}

export async function getAiStatus(): Promise<AiStatus> {
  const { data } = await api.get<AiStatus>('/api/ai/status')
  return data
}

export interface RefactorSelectionBody { xslt?: string; selection: string; goal?: string }

export interface AssistantMessage { role: 'user' | 'assistant'; content: string }

export interface AssistantBody {
  xslt: string
  xml: string | null
  xmlSelection?: string
  history: AssistantMessage[]
  message: string
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
  const token = useAuthStore.getState().accessToken
  const res = await fetch(`${API_BASE}/api/ai/${task}`, {
    method: 'POST',
    headers: {
      'Content-Type': 'application/json',
      ...(token ? { Authorization: `Bearer ${token}` } : {}),
    },
    body: JSON.stringify(body),
    signal,
    credentials: 'include',
  })

  if (!res.ok) {
    let message = `AI isteği başarısız (${res.status}).`
    try {
      const data = await res.json()
      message = data?.message ?? data?.error ?? message
    } catch { /* ignore */ }
    onChunk({ type: 'error', code: `http_${res.status}`, message })
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
