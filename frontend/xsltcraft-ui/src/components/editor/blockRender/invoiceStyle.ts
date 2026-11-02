import type { CSSProperties } from 'react'

/*
 * Fatura içeriği varsayılanları — üretilen XSLT'nin temel stilinin (XsltGeneratorService) yaklaşığı.
 * Bunlar UI renkleri değil, A4 belge içeriğinin renkleridir (sayfa her temada beyaz); blok config'inde
 * bir renk/boyut varsa o kullanılır.
 */

/** 1 mm, CSS px (96 dpi). İçerik bu ölçekte çizilir, canvas `scale`'ine `zoom` ile yaklaştırılır. */
export const CSS_PX_PER_MM = 96 / 25.4

export const DOC = {
  font: "Tahoma, 'Times New Roman', Times, serif",
  headingFont: 'Arial, Helvetica, sans-serif',
  fontSize: '11px',
  ink: '#000000',
  border: '#555555',
  borderStrong: '#333333',
  totalsBorder: '#AAAAAA',
  headerBg: '#E0E0E0',
  altRow: '#F9F9F9',
  highlight: '#FFFFCC',
  dividerColor: '#CCCCCC',
  placeholderLine: '#C3CEDD',
  placeholderInk: '#5B6B82',
  qrInk: '#1B2433',
} as const

export const ROOT_STYLE: CSSProperties = {
  fontFamily: DOC.font,
  fontSize: DOC.fontSize,
  color: DOC.ink,
  lineHeight: 1.4,
  textAlign: 'left',
}

export function borderOf(style: string | undefined, color: string = DOC.border): string {
  return `1px ${style || 'solid'} ${color}`
}

export const P_RESET: CSSProperties = { margin: 0 }
