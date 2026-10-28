import React, { useEffect, useRef, useState, useCallback } from 'react'
import { useParams, useNavigate, useSearchParams } from 'react-router-dom'
import {
  DndContext,
  DragOverlay,
  PointerSensor,
  useSensor,
  useSensors,
  type DragStartEvent,
  type DragEndEvent,
} from '@dnd-kit/core'
import EditorLeftPanel from '../components/editor/EditorLeftPanel'
import DesignStatusBar from '../components/editor/DesignStatusBar'
import { DOCUMENT_TYPE_LABEL, findPaletteItem } from '../components/editor/paletteIcons'
import WorkspaceHeader from '../components/shell/WorkspaceHeader'
import { HeaderButton, HeaderChip, HeaderDivider, HeaderStatus } from '../components/shell/headerParts'
import { Check, Download, Eye, PanelLeft, Pencil, Redo2, Save, Undo2 } from 'lucide-react'
import Canvas from '../components/editor/Canvas'
import { CANVAS_DROPPABLE_ID, canvasPageRef, canvasScaleRef } from '../components/editor/canvasRefs'
import PropertyPanel from '../components/editor/PropertyPanel'
import EditorPreviewPanel from '../components/editor/EditorPreviewPanel'
import { useEditorStore } from '../store/editorStore'
import { useXmlStore } from '../store/xmlStore'
import {
  getTemplate,
  createTemplate,
  updateTemplate,
  downloadTemplate,
} from '../services/templateService'
import { generateXslt } from '../services/previewService'
import { useEntitlementStore } from '../store/entitlementStore'
import { openUpgradeModal } from '../store/upgradeModalStore'
import { exportsRemaining, parseGateError } from '../services/entitlementService'
import { toast } from '../store/toastStore'
import { readUploadFile, UPLOAD_ACCEPT } from '../utils/uploadValidation'
import type { BlockTree, BlockTreeV1, BlockTreeV2, DocumentType } from '../types/template'
import type { BlockType } from '../types/blocks'
import { migrateV1toV2 } from '../utils/treeMigration'
import { snapToGrid, clampToPage, pxToMm } from '../utils/gridSnap'
import defaultInvoiceXml from '../assets/default-invoice.xml?raw'
import defaultDespatchXml from '../assets/default-despatch.xml?raw'

const AUTOSAVE_DELAY_MS = 30_000

/** Belge türüne göre editörde varsayılan olarak yüklenecek örnek XML. */
function loadDefaultXml(documentType: DocumentType) {
  useXmlStore.setState({ xmlFiles: [], activeXmlId: null })
  if (documentType === 'Despatch') {
    useXmlStore.getState().addXmlFile('varsayilan-irsaliye.xml', defaultDespatchXml)
  } else {
    useXmlStore.getState().addXmlFile('varsayilan-fatura.xml', defaultInvoiceXml)
  }
}

export default function EditorPage() {
  const { templateId: routeTemplateId } = useParams<{ templateId?: string }>()
  const [searchParams] = useSearchParams()
  const newDocumentType: DocumentType = searchParams.get('type') === 'despatch' ? 'Despatch' : 'Invoice'
  const navigate = useNavigate()

  const templateId      = useEditorStore((s) => s.templateId)
  const templateName    = useEditorStore((s) => s.templateName)
  const blocks          = useEditorStore((s) => s.blocks)
  const documentType    = useEditorStore((s) => s.documentType)
  const addBlock        = useEditorStore((s) => s.addBlock)
  const isDirty         = useEditorStore((s) => s.isDirty)
  const past            = useEditorStore((s) => s.past)
  const future          = useEditorStore((s) => s.future)
  const undo            = useEditorStore((s) => s.undo)
  const redo            = useEditorStore((s) => s.redo)
  const resetTree       = useEditorStore((s) => s.resetTree)
  const loadTree        = useEditorStore((s) => s.loadTree)
  const setTemplateId   = useEditorStore((s) => s.setTemplateId)
  const setTemplateName = useEditorStore((s) => s.setTemplateName)
  const setHasStoredXslt = useEditorStore((s) => s.setHasStoredXslt)

  // XML store
  const addXmlFile  = useXmlStore((s) => s.addXmlFile)
  const isSelecting = useXmlStore((s) => s.isSelecting)


  const [isLoading, setIsLoading]         = useState(!!routeTemplateId)
  const [isSaving, setIsSaving]           = useState(false)
  const [isDownloading, setIsDownloading] = useState(false)
  const [saveError, setSaveError]         = useState<string | null>(null)
  const [isEditingName, setIsEditingName] = useState(false)
  const [nameInput, setNameInput]         = useState(templateName)
  const [showPreview, setShowPreview]     = useState(false)
  const [xmlError, setXmlError]           = useState<string | null>(null)
  const [leftOpen, setLeftOpen]           = useState(() => window.innerWidth >= 900)
  // XPath seçimi başladığında (sağ paneldeki bağlantı düğmesi) sol panel kapalıysa aç — ağaç orada.
  if (isSelecting && !leftOpen) setLeftOpen(true)
  const [namePrompt, setNamePrompt]       = useState<null | 'save' | 'download'>(null)
  const [saveSuccess, setSaveSuccess]       = useState(false)
  const saveSuccessTimer                    = useRef<ReturnType<typeof setTimeout> | null>(null)
  const [downloadSuccess, setDownloadSuccess] = useState(false)
  const downloadSuccessTimer                = useRef<ReturnType<typeof setTimeout> | null>(null)

  const autosaveTimerRef = useRef<ReturnType<typeof setTimeout> | null>(null)
  const xmlInputRef      = useRef<HTMLInputElement>(null)

  // ── Kaydedilmemiş değişiklik uyarısı ────────────────────────────────────────
  useEffect(() => {
    const handler = (e: BeforeUnloadEvent) => {
      if (isDirty) { e.preventDefault(); e.returnValue = '' }
    }
    window.addEventListener('beforeunload', handler)
    return () => window.removeEventListener('beforeunload', handler)
  }, [isDirty])

  const confirmLeave = useCallback(
    () => !isDirty || window.confirm('Kaydedilmemiş değişiklikler var. Sayfadan ayrılmak istiyor musun?'),
    [isDirty],
  )

  const handleNavigate = useCallback((path: string) => {
    if (!confirmLeave()) return
    navigate(path)
  }, [confirmLeave, navigate])

  // ── DnD (sadece palet -> canvas icin) ────────────────────────────────────────
  const [activeId, setActiveDndId] = useState<string | null>(null)
  const dragStartPointerRef = useRef<{ clientX: number; clientY: number } | null>(null)

  const sensors = useSensors(
    useSensor(PointerSensor, { activationConstraint: { distance: 5 } }),
  )

  function handleDragStart({ active, activatorEvent }: DragStartEvent) {
    setActiveDndId(String(active.id))
    const pe = activatorEvent as PointerEvent
    dragStartPointerRef.current = { clientX: pe.clientX, clientY: pe.clientY }
  }

  function handleDragEnd({ active, over, delta }: DragEndEvent) {
    setActiveDndId(null)

    const activeData = active.data.current as { source: string; blockType?: BlockType; configOverride?: Record<string, unknown> } | undefined
    if (activeData?.source !== 'palette') return

    const blockType = activeData.blockType!
    const configOverride = activeData.configOverride

    // Drop hedefi canvas degil ise yoksay
    if (!over || over.id !== CANVAS_DROPPABLE_ID) return

    // Pointer pozisyonunu hesapla: dragStart + delta
    const start = dragStartPointerRef.current
    if (!start || !canvasPageRef.current) return

    const finalX = start.clientX + delta.x
    const finalY = start.clientY + delta.y
    const rect = canvasPageRef.current.getBoundingClientRect()
    const scale = canvasScaleRef.current

    const xMm = pxToMm(finalX - rect.left, scale)
    const yMm = pxToMm(finalY - rect.top, scale)

    const snapped = {
      x: snapToGrid(xMm),
      y: snapToGrid(yMm),
    }
    const clamped = clampToPage(snapped.x, snapped.y, 30, 20)
    addBlock(blockType, { x: clamped.x, y: clamped.y }, configOverride)
  }

  const activePaletteItem = activeId ? findPaletteItem(activeId) : undefined

  // ── XML yükleme ──────────────────────────────────────────────────────────────
  async function handleXmlUpload(e: React.ChangeEvent<HTMLInputElement>) {
    const file = e.target.files?.[0]
    e.target.value = ''
    if (!file) return
    const showError = (msg: string) => {
      setXmlError(msg)
      setTimeout(() => setXmlError(null), 4000)
    }
    let content: string
    try {
      content = await readUploadFile(file, 'xml')
    } catch (err) {
      showError((err as Error).message)
      return
    }
    const doc = new DOMParser().parseFromString(content, 'application/xml')
    if (doc.getElementsByTagName('parsererror').length > 0) {
      showError(`"${file.name}" geçerli bir XML dosyası değil.`)
      return
    }
    setXmlError(null)
    addXmlFile(file.name, content)
  }

  // ── Template yükleme ─────────────────────────────────────────────────────────
  useEffect(() => {
    if (!routeTemplateId) {
      resetTree(newDocumentType)
      setNameInput(useEditorStore.getState().templateName)
      loadDefaultXml(newDocumentType)
      return
    }

    resetTree()
    setIsLoading(true)
    getTemplate(routeTemplateId)
      .then((tpl) => {
        setTemplateId(tpl.id)
        setTemplateName(tpl.name)
        setNameInput(tpl.name)
        setHasStoredXslt(tpl.hasStoredXslt)

        // Ağaçta belge türü yoksa (eski kayıtlar) şablonun kendi türüne düş.
        const withType = (tree: BlockTreeV2): BlockTreeV2 => ({ ...tree, documentType: tree.documentType ?? tpl.documentType })
        let tree: BlockTreeV2 = { version: 2, blocks: {} }
        if (tpl.blockTree) {
          try {
            const raw: BlockTree = JSON.parse(tpl.blockTree)
            // V1 mi V2 mi?
            tree = !('version' in raw) || (raw as { version?: number }).version !== 2
              ? migrateV1toV2(raw as BlockTreeV1)
              : raw as BlockTreeV2
          } catch { /* bozuk ağaç → boş şablon */ }
        }
        loadTree(withType(tree))
        loadDefaultXml(useEditorStore.getState().documentType)
      })
      .catch(() => { setSaveError('Template yüklenemedi.'); loadDefaultXml('Invoice') })
      .finally(() => setIsLoading(false))
  }, [routeTemplateId, newDocumentType]) // eslint-disable-line react-hooks/exhaustive-deps

  // ── Kaydetme ─────────────────────────────────────────────────────────────────
  const serializeTree = useCallback((): string => {
    return JSON.stringify({ version: 2, documentType, blocks })
  }, [blocks, documentType])

  const save = useCallback(async (forceName?: string) => {
    if (isSaving) return
    if (!templateId && !forceName) {
      setNamePrompt('save')
      return
    }
    const nameToUse = forceName ?? templateName
    setSaveError(null)
    setIsSaving(true)
    try {
      const blockTreeJson = serializeTree()
      if (!templateId) {
        const created = await createTemplate({
          name: nameToUse,
          documentType,
          blockTree: blockTreeJson,
        })
        setTemplateId(created.id)
        // URL'yi güncelle ama komponent remount olmadan (navigate farklı route → remount eder)
        window.history.replaceState({}, '', `/editor/${created.id}`)
      } else {
        await updateTemplate(templateId, { name: nameToUse, blockTree: blockTreeJson })
      }
      useEditorStore.setState({ isDirty: false })
      if (saveSuccessTimer.current) clearTimeout(saveSuccessTimer.current)
      setSaveSuccess(true)
      saveSuccessTimer.current = setTimeout(() => setSaveSuccess(false), 3000)
    } catch (err: unknown) {
      const msg = (err as { response?: { data?: { message?: string } } })?.response?.data?.message
      setSaveError(msg ?? 'Kayıt başarısız. Tekrar deneyin.')
    } finally {
      setIsSaving(false)
    }
  }, [isSaving, templateId, templateName, documentType, serializeTree, navigate]) // eslint-disable-line react-hooks/exhaustive-deps

  // ── Otomatik kaydetme — sadece mevcut (kayıtlı) şablonlar için ───────────────
  useEffect(() => {
    if (!isDirty || !templateId) return
    if (autosaveTimerRef.current) clearTimeout(autosaveTimerRef.current)
    autosaveTimerRef.current = setTimeout(() => { save() }, AUTOSAVE_DELAY_MS)
    return () => { if (autosaveTimerRef.current) clearTimeout(autosaveTimerRef.current) }
  }, [isDirty, templateId, blocks, templateName]) // eslint-disable-line react-hooks/exhaustive-deps

  // ── XSLT indirme ─────────────────────────────────────────────────────────────
  const handleDownload = useCallback(async (forceName?: string) => {
    const hasBlocks = Object.keys(blocks).length > 0
    if (isDownloading || !hasBlocks) return

    // Proaktif gate (backend yine de zorunlu kılar): Standart indiremez → upsell; Pro günlük 3 dolduysa uyar.
    const ent = useEntitlementStore.getState().entitlements
    if (ent && !ent.canDownloadGridXslt && !ent.isPrivileged) {
      openUpgradeModal({
        title: 'İndirme Pro üyelik gerektirir',
        message: 'Tasarladığın şablonun production XSLT’sini indirmek XsltCraft Pro’ya dahildir.',
      })
      return
    }
    if (ent && !ent.isPrivileged && exportsRemaining(ent) === 0) {
      toast.warning('Bugünkü 3 şablon indirme hakkınız doldu. Yarın tekrar deneyebilirsiniz.', { title: 'Günlük limit' })
      return
    }

    if (!templateId && !forceName) {
      setNamePrompt('download')
      return
    }
    const nameToUse = forceName ?? templateName
    setIsDownloading(true)
    setSaveError(null)
    try {
      if (templateId) {
        await downloadTemplate(templateId, nameToUse)
      } else {
        const xslt = await generateXslt(blocks, documentType)
        const blob = new Blob([xslt], { type: 'application/xslt+xml' })
        const url = URL.createObjectURL(blob)
        const a = document.createElement('a')
        a.href = url
        a.download = `${nameToUse.replace(/[^a-zA-Z0-9_-]/g, '_') || 'template'}.xslt`
        document.body.appendChild(a)
        a.click()
        document.body.removeChild(a)
        URL.revokeObjectURL(url)
      }
      if (downloadSuccessTimer.current) clearTimeout(downloadSuccessTimer.current)
      setDownloadSuccess(true)
      downloadSuccessTimer.current = setTimeout(() => setDownloadSuccess(false), 3000)
      // İndirme sayacını tazele (Pro "x/3" rozeti güncel kalsın).
      useEntitlementStore.getState().refresh()
    } catch (err: unknown) {
      const gate = await parseGateError(err)
      if (gate.status === 402) {
        openUpgradeModal({
          title: 'İndirme Pro üyelik gerektirir',
          message: gate.message ?? 'Bu işlem XsltCraft Pro’ya dahildir.',
        })
      } else if (gate.status === 429) {
        toast.warning(gate.message ?? 'Günlük indirme limitiniz doldu.', { title: 'Günlük limit' })
        useEntitlementStore.getState().refresh()
      } else {
        setSaveError(gate.message ?? 'XSLT indirilemedi.')
        setTimeout(() => setSaveError(null), 4000)
      }
    } finally {
      setIsDownloading(false)
    }
  }, [isDownloading, templateId, blocks, templateName, documentType])

  // ── Template adı düzenleme ────────────────────────────────────────────────────
  function commitName() {
    const trimmed = nameInput.trim()
    if (trimmed) setTemplateName(trimmed)
    else setNameInput(templateName)
    setIsEditingName(false)
  }

  const handleNamePromptConfirm = useCallback((name: string) => {
    const action = namePrompt
    setTemplateName(name)
    setNamePrompt(null)
    if (action === 'save') save(name)
    else handleDownload(name)
  }, [namePrompt, setTemplateName, save, handleDownload])

  // ── Render ────────────────────────────────────────────────────────────────────
  if (isLoading) {
    return (
      <div role="status" className="flex h-screen items-center justify-center bg-ui-bg font-sans text-sm text-tx-muted">
        Şablon yükleniyor…
      </div>
    )
  }

  const hasBlocks = Object.keys(blocks).length > 0
  const panelVisible = leftOpen

  const nameEditor = isEditingName ? (
    <>
      <label htmlFor="design-template-name" className="sr-only">Şablon adı</label>
      <input
        id="design-template-name"
        autoFocus
        className="h-[30px] w-[220px] rounded-md border border-ui-btn-line bg-ui-search px-2 text-[13px] font-medium text-ui-bar-fg outline-none focus:border-ui-accent"
        value={nameInput}
        onChange={(e) => setNameInput(e.target.value)}
        onBlur={commitName}
        onKeyDown={(e) => {
          if (e.key === 'Enter') commitName()
          if (e.key === 'Escape') { setNameInput(templateName); setIsEditingName(false) }
        }}
      />
    </>
  ) : (
    <button
      type="button"
      onClick={() => { setNameInput(templateName); setIsEditingName(true) }}
      className="xc-focus group flex max-w-[240px] items-center gap-1.5 rounded px-1 text-[13px] font-medium text-ui-bar-fg hover:bg-ui-bar-line"
      title="Adı düzenle"
      aria-label={`Şablon adı: ${templateName} — düzenle`}
    >
      <span className="truncate">{templateName}</span>
      <Pencil size={12} aria-hidden="true" className="flex-none text-ui-bar-muted opacity-0 group-hover:opacity-100 group-focus-visible:opacity-100" />
    </button>
  )

  return (
    <div className="flex h-screen flex-col overflow-hidden bg-ui-bg font-sans text-[13px] text-tx">
      {/* Gizli XML dosya input'u — sayfada tek; sol paneldeki "XML yükle" tetikler */}
      <input ref={xmlInputRef} type="file" accept={UPLOAD_ACCEPT.xml} className="hidden" onChange={handleXmlUpload} />

      <WorkspaceHeader
        active="design"
        onNavigate={handleNavigate}
        confirmLeave={confirmLeave}
        leading={
          <HeaderButton
            variant="icon"
            icon={PanelLeft}
            onClick={() => setLeftOpen((v) => !v)}
            aria-label={panelVisible ? 'Sol paneli kapat' : 'Sol paneli aç'}
            aria-expanded={panelVisible}
            aria-controls="design-left-panel"
            title="Bloklar / XML ağacı panelini aç/kapat"
          />
        }
        center={
          <>
            {nameEditor}
            <HeaderChip>{DOCUMENT_TYPE_LABEL[documentType]}</HeaderChip>
            {isSaving ? (
              <HeaderStatus>Kaydediliyor…</HeaderStatus>
            ) : isDirty ? (
              <HeaderStatus>
                <span className="inline-flex items-center gap-1.5">
                  <span aria-hidden="true" className="inline-block h-1.5 w-1.5 rounded-full bg-fn-orange" />
                  Kaydedilmedi
                </span>
              </HeaderStatus>
            ) : null}
            {saveError && (
              <span role="alert" className="max-w-[260px] truncate rounded bg-fn-red-tint px-2 py-0.5 text-[11.5px] text-fn-red-ink" title={saveError}>
                {saveError}
              </span>
            )}
          </>
        }
        actions={
          <>
            <HeaderButton
              variant="icon"
              icon={Undo2}
              onClick={undo}
              disabled={past.length === 0}
              aria-label="Geri al"
              title="Geri al (Ctrl+Z)"
            />
            <HeaderButton
              variant="icon"
              icon={Redo2}
              onClick={redo}
              disabled={future.length === 0}
              aria-label="Yinele"
              title="Yinele (Ctrl+Y)"
            />
            <HeaderDivider />
            <HeaderButton
              icon={Eye}
              onClick={() => setShowPreview((v) => !v)}
              aria-pressed={showPreview}
              title="Canlı önizlemeyi aç/kapat"
              className={showPreview ? 'bg-ui-bar-line' : ''}
            >
              Önizle
            </HeaderButton>
            <HeaderButton icon={Save} onClick={() => save()} disabled={isSaving || !isDirty} title="Taslaklarıma kaydet">
              {isSaving ? 'Kaydediliyor…' : 'Kaydet'}
            </HeaderButton>
            <HeaderButton
              variant="primary"
              icon={Download}
              onClick={() => handleDownload()}
              disabled={isDownloading || !hasBlocks}
              title="XSLT dosyasını indir"
            >
              {isDownloading ? 'İndiriliyor…' : 'XSLT İndir'}
            </HeaderButton>
          </>
        }
      />

      {/* Editor layout */}
      <DndContext
        sensors={sensors}
        onDragStart={handleDragStart}
        onDragEnd={handleDragEnd}
      >
        <div className="flex min-h-0 flex-1 overflow-hidden">
          {panelVisible && (
            <div id="design-left-panel" className="flex">
              <EditorLeftPanel onXmlUploadClick={() => xmlInputRef.current?.click()} xmlError={xmlError} />
            </div>
          )}
          <Canvas />
          {showPreview && <EditorPreviewPanel />}
          <PropertyPanel />
        </div>

        {/* Suruklenirken gosterilen overlay (sadece paletten) */}
        <DragOverlay>
          {activePaletteItem && (
            <div className="flex cursor-grabbing items-center gap-2 rounded-md border border-ui-soft-line bg-ui-card px-3 py-1.5 text-[12px] font-medium text-ui-ink shadow-[var(--ui-shadow-pop)]">
              <activePaletteItem.icon size={15} strokeWidth={1.8} aria-hidden="true" className="text-ui-accent" />
              {activePaletteItem.label}
            </div>
          )}
        </DragOverlay>
      </DndContext>

      <DesignStatusBar isSaving={isSaving} />

      {/* Kayıt / indirme bildirimleri */}
      <div aria-live="polite" className="pointer-events-none fixed bottom-10 left-1/2 z-[99999] -translate-x-1/2">
        {saveSuccess && (
          <div className="flex items-center gap-2 whitespace-nowrap rounded-lg bg-fn-green-ink px-5 py-2.5 text-[13px] font-medium text-on-fn shadow-[var(--ui-shadow-pop)]">
            <Check size={15} strokeWidth={2.4} aria-hidden="true" /> Taslaklarım'a kaydedildi
          </div>
        )}
        {downloadSuccess && (
          <div className="flex items-center gap-2 whitespace-nowrap rounded-lg bg-ui-accent px-5 py-2.5 text-[13px] font-medium text-ui-accent-fg shadow-[var(--ui-shadow-pop)]">
            <Download size={15} strokeWidth={2.2} aria-hidden="true" /> Şablon indirme başlatıldı
          </div>
        )}
      </div>

      {/* İsim verme modalı — yeni şablon */}
      {namePrompt && (
        <NamePromptModal
          initial={templateName === 'Yeni Şablon' || templateName === 'Yeni İrsaliye Şablonu' ? '' : templateName}
          action={namePrompt}
          onConfirm={handleNamePromptConfirm}
          onClose={() => setNamePrompt(null)}
        />
      )}
    </div>
  )
}

// ── İsim verme modalı ─────────────────────────────────────────────────────────
function NamePromptModal({ initial, action, onConfirm, onClose }: {
  initial: string
  action: 'save' | 'download'
  onConfirm: (name: string) => void
  onClose: () => void
}) {
  const [name, setName] = React.useState(initial)
  const inputRef = React.useRef<HTMLInputElement>(null)

  React.useEffect(() => { inputRef.current?.focus() }, [])

  const confirm = () => {
    const trimmed = name.trim()
    if (!trimmed) return
    onConfirm(trimmed)
  }

  return (
    <div className="fixed inset-0 z-[99999] flex items-center justify-center bg-black/50">
      <div
        role="dialog"
        aria-modal="true"
        aria-labelledby="name-prompt-title"
        aria-describedby="name-prompt-desc"
        className="mx-4 w-full max-w-sm rounded-xl border border-ui-line bg-ui-card p-6 font-sans shadow-[var(--ui-shadow-pop)]"
      >
        <h3 id="name-prompt-title" className="mb-1 text-base font-semibold text-tx">Şablona isim ver</h3>
        <p id="name-prompt-desc" className="mb-4 text-sm text-tx-muted">
          {action === 'save' ? 'Kaydetmeden önce' : 'İndirmeden önce'} şablona bir isim belirleyin.
        </p>
        <label htmlFor="name-prompt-input" className="sr-only">Şablon adı</label>
        <input
          id="name-prompt-input"
          ref={inputRef}
          type="text"
          value={name}
          onChange={e => setName(e.target.value)}
          onKeyDown={e => { if (e.key === 'Enter') confirm(); if (e.key === 'Escape') onClose() }}
          placeholder="Şablon adı…"
          className="mb-5 w-full rounded-lg border border-field-line bg-ui-card px-3 py-2 text-sm text-tx outline-none placeholder:text-tx-subtle focus:border-ui-accent focus:ring-2 focus:ring-ui-soft"
        />
        <div className="flex justify-end gap-3">
          <button
            type="button"
            onClick={onClose}
            className="xc-focus rounded-lg border border-field-line px-4 py-2 text-sm text-tx-2 transition hover:bg-ui-hover"
          >
            İptal
          </button>
          <button
            type="button"
            onClick={confirm}
            disabled={!name.trim()}
            className="xc-focus rounded-lg bg-ui-accent px-4 py-2 text-sm font-semibold text-ui-accent-fg transition hover:brightness-110 disabled:opacity-40"
          >
            {action === 'save' ? 'Kaydet' : 'İndir'}
          </button>
        </div>
      </div>
    </div>
  )
}
