import { isAxiosError } from 'axios'
import { previewFromStoredXslt } from '../../services/previewService'
import defaultInvoiceXml from '../../assets/default-invoice.xml?raw'
import defaultDespatchXml from '../../assets/default-despatch.xml?raw'

/**
 * Hazır tema önizleme HTML'i — galeri kartları ve büyük önizleme aynı sonucu paylaşır.
 * Sunucu kullanıcı başına tek eşzamanlı tema render'ına izin verir (UserGate, aşımda 429 UserBusy) ve
 * uç 30 istek/dk sınırlıdır: istekler tek tek sıraya alınır, 429/503'te `Retry-After` kadar beklenip
 * yeniden denenir. Sonuç oturum boyunca `id:updatedAt` anahtarıyla saklanır; hata önbelleğe yazılmaz.
 */
const MAX_CONCURRENT = 1
const MAX_ATTEMPTS = 4
const cache = new Map<string, Promise<string>>()
const queue: (() => void)[] = []
let active = 0

function schedule<T>(task: () => Promise<T>, urgent: boolean): Promise<T> {
  return new Promise<T>((resolve, reject) => {
    const run = () => {
      active++
      task()
        .then(resolve, reject)
        .finally(() => {
          active--
          queue.shift()?.()
        })
    }
    if (active < MAX_CONCURRENT) run()
    else if (urgent) queue.unshift(run)
    else queue.push(run)
  })
}

function retryDelayMs(err: unknown, attempt: number): number | null {
  if (!isAxiosError(err)) return null
  const status = err.response?.status
  if (status !== 429 && status !== 503) return null
  const header = Number(err.response?.headers?.['retry-after'])
  return Number.isFinite(header) && header > 0 ? header * 1000 : 500 * 2 ** attempt
}

async function fetchWithRetry(id: string, xml: string): Promise<string> {
  for (let attempt = 0; ; attempt++) {
    try {
      return (await previewFromStoredXslt(id, xml)).html
    } catch (err) {
      const delay = attempt + 1 < MAX_ATTEMPTS ? retryDelayMs(err, attempt) : null
      if (delay === null) throw err
      await new Promise((r) => setTimeout(r, delay))
    }
  }
}

export interface ThemePreviewKey {
  id: string
  documentType: string
  updatedAt: string
}

/** `urgent`: büyük önizleme gibi kullanıcının beklediği istek, kart küçük resimlerinin önüne alınır. */
export function loadThemePreview(theme: ThemePreviewKey, urgent = false): Promise<string> {
  const key = `${theme.id}:${theme.updatedAt}`
  let pending = cache.get(key)
  if (!pending) {
    const xml = theme.documentType === 'Despatch' ? defaultDespatchXml : defaultInvoiceXml
    pending = schedule(() => fetchWithRetry(theme.id, xml), urgent)
    pending.catch(() => cache.delete(key))
    cache.set(key, pending)
  }
  return pending
}
