// Kapsayıcısının sağ-üst köşesine çapalı yüzen pencere geometrisi (saf fonksiyonlar).

export interface AnchoredRect { right: number; top: number; width: number; height: number }
export interface Size { width: number; height: number }
export interface Insets { top: number; right: number; bottom: number; left: number }
export interface RectLimits { minWidth: number; minHeight: number; maxWidth: number; maxHeight: number }

function clamp(value: number, min: number, max: number): number {
  return Math.round(Math.min(Math.max(value, min), Math.max(min, max)))
}

/** Dikdörtgeni boyut sınırlarına ve kapsayıcının iç paylarına sığdırır. */
export function clampRect(r: AnchoredRect, c: Size, l: RectLimits, i: Insets): AnchoredRect {
  const width = clamp(r.width, l.minWidth, Math.min(l.maxWidth, c.width - i.left - i.right))
  const height = clamp(r.height, l.minHeight, Math.min(l.maxHeight, c.height - i.top - i.bottom))
  return {
    width,
    height,
    right: clamp(r.right, i.right, c.width - i.left - width),
    top: clamp(r.top, i.top, c.height - i.bottom - height),
  }
}

/** Kapsayıcı, en küçük boyuttaki pencereyi paylarıyla birlikte alabiliyor mu? */
export function fitsContainer(c: Size, l: RectLimits, i: Insets): boolean {
  return c.width >= l.minWidth + i.left + i.right && c.height >= l.minHeight + i.top + i.bottom
}

export function moveRect(start: AnchoredRect, dx: number, dy: number): AnchoredRect {
  return { ...start, right: start.right - dx, top: start.top + dy }
}

/** Sol-alt köşeden boyutlandırma: sağ-üst köşe sabit kalır. */
export function resizeFromBottomLeft(
  start: AnchoredRect, dx: number, dy: number, c: Size, l: RectLimits, i: Insets,
): AnchoredRect {
  return {
    right: start.right,
    top: start.top,
    width: clamp(start.width - dx, l.minWidth, Math.min(l.maxWidth, c.width - i.left - start.right)),
    height: clamp(start.height + dy, l.minHeight, Math.min(l.maxHeight, c.height - i.bottom - start.top)),
  }
}
