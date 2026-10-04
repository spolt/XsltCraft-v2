/**
 * AI sohbetine eklenen ekran görüntülerini gönderime hazırlar: canvas ile yeniden boyutlandırır ve
 * yeniden kodlar (EXIF/GPS silinir, polyglot dosyalar nötralize olur), PNG ile JPEG'den küçük olanı seçer.
 * Sunucu yine de magic-byte + boyut doğrulaması yapar; buradaki limitler maliyet/gecikme içindir.
 */

/** Sunucunun teknik tavanı (Ai:Vision:MaxImagesPerMessage); plan limiti bunu aşamaz. */
export const MAX_IMAGES_TECHNICAL = 3
/** Uzun kenar üst sınırı (px). Sunucu tavanı 2048; 1536 token maliyetini düşürür. */
export const MAX_LONG_EDGE = 1536
/** Kodlanmış görsel başına hedef üst sınır (sunucu: 1.5 MB). */
export const MAX_ENCODED_BYTES = 1_572_864
/** Seçilen ham dosya üst sınırı. */
export const MAX_INPUT_BYTES = 20 * 1024 * 1024

/** Yalnız PNG / JPG / JPEG. */
export const ACCEPT_ATTR = 'image/png,image/jpeg,.png,.jpg,.jpeg'
const ACCEPTED_TYPES = ['image/png', 'image/jpeg', 'image/jpg']
const ACCEPTED_EXT = /\.(png|jpe?g)$/i

export interface PreparedImage {
  id: string
  mimeType: 'image/png' | 'image/jpeg'
  /** data: öneki olmadan. */
  base64: string
  width: number
  height: number
  sizeBytes: number
  /** URL.createObjectURL — sahibi kullanım bitince revoke etmeli. */
  previewUrl: string
}

export class ImageAttachmentError extends Error {}

export function isAcceptedImage(file: { type: string; name?: string }): boolean {
  const type = file.type.toLowerCase()
  if (type) return ACCEPTED_TYPES.includes(type)
  return !!file.name && ACCEPTED_EXT.test(file.name)
}

export async function prepareImage(file: File): Promise<PreparedImage> {
  if (!isAcceptedImage(file))
    throw new ImageAttachmentError('Yalnız PNG, JPG ve JPEG ekran görüntüleri eklenebilir.')
  if (file.size > MAX_INPUT_BYTES)
    throw new ImageAttachmentError('Görsel çok büyük (en fazla 20 MB).')

  let bitmap: ImageBitmap
  try {
    bitmap = await createImageBitmap(file, { imageOrientation: 'from-image' })
  } catch {
    throw new ImageAttachmentError('Görsel okunamadı; dosya bozuk olabilir.')
  }

  try {
    let scale = Math.min(1, MAX_LONG_EDGE / Math.max(bitmap.width, bitmap.height))
    for (let attempt = 0; attempt < 6; attempt++) {
      const width = Math.max(1, Math.round(bitmap.width * scale))
      const height = Math.max(1, Math.round(bitmap.height * scale))
      const canvas = draw(bitmap, width, height)

      // Ekran görüntülerinde PNG çoğu zaman JPEG'den küçüktür; ikisini dene, küçüğü al.
      const candidates = [
        await toBlob(canvas, 'image/png'),
        await toBlob(canvas, 'image/jpeg', 0.85),
      ]
      let best = candidates.reduce((a, b) => (b.size < a.size ? b : a))
      for (const quality of [0.7, 0.55]) {
        if (best.size <= MAX_ENCODED_BYTES) break
        best = await toBlob(canvas, 'image/jpeg', quality)
      }

      if (best.size <= MAX_ENCODED_BYTES) {
        return {
          id: newId(),
          mimeType: best.type === 'image/png' ? 'image/png' : 'image/jpeg',
          base64: await toBase64(best),
          width,
          height,
          sizeBytes: best.size,
          previewUrl: URL.createObjectURL(best),
        }
      }
      scale *= 0.8
    }
    throw new ImageAttachmentError('Görsel yeterince küçültülemedi; daha küçük bir alan seçin.')
  } finally {
    bitmap.close()
  }
}

function draw(bitmap: ImageBitmap, width: number, height: number): HTMLCanvasElement {
  const canvas = document.createElement('canvas')
  canvas.width = width
  canvas.height = height
  const ctx = canvas.getContext('2d')
  if (!ctx) throw new ImageAttachmentError('Tarayıcı görseli işleyemedi.')
  // Şeffaf alanlar JPEG'de siyaha dönmesin.
  ctx.fillStyle = '#ffffff'
  ctx.fillRect(0, 0, width, height)
  ctx.imageSmoothingQuality = 'high'
  ctx.drawImage(bitmap, 0, 0, width, height)
  return canvas
}

function toBlob(canvas: HTMLCanvasElement, type: string, quality?: number): Promise<Blob> {
  return new Promise((resolve, reject) =>
    canvas.toBlob(
      b => (b ? resolve(b) : reject(new ImageAttachmentError('Görsel kodlanamadı.'))),
      type,
      quality,
    ))
}

function toBase64(blob: Blob): Promise<string> {
  return new Promise((resolve, reject) => {
    const reader = new FileReader()
    reader.onload = () => resolve(String(reader.result).split(',', 2)[1] ?? '')
    reader.onerror = () => reject(new ImageAttachmentError('Görsel okunamadı.'))
    reader.readAsDataURL(blob)
  })
}

function newId(): string {
  return typeof crypto !== 'undefined' && 'randomUUID' in crypto
    ? crypto.randomUUID()
    : `${Date.now()}-${Math.random().toString(36).slice(2)}`
}

/** Clipboard / drag-drop / file input'tan gelen listeden yalnız dosyaları çıkarır. */
export function filesFrom(list: FileList | DataTransferItemList | null | undefined): File[] {
  if (!list) return []
  const out: File[] = []
  for (let i = 0; i < list.length; i++) {
    const item = list[i]
    const file = item instanceof File ? item : (item as DataTransferItem).kind === 'file' ? (item as DataTransferItem).getAsFile() : null
    if (file) out.push(file)
  }
  return out
}
