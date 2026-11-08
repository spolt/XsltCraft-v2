import { create } from 'zustand'
import type { EasyEditAnalyzeResponse } from '../services/easyEditService'
import { useAuthStore } from './authStore'

/** Analiz yanıtı; store yanıtı olduğu gibi taşır. */
export type EasyEditAnalysisSnapshot = EasyEditAnalyzeResponse

export type DocumentHandoffSource = 'upload' | 'template'

/** Dashboard / Şablonlarım'dan Kolay düzenle ekranına bir kez aktarılan belge. */
export interface DocumentHandoffPayload {
  source: DocumentHandoffSource
  templateId?: string
  /** Şablonun yüklendiği andaki `updatedAt`; kayıtta çakışma denetimi için. */
  savedUpdatedAt?: string
  name: string
  fileName: string
  xslt: string
  xml?: string
  dirty: boolean
  analysis?: EasyEditAnalysisSnapshot
  /** `analysisKey(xslt, imageRoles)` sonucu; `analysis` bu anahtarla üretildi. */
  analysisKey?: string
}

const ANALYSIS_CACHE_LIMIT = 8
const FNV_PREFIX = 'fnv-'
/** FNV-1a özeti çakışabilir; bu anahtarlarla önbellek kullanılmaz (metin yeniden analiz edilir). */
const isCacheableKey = (key: string) => !key.startsWith(FNV_PREFIX)

interface DocumentHandoffState {
  staged: Record<string, DocumentHandoffPayload>
  /** Anahtar → son analiz yanıtı; ekleme sırası en eskiden en yeniye. */
  analysisCache: Map<string, EasyEditAnalysisSnapshot>
  /** Belgeyi taslak kimliğine bırakır; aynı kimlikteki önceki aktarımın yerini alır. */
  stage: (draftId: string, payload: DocumentHandoffPayload) => void
  /** Eşleşen aktarımı döndürür ve siler; ikinci çağrı `null` döner. */
  consume: (draftId: string) => DocumentHandoffPayload | null
  getCachedAnalysis: (key: string) => EasyEditAnalysisSnapshot | undefined
  cacheAnalysis: (key: string, analysis: EasyEditAnalysisSnapshot) => void
  /** Aktarımları ve önbelleği boşaltır (oturum kapanışı). */
  clear: () => void
}

/** Ekranlar arası belge aktarımı ve analiz önbelleği: bellek içi, kalıcı değil. */
export const useDocumentHandoffStore = create<DocumentHandoffState>((set, get) => ({
  staged: {},
  analysisCache: new Map(),

  stage: (draftId, payload) => {
    const { staged, cacheAnalysis } = get()
    set({ staged: { ...staged, [draftId]: payload } })
    if (payload.analysis !== undefined && payload.analysisKey) cacheAnalysis(payload.analysisKey, payload.analysis)
  },

  consume: (draftId) => {
    const { staged } = get()
    const payload = staged[draftId]
    if (!payload) return null
    const rest = { ...staged }
    delete rest[draftId]
    set({ staged: rest })
    return payload
  },

  getCachedAnalysis: (key) => (isCacheableKey(key) ? get().analysisCache.get(key) : undefined),

  cacheAnalysis: (key, analysis) => {
    // Geçici "analiz yapılamadı" yanıtı saklanmaz: yeniden deneme sunucuya gitsin.
    if (!isCacheableKey(key) || analysis.verdict === 'unavailable') return
    const next = new Map(get().analysisCache)
    next.delete(key)
    next.set(key, analysis)
    while (next.size > ANALYSIS_CACHE_LIMIT) next.delete(next.keys().next().value as string)
    set({ analysisCache: next })
  },

  clear: () => set({ staged: {}, analysisCache: new Map() }),
}))

// Çıkışta sayfa yenilenmez: önceki kullanıcının belgeleri bellekte kalmasın.
useAuthStore.subscribe((state, prev) => {
  if (state.user?.id !== prev.user?.id) useDocumentHandoffStore.getState().clear()
})

/** 32 bit FNV-1a; `crypto.subtle` olmayan (güvenli olmayan) bağlamlar için. */
function fnv1a(text: string): string {
  let h = 0x811c9dc5
  for (let i = 0; i < text.length; i++) {
    h ^= text.charCodeAt(i)
    h = Math.imul(h, 0x01000193)
  }
  return (h >>> 0).toString(16).padStart(8, '0')
}

/** Metnin özeti: SHA-256 (hex), yoksa uzunluk + FNV-1a. */
export async function hashText(text: string): Promise<string> {
  const subtle = globalThis.crypto?.subtle
  if (subtle) {
    const digest = await subtle.digest('SHA-256', new TextEncoder().encode(text))
    return Array.from(new Uint8Array(digest), (b) => b.toString(16).padStart(2, '0')).join('')
  }
  return `${FNV_PREFIX}${text.length.toString(16)}-${fnv1a(text)}`
}

/** Analiz önbelleği anahtarı: aynı metin + aynı görsel rolleri → aynı anahtar. */
export function analysisKey(xslt: string, imageRoles?: readonly string[]): Promise<string> {
  const roles = imageRoles?.length ? imageRoles.join(',') : ''
  return hashText(`${roles}\n${xslt}`)
}
