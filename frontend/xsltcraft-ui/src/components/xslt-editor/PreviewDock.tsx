import Editor from '@monaco-editor/react'
import type { editor as MonacoEditor } from 'monaco-editor'
import { Eye, FileCode2, PictureInPicture2, RefreshCw, WandSparkles } from 'lucide-react'
import XsltEditorPreview from './XsltEditorPreview'
import { useUiPrefsStore } from '../../store/uiPrefsStore'
import { defineMonacoThemes, monacoThemeId } from '../../theme/monacoThemes'
import type { XsltEngineName } from '../../services/previewService'

export type DockTab = 'preview' | 'xml'

interface Props {
  tab: DockTab
  onTabChange: (tab: DockTab) => void
  /** Önizleme iframe'i mount edilsin mi (tam ekranda mini pencere tek iframe'dir). */
  showFrame: boolean
  html: string
  onElementClick?: (text: string, opts?: { exact?: boolean }) => void
  previewLoading: boolean
  lastMs: number | null
  engine: XsltEngineName | null
  onRefresh: () => void
  onPopOut: () => void
  hasXml: boolean
  xmlDisplay: string
  xmlPretty: boolean
  onToggleXmlPretty: () => void
  xmlDisplayReformatted: boolean
  onXmlEditorMount: (editor: MonacoEditor.IStandaloneCodeEditor) => void
  /** Sekmeli sağ panelde başlık sekmede yazdığı için "Önizleme" başlığı görsel olarak gizlenir. */
  hideTitle?: boolean
}

const ENGINE_LABEL: Record<XsltEngineName, string> = { Saxon: 'Saxon-HE', DotNet: '.NET' }

const tabBtn = (on: boolean) =>
  `xc-focus flex h-[22px] items-center whitespace-nowrap rounded-[5px] px-2 text-[12px] transition-colors ${
    on ? 'bg-ui-card font-semibold text-tx shadow-[var(--ui-shadow-rest)]' : 'font-medium text-tx-muted hover:text-tx'
  }`

const toolBtn =
  'xc-focus flex h-[26px] w-[26px] flex-none items-center justify-center rounded-md border border-field-line bg-ui-card text-tx-2 transition-colors hover:bg-ui-hover disabled:opacity-40'

/** Sağdaki önizleme paneli: Önizleme | XML Kaynak, yenile, mini pencereye al. */
export default function PreviewDock({
  tab,
  onTabChange,
  showFrame,
  html,
  onElementClick,
  previewLoading,
  lastMs,
  engine,
  onRefresh,
  onPopOut,
  hasXml,
  xmlDisplay,
  xmlPretty,
  onToggleXmlPretty,
  xmlDisplayReformatted,
  onXmlEditorMount,
  hideTitle = false,
}: Props) {
  const editorTheme = useUiPrefsStore(s => s.editorTheme)
  const fontSize = useUiPrefsStore(s => s.editorFontSize)

  const meta = previewLoading
    ? 'Derleniyor…'
    : lastMs !== null
      ? `${engine ? `${ENGINE_LABEL[engine]} · ` : ''}${lastMs} ms`
      : null

  return (
    <section aria-label="Önizleme" className="@container flex h-full min-w-0 flex-col bg-ui-bg">
      <div className="flex h-[36px] flex-none items-center gap-1.5 overflow-hidden border-b border-ui-line bg-ui-card px-3">
        <h2 className={hideTitle ? 'sr-only' : 'm-0 flex-none text-[13px] font-semibold text-tx'}>Önizleme</h2>
        {meta && <span aria-live="polite" className="min-w-0 truncate text-[11.5px] text-tx-muted">{meta}</span>}
        <div className="flex-1" />
        {/* Görünüm seçici: e2e "XML Kaynak" butonuna dayanır — aria-pressed'li buton grubu. */}
        <div role="group" aria-label="Önizleme görünümü" className="flex flex-none items-center gap-0.5 rounded-md bg-ui-hover p-0.5">
          {/* Dar bölmede yalnız simge; erişilebilir ad ("Önizleme" / "XML Kaynak") her genişlikte aynı. */}
          <button
            type="button"
            aria-pressed={tab === 'preview'}
            onClick={() => onTabChange('preview')}
            title="Önizleme"
            className={tabBtn(tab === 'preview')}
          >
            <Eye size={13} aria-hidden="true" className="@min-[440px]:hidden" />
            <span className="sr-only @min-[440px]:not-sr-only">Önizleme</span>
          </button>
          <button
            type="button"
            aria-pressed={tab === 'xml'}
            onClick={() => onTabChange('xml')}
            disabled={!hasXml}
            title="XML Kaynak"
            className={`${tabBtn(tab === 'xml')} disabled:opacity-40`}
          >
            <FileCode2 size={13} aria-hidden="true" className="@min-[440px]:hidden" />
            <span className="sr-only @min-[440px]:not-sr-only">XML Kaynak</span>
          </button>
        </div>
        {tab === 'xml' && hasXml && (
          <button
            type="button"
            onClick={onToggleXmlPretty}
            aria-pressed={xmlPretty}
            className={`xc-focus flex h-[26px] flex-none items-center gap-1 rounded-md border px-2 text-[12px] transition-colors ${
              xmlPretty ? 'border-ui-soft-line bg-ui-soft text-ui-ink' : 'border-field-line bg-ui-card text-tx-muted hover:bg-ui-hover'
            }`}
            title={xmlPretty
              ? 'Biçimlendirme açık — ham baytları görmek için kapatın'
              : 'Ham görünüm — okunabilir girintileme için açın'}
          >
            <WandSparkles size={12} aria-hidden="true" />
            <span className="sr-only @min-[440px]:not-sr-only">{xmlPretty ? 'Biçimli' : 'Ham'}</span>
          </button>
        )}
        <button type="button" onClick={onRefresh} disabled={previewLoading} className={toolBtn} title="Önizlemeyi yenile" aria-label="Önizlemeyi yenile">
          <RefreshCw size={14} aria-hidden="true" className={previewLoading ? 'animate-spin motion-reduce:animate-none' : ''} />
        </button>
        <button
          type="button"
          onClick={onPopOut}
          className={toolBtn}
          title="Tam ekran editör + mini önizleme penceresi (Ctrl+Shift+F)"
          aria-label="Önizlemeyi mini pencerede göster"
        >
          <PictureInPicture2 size={14} aria-hidden="true" />
        </button>
      </div>

      <div className="min-h-0 flex-1">
        {tab === 'preview' ? (
          showFrame && <XsltEditorPreview html={html} onElementClick={onElementClick} fitToWidth />
        ) : (
          <div className="flex h-full flex-col bg-ed-bg">
            {xmlDisplayReformatted && (
              <div className="flex-none border-b border-ed-border bg-ed-panel px-3 py-1 text-[11px] text-ed-muted">
                Okunabilirlik için biçimlendirildi — satır numaraları kaynak dosyayla birebir örtüşmez.
              </div>
            )}
            <div className="min-h-0 flex-1">
              <Editor
                height="100%"
                language="xml"
                beforeMount={defineMonacoThemes}
                theme={monacoThemeId(editorTheme)}
                value={xmlDisplay}
                onMount={onXmlEditorMount}
                options={{
                  readOnly: true,
                  minimap: { enabled: false },
                  fontSize,
                  fontFamily: "'IBM Plex Mono', Consolas, 'Courier New', monospace",
                  lineHeight: Math.round(fontSize * 1.6),
                  wordWrap: 'on',
                  lineNumbers: 'on',
                  scrollBeyondLastLine: false,
                  folding: true,
                }}
              />
            </div>
          </div>
        )}
      </div>
    </section>
  )
}
