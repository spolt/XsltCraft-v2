import { useEffect, useRef, useState, useCallback } from 'react'
import { useParams, useNavigate } from 'react-router-dom'
import BlockPalette from '../components/editor/BlockPalette'
import Canvas from '../components/editor/Canvas'
import PropertyPanel from '../components/editor/PropertyPanel'
import { useEditorStore } from '../store/editorStore'
import {
  getTemplate,
  createTemplate,
  updateTemplate,
} from '../services/templateService'
import type { BlockTree } from '../types/template'

const AUTOSAVE_DELAY_MS = 30_000

export default function EditorPage() {
  const { templateId: routeTemplateId } = useParams<{ templateId?: string }>()
  const navigate = useNavigate()

  const templateId   = useEditorStore((s) => s.templateId)
  const templateName = useEditorStore((s) => s.templateName)
  const sections     = useEditorStore((s) => s.sections)
  const blocks       = useEditorStore((s) => s.blocks)
  const isDirty      = useEditorStore((s) => s.isDirty)
  const past         = useEditorStore((s) => s.past)
  const future       = useEditorStore((s) => s.future)
  const undo         = useEditorStore((s) => s.undo)
  const redo         = useEditorStore((s) => s.redo)
  const resetTree    = useEditorStore((s) => s.resetTree)
  const loadTree     = useEditorStore((s) => s.loadTree)
  const setTemplateId   = useEditorStore((s) => s.setTemplateId)
  const setTemplateName = useEditorStore((s) => s.setTemplateName)

  const [isLoading, setIsLoading]   = useState(false)
  const [isSaving, setIsSaving]     = useState(false)
  const [saveError, setSaveError]   = useState<string | null>(null)
  const [isEditingName, setIsEditingName] = useState(false)
  const [nameInput, setNameInput]   = useState(templateName)

  const autosaveTimerRef = useRef<ReturnType<typeof setTimeout> | null>(null)

  // ── Yükleme ──────────────────────────────────────────────────────────────

  useEffect(() => {
    if (!routeTemplateId) {
      resetTree()
      return
    }

    setIsLoading(true)
    getTemplate(routeTemplateId)
      .then((tpl) => {
        setTemplateId(tpl.id)
        setTemplateName(tpl.name)
        setNameInput(tpl.name)

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

  // ── Kaydetme ─────────────────────────────────────────────────────────────

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
        // Yeni template oluştur
        const created = await createTemplate({
          name: templateName,
          documentType: 'Invoice',
          blockTree: blockTreeJson,
        })
        setTemplateId(created.id)
        // URL'yi güncelle — tarayıcı geçmişine eklemeden
        navigate(`/editor/${created.id}`, { replace: true })
      } else {
        // Mevcut template'i güncelle
        await updateTemplate(templateId, {
          name: templateName,
          blockTree: blockTreeJson,
        })
      }

      // isDirty sıfırla
      useEditorStore.setState({ isDirty: false })
    } catch {
      setSaveError('Kayıt başarısız. Tekrar deneyin.')
    } finally {
      setIsSaving(false)
    }
  }, [isSaving, templateId, templateName, serializeTree, navigate]) // eslint-disable-line react-hooks/exhaustive-deps

  // ── Otomatik kaydetme (30 sn debounce) ───────────────────────────────────

  useEffect(() => {
    if (!isDirty) return

    if (autosaveTimerRef.current) clearTimeout(autosaveTimerRef.current)
    autosaveTimerRef.current = setTimeout(() => {
      save()
    }, AUTOSAVE_DELAY_MS)

    return () => {
      if (autosaveTimerRef.current) clearTimeout(autosaveTimerRef.current)
    }
  }, [isDirty, sections, blocks, templateName]) // eslint-disable-line react-hooks/exhaustive-deps

  // ── Template adı düzenleme ───────────────────────────────────────────────

  function commitName() {
    const trimmed = nameInput.trim()
    if (trimmed) setTemplateName(trimmed)
    else setNameInput(templateName)
    setIsEditingName(false)
  }

  // ── Render ───────────────────────────────────────────────────────────────

  if (isLoading) {
    return (
      <div className="h-screen flex items-center justify-center bg-gray-50 text-gray-500 text-sm">
        Şablon yükleniyor…
      </div>
    )
  }

  return (
    <div className="h-screen flex flex-col bg-gray-100">
      {/* Toolbar */}
      <header className="h-12 flex items-center gap-3 px-4 bg-white border-b border-gray-200 flex-shrink-0">
        <button
          onClick={() => navigate('/dashboard')}
          className="text-sm text-gray-500 hover:text-gray-700 flex-shrink-0"
        >
          ← Geri
        </button>

        {/* Template adı — inline edit */}
        <div className="flex items-center gap-1 flex-1 min-w-0">
          {isEditingName ? (
            <input
              autoFocus
              className="text-sm font-medium border border-blue-300 rounded px-2 py-0.5 outline-none w-64 focus:ring-1 focus:ring-blue-100"
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
              className="text-sm font-medium text-gray-700 hover:text-blue-600 truncate max-w-xs text-left"
              title="Adı düzenle"
            >
              {templateName}
            </button>
          )}
          {isDirty && !isSaving && (
            <span className="text-xs text-amber-500 flex-shrink-0">● Kaydedilmedi</span>
          )}
          {isSaving && (
            <span className="text-xs text-gray-400 flex-shrink-0">Kaydediliyor…</span>
          )}
        </div>

        {saveError && (
          <span className="text-xs text-red-500 flex-shrink-0">{saveError}</span>
        )}

        {/* Undo / Redo */}
        <button
          onClick={undo}
          disabled={past.length === 0}
          className="px-2 py-1 text-xs rounded border border-gray-200 text-gray-600 hover:bg-gray-50 disabled:opacity-30 disabled:cursor-not-allowed flex-shrink-0"
          title="Geri Al (Ctrl+Z)"
        >
          ↩ Geri
        </button>
        <button
          onClick={redo}
          disabled={future.length === 0}
          className="px-2 py-1 text-xs rounded border border-gray-200 text-gray-600 hover:bg-gray-50 disabled:opacity-30 disabled:cursor-not-allowed flex-shrink-0"
          title="Yinele (Ctrl+Y)"
        >
          ↪ Yinele
        </button>

        {/* İndir (Faz 5'te aktif) */}
        <button
          disabled
          className="px-3 py-1.5 text-sm rounded border border-gray-200 text-gray-400 cursor-not-allowed flex-shrink-0"
          title="İndir — Faz 5'te aktif"
        >
          ↓ İndir
        </button>

        {/* Manuel Kaydet */}
        <button
          onClick={save}
          disabled={isSaving || !isDirty}
          className="px-3 py-1.5 text-sm rounded bg-blue-600 text-white hover:bg-blue-700 disabled:opacity-40 disabled:cursor-not-allowed flex-shrink-0"
        >
          {isSaving ? 'Kaydediliyor…' : 'Kaydet'}
        </button>
      </header>

      {/* Editor layout: palette | canvas | properties */}
      <div className="flex flex-1 overflow-hidden">
        <BlockPalette />
        <Canvas />
        <PropertyPanel />
      </div>
    </div>
  )
}
