import type { AnchoredRect, Insets, RectLimits } from '../../utils/floatingRect'

export const MINI_PREVIEW_STORAGE_KEY = 'xsltcraft-mini-preview'
// Üst boşluk, Monaco bul kutusunun (sağ üst, ~33 px) pencere başlığını örtmemesi için.
export const DEFAULT_MINI_RECT: AnchoredRect = { right: 24, top: 44, width: 360, height: 480 }
export const MINI_LIMITS: RectLimits = { minWidth: 240, minHeight: 200, maxWidth: 720, maxHeight: 1600 }
// Sağ pay, Monaco'nun dikey kaydırma çubuğunu açıkta bırakır.
export const MINI_INSETS: Insets = { top: 8, right: 20, bottom: 16, left: 8 }

export interface MiniPreviewPrefs {
  open: boolean
  rect: AnchoredRect
}

const DEFAULT_PREFS: MiniPreviewPrefs = { open: true, rect: DEFAULT_MINI_RECT }

export function loadMiniPreviewPrefs(): MiniPreviewPrefs {
  try {
    const raw = localStorage.getItem(MINI_PREVIEW_STORAGE_KEY)
    if (!raw) return DEFAULT_PREFS
    const parsed = JSON.parse(raw) as Partial<MiniPreviewPrefs> | null
    const rect = parsed?.rect
    const rectValid = rect != null
      && [rect.right, rect.top, rect.width, rect.height].every(n => Number.isFinite(n))
    return {
      open: typeof parsed?.open === 'boolean' ? parsed.open : DEFAULT_PREFS.open,
      rect: rectValid
        ? { right: rect.right, top: rect.top, width: rect.width, height: rect.height }
        : DEFAULT_MINI_RECT,
    }
  } catch {
    return DEFAULT_PREFS
  }
}

export function saveMiniPreviewPrefs(prefs: MiniPreviewPrefs): void {
  try {
    localStorage.setItem(MINI_PREVIEW_STORAGE_KEY, JSON.stringify(prefs))
  } catch {
    // depolama kapalı/dolu — tercih yalnız bu oturumda geçerli kalır
  }
}
