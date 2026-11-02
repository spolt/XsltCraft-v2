import type { CSSProperties } from 'react'
import type { EditorThemeId, UiThemeId } from '../store/uiPrefsStore'

export interface UiThemeOption {
  id: UiThemeId
  name: string
  /** CSS background değeri (sabit örnek rengi; theme.css --ui-swatch-*). */
  swatch: string
}

export const UI_THEMES: readonly UiThemeOption[] = [
  { id: 'lacivert', name: 'Kurumsal Lacivert', swatch: 'var(--ui-swatch-lacivert)' },
  { id: 'mor', name: 'Mor', swatch: 'var(--ui-swatch-mor)' },
  { id: 'gece', name: 'Gece Mavisi', swatch: 'var(--ui-swatch-gece)' },
]

export const EDITOR_THEMES: readonly { id: EditorThemeId; name: string }[] = [
  { id: 'dark', name: 'Koyu' },
  { id: 'light', name: 'Açık' },
  { id: 'contrast', name: 'Yüksek kontrast' },
]

/** İşlev renkleri (theme.css --color-fn-*). 'accent' aktif arayüz temasının vurgu rengidir. */
export type FnColor = 'blue' | 'orange' | 'green' | 'gray' | 'purple' | 'violet' | 'slate' | 'red' | 'accent'

/**
 * Bir kapsayıcıya işlev rengi değişkenlerini verir: --fn, --fn-tint, --fn-ink, --fn-line, --fn-fg
 * (--fn-fg: dolu --fn zemini üstündeki metin/ikon rengi; açık temalarda beyaz, gece'de koyu).
 * Alt öğeler `bg-(--fn-tint) text-(--fn) group-hover:bg-(--fn) group-hover:text-(--fn-fg)` gibi kullanır;
 * `.xc-lift` hover kenarı --fn-line'ı okur.
 */
export function fnStyle(color: FnColor): CSSProperties {
  if (color === 'accent') {
    return {
      '--fn': 'var(--ui-accent)',
      '--fn-tint': 'var(--ui-soft)',
      '--fn-ink': 'var(--ui-ink)',
      '--fn-line': 'var(--ui-soft-line)',
      '--fn-fg': 'var(--ui-accent-fg)',
    } as CSSProperties
  }
  return {
    '--fn': `var(--color-fn-${color})`,
    '--fn-tint': `var(--color-fn-${color}-tint)`,
    '--fn-ink': `var(--color-fn-${color}-ink)`,
    '--fn-line': `var(--color-fn-${color}-line)`,
    '--fn-fg': 'var(--color-on-fn)',
  } as CSSProperties
}
