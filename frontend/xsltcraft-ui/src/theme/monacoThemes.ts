import type { Monaco } from '@monaco-editor/react'
import type { EditorThemeId } from '../store/uiPrefsStore'

/*
 * Monaco CSS değişkeni okuyamaz; renkler theme.css'teki --ed-* paletinin (XsltEditor.dc.html ED)
 * hex karşılıklarıdır. Paleti değiştirirken iki yeri birlikte güncelle.
 */

export type MonacoThemeId = 'xc-dark' | 'xc-light' | 'xc-contrast'

const MONACO_THEME: Record<EditorThemeId, MonacoThemeId> = {
  dark: 'xc-dark',
  light: 'xc-light',
  contrast: 'xc-contrast',
}

/** Tercihteki editör temasının Monaco tema adı. `<Editor theme={monacoThemeId(editorTheme)} />` */
export function monacoThemeId(editorTheme: EditorThemeId): MonacoThemeId {
  return MONACO_THEME[editorTheme] ?? 'xc-dark'
}

interface Palette {
  bg: string
  text: string
  num: string
  numCur: string
  cur: string
  tag: string
  attr: string
  str: string
  punct: string
  comment: string
  selection: string
}

const PALETTES: Record<EditorThemeId, Palette> = {
  dark: {
    bg: '#1B1E27', text: '#D5DAE5', num: '#5F6779', numCur: '#D5DAE5', cur: '#262B38',
    tag: '#82AAFF', attr: '#F0B86E', str: '#A5D6A7', punct: '#8F9AB0', comment: '#6B7489', selection: '#33405C',
  },
  light: {
    bg: '#FFFFFF', text: '#1B2433', num: '#8592A6', numCur: '#1B2433', cur: '#EEF3FB',
    tag: '#1F4E9C', attr: '#9A4A00', str: '#1E7A4C', punct: '#5B6B82', comment: '#6B7A90', selection: '#CFDDF5',
  },
  contrast: {
    bg: '#000000', text: '#FFFFFF', num: '#B0B6C2', numCur: '#FFFFFF', cur: '#1C1C1C',
    tag: '#6FC3FF', attr: '#FFD166', str: '#7CFC9A', punct: '#FFFFFF', comment: '#C9CED8', selection: '#264F78',
  },
}

const BASE: Record<EditorThemeId, 'vs' | 'vs-dark' | 'hc-black'> = {
  dark: 'vs-dark',
  light: 'vs',
  contrast: 'hc-black',
}

const strip = (hex: string) => hex.replace('#', '')

/**
 * xc-dark / xc-light / xc-contrast temalarını tanımlar. `<Editor beforeMount={defineMonacoThemes} />`
 * olarak verilebilir; tekrar çağrılması güvenlidir.
 */
export function defineMonacoThemes(monaco: Monaco): void {
  for (const id of Object.keys(PALETTES) as EditorThemeId[]) {
    const p = PALETTES[id]
    monaco.editor.defineTheme(MONACO_THEME[id], {
      base: BASE[id],
      inherit: true,
      rules: [
        { token: 'tag', foreground: strip(p.tag) },
        { token: 'metatag', foreground: strip(p.tag) },
        { token: 'attribute.name', foreground: strip(p.attr) },
        { token: 'attribute.value', foreground: strip(p.str) },
        { token: 'string', foreground: strip(p.str) },
        { token: 'delimiter', foreground: strip(p.punct) },
        { token: 'comment', foreground: strip(p.comment), fontStyle: 'italic' },
      ],
      colors: {
        'editor.background': p.bg,
        'editor.foreground': p.text,
        'editorLineNumber.foreground': p.num,
        'editorLineNumber.activeForeground': p.numCur,
        'editor.lineHighlightBackground': p.cur,
        'editor.selectionBackground': p.selection,
        'editorGutter.background': p.bg,
      },
    })
  }
}
