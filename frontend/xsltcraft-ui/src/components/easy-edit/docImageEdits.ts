import type { ImgState } from '../theme-assets/imageState'

/** Dosyadaki görsel kendi yuvasına dönerse taşıma yoktur (`slot` null). */
const normalizeSlot = (state: ImgState, slot: string | null) =>
  state.origin === 'extracted' && slot === state.homeSlot ? null : slot

/** Paneldeki düzenleme: dosyadaki görselde hizanın değiştiği işaretlenir. */
export function patchImage(state: ImgState, patch: Partial<ImgState>): ImgState {
  const next = { ...state, ...patch }
  if ('alignment' in patch && state.origin === 'extracted') next.alignTouched = true
  if ('slot' in patch) next.slot = normalizeSlot(state, patch.slot ?? null)
  return next
}

/** Önizlemede bırakılan yuvaya taşır. */
export const moveImage = (state: ImgState, slot: string): ImgState => patchImage(state, { slot })

/**
 * Yalnız doc. Yeni dosya: yüklenen görsel asset olarak gider. Dosyadaki görselin yerine geçiyorsa onun yeri korunur,
 * `index` geri dönüş için saklanır.
 */
export function replaceWithUpload(state: ImgState, dataUrl: string): ImgState {
  if (state.origin !== 'extracted') return { ...state, dataUrl, origin: 'upload' }
  return {
    ...state,
    dataUrl,
    origin: 'upload',
    slot: state.slot ?? state.homeSlot ?? null,
    width: '',
    height: '',
    sizeTouched: false,
    alignTouched: false,
  }
}
