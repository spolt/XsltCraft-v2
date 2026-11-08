import type { ComposeFreeImage } from '../../services/themeComposeService'
import { isValidSlotId } from '../../services/easyEditGuards'
import { isThemeImageSlot, parseSize, type ImgState } from './imageState'

/**
 * Yalnız Kolay düzenle (doc): belgede boş bir yuvaya eklenen serbest görsel. `ImgState`'in alt kümesidir, böylece
 * `ImagePanel` ile düzenlenir. `slot` null iken görsel gönderilmez (konum seçimi zorunlu).
 */
export type FreeImgState = Pick<ImgState, 'dataUrl' | 'width' | 'height' | 'alignment' | 'slot' | 'sizeTouched'> & {
  /** `previous` → önceki kayıttan aktarılan; `upload` → kullanıcının yüklediği. */
  origin?: 'previous' | 'upload'
}

/** Sunucu sınırıyla aynı. */
export const MAX_FREE_IMAGES = 1

export const FREE_IMAGE_LABEL = 'Serbest görsel'

export const defaultFreeImg = (): FreeImgState => ({ dataUrl: '', width: '150', height: '', alignment: 'left', slot: null })

const NONE: ReadonlySet<string> = new Set()

/**
 * Boşalmış temadaki görsel yerleri (`existing-*`): o yuvadaki dosya görseli taşındı ya da kaldırıldı. Sunucu eski
 * yerde boş bir kutu bırakır; serbest görsel oraya konabilir.
 */
export function vacatedThemeSlots(...images: readonly ImgState[]): ReadonlySet<string> {
  const out = new Set<string>()
  for (const s of images) {
    if (s.origin !== 'extracted' || !s.homeSlot || !isThemeImageSlot(s.homeSlot)) continue
    if (!s.dataUrl || (s.slot && s.slot !== s.homeSlot)) out.add(s.homeSlot)
  }
  return out
}

/**
 * Serbest görselin konabileceği yuva mı: biçimi geçerli; temadaki görselin yeri (`existing-*`) yalnız boşaldıysa
 * (`vacated`).
 */
export const isFreeSlot = (slot: string | null | undefined, vacated: ReadonlySet<string> = NONE): slot is string =>
  isValidSlotId(slot) && (!isThemeImageSlot(slot) || vacated.has(slot))

/** Görselin önizlemedeki yuvası (boş ya da konumsuz görselde null). */
export const freePlacementSlot = (state: FreeImgState, vacated: ReadonlySet<string> = NONE): string | null =>
  state.dataUrl && isFreeSlot(state.slot, vacated) ? state.slot : null

/** Listedeki `index`. öğeye yama uygular; öğe yoksa varsayılandan oluşturur (en fazla `MAX_FREE_IMAGES`). */
export function patchFreeImage(list: readonly FreeImgState[], index: number, patch: Partial<FreeImgState>): FreeImgState[] {
  if (index < 0 || index >= MAX_FREE_IMAGES || index > list.length) return [...list]
  const next = [...list]
  next[index] = { ...(list[index] ?? defaultFreeImg()), ...patch }
  return next
}

const PANEL_FIELDS = ['dataUrl', 'width', 'height', 'alignment', 'slot', 'sizeTouched'] as const

/** `ImagePanel` yamasından serbest görselin alanları (diğerleri — köken, temadaki görsel — yok sayılır). */
export function toFreePatch(patch: Partial<ImgState>): Partial<FreeImgState> {
  const out: Partial<FreeImgState> = {}
  for (const key of PANEL_FIELDS) {
    if (key in patch) Object.assign(out, { [key]: patch[key] })
  }
  return out
}

export function removeFreeImage(list: readonly FreeImgState[], index: number): FreeImgState[] {
  return list.filter((_, i) => i !== index)
}

/** Compose isteği: yalnız görseli ve geçerli yuvası (temadaki görselin yeri yalnız boşaldıysa) olan öğeler. */
export function toComposeFreeImages(list: readonly FreeImgState[], vacated: ReadonlySet<string> = NONE): ComposeFreeImage[] {
  return list
    .filter((s) => s.dataUrl && isFreeSlot(s.slot, vacated))
    .slice(0, MAX_FREE_IMAGES)
    .map((s) => ({
      slot: s.slot as string,
      dataUrl: s.dataUrl,
      width: parseSize(s.width),
      height: parseSize(s.height),
      alignment: s.alignment,
    }))
}
