import {
  analyzeXslt,
  type EasyEditAnalyzeResponse,
  type EasyEditError,
  type EasyEditImageRole,
} from '../../services/easyEditService'
import { analysisKey, useDocumentHandoffStore } from '../../store/documentHandoffStore'

export interface CachedAnalysis {
  key: string
  analysis: EasyEditAnalyzeResponse
}

/**
 * Metni (ve görsel rollerini) analiz eder: önce önbelleğe bakar, yoksa sunucuya gider ve yanıtı önbelleğe yazar
 * (`unavailable` yanıtı store yazmaz; "Tekrar dene" yeniden sunucuya gider). İptalde `null`; istek hatası fırlatılır
 * (`parseEasyEditError` ile sınıflandırılır).
 */
export async function analyzeCached(
  xslt: string,
  roles: EasyEditImageRole[] | undefined,
  signal: AbortSignal,
): Promise<CachedAnalysis | null> {
  const key = await analysisKey(xslt, roles)
  if (signal.aborted) return null
  const cached = useDocumentHandoffStore.getState().getCachedAnalysis(key)
  if (cached) return { key, analysis: cached }
  const analysis = await analyzeXslt(xslt, roles, signal)
  if (signal.aborted) return null
  useDocumentHandoffStore.getState().cacheAnalysis(key, analysis)
  return { key, analysis }
}

export const EASY_EDIT_PRO_MESSAGE = 'Kolay düzenle XsltCraft Pro’ya dahildir.'
const NETWORK_MESSAGE = 'Sunucuya ulaşılamadı; bağlantını kontrol edip tekrar dene.'
const DEFAULT_FALLBACK = 'Dosya analiz edilemedi; tekrar dene.'

/** Analiz isteği hatasının kullanıcı metni; `fallback` 400 ve bilinmeyen hatalarda kullanılır. */
export function easyEditErrorText(error: EasyEditError, fallback = DEFAULT_FALLBACK): string {
  switch (error.kind) {
    case 'upgrade':
      return error.message || EASY_EDIT_PRO_MESSAGE
    case 'rateLimited':
      return `Şu an yoğunluk var; ${error.retryAfterSeconds ? `${error.retryAfterSeconds} saniye sonra` : 'biraz sonra'} yeniden dene.`
    case 'network':
      return NETWORK_MESSAGE
    case 'badRequest':
      return error.message || fallback
    default:
      return fallback
  }
}
