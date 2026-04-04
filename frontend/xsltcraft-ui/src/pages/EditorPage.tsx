import { useEffect, useRef, useState, useCallback } from 'react'
import { useParams, useNavigate, Link, useLocation } from 'react-router-dom'
import {
  LayoutDashboard,
  BookOpen,
  FilePlus,
  FolderOpen,
  Shield,
} from 'lucide-react'
import {
  DndContext,
  DragOverlay,
  PointerSensor,
  useSensor,
  useSensors,
  pointerWithin,
  closestCenter,
  type CollisionDetection,
  type DragStartEvent,
  type DragEndEvent,
  type DragOverEvent,
} from '@dnd-kit/core'
import { arrayMove } from '@dnd-kit/sortable'
import BlockPalette from '../components/editor/BlockPalette'
import Canvas from '../components/editor/Canvas'
import PropertyPanel from '../components/editor/PropertyPanel'
import EditorPreviewPanel from '../components/editor/EditorPreviewPanel'
import { useEditorStore } from '../store/editorStore'
import { useAuthStore } from '../store/authStore'
import { useXmlStore } from '../store/xmlStore'
import {
  getTemplate,
  createTemplate,
  updateTemplate,
  downloadTemplate,
} from '../services/templateService'
import { generateXslt } from '../services/previewService'
import type { BlockTree } from '../types/template'
import type { BlockType } from '../types/blocks'
import defaultInvoiceXml from '../assets/default-invoice.xml?raw'

const AUTOSAVE_DELAY_MS = 30_000

function EditorSidebar() {
  const { user } = useAuthStore()
  const location = useLocation()

  const isActive = (path: string) => location.pathname === path

  const itemBase =
    'flex items-center justify-center w-9 h-9 rounded-lg transition-colors'
  const itemActive = 'bg-blue-50 text-blue-700'
  const itemInactive = 'text-gray-500 hover:bg-gray-100 hover:text-gray-900'

  return (
    <div className="w-12 border-r border-gray-200 bg-white py-2 flex flex-col gap-1 items-center flex-shrink-0">
      <Link
        to="/dashboard"
        className={`${itemBase} ${isActive('/dashboard') ? itemActive : itemInactive}`}
        title="Dashboard"
      >
        <LayoutDashboard size={18} />
      </Link>
      <Link
        to="/templates"
        className={`${itemBase} ${isActive('/templates') ? itemActive : itemInactive}`}
        title="Tema Kütüphanesi"
      >
        <BookOpen size={18} />
      </Link>
      <Link
        to="/editor/new"
        className={`${itemBase} ${isActive('/editor/new') ? itemActive : itemInactive}`}
        title="Yeni Şablon Oluştur"
      >
        <FilePlus size={18} />
      </Link>
      <Link
        to="/drafts"
        className={`${itemBase} ${isActive('/drafts') ? itemActive : itemInactive}`}
        title="Taslaklarım"
      >
        <FolderOpen size={18} />
      </Link>
      {user?.role === 'Admin' && (
        <Link
          to="/admin/themes"
          className={`${itemBase} ${isActive('/admin/themes') ? itemActive : itemInactive}`}
          title="Admin Paneli"
        >
          <Shield size={18} />
        </Link>
      )}
    </div>
  )
}

export default function EditorPage() {
  const { templateId: routeTemplateId } = useParams<{ templateId?: string }>()
  const navigate = useNavigate()

  const templateId   = useEditorStore((s) => s.templateId)
  const templateName = useEditorStore((s) => s.templateName)
  const sections     = useEditorStore((s) => s.sections)
  const blocks       = useEditorStore((s) => s.blocks)
  const addBlock        = useEditorStore((s) => s.addBlock)
  const moveBlock       = useEditorStore((s) => s.moveBlock)
  const moveBlockToCol  = useEditorStore((s) => s.moveBlockToCol)
  const isDirty      = useEditorStore((s) => s.isDirty)
  const past         = useEditorStore((s) => s.past)
  const future       = useEditorStore((s) => s.future)
  const undo         = useEditorStore((s) => s.undo)
  const redo         = useEditorStore((s) => s.redo)
  const resetTree    = useEditorStore((s) => s.resetTree)
  const reorderSections = useEditorStore((s) => s.reorderSections)
  const loadTree        = useEditorStore((s) => s.loadTree)
  const setTemplateId      = useEditorStore((s) => s.setTemplateId)
  const setTemplateName    = useEditorStore((s) => s.setTemplateName)
  const setHasStoredXslt = useEditorStore((s) => s.setHasStoredXslt)

  // XML store
  const xmlFiles    = useXmlStore((s) => s.xmlFiles)
  const activeXmlId = useXmlStore((s) => s.activeXmlId)
  const addXmlFile  = useXmlStore((s) => s.addXmlFile)
  const setActiveXml = useXmlStore((s) => s.setActiveXml)

  const [isLoading, setIsLoading]       = useState(false)
  const [isSaving, setIsSaving]         = useState(false)
  const [isDownloading, setIsDownloading] = useState(false)
  const [saveError, setSaveError]       = useState<string | null>(null)
  const [isEditingName, setIsEditingName] = useState(false)
  const [nameInput, setNameInput]   = useState(templateName)
  const [showPreview, setShowPreview] = useState(false)
  const [xmlError, setXmlError] = useState<string | null>(null)

  const autosaveTimerRef = useRef<ReturnType<typeof setTimeout> | null>(null)
  const xmlInputRef = useRef<HTMLInputElement>(null)

  // ── DnD ──────────────────────────────────────────────────────────────────────
  const [activeId, setActiveDndId] = useState<string | null>(null)

  const sensors = useSensors(
    useSensor(PointerSensor, { activationConstraint: { distance: 5 } })
  )

  // Pointer gerçekten o alan üzerindeyse onu seç; değilse en yakın merkezi kullan.
  // closestCorners yerine bu kombinasyon, bölüm kaymasını önler.
  const collisionDetection: CollisionDetection = useCallback((args) => {
    const within = pointerWithin(args)
    if (within.length > 0) return within
    return closestCenter(args)
  }, [])

  function findSectionByBlockId(blockId: string): string | undefined {
    return sections.find((s) => s.blockIds.includes(blockId))?.id
  }

  function handleDragStart({ active }: DragStartEvent) {
    setActiveDndId(String(active.id))
  }

  function handleDragOver(_event: DragOverEvent) {
    // reserved for future highlight logic
  }

  function handleDragEnd({ active, over }: DragEndEvent) {
    setActiveDndId(null)
    if (!over) return

    const activeData = active.data.current as { source: string; blockType?: BlockType; blockId?: string; sectionId?: string }

    // Section reorder
    if (activeData?.source === 'section') {
      const fromIndex = sections.findIndex((s) => `sort-section-${s.id}` === String(active.id))
      const toIndex = sections.findIndex((s) => `sort-section-${s.id}` === String(over.id))
      if (fromIndex !== -1 && toIndex !== -1 && fromIndex !== toIndex) {
        reorderSections(fromIndex, toIndex)
      }
      return
    }
    const overIdStr = String(over.id)
    const overData = over.data.current as { type?: string; sectionId?: string; colIdx?: number } | undefined

    // Sütun drop zone'u (colzone-{sectionId}-{ci})
    if (overData?.type === 'colzone') {
      const toSectionId = overData.sectionId!
      const colIdx = overData.colIdx!

      if (activeData?.source === 'palette') {
        addBlock(toSectionId, activeData.blockType!, undefined, colIdx)
        return
      }

      if (activeData?.source === 'canvas') {
        const blockId = activeData.blockId!
        const fromSectionId = activeData.sectionId ?? findSectionByBlockId(blockId)
        if (!fromSectionId) return
        if (fromSectionId !== toSectionId) {
          const toSection = sections.find((s) => s.id === toSectionId)!
          moveBlock(fromSectionId, toSectionId, blockId, toSection.blockIds.length)
        }
        moveBlockToCol(blockId, colIdx)
        return
      }
      return
    }

    // Palette → Canvas
    if (activeData?.source === 'palette') {
      const blockType = activeData.blockType!
      if (overIdStr.startsWith('section-')) {
        addBlock(overIdStr.replace('section-', ''), blockType)
        return
      }
      const targetSectionId = findSectionByBlockId(overIdStr)
      if (targetSectionId) {
        const section = sections.find((s) => s.id === targetSectionId)!
        addBlock(targetSectionId, blockType, section.blockIds.indexOf(overIdStr))
      }
      return
    }

    // Canvas → Canvas
    if (activeData?.source === 'canvas') {
      const blockId = activeData.blockId!
      const fromSectionId = activeData.sectionId ?? findSectionByBlockId(blockId)
      if (!fromSectionId) return

      let toSectionId: string | undefined
      let toIndex: number

      if (overIdStr.startsWith('section-')) {
        toSectionId = overIdStr.replace('section-', '')
        toIndex = sections.find((s) => s.id === toSectionId)!.blockIds.length
      } else {
        toSectionId = findSectionByBlockId(overIdStr)
        if (!toSectionId) return
        const toSection = sections.find((s) => s.id === toSectionId)!
        toIndex = toSection.blockIds.indexOf(overIdStr)

        if (fromSectionId === toSectionId) {
          const fromSection = sections.find((s) => s.id === fromSectionId)!
          const oldIndex = fromSection.blockIds.indexOf(blockId)
          if (oldIndex !== toIndex) {
            const newIds = arrayMove(fromSection.blockIds, oldIndex, toIndex)
            newIds.forEach((id, idx) => {
              if (id === blockId) moveBlock(fromSectionId, toSectionId!, blockId, idx)
            })
          }
          return
        }
      }

      if (toSectionId) moveBlock(fromSectionId, toSectionId, blockId, toIndex)
    }
  }

  const activePaletteType = activeId?.startsWith('palette-')
    ? (activeId.replace('palette-', '') as BlockType)
    : null
  const activeCanvasBlock = activeId && !activeId.startsWith('palette-') ? blocks[activeId] : null

  // ── XML yükleme ──────────────────────────────────────────────────────────────

  function handleXmlUpload(e: React.ChangeEvent<HTMLInputElement>) {
    const file = e.target.files?.[0]
    if (!file) return
    if (file.size > 1 * 1024 * 1024) {
      alert('XML dosyası 1 MB\'dan büyük olamaz.')
      e.target.value = ''
      return
    }
    const reader = new FileReader()
    reader.onload = (ev) => {
      const content = ev.target?.result as string
      const doc = new DOMParser().parseFromString(content, 'application/xml')
      const parseError = doc.getElementsByTagName('parsererror')
      if (parseError.length > 0) {
        setXmlError(`"${file.name}" geçerli bir XML dosyası değil.`)
        setTimeout(() => setXmlError(null), 4000)
        return
      }
      setXmlError(null)
      addXmlFile(file.name, content)
    }
    reader.readAsText(file)
    // Reset input so the same file can be re-uploaded
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
            const tree: BlockTree = JSON.parse(tpl.blockTree)
            loadTree(tree)
          } catch {
            loadTree({ sections: [], blocks: {} })
          }
        } else {
          loadTree({ sections: [], blocks: {} })
        }
      })
      .catch(() => setSaveError('Template yüklenemedi.'))
      .finally(() => setIsLoading(false))
  }, [routeTemplateId]) // eslint-disable-line react-hooks/exhaustive-deps

  // ── Kaydetme ─────────────────────────────────────────────────────────────────

  const serializeTree = useCallback((): string => {
    return JSON.stringify({ sections, blocks } satisfies BlockTree)
  }, [sections, blocks])

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
        await updateTemplate(templateId, {
          name: templateName,
          blockTree: blockTreeJson,
        })
      }

      useEditorStore.setState({ isDirty: false })
    } catch {
      setSaveError('Kayıt başarısız. Tekrar deneyin.')
    } finally {
      setIsSaving(false)
    }
  }, [isSaving, templateId, templateName, serializeTree, navigate]) // eslint-disable-line react-hooks/exhaustive-deps

  // ── Otomatik kaydetme (30 sn debounce) ───────────────────────────────────────

  useEffect(() => {
    if (!isDirty) return
    if (autosaveTimerRef.current) clearTimeout(autosaveTimerRef.current)
    autosaveTimerRef.current = setTimeout(() => { save() }, AUTOSAVE_DELAY_MS)
    return () => { if (autosaveTimerRef.current) clearTimeout(autosaveTimerRef.current) }
  }, [isDirty, sections, blocks, templateName]) // eslint-disable-line react-hooks/exhaustive-deps

  // ── XSLT indirme ─────────────────────────────────────────────────────────

  const handleDownload = useCallback(async () => {
    if (isDownloading || sections.length === 0) return
    setIsDownloading(true)
    setSaveError(null)
    try {
      if (templateId) {
        // Kayıtlı template: backend endpoint (üretir + storage'a yazar + cache)
        await downloadTemplate(templateId, templateName)
      } else {
        // Kaydedilmemiş template: anlık istemci-taraflı üretim
        const xslt = await generateXslt(sections, blocks)
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
  }, [isDownloading, templateId, sections, blocks, templateName])

  // ── Template adı düzenleme ───────────────────────────────────────────────────

  function commitName() {
    const trimmed = nameInput.trim()
    if (trimmed) setTemplateName(trimmed)
    else setNameInput(templateName)
    setIsEditingName(false)
  }

  // ── Render ───────────────────────────────────────────────────────────────────

  if (isLoading) {
    return (
      <div className="h-screen flex items-center justify-center bg-gray-50 text-gray-500 text-sm">
        Şablon yükleniyor…
      </div>
    )
  }

  return (
    <div className="h-screen flex flex-col bg-gray-100">
      {/* Hidden XML file input */}
      <input
        ref={xmlInputRef}
        type="file"
        accept=".xml"
        className="hidden"
        onChange={handleXmlUpload}
      />

      {/* Topbar */}
      <header className="flex items-center gap-0 px-4 bg-white border-b flex-shrink-0" style={{ height: 48, borderColor: 'var(--color-border-default)' }}>
        {/* Brand */}
        <span style={{ fontSize: 13, fontWeight: 600, color: 'var(--color-brand-primary)', marginRight: 6, flexShrink: 0, userSelect: 'none' }}>
          XsltCraft
        </span>

        {/* Separator */}
        <span style={{ color: 'var(--color-border-default)', margin: '0 8px', flexShrink: 0 }}>—</span>

        {/* Geri */}
        <button
          onClick={() => navigate('/dashboard')}
          style={{ fontSize: 12, color: 'var(--color-text-secondary)', cursor: 'pointer', background: 'none', border: 'none', padding: 0, flexShrink: 0 }}
          onMouseEnter={e => (e.currentTarget.style.color = 'var(--color-brand-primary)')}
          onMouseLeave={e => (e.currentTarget.style.color = 'var(--color-text-secondary)')}
        >
          ‹ Geri
        </button>

        {/* Separator */}
        <span style={{ color: 'var(--color-border-default)', margin: '0 8px', flexShrink: 0 }}>›</span>

        {/* Template adı — inline edit */}
        <div className="flex items-center gap-1 flex-1 min-w-0">
          {isEditingName ? (
            <input
              autoFocus
              style={{ fontSize: 13, fontWeight: 500, border: '0.5px solid var(--color-brand-primary)', borderRadius: 5, padding: '2px 8px', outline: 'none', width: 220, color: 'var(--color-text-primary)', background: 'var(--color-surface-card)' }}
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
              style={{ fontSize: 13, fontWeight: 500, color: 'var(--color-text-primary)', background: 'none', border: 'none', cursor: 'pointer', padding: 0, maxWidth: 240, overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }}
              title="Adı düzenle"
            >
              {templateName}
            </button>
          )}
          {isDirty && !isSaving && (
            <span className="flex items-center gap-1 flex-shrink-0" style={{ fontSize: 11, color: 'var(--color-text-muted)', marginLeft: 6 }}>
              <span style={{ display: 'inline-block', width: 6, height: 6, borderRadius: '50%', background: 'var(--color-danger)', flexShrink: 0 }} />
              Kaydedilmedi
            </span>
          )}
          {isSaving && (
            <span style={{ fontSize: 11, color: 'var(--color-text-muted)', flexShrink: 0, marginLeft: 6 }}>Kaydediliyor…</span>
          )}
        </div>

        {saveError && (
          <span style={{ fontSize: 11, color: 'var(--color-danger)', flexShrink: 0 }}>{saveError}</span>
        )}
        {xmlError && (
          <span style={{ fontSize: 11, color: 'var(--color-danger)', flexShrink: 0 }}>{xmlError}</span>
        )}

        {/* Group 1: +XML | Undo | Redo */}
        <div className="flex items-center flex-shrink-0" style={{ gap: 2 }}>
          <button
            onClick={() => xmlInputRef.current?.click()}
            className="topbar-ghost-btn"
            title="XML dosyası yükle"
          >
            + XML
          </button>

          {xmlFiles.length > 1 && (
            <select
              value={activeXmlId ?? ''}
              onChange={(e) => setActiveXml(e.target.value)}
              className="topbar-ghost-btn"
              style={{ paddingRight: 4 }}
              title="Aktif XML seç"
            >
              {xmlFiles.map((f) => (
                <option key={f.id} value={f.id}>{f.name}</option>
              ))}
            </select>
          )}
          {xmlFiles.length === 1 && (
            <span style={{ fontSize: 11, color: 'var(--color-text-muted)', maxWidth: 100, overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }} title={xmlFiles[0].name}>
              {xmlFiles[0].name}
            </span>
          )}

          <button
            onClick={undo}
            disabled={past.length === 0}
            className="topbar-ghost-btn"
            title="Geri Al (Ctrl+Z)"
          >
            ↺
          </button>
          <button
            onClick={redo}
            disabled={future.length === 0}
            className="topbar-ghost-btn"
            title="Yinele (Ctrl+Y)"
          >
            ↻
          </button>
        </div>

        {/* Divider */}
        <div style={{ width: '0.5px', height: 20, background: 'var(--color-border-default)', margin: '0 2px', flexShrink: 0 }} />

        {/* Group 2: Önizle | İndir */}
        <div className="flex items-center flex-shrink-0" style={{ gap: 2 }}>
          <button
            onClick={() => setShowPreview((v) => !v)}
            className="topbar-ghost-btn"
            style={showPreview ? { background: 'var(--color-brand-light)', color: 'var(--color-brand-primary)', borderColor: 'var(--color-brand-border)' } : {}}
            title="Canlı önizlemeyi aç/kapat"
          >
            ⊙ Önizle
          </button>
          <button
            onClick={handleDownload}
            disabled={isDownloading || sections.length === 0}
            className="topbar-ghost-btn"
            title="XSLT dosyasını indir"
          >
            {isDownloading ? '…' : '↓ İndir'}
          </button>
        </div>

        {/* Divider */}
        <div style={{ width: '0.5px', height: 20, background: 'var(--color-border-default)', margin: '0 2px', flexShrink: 0 }} />

        {/* Group 3: Kaydet */}
        <button
          onClick={save}
          disabled={isSaving || !isDirty}
          className="flex-shrink-0"
          style={{ fontSize: 11, fontWeight: 500, padding: '0 12px', height: 30, borderRadius: 5, border: 'none', cursor: isSaving || !isDirty ? 'not-allowed' : 'pointer', background: 'var(--color-brand-primary)', color: '#fff', opacity: isSaving || !isDirty ? 0.5 : 1, transition: 'background 150ms' }}
          onMouseEnter={e => { if (!isSaving && isDirty) e.currentTarget.style.background = 'var(--color-brand-hover)' }}
          onMouseLeave={e => { e.currentTarget.style.background = 'var(--color-brand-primary)' }}
        >
          {isSaving ? 'Kaydediliyor…' : 'Kaydet'}
        </button>
      </header>

      {/* Editor layout */}
      <DndContext
        sensors={sensors}
        collisionDetection={collisionDetection}
        onDragStart={handleDragStart}
        onDragOver={handleDragOver}
        onDragEnd={handleDragEnd}
      >
        <div className="flex flex-1 overflow-hidden">
          <EditorSidebar />
          <BlockPalette />
          <Canvas />
          {showPreview && <EditorPreviewPanel />}
          <PropertyPanel />
        </div>

        <DragOverlay>
          {activePaletteType && (
            <div className="px-3 py-2 rounded-md border border-blue-400 bg-white shadow-xl text-sm text-gray-700 opacity-90">
              {activePaletteType}
            </div>
          )}
          {activeCanvasBlock && (
            <div className="px-3 py-2 rounded-md border border-blue-400 bg-white shadow-xl text-sm text-gray-700 opacity-90">
              {activeCanvasBlock.type}
            </div>
          )}
        </DragOverlay>
      </DndContext>
    </div>
  )
}
