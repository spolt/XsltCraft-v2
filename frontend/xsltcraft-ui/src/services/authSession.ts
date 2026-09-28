import axios from 'axios'
import { useAuthStore } from '../store/authStore'

const API_BASE = import.meta.env.VITE_API_URL ?? 'http://localhost:5000'
const CONCURRENT_RETRY_DELAY_MS = 300

let inflight: Promise<string> | null = null

async function requestNewAccessToken(): Promise<string> {
  const post = () => axios.post<{ accessToken: string }>(`${API_BASE}/api/auth/refresh`, {}, { withCredentials: true })
  try {
    return (await post()).data.accessToken
  } catch (err) {
    // 409: başka bir sekme aynı refresh token'ı az önce döndürdü; tarayıcıdaki çerez artık
    // yenisi — kısa bir beklemeden sonra bir kez daha dene.
    if (axios.isAxiosError(err) && err.response?.status === 409) {
      await new Promise((r) => setTimeout(r, CONCURRENT_RETRY_DELAY_MS))
      return (await post()).data.accessToken
    }
    throw err
  }
}

/**
 * HttpOnly refresh çereziyle yeni access token alır. Eşzamanlı çağrılar tek isteği paylaşır.
 * Başarısızlıkta oturum yerelde de kapatılır ve hata fırlatılır.
 */
export function refreshAccessToken(): Promise<string> {
  inflight ??= requestNewAccessToken()
    .then((token) => {
      useAuthStore.getState().setAccessToken(token)
      return token
    })
    .catch((err) => {
      useAuthStore.getState().logout()
      throw err
    })
    .finally(() => { inflight = null })
  return inflight
}

/**
 * Access token yalnız bellekte tutulur; sayfa yenilenince kaybolur. Önceden giriş yapılmışsa
 * (kullanıcı bilgisi saklı) refresh çereziyle oturumu sessizce geri yükler.
 */
export async function ensureSession(): Promise<boolean> {
  const { accessToken, user } = useAuthStore.getState()
  if (accessToken) return true
  if (!user) return false
  try {
    await refreshAccessToken()
    return true
  } catch {
    return false
  }
}
