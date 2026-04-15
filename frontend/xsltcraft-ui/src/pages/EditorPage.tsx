import React, { useEffect, useRef, useState, useCallback } from 'react'
import { useParams, useNavigate } from 'react-router-dom'
import {
  DndContext,
  DragOverlay,
  PointerSensor,
  useSensor,
  useSensors,
  type DragStartEvent,
  type DragEndEvent,
} from '@dnd-kit/core'
import BlockPalette from '../components/editor/BlockPalette'
import Canvas, { CANVAS_DROPPABLE_ID, canvasPageRef, canvasScaleRef } from '../components/editor/Canvas'
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
import type { BlockTree, BlockTreeV1 } from '../types/template'
import type { BlockType } from '../types/blocks'
import { migrateV1toV2 } from '../utils/treeMigration'
import { snapToGrid, clampToPage, pxToMm } from '../utils/gridSnap'
import defaultInvoiceXml from '../assets/default-invoice.xml?raw'

const AUTOSAVE_DELAY_MS = 30_000

export default function EditorPage() {
  const { templateId: routeTemplateId } = useParams<{ templateId?: string }>()
  const navigate = useNavigate()

  const templateId      = useEditorStore((s) => s.templateId)
  const templateName    = useEditorStore((s) => s.templateName)
  const blocks          = useEditorStore((s) => s.blocks)
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
  const xmlFiles    = useXmlStore((s) => s.xmlFiles)
  const activeXmlId = useXmlStore((s) => s.activeXmlId)
  const addXmlFile  = useXmlStore((s) => s.addXmlFile)
  const setActiveXml = useXmlStore((s) => s.setActiveXml)

  const [isLoading, setIsLoading]         = useState(false)
  const [isSaving, setIsSaving]           = useState(false)
  const [isDownloading, setIsDownloading] = useState(false)
  const [saveError, setSaveError]         = useState<string | null>(null)
  const [isEditingName, setIsEditingName] = useState(false)
  const [nameInput, setNameInput]         = useState(templateName)
  const [showPreview, setShowPreview]     = useState(false)
  const [xmlError, setXmlError]           = useState<string | null>(null)
  const [paletteOpen, setPaletteOpen]     = useState(() => window.innerWidth >= 900)

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

  const handleNavigate = useCallback((path: string) => {
    if (isDirty && !window.confirm('Kaydedilmemiş değişiklikler var. Sayfadan ayrılmak istiyor musun?')) return
    navigate(path)
  }, [isDirty, navigate])

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

  const activePaletteType = activeId?.startsWith('palette-')
    ? (activeId.replace('palette-', '') as BlockType)
    : null

  // ── XML yükleme ──────────────────────────────────────────────────────────────
  function handleXmlUpload(e: React.ChangeEvent<HTMLInputElement>) {
    const file = e.target.files?.[0]
    if (!file) return
    if (file.size > 1 * 1024 * 1024) {
      alert("XML dosyası 1 MB'dan büyük olamaz.")
      e.target.value = ''
      return
    }
    const reader = new FileReader()
    reader.onload = (ev) => {
      const content = ev.target?.result as string
      const doc = new DOMParser().parseFromString(content, 'application/xml')
      if (doc.getElementsByTagName('parsererror').length > 0) {
        setXmlError(`"${file.name}" geçerli bir XML dosyası değil.`)
        setTimeout(() => setXmlError(null), 4000)
        return
      }
      setXmlError(null)
      addXmlFile(file.name, content)
    }
    reader.readAsText(file)
    e.target.value = ''
  }

  // ── Template yükleme ─────────────────────────────────────────────────────────
  useEffect(() => {
    if (!routeTemplateId) {
      resetTree()
      useXmlStore.setState({ xmlFiles: [], activeXmlId: null })
      addXmlFile('varsayilan-fatura.xml', defaultInvoiceXml)
      return
    }

    setIsLoading(true)
    getTemplate(routeTemplateId)
      .then((tpl) => {
        setTemplateId(tpl.id)
        setTemplateName(tpl.name)
        setNameInput(tpl.name)
        setHasStoredXslt(tpl.hasStoredXslt)

        if (tpl.blockTree) {
          try {
            const raw: BlockTree = JSON.parse(tpl.blockTree)

            // V1 mi V2 mi?
            if (!('version' in raw) || (raw as { version?: number }).version !== 2) {
              // V1 → migrate
              const v2 = migrateV1toV2(raw as BlockTreeV1)
              loadTree(v2)
            } else {
              loadTree(raw as import('../types/template').BlockTreeV2)
            }
          } catch {
            loadTree({ version: 2, blocks: {} })
          }
        } else {
          loadTree({ version: 2, blocks: {} })
        }
      })
      .catch(() => setSaveError('Template yüklenemedi.'))
      .finally(() => setIsLoading(false))
  }, [routeTemplateId]) // eslint-disable-line react-hooks/exhaustive-deps

  // ── Kaydetme ─────────────────────────────────────────────────────────────────
  const serializeTree = useCallback((): string => {
    return JSON.stringify({ version: 2, blocks })
  }, [blocks])

  const save = useCallback(async () => {
    if (isSaving) return
    setSaveError(null)
    setIsSaving(true)
    try {
      const blockTreeJson = serializeTree()
      if (!templateId) {
        const created = await createTemplate({
          name: templateName,
          documentType: 'Invoice',
          blockTree: blockTreeJson,
        })
        setTemplateId(created.id)
        navigate(`/editor/${created.id}`, { replace: true })
      } else {
        await updateTemplate(templateId, { name: templateName, blockTree: blockTreeJson })
      }
      useEditorStore.setState({ isDirty: false })
    } catch {
      setSaveError('Kayıt başarısız. Tekrar deneyin.')
    } finally {
      setIsSaving(false)
    }
  }, [isSaving, templateId, templateName, serializeTree, navigate]) // eslint-disable-line react-hooks/exhaustive-deps

  // ── Otomatik kaydetme ─────────────────────────────────────────────────────────
  useEffect(() => {
    if (!isDirty) return
    if (autosaveTimerRef.current) clearTimeout(autosaveTimerRef.current)
    autosaveTimerRef.current = setTimeout(() => { save() }, AUTOSAVE_DELAY_MS)
    return () => { if (autosaveTimerRef.current) clearTimeout(autosaveTimerRef.current) }
  }, [isDirty, blocks, templateName]) // eslint-disable-line react-hooks/exhaustive-deps

  // ── XSLT indirme ─────────────────────────────────────────────────────────────
  const handleDownload = useCallback(async () => {
    const hasBlocks = Object.keys(blocks).length > 0
    if (isDownloading || !hasBlocks) return
    setIsDownloading(true)
    setSaveError(null)
    try {
      if (templateId) {
        await downloadTemplate(templateId, templateName)
      } else {
        const xslt = await generateXslt(blocks)
        const blob = new Blob([xslt], { type: 'application/xslt+xml' })
        const url = URL.createObjectURL(blob)
        const a = document.createElement('a')
        a.href = url
        a.download = `${templateName.replace(/[^a-zA-Z0-9_-]/g, '_') || 'template'}.xslt`
        document.body.appendChild(a)
        a.click()
        document.body.removeChild(a)
        URL.revokeObjectURL(url)
      }
    } catch {
      setSaveError('XSLT indirilemedi.')
      setTimeout(() => setSaveError(null), 4000)
    } finally {
      setIsDownloading(false)
    }
  }, [isDownloading, templateId, blocks, templateName])

  // ── Template adı düzenleme ────────────────────────────────────────────────────
  function commitName() {
    const trimmed = nameInput.trim()
    if (trimmed) setTemplateName(trimmed)
    else setNameInput(templateName)
    setIsEditingName(false)
  }

  // ── Render ────────────────────────────────────────────────────────────────────
  if (isLoading) {
    return (
      <div className="h-screen flex items-center justify-center bg-gray-50 text-gray-500 text-sm">
        Şablon yükleniyor…
      </div>
    )
  }

  const hasBlocks = Object.keys(blocks).length > 0

  const GHOST_BTN: React.CSSProperties = {
    padding: '6px 12px', borderRadius: 6, fontSize: 12,
    border: '1px solid #D3D1C7', background: '#fff', cursor: 'pointer',
    color: '#5F5E5A', transition: 'all 150ms', whiteSpace: 'nowrap',
    display: 'inline-flex', alignItems: 'center', height: 32,
  }
  const PRIMARY_BTN: React.CSSProperties = {
    padding: '6px 16px', borderRadius: 6, fontSize: 12,
    border: '1px solid #185FA5', background: '#185FA5',
    color: '#fff', cursor: isSaving || !isDirty ? 'not-allowed' : 'pointer',
    opacity: isSaving || !isDirty ? 0.45 : 1, whiteSpace: 'nowrap',
    display: 'inline-flex', alignItems: 'center', height: 32, flexShrink: 0,
  }

  return (
    <div style={{ height: '100vh', display: 'flex', flexDirection: 'column', background: '#F1F0EC' }}>
      {/* Gizli XML dosya input'u */}
      <input ref={xmlInputRef} type="file" accept=".xml" className="hidden" onChange={handleXmlUpload} />

      {/* Topbar */}
      <header style={{ height: 48, background: '#fff', borderBottom: '1px solid #E0DDD8', display: 'flex', alignItems: 'center', padding: '0 16px', gap: 12, flexShrink: 0 }}>
        {/* Logo */}
        <span style={{ fontWeight: 700, color: '#185FA5', fontSize: 14, flexShrink: 0 }}>XsltCraft</span>
        <div style={{ width: 1, height: 24, background: '#E0DDD8', flexShrink: 0 }} />

        {/* Palette toggle */}
        <button onClick={() => setPaletteOpen(v => !v)} style={GHOST_BTN} title="Blok listesini aç/kapat">
          ☰
        </button>
        <div style={{ width: 1, height: 24, background: '#E0DDD8', flexShrink: 0 }} />

        {/* Geri */}
        <button onClick={() => handleNavigate('/dashboard')} style={GHOST_BTN}>
          ‹ Geri
        </button>
        <div style={{ width: 1, height: 24, background: '#E0DDD8', flexShrink: 0 }} />

        {/* Template adı */}
        <div style={{ display: 'flex', alignItems: 'center', gap: 6, flex: 1, minWidth: 0 }}>
          {isEditingName ? (
            <input
              autoFocus
              style={{ fontSize: 13, fontWeight: 500, border: '1px solid #185FA5', borderRadius: 5, padding: '2px 8px', outline: 'none', width: 220, color: '#2C2C2A', background: '#fff', height: 28, boxSizing: 'border-box' }}
              value={nameInput}
              onChange={(e) => setNameInput(e.target.value)}
              onBlur={commitName}
              onKeyDown={(e) => {
                if (e.key === 'Enter') commitName()
                if (e.key === 'Escape') { setNameInput(templateName); setIsEditingName(false) }
              }}
            />
          ) : (
            <button
              onClick={() => { setNameInput(templateName); setIsEditingName(true) }}
              style={{ fontSize: 13, fontWeight: 500, color: '#2C2C2A', background: 'none', border: 'none', cursor: 'pointer', padding: 0, maxWidth: 240, overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }}
              title="Adı düzenle"
            >
              {templateName}
            </button>
          )}
          {isDirty && !isSaving && (
            <span style={{ display: 'flex', alignItems: 'center', gap: 4, flexShrink: 0, fontSize: 11, color: '#888780' }}>
              <span style={{ display: 'inline-block', width: 6, height: 6, borderRadius: '50%', background: '#E53E3E', flexShrink: 0 }} />
              Kaydedilmedi
            </span>
          )}
          {isSaving && (
            <span style={{ fontSize: 11, color: '#888780', flexShrink: 0 }}>Kaydediliyor…</span>
          )}
        </div>

        {saveError && <span style={{ fontSize: 11, color: '#E53E3E', flexShrink: 0 }}>{saveError}</span>}
        {xmlError  && <span style={{ fontSize: 11, color: '#E53E3E', flexShrink: 0 }}>{xmlError}</span>}

        {/* XML upload */}
        <div style={{ display: 'flex', alignItems: 'center', gap: 6, flexShrink: 0 }}>
          <button onClick={() => xmlInputRef.current?.click()} style={GHOST_BTN} title="XML dosyası yükle">
            + XML
          </button>
          {xmlFiles.length > 1 && (
            <select value={activeXmlId ?? ''} onChange={(e) => setActiveXml(e.target.value)} style={{ ...GHOST_BTN, paddingRight: 4 }}>
              {xmlFiles.map((f) => (<option key={f.id} value={f.id}>{f.name}</option>))}
            </select>
          )}
          {xmlFiles.length === 1 && (
            <span style={{ fontSize: 11, color: '#888780', maxWidth: 100, overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }} title={xmlFiles[0].name}>
              {xmlFiles[0].name}
            </span>
          )}
        </div>
        <div style={{ width: 1, height: 24, background: '#E0DDD8', flexShrink: 0 }} />

        {/* Undo/Redo */}
        <div style={{ display: 'flex', gap: 4, flexShrink: 0 }}>
          <button onClick={undo} disabled={past.length === 0} style={{ ...GHOST_BTN, opacity: past.length === 0 ? 0.4 : 1, cursor: past.length === 0 ? 'not-allowed' : 'pointer' }} title="Geri Al (Ctrl+Z)">Geri Al</button>
          <button onClick={redo} disabled={future.length === 0} style={{ ...GHOST_BTN, opacity: future.length === 0 ? 0.4 : 1, cursor: future.length === 0 ? 'not-allowed' : 'pointer' }} title="Yinele (Ctrl+Y)">Yinele</button>
        </div>
        <div style={{ width: 1, height: 24, background: '#E0DDD8', flexShrink: 0 }} />

        {/* Preview + Download */}
        <div style={{ display: 'flex', gap: 4, flexShrink: 0 }}>
          <button
            onClick={() => setShowPreview(v => !v)}
            style={{ ...GHOST_BTN, ...(showPreview ? { borderColor: '#185FA5', color: '#185FA5' } : {}) }}
            title="Canlı önizlemeyi aç/kapat"
          >
            Önizle
          </button>
          <button
            onClick={handleDownload}
            disabled={isDownloading || !hasBlocks}
            style={{ ...GHOST_BTN, opacity: isDownloading || !hasBlocks ? 0.4 : 1, cursor: isDownloading || !hasBlocks ? 'not-allowed' : 'pointer' }}
            title="XSLT dosyasını indir"
          >
            {isDownloading ? '…' : '↓ İndir'}
          </button>
        </div>
        <div style={{ width: 1, height: 24, background: '#E0DDD8', flexShrink: 0 }} />

        {/* Save */}
        <button onClick={save} disabled={isSaving || !isDirty} style={PRIMARY_BTN}>
          {isSaving ? 'Kaydediliyor…' : 'Kaydet'}
        </button>
      </header>

      {/* Editor layout */}
      <DndContext
        sensors={sensors}
        onDragStart={handleDragStart}
        onDragEnd={handleDragEnd}
      >
        <div style={{ flex: 1, display: 'flex', overflow: 'hidden' }}>
          <BlockPalette isOpen={paletteOpen} />
          <Canvas />
          {showPreview && <EditorPreviewPanel />}
          <PropertyPanel />
        </div>

        {/* Suruklenirken gosterilen overlay (sadece paletten) */}
        <DragOverlay>
          {activePaletteType && (
            <div
              style={{
                padding: '6px 12px',
                borderRadius: 6,
                border: '1.5px solid #B5D4F4',
                background: 'rgba(235,243,252,0.9)',
                boxShadow: '0 4px 16px rgba(0,0,0,0.15)',
                fontSize: 11,
                fontWeight: 500,
                color: '#185FA5',
                cursor: 'grabbing',
              }}
            >
              {activePaletteType}
            </div>
          )}
        </DragOverlay>
      </DndContext>
    </div>
  )
}
