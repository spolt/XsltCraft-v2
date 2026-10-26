import { Columns3, Minus, PanelsTopLeft, Plus } from 'lucide-react'
import Switch from '../ui/Switch'
import ThemeSwatches from '../ui/ThemeSwatches'
import { EDITOR_THEMES } from '../../theme/uiThemes'
import { EDITOR_FONT_MAX, EDITOR_FONT_MIN, useUiPrefsStore, type XsltRightPanelMode } from '../../store/uiPrefsStore'
import { EDITOR_LAYOUTS } from './editorLayouts'

const groupTitle = 'text-[12px] font-semibold text-tx-2'

const RIGHT_PANEL_MODES: readonly { id: XsltRightPanelMode; label: string; hint: string; icon: typeof Columns3 }[] = [
  { id: 'split', label: 'Yan yana', hint: 'Önizleme ve AI ayrı sütunlarda', icon: Columns3 },
  { id: 'tabs', label: 'Tek panel', hint: 'Önizleme ve AI aynı geniş sütunda, sekmeli', icon: PanelsTopLeft },
]

const optionBtn = (on: boolean) =>
  `xc-focus flex h-8 items-center gap-1.5 rounded-md border px-2 text-[11.5px] transition-colors ${
    on ? 'border-ui-soft-line bg-ui-soft font-semibold text-ui-ink' : 'border-field-line bg-ui-card text-tx-2 hover:bg-ui-hover'
  }`

/**
 * XSLT editör sol menüsündeki saf görünüm tercihleri (uiPrefsStore). İşlevi olmayan
 * seçenekler (otomatik önizleme, kaydederken biçimlendir, problemleri otomatik aç) gösterilmez.
 */
export default function XsltEditorSettings() {
  const editorTheme = useUiPrefsStore(s => s.editorTheme)
  const setEditorTheme = useUiPrefsStore(s => s.setEditorTheme)
  const defaultLayout = useUiPrefsStore(s => s.editorDefaultLayout)
  const setDefaultLayout = useUiPrefsStore(s => s.setEditorDefaultLayout)
  const fontSize = useUiPrefsStore(s => s.editorFontSize)
  const setFontSize = useUiPrefsStore(s => s.setEditorFontSize)
  const rightMode = useUiPrefsStore(s => s.xsltRightPanelMode)
  const setRightMode = useUiPrefsStore(s => s.setXsltRightPanelMode)
  const wordWrap = useUiPrefsStore(s => s.editorWordWrap)
  const setWordWrap = useUiPrefsStore(s => s.setEditorWordWrap)

  return (
    <div className="flex flex-col gap-3.5 overflow-auto px-3.5 pb-3.5 pt-1">
      <div role="group" aria-labelledby="xs-set-ed-theme" className="flex flex-col gap-1.5">
        <span id="xs-set-ed-theme" className={groupTitle}>Editör teması</span>
        <div className="grid grid-cols-3 gap-1.5">
          {EDITOR_THEMES.map(t => {
            const on = t.id === editorTheme
            return (
              <button
                key={t.id}
                type="button"
                aria-pressed={on}
                onClick={() => setEditorTheme(t.id)}
                className="xc-focus flex flex-col gap-1 rounded-md text-left"
              >
                {/* Önizleme şeridi: data-editor-theme ile o temanın --ed-* değerleri çözülür. */}
                <span
                  aria-hidden="true"
                  data-editor-theme={t.id}
                  className={`flex h-10 flex-col gap-1 rounded-md bg-ed-bg p-[7px] ${
                    on ? 'ring-2 ring-ui-accent ring-offset-2 ring-offset-ui-card' : 'shadow-[inset_0_0_0_1px_var(--color-field-line)]'
                  }`}
                >
                  <span className="h-[3px] w-[70%] rounded-sm bg-ed-text" />
                  <span className="h-[3px] w-1/2 rounded-sm bg-ed-warn" />
                  <span className="h-[3px] w-4/5 rounded-sm bg-ed-muted" />
                </span>
                <span className={`text-[11.5px] text-tx-2 ${on ? 'font-semibold' : ''}`}>{t.name}</span>
              </button>
            )
          })}
        </div>
      </div>

      <div role="group" aria-labelledby="xs-set-layout" className="flex flex-col gap-1.5">
        <span id="xs-set-layout" className={groupTitle}>Varsayılan düzen</span>
        <div className="grid grid-cols-2 gap-1.5">
          {EDITOR_LAYOUTS.map(l => {
            const on = l.id === defaultLayout
            const Icon = l.icon
            return (
              <button
                key={l.id}
                type="button"
                aria-pressed={on}
                onClick={() => setDefaultLayout(l.id)}
                className={optionBtn(on)}
              >
                <Icon size={14} aria-hidden="true" className="flex-none" />
                {l.short}
              </button>
            )
          })}
        </div>
      </div>

      <div role="group" aria-labelledby="xs-set-right" className="flex flex-col gap-1.5">
        <span id="xs-set-right" className={groupTitle}>Sağ panel (Önizleme + AI)</span>
        <div className="grid grid-cols-2 gap-1.5">
          {RIGHT_PANEL_MODES.map(m => {
            const on = m.id === rightMode
            const Icon = m.icon
            return (
              <button key={m.id} type="button" aria-pressed={on} title={m.hint} onClick={() => setRightMode(m.id)} className={optionBtn(on)}>
                <Icon size={14} aria-hidden="true" className="flex-none" />
                <span className="truncate">{m.label}</span>
              </button>
            )
          })}
        </div>
      </div>

      <div className="flex items-center justify-between text-[12px]">
        <span id="xs-set-font" className="font-semibold text-tx-2">Yazı boyutu</span>
        <div role="group" aria-labelledby="xs-set-font" className="flex items-center rounded-md border border-field-line">
          <button
            type="button"
            aria-label="Yazı boyutunu küçült"
            onClick={() => setFontSize(fontSize - 1)}
            disabled={fontSize <= EDITOR_FONT_MIN}
            className="xc-focus flex h-7 w-7 items-center justify-center rounded-l-md text-tx-2 hover:bg-ui-hover disabled:opacity-40"
          >
            <Minus size={13} aria-hidden="true" />
          </button>
          <span aria-live="polite" className="w-[42px] text-center text-tx">{fontSize} px</span>
          <button
            type="button"
            aria-label="Yazı boyutunu büyüt"
            onClick={() => setFontSize(fontSize + 1)}
            disabled={fontSize >= EDITOR_FONT_MAX}
            className="xc-focus flex h-7 w-7 items-center justify-center rounded-r-md text-tx-2 hover:bg-ui-hover disabled:opacity-40"
          >
            <Plus size={13} aria-hidden="true" />
          </button>
        </div>
      </div>

      <div className="flex flex-col gap-1.5">
        <span className={groupTitle}>Arayüz rengi</span>
        <ThemeSwatches variant="dots" ariaLabel="Arayüz rengi" />
      </div>

      <Switch label="Satır kaydırma" checked={wordWrap} onChange={setWordWrap} />
    </div>
  )
}
