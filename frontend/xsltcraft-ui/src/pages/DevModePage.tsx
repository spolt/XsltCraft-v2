import { useCallback, useEffect, useMemo, useRef, useState } from 'react'
import { useNavigate, useParams } from 'react-router-dom'
import { Panel, Group as PanelGroup, Separator as PanelResizeHandle } from 'react-resizable-panels'
import { DndContext, DragOverlay, pointerWithin } from '@dnd-kit/core'
import { TriangleAlert } from 'lucide-react'
import AiRefactorDialog from '../components/ai/AiRefactorDialog'
import DevAssetsPanel from '../components/dev-mode/DevAssetsPanel'
import DevEditorPane from '../components/dev-mode/DevEditorPane'
import DevModeHeader from '../components/dev-mode/DevModeHeader'
import DevModeLoadError from '../components/dev-mode/DevModeLoadError'
import DevPreviewPane from '../components/dev-mode/DevPreviewPane'
import DevToolsSections from '../components/dev-mode/DevToolsSections'
import SlotPlacementOverlay from '../components/dev-mode/SlotPlacementOverlay'
import { draftHasContent } from '../components/dev-mode/draftCompose'
import { AssetDragPreview } from '../components/theme-assets/DraggableAssetChip'
import { announcements, screenReaderInstructions } from '../components/theme-assets/slotDnd'
import SaveTemplateDialog from '../components/xslt-editor/SaveTemplateDialog'
import ShortcutsDialog from '../components/xslt-editor/ShortcutsDialog'
import SnippetManagerDialog from '../components/xslt-editor/SnippetManagerDialog'
import XsltStatusBar from '../components/xslt-editor/XsltStatusBar'
import { EDITOR_PANE, layoutName, PREVIEW_PANE, separatorCls } from '../components/xslt-editor/editorLayouts'
import { printActivePreview } from '../components/xslt-editor/previewFrame'
import { formatSize, snippetPlainText, stylesheetVersion } from '../components/xslt-editor/xsltText'
import { useDevModeAi } from '../hooks/useDevModeAi'
import { useDevModeCompose } from '../hooks/useDevModeCompose'
import { useDevModeDocument } from '../hooks/useDevModeDocument'
import { useDevModeLayout } from '../hooks/useDevModeLayout'
import { useEditorImageInsert } from '../hooks/useEditorImageInsert'
import { useLeaveGuard } from '../hooks/useLeaveGuard'
import { useRawPreview } from '../hooks/useRawPreview'
import { useSlotDragDrop } from '../hooks/useSlotDragDrop'
import { useTemplateSave } from '../hooks/useTemplateSave'
import { useXmlDock } from '../hooks/useXmlDock'
import { useXsltDiagnostics } from '../hooks/useXsltDiagnostics'
import { reportDownload } from '../services/activityService'
import { listSnippets, type UserSnippet } from '../services/snippetService'
import { useThemeDraftStore } from '../store/themeDraftStore'
import { toast } from '../store/toastStore'
import { readUploadFile, UPLOAD_ACCEPT } from '../utils/uploadValidation'

export default function DevModePage() {
  const { templateId } = useParams<{ templateId: string }>()
  const navigate = useNavigate()

  const doc = useDevModeDocument(templateId)
  const { ready, xmlContent } = doc
  const workingXslt = useThemeDraftStore((s) => s.workingXslt)
  const baselineXslt = useThemeDraftStore((s) => s.baselineXslt)
  const setManualXslt = useThemeDraftStore((s) => s.setManualXslt)
  const applyAiXslt = useThemeDraftStore((s) => s.applyAiXslt)
  const xslt = workingXslt ?? ''
  const isDirty = workingXslt !== baselineXslt

  const diag = useXsltDiagnostics({ xslt, xml: xmlContent })
  const preview = useRawPreview({ xslt, xml: xmlContent, onLineError: diag.reportXsltError })
  const compose = useDevModeCompose(doc.initialWarnings)
  const save = useTemplateSave({ themeId: templateId, defaultName: doc.templateName, xmlContent, enabled: ready, flush: compose.flush })
  const { confirmLeave, guardedNavigate } = useLeaveGuard(isDirty)
  const ai = useDevModeAi()
  const { showPreview: showAiPreviewTab } = ai
  const dnd = useSlotDragDrop(compose.composeNow)

  const [panelOpen, setPanelOpen] = useState(() => draftHasContent(useThemeDraftStore.getState()))
  const [slotsOpen, setSlotsOpen] = useState(false)
  const [userSnippets, setUserSnippets] = useState<UserSnippet[]>([])
  const [showSnippetManager, setShowSnippetManager] = useState(false)
  const [showShortcuts, setShowShortcuts] = useState(false)
  const [cursor, setCursor] = useState<{ line: number; column: number } | null>(null)

  // Editör işlevleri ve sayfadaki tek XML / tek görsel girişi.
  const goToRef = useRef<((term: string) => void) | null>(null)
  const insertTextAtLineRef = useRef<((lineNumber: number, text: string) => void) | null>(null)
  const focusEditorRef = useRef<(() => void) | null>(null)
  const xmlInputRef = useRef<HTMLInputElement>(null)
  const downloadingRef = useRef(false)
  const { inputRef: imageInputRef, request: requestImageInsert, handleFile: handleImageFile } = useEditorImageInsert(insertTextAtLineRef)

  // ─── Düzen ──────────────────────────────────────────────────────────────────
  // Kısayol yalnız editör hazırken ve üzerinde diyalog yokken etkindir.
  const { max, splitMode, groupRef, layoutMode, applyLayout } = useDevModeLayout({
    ready,
    shortcutEnabled: ready && !save.dialogOpen && !showShortcuts && !showSnippetManager && !ai.refactor,
  })
  const { exit: maxExit } = max
  const panelVisible = panelOpen && !max.maximized
  // Tam ekrana geçiş alan görünümünü kapatır; dönüşte kendiliğinden yeniden açılmaz.
  if (max.maximized && slotsOpen) setSlotsOpen(false)
  const showSlots = slotsOpen && doc.slots.length > 0

  const showPreviewPane = useCallback(() => {
    maxExit()
    showAiPreviewTab()
    setSlotsOpen(false)
  }, [maxExit, showAiPreviewTab])
  const xmlDock = useXmlDock({ xml: xmlContent, xmlValid: diag.xmlValid, showPane: showPreviewPane })
  const closeSlots = useCallback(() => setSlotsOpen(false), [])
  const focusEditor = useCallback(() => focusEditorRef.current?.(), [])

  // Moda girince odağı editöre al (örtülen önizleme panelinde ya da tıklanan butonda kalmasın).
  useEffect(() => {
    if (max.maximized) focusEditorRef.current?.()
  }, [max.maximized])

  function togglePanel() {
    if (max.maximized) {
      maxExit()
      setPanelOpen(true)
    } else {
      setPanelOpen((o) => !o)
    }
  }

  // ─── Kısayol ve veri ────────────────────────────────────────────────────────
  useEffect(() => {
    listSnippets().then(setUserSnippets).catch(() => {})
  }, [])

  useEffect(() => {
    function handleKeyDown(e: KeyboardEvent) {
      if (e.key === 'F1') {
        e.preventDefault()
        setShowShortcuts((s) => !s)
      }
    }
    document.addEventListener('keydown', handleKeyDown)
    return () => document.removeEventListener('keydown', handleKeyDown)
  }, [])

  // ─── XML ────────────────────────────────────────────────────────────────────
  async function handleXmlFile(e: React.ChangeEvent<HTMLInputElement>) {
    const file = e.target.files?.[0]
    e.target.value = ''
    if (!file) return
    try {
      doc.setXml({ content: await readUploadFile(file, 'xml'), fileName: file.name })
    } catch (err) {
      toast.error((err as Error).message, { title: 'XML yüklenemedi' })
    }
  }

  // ─── Editör eylemleri ───────────────────────────────────────────────────────
  function handleInsertSnippet(snippet: UserSnippet) {
    // Konum yokken 1. satıra eklemek metni XML bildirimi ile kökün arasına yazar ve bozar.
    if (!cursor) {
      toast.warning('Snippet eklemek için önce editörde bir konuma tıklayın.')
      return
    }
    setSlotsOpen(false)
    insertTextAtLineRef.current?.(cursor.line, snippetPlainText(snippet.body))
  }

  const handlePreviewClick = useCallback((text: string, opts?: { exact?: boolean }) => {
    // Görsel tıklamaları base64 imzasını birebir arar; metinde kısa bir parça yeter.
    goToRef.current?.(opts?.exact || text.length <= 50 ? text : text.substring(0, 50))
  }, [])

  function handlePrint() {
    if (!printActivePreview()) {
      toast.warning('Yazdırmak için önizleme açık olmalı (Önizleme sekmesi veya mini pencere).', { title: 'Önizleme kapalı' })
    }
  }

  async function handleDownload() {
    if (downloadingRef.current) return
    downloadingRef.current = true
    try {
      if (!(await compose.flush())) return
      const content = useThemeDraftStore.getState().workingXslt
      if (content === null) return
      const blob = new Blob([content], { type: 'application/xslt+xml' })
      const a = document.createElement('a')
      a.href = URL.createObjectURL(blob)
      a.download = `${doc.templateName.replace(/[^a-z0-9çğıöşüÇĞİÖŞÜ]/gi, '_')}.xslt`
      a.click()
      URL.revokeObjectURL(a.href)
      reportDownload('Template', templateId)
    } finally {
      downloadingRef.current = false
    }
  }

  // ─── Görünüm ────────────────────────────────────────────────────────────────
  const version = useMemo(() => stylesheetVersion(xslt), [xslt])
  const xsltSize = useMemo(() => formatSize(xslt), [xslt])
  const xmlSize = useMemo(() => (xmlContent ? formatSize(xmlContent) : ''), [xmlContent])
  const xsltEol = useMemo(() => (xslt.includes('\r\n') ? 'CRLF' : 'LF'), [xslt])

  const header = (
    <DevModeHeader
      name={doc.templateName}
      version={version}
      dirty={isDirty}
      ready={ready}
      panelOpen={panelVisible}
      onNavigate={guardedNavigate}
      confirmLeave={confirmLeave}
      onBack={() => navigate(`/theme-use/${templateId}`)}
      onTogglePanel={togglePanel}
      onSave={save.requestSave}
      onDownload={handleDownload}
    />
  )

  if (doc.loadError) {
    return (
      <div className="flex h-screen flex-col overflow-hidden bg-ui-bg text-tx">
        {header}
        <DevModeLoadError message={doc.loadError} />
      </div>
    )
  }

  return (
    <div className="flex h-screen flex-col overflow-hidden bg-ui-bg text-tx">
      {header}

      {/* İlk render'dan itibaren koşulsuz sarar: sonradan eklenmesi Monaco'yu remount ederdi. */}
      <DndContext
        sensors={dnd.sensors}
        collisionDetection={pointerWithin}
        onDragStart={dnd.onDragStart}
        onDragEnd={dnd.onDragEnd}
        onDragCancel={dnd.onDragCancel}
        accessibility={{ announcements, screenReaderInstructions }}
      >
      <div className="flex min-h-0 flex-1">
        {/* Grup dışında: açılıp kapanması editör ve önizleme düğümlerine dokunmaz. */}
        <aside
          aria-label="Tema düzenleme"
          className={panelVisible ? 'flex w-80 flex-none flex-col overflow-y-auto border-r border-ui-line bg-ui-card' : 'hidden'}
        >
          <DevAssetsPanel
            slots={doc.slots}
            fixedNotesSupported={compose.fixedNotesSupported}
            composeError={compose.error}
            composeErrorField={compose.errorField}
            warnings={compose.warnings}
            pendingInvalid={compose.pendingInvalid}
            composeNow={compose.composeNow}
            showSlots={showSlots}
            onToggleSlots={() => setSlotsOpen((o) => !o)}
          />
          <DevToolsSections
            xsltFile={{ name: `${doc.templateName}.xslt`, size: xsltSize, dirty: isDirty }}
            xmlFile={{ name: doc.xmlName, size: xmlSize }}
            onReplaceXml={() => xmlInputRef.current?.click()}
            onOpenXml={xmlDock.openSource}
            snippets={userSnippets}
            onInsertSnippet={handleInsertSnippet}
            onManageSnippets={() => setShowSnippetManager(true)}
          />
        </aside>

        <div className="flex min-w-0 flex-1 flex-col">
          {preview.error && (
            <div role="alert" className="flex flex-none items-start gap-2 border-b border-fn-red-line bg-fn-red-tint px-4 py-2 text-xs text-fn-red-ink">
              <TriangleAlert size={13} aria-hidden="true" className="mt-0.5 flex-none" />
              <span className="whitespace-pre-wrap break-all font-mono">{preview.error}</span>
            </div>
          )}

          <div className="relative min-h-0 flex-1">
            {/* Örtü açıkken grup yerinde durur ama odak ve imleç almaz. */}
            <PanelGroup inert={showSlots} id="dev-mode-editor-panels" groupRef={groupRef} orientation="horizontal" className="relative h-full overflow-hidden">
              <Panel id={EDITOR_PANE} defaultSize={splitMode === 'half' ? '50%' : '65%'} minSize="25%">
                <DevEditorPane
                  ready={ready}
                  xslt={xslt}
                  xml={xmlContent}
                  userSnippets={userSnippets}
                  ai={ai}
                  diag={diag}
                  max={max}
                  preview={preview}
                  xmlDock={xmlDock}
                  layoutMode={layoutMode}
                  applyLayout={applyLayout}
                  onChange={setManualXslt}
                  onEditorReady={({ goTo, insertTextAtLine, focus }) => {
                    goToRef.current = goTo
                    insertTextAtLineRef.current = insertTextAtLine
                    focusEditorRef.current = focus
                  }}
                  onRequestImageInsert={requestImageInsert}
                  onCursorChange={(line, column) => setCursor({ line, column })}
                  onPreviewClick={handlePreviewClick}
                  onPrint={handlePrint}
                  onShowShortcuts={() => setShowShortcuts(true)}
                />
              </Panel>

              <PanelResizeHandle
                aria-label="Editör ile önizleme arasını boyutlandır"
                disabled={max.maximized}
                className={`${separatorCls} w-px`}
              />

              <Panel id={PREVIEW_PANE} defaultSize={splitMode === 'half' ? '50%' : '35%'} minSize="340px">
                {/* Tam ekranda mini pencere tek iframe'dir; bu bölme görünmez ama mount kalır. */}
                <div className={`h-full ${max.maximized ? 'invisible' : ''}`}>
                  <DevPreviewPane
                    ai={ai}
                    preview={preview}
                    xmlDock={xmlDock}
                    showFrame={!max.maximized && !showSlots}
                    onElementClick={handlePreviewClick}
                    onPopOut={() => applyLayout('mini')}
                    xslt={xslt}
                    xml={xmlContent}
                    xsltCursorLine={cursor?.line}
                    xmlDeclarationMissing={diag.declProblem != null}
                    onApplyXslt={applyAiXslt}
                    confirmLeave={confirmLeave}
                  />
                </div>
              </Panel>
            </PanelGroup>
            {showSlots && (
              <SlotPlacementOverlay
                html={preview.html}
                slots={doc.slots}
                dragging={dnd.dragKind !== null}
                dialogOpen={save.dialogOpen || showShortcuts || showSnippetManager}
                onClose={closeSlots}
                focusFallback={focusEditor}
              />
            )}
          </div>
        </div>
      </div>

      <DragOverlay dropAnimation={null}>
        {dnd.dragKind && dnd.draggedDataUrl ? <AssetDragPreview kind={dnd.dragKind} dataUrl={dnd.draggedDataUrl} /> : null}
      </DragOverlay>
      </DndContext>

      {ready && (
        <XsltStatusBar
          previewLoading={preview.loading}
          lastMs={preview.lastMs}
          lastTimings={preview.timings}
          engine={preview.engine}
          errorCount={diag.errorCount}
          warningCount={diag.warningCount}
          onToggleProblems={() => diag.setShowProblems((s) => !s)}
          cursorLine={cursor?.line}
          cursorColumn={cursor?.column}
          xsltVersion={version}
          eol={xsltEol}
          layoutLabel={layoutName(layoutMode)}
        />
      )}

      <input ref={xmlInputRef} type="file" accept={UPLOAD_ACCEPT.xml} className="hidden" onChange={handleXmlFile} />
      <input ref={imageInputRef} type="file" accept="image/*" className="hidden" onChange={handleImageFile} />

      {showSnippetManager && (
        <SnippetManagerDialog onClose={() => setShowSnippetManager(false)} onSnippetsChanged={setUserSnippets} />
      )}

      {save.dialogOpen && (
        <SaveTemplateDialog
          defaultName={save.dialogName}
          isUpdate={save.isUpdate}
          saving={save.saving}
          onSave={save.save}
          onClose={save.closeDialog}
        />
      )}

      {showShortcuts && <ShortcutsDialog onClose={() => setShowShortcuts(false)} showMaximize />}

      {ai.refactor && (
        <AiRefactorDialog
          originalSelection={ai.refactor.selection}
          fullXslt={xslt}
          onAccept={ai.applyRefactor}
          onClose={ai.closeRefactor}
        />
      )}
    </div>
  )
}
