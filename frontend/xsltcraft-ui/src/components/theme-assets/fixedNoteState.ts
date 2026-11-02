import type { Alignment } from '../../services/previewService'
import type { ComposeFixedNote, FixedNoteFontFamily, FixedNotePosition } from '../../services/themeComposeService'

/** Sunucu sınırlarıyla aynı (ThemeComposeRequestValidator). */
export const NOTE_MAX_CHARS = 500
export const NOTE_MAX_LINES = 10

export const NOTE_FONT_SIZES = [9, 10, 11, 12, 13, 14, 16, 18] as const

/** Hazır renkler (not içeriği verisidir; UI rengi değildir). */
export const NOTE_COLOR_PRESETS: { value: string; label: string }[] = [
  { value: '#000000', label: 'Siyah' },
  { value: '#4B5563', label: 'Gri' },
  { value: '#1D4ED8', label: 'Mavi' },
  { value: '#B91C1C', label: 'Kırmızı' },
  { value: '#15803D', label: 'Yeşil' },
  { value: '#C2410C', label: 'Turuncu' },
]

export const NOTE_FONTS: { value: FixedNoteFontFamily; label: string; css: string }[] = [
  { value: 'theme', label: 'Tema yazı tipi', css: 'inherit' },
  { value: 'arial', label: 'Arial', css: 'Arial, Helvetica, sans-serif' },
  { value: 'verdana', label: 'Verdana', css: 'Verdana, Geneva, sans-serif' },
  { value: 'tahoma', label: 'Tahoma', css: 'Tahoma, Verdana, sans-serif' },
  { value: 'times', label: 'Times', css: '"Times New Roman", Times, serif' },
  { value: 'georgia', label: 'Georgia', css: 'Georgia, serif' },
  { value: 'courier', label: 'Courier', css: '"Courier New", Courier, monospace' },
]

export const NOTE_POSITIONS: { value: FixedNotePosition; label: string; hint: string }[] = [
  { value: 'before', label: 'Notların üstü', hint: 'Faturadaki notlardan hemen önce görünür.' },
  { value: 'after', label: 'Notların altı', hint: 'Notlar kutusunun en sonunda görünür.' },
]

const HEX_RE = /^#[0-9a-fA-F]{6}$/

export interface FixedNoteState {
  text: string
  bold: boolean
  italic: boolean
  underline: boolean
  fontSize: number
  color: string
  fontFamily: FixedNoteFontFamily
  align: Alignment
}

export type FixedNotesState = Record<FixedNotePosition, FixedNoteState>

export function defaultNote(): FixedNoteState {
  return { text: '', bold: false, italic: false, underline: false, fontSize: 11, color: '#000000', fontFamily: 'theme', align: 'left' }
}

export function defaultFixedNotes(): FixedNotesState {
  return { before: defaultNote(), after: defaultNote() }
}

export const isHexColor = (v: string) => HEX_RE.test(v)

export const noteHasText = (n: FixedNoteState) => n.text.trim().length > 0

/** İstemci sınırı: karakter + satır sayısı; \r ve \n dışı kontrol karakterleri atılır. */
export function clampNoteText(raw: string): string {
  // eslint-disable-next-line no-control-regex
  let text = raw.replace(/\r\n?/g, '\n').replace(/[\u0000-\u0009\u000B-\u001F\u007F]/g, '')
  const lines = text.split('\n')
  if (lines.length > NOTE_MAX_LINES) text = lines.slice(0, NOTE_MAX_LINES).join('\n')
  return text.slice(0, NOTE_MAX_CHARS)
}

export const lineCount = (text: string) => (text ? text.split('\n').length : 0)

/** Dolu notları istek biçimine çevirir; hiç not yoksa undefined (XSLT'ye dokunulmaz). */
export function toComposeNotes(state: FixedNotesState): ComposeFixedNote[] | undefined {
  const notes = NOTE_POSITIONS
    .map(({ value }) => ({ position: value, ...state[value] }))
    .filter(noteHasText)
    .map((n) => ({ ...n, color: isHexColor(n.color) ? n.color.toUpperCase() : '#000000' }))
  return notes.length > 0 ? notes : undefined
}

export function countNotes(state: FixedNotesState): number {
  return NOTE_POSITIONS.filter(({ value }) => noteHasText(state[value])).length
}

export function noteFontCss(family: FixedNoteFontFamily): string {
  return NOTE_FONTS.find((f) => f.value === family)?.css ?? 'inherit'
}
