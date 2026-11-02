import { useRef } from 'react'
import XsltEditor, { type EditorFns } from '../Xslteditor'
import FloatingPreviewWindow from '../xslt-editor/FloatingPreviewWindow'
import ProblemsPanel from '../xslt-editor/ProblemsPanel'
import XsltEditorToolbar from '../xslt-editor/XsltEditorToolbar'
import { aiPromptForProblem } from '../xslt-editor/xsltText'
import type { DevModeAi } from '../../hooks/useDevModeAi'
import type { EditorMaximize } from '../../hooks/useEditorMaximize'
import type { RawPreview } from '../../hooks/useRawPreview'
import type { useXmlDock } from '../../hooks/useXmlDock'
import type { XsltDiagnostics } from '../../hooks/useXsltDiagnostics'
import type { UserSnippet } from '../../services/snippetService'
import type { EditorLayoutId } from '../../store/uiPrefsStore'

interface Props {
  ready: boolean
  xslt: string
  xml: string | null
  userSnippets: UserSnippet[]
  ai: DevModeAi
  diag: XsltDiagnostics
  max: EditorMaximize
  preview: RawPreview
  xmlDock: ReturnType<typeof useXmlDock>
  layoutMode: EditorLayoutId
  applyLayout: (mode: EditorLayoutId) => void
  onChange: (xslt: string) => void
  onEditorReady: (fns: EditorFns) => void
  onRequestImageInsert: (lineNumber: number) => void
  onCursorChange: (line: number, column: number) => void
  onPreviewClick: (text: string, opts?: { exact?: boolean }) => void
  onPrint: () => void
  onShowShortcuts: () => void
}

/** Editör sütunu: araç çubuğu, Monaco, tam ekrandaki mini pencere ve Problemler. */
export default function DevEditorPane({
  ready,
  xslt,
  xml,
  userSnippets,
  ai,
  diag,
  max,
  preview,
  xmlDock,
  layoutMode,
  applyLayout,
  onChange,
  onEditorReady,
  onRequestImageInsert,
  onCursorChange,
  onPreviewClick,
  onPrint,
  onShowShortcuts,
}: Props) {
  const fnsRef = useRef<EditorFns | null>(null)
  const hostRef = useRef<HTMLDivElement>(null)

  // Tam ekranda bölme görünmez: AI açılınca önce çıkılır.
  function toggleAi() {
    if (!max.maximized && ai.pane === 'ai') {
      ai.showPreview()
    } else {
      max.exit()
      ai.open()
    }
  }

  return (
    // Tam ekran yalnız sınıf değişimidir: düğüm yer değiştirirse Monaco remount olur.
    <div className={max.maximized ? 'absolute inset-0 z-20 flex flex-col bg-ed-bg' : 'flex h-full flex-col bg-ed-bg'}>
      <XsltEditorToolbar
        onFormat={() => fnsRef.current?.formatDocument()}
        onToggleComment={() => fnsRef.current?.toggleComment()}
        showProblems={diag.showProblems}
        onToggleProblems={() => diag.setShowProblems((s) => !s)}
        errorCount={diag.errorCount}
        warningCount={diag.warningCount}
        onValidateUblTr={diag.validateUblTr}
        ublTrDisabled={!xml || diag.ublTrLoading}
        ublTrErrorCount={diag.ublTrErrorCount}
        layout={layoutMode}
        onLayoutChange={applyLayout}
        maximized={max.maximized}
        onToggleMaximize={max.toggle}
        onShowMini={layoutMode === 'editorOnly' ? () => max.setMiniOpen(true) : undefined}
        onPrint={onPrint}
        onShowShortcuts={onShowShortcuts}
        aiOpen={ai.pane === 'ai' && !max.maximized}
        onToggleAi={ai.enabled ? toggleAi : undefined}
      />
      <div ref={hostRef} className="relative flex-1 overflow-hidden">
        {ready ? (
          <XsltEditor
            value={xslt}
            preserveCursorOnExternalChange
            onChange={onChange}
            xmlContent={xml}
            userSnippets={userSnippets}
            onEditorReady={(fns) => {
              fnsRef.current = fns
              onEditorReady(fns)
            }}
            onRequestImageInsert={onRequestImageInsert}
            errors={diag.editorMarkers}
            aiEnabled={ai.enabled}
            onAiRefactor={ai.requestRefactor}
            onCursorLineChange={onCursorChange}
            onSelectionChange={ai.setXsltSelection}
          />
        ) : (
          <div className="flex h-full items-center justify-center">
            <span role="status" className="text-xs text-ed-muted">XSLT yükleniyor…</span>
          </div>
        )}
        {max.showMini && (
          <FloatingPreviewWindow
            containerRef={hostRef}
            html={preview.html}
            status={preview.error ? 'error' : preview.loading ? 'loading' : 'idle'}
            initialRect={max.miniRect}
            onRectCommit={max.commitMiniRect}
            onElementClick={onPreviewClick}
            onDock={() => { applyLayout('side'); ai.showPreview(); xmlDock.setTab('preview'); fnsRef.current?.focus() }}
            onClose={() => { max.setMiniOpen(false); fnsRef.current?.focus() }}
          />
        )}
      </div>
      {diag.showProblems && (
        <ProblemsPanel
          xsltProblems={diag.xsltProblems}
          xmlProblems={diag.xmlProblems}
          ublTrProblems={diag.ublTrProblems}
          ublTrLoading={diag.ublTrLoading}
          ublTrError={diag.ublTrError}
          onLocateXslt={(line, column) => fnsRef.current?.revealLine(line, column ?? undefined)}
          onLocateXml={xmlDock.locateLine}
          onAskAi={(problem) => { max.exit(); ai.ask(aiPromptForProblem(problem, xslt, xml)) }}
          onClose={() => diag.setShowProblems(false)}
        />
      )}
    </div>
  )
}
