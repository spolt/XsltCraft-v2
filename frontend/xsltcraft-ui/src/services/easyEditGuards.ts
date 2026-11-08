/**
 * Kolay düzenle istemci doğrulamaları (sunucu otoritatif; bunlar savunma katmanı).
 * - Görsel data URL'leri yalnız `<img src>` içinde kullanılır; `href`, `window.open` ya da CSS `url()` içinde kullanılmaz.
 * - Yuva kimlikleri sunucudan da gelse istemcide biçim denetiminden geçer.
 */

const IMAGE_DATA_URL = /^data:image\/(png|jpe?g|gif|webp|bmp);base64,/i
const SLOT_ID = /^[a-z][a-z0-9-]{1,40}$/

/** Değer izin verilen raster biçimlerinden birinin base64 data URL'si mi. */
export function isSafeImageDataUrl(value: unknown): value is string {
  return typeof value === 'string' && IMAGE_DATA_URL.test(value)
}

/** `<img src>` için güvenli değer; uymayan ya da boş girdide null. */
export function safeImageSrc(value: string | null | undefined): string | null {
  return isSafeImageDataUrl(value) ? value : null
}

/** Yuva kimliği biçimi geçerli mi. */
export function isValidSlotId(value: unknown): value is string {
  return typeof value === 'string' && SLOT_ID.test(value)
}

/** Geçerli biçimdeki yuva kimliğini döndürür; değilse null. */
export function safeSlotId(value: string | null | undefined): string | null {
  return isValidSlotId(value) ? value : null
}

/** Listeden geçersiz ve tekrarlanan yuva kimliklerini ayıklar (sıra korunur). */
export function filterSlotIds(values: readonly (string | null | undefined)[] | null | undefined): string[] {
  if (!values) return []
  const seen = new Set<string>()
  const result: string[] = []
  for (const value of values) {
    if (isValidSlotId(value) && !seen.has(value)) {
      seen.add(value)
      result.push(value)
    }
  }
  return result
}
