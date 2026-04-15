export const GRID_SIZE_MM = 5
export const PAGE_WIDTH_MM = 210
export const PAGE_HEIGHT_MM = 297

/** Degeri en yakin grid boyutuna (varsayilan 5mm) dondurur. */
export function snapToGrid(valueMm: number, gridSize = GRID_SIZE_MM): number {
  return Math.round(valueMm / gridSize) * gridSize
}

/**
 * Bloku sayfa sinirlari icerisinde tutar.
 * Minimum boyut: 5mm x 5mm
 */
export function clampToPage(
  x: number,
  y: number,
  w: number,
  h: number,
): { x: number; y: number; width: number; height: number } {
  const cw = Math.max(5, Math.min(w, PAGE_WIDTH_MM))
  const ch = Math.max(5, Math.min(h, PAGE_HEIGHT_MM))
  const cx = Math.max(0, Math.min(x, PAGE_WIDTH_MM - cw))
  const cy = Math.max(0, Math.min(y, PAGE_HEIGHT_MM - ch))
  return { x: cx, y: cy, width: cw, height: ch }
}

/** Piksel degerini mm'ye cevirir. */
export function pxToMm(px: number, scale: number): number {
  return px / scale
}

/** Mm degerini piksele cevirir. */
export function mmToPx(mm: number, scale: number): number {
  return mm * scale
}
