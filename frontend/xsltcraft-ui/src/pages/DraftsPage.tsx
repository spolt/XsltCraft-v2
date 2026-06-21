import { useCallback, useEffect, useMemo, useState } from 'react'
import { Link, useNavigate, useSearchParams } from 'react-router-dom'
import { Check, Copy, Eye, FilePlus, FolderInput, LayoutGrid, Pencil, Star, Trash2, X } from 'lucide-react'
import {
  cloneTemplate,
  deleteTemplate,
  getMyTemplates,
  moveTemplateToFolder,
  setTemplateFavorite,
  updateTemplate,
  type TemplateDetail,
} from '../services/templateService'
import {
  createFolder,
  deleteFolder,
  getFolders,
  updateFolder,
  type Folder,
} from '../services/folderService'
import { previewFromUserTemplate } from '../services/previewService'
import TemplatePreviewPanel from '../components/TemplatePreviewPanel'
import FolderSidebar from '../components/storage/FolderSidebar'
import StorageToolbar from '../components/storage/StorageToolbar'
import MoveToFolderMenu from '../components/storage/MoveToFolderMenu'
import { useTemplateLibrary, type SortKey } from '../components/storage/useTemplateLibrary'
import { toast } from '../store/toastStore'
import defaultInvoiceXml from '../assets/default-invoice.xml?raw'

const DOC_TYPE_LABEL: Record<string, string> = {
  Invoice: 'e-Fatura / e-Arşiv',
  Despatch: 'e-İrsaliye',
}

export default function DraftsPage() {
  const [templates, setTemplates] = useState<TemplateDetail[]>([])
  const [folders, setFolders] = useState<Folder[]>([])
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)
  const [selectedIds, setSelectedIds] = useState<Set<string>>(new Set())
  const [bulkConfirm, setBulkConfirm] = useState(false)
  const [bulkMoveOpen, setBulkMoveOpen] = useState(false)
  const [search, setSearch] = useState('')
  const [sort, setSort] = useState<SortKey>('updated')
  const [previewTemplate, setPreviewTemplate] = useState<TemplateDetail | null>(null)
  const [previewHtml, setPreviewHtml] = useState('')
  const [previewLoading, setPreviewLoading] = useState(false)
  const [searchParams, setSearchParams] = useSearchParams()
  const navigate = useNavigate()

  const activeKey = searchParams.get('folder') ?? 'all'
  function setActiveKey(key: string) {
    setSearchParams(key === 'all' ? {} : { folder: key }, { replace: true })
  }

  useEffect(() => {
    if (!previewTemplate) return
    let cancelled = false
    setPreviewHtml('')
    setPreviewLoading(true)
    previewFromUserTemplate(previewTemplate.id, defaultInvoiceXml)
      .then((res) => { if (!cancelled) setPreviewHtml(res.html) })
      .catch(() => { if (!cancelled) setPreviewHtml('<html><body style="display:flex;align-items:center;justify-content:center;height:100%;color:#ef4444;font-family:sans-serif;font-size:13px">Önizleme alınamadı.</body></html>') })
      .finally(() => { if (!cancelled) setPreviewLoading(false) })
    return () => { cancelled = true }
  }, [previewTemplate])

  useEffect(() => {
    load()
  }, [])

  async function load() {
    setLoading(true)
    setError(null)
    try {
      const [tpls, fldrs] = await Promise.all([getMyTemplates(), getFolders('Draft')])
      setTemplates(tpls)
      setFolders(fldrs)
    } catch {
      setError('Şablonlar yüklenirken bir hata oluştu.')
    } finally {
      setLoading(false)
    }
  }

  // --- Klasör/favori filtresi ---
  const match = useCallback((t: TemplateDetail) => {
    if (activeKey === 'all') return true
    if (activeKey === 'favorites') return t.isFavorite
    return t.folderId === activeKey
  }, [activeKey])

  const visible = useTemplateLibrary(templates, { search, match, sort })

  const counts = useMemo(() => {
    const byFolder: Record<string, number> = {}
    let fav = 0
    for (const t of templates) {
      if (t.folderId) byFolder[t.folderId] = (byFolder[t.folderId] ?? 0) + 1
      if (t.isFavorite) fav++
    }
    return { byFolder, fav, all: templates.length }
  }, [templates])

  const sortedFolders = useMemo(
    () => [...folders].sort((a, b) => a.name.localeCompare(b.name, 'tr')),
    [folders],
  )

  // --- Klasör CRUD ---
  async function handleCreateFolder(name: string, color: string | null) {
    try {
      const f = await createFolder({ name, kind: 'Draft', color })
      setFolders((prev) => [...prev, f])
    } catch { toast.error('Klasör oluşturulamadı.') }
  }
  async function handleRenameFolder(id: string, name: string) {
    try {
      const f = await updateFolder(id, { name })
      setFolders((prev) => prev.map((x) => (x.id === id ? f : x)))
    } catch { toast.error('Klasör güncellenemedi.') }
  }
  async function handleRecolorFolder(id: string, color: string | null) {
    try {
      const f = await updateFolder(id, { color })
      setFolders((prev) => prev.map((x) => (x.id === id ? f : x)))
    } catch { toast.error('Klasör güncellenemedi.') }
  }
  async function handleDeleteFolder(id: string) {
    try {
      await deleteFolder(id)
      setFolders((prev) => prev.filter((x) => x.id !== id))
      setTemplates((prev) => prev.map((t) => (t.folderId === id ? { ...t, folderId: null } : t)))
      if (activeKey === id) setActiveKey('all')
    } catch { toast.error('Klasör silinemedi.') }
  }

  // --- Şablon işlemleri ---
  async function handleMove(id: string, folderId: string | null) {
    try {
      await moveTemplateToFolder(id, folderId)
      setTemplates((prev) => prev.map((t) => (t.id === id ? { ...t, folderId } : t)))
      toast.success(folderId ? 'Klasöre taşındı.' : 'Klasörden çıkarıldı.', { durationMs: 4000 })
    } catch { toast.error('Taşıma başarısız.') }
  }
  async function handleToggleFavorite(id: string, value: boolean) {
    setTemplates((prev) => prev.map((t) => (t.id === id ? { ...t, isFavorite: value } : t)))
    try {
      await setTemplateFavorite(id, value)
    } catch {
      setTemplates((prev) => prev.map((t) => (t.id === id ? { ...t, isFavorite: !value } : t)))
      toast.error('İşlem başarısız.')
    }
  }
  async function handleBulkMove(folderId: string | null) {
    const ids = [...selectedIds]
    try {
      await Promise.all(ids.map((id) => moveTemplateToFolder(id, folderId)))
      setTemplates((prev) => prev.map((t) => (ids.includes(t.id) ? { ...t, folderId } : t)))
      setSelectedIds(new Set())
      toast.success(folderId ? 'Seçilenler taşındı.' : 'Seçilenler klasörden çıkarıldı.', { durationMs: 4000 })
    } catch { toast.error('Toplu taşıma başarısız.') } finally { setBulkMoveOpen(false) }
  }

  async function handleClone(id: string) {
    try {
      const clone = await cloneTemplate(id)
      navigate(`/editor/${clone.id}`)
    } catch { toast.error('Kopyalama başarısız.') }
  }

  async function handleDelete(id: string) {
    try {
      await deleteTemplate(id)
      setTemplates((prev) => prev.filter((t) => t.id !== id))
      setSelectedIds((prev) => { const next = new Set(prev); next.delete(id); return next })
    } catch { toast.error('Silme başarısız.') }
  }

  async function handleRename(id: string, name: string) {
    try {
      const updated = await updateTemplate(id, { name })
      setTemplates((prev) => prev.map((t) => (t.id === id ? updated : t)))
    } catch { toast.error('Yeniden adlandırma başarısız.') }
  }

  function toggleSelect(id: string) {
    setSelectedIds((prev) => { const next = new Set(prev); if (next.has(id)) next.delete(id); else next.add(id); return next })
  }

  function toggleAll() {
    const visibleIds = visible.map((t) => t.id)
    if (visibleIds.every((id) => selectedIds.has(id)) && visibleIds.length > 0) {
      setSelectedIds(new Set())
    } else {
      setSelectedIds(new Set(visibleIds))
    }
  }

  async function handleBulkDelete() {
    const ids = [...selectedIds]
    try {
      await Promise.all(ids.map((id) => deleteTemplate(id)))
      setTemplates((prev) => prev.filter((t) => !ids.includes(t.id)))
      setSelectedIds(new Set())
    } catch { toast.error('Toplu silme sırasında hata oluştu.') } finally { setBulkConfirm(false) }
  }

  if (loading) {
    return <div className="flex items-center justify-center py-24"><p className="text-gray-400 text-sm">Yükleniyor...</p></div>
  }
  if (error) {
    return <div className="flex items-center justify-center py-24"><p className="text-red-500 text-sm">{error}</p></div>
  }

  const activeLabel = activeKey === 'all' ? 'Tümü'
    : activeKey === 'favorites' ? 'Favoriler'
    : folders.find((f) => f.id === activeKey)?.name ?? 'Tümü'
  const visibleIds = visible.map((t) => t.id)
  const allSelected = visibleIds.length > 0 && visibleIds.every((id) => selectedIds.has(id))
  const someSelected = selectedIds.size > 0

  return (
    <div className="flex h-full overflow-hidden">
      <FolderSidebar
        specials={[
          { key: 'all', label: 'Tümü', icon: <LayoutGrid size={15} />, count: counts.all },
          { key: 'favorites', label: 'Favoriler', icon: <Star size={15} />, count: counts.fav },
        ]}
        folders={sortedFolders.map((f) => ({ id: f.id, name: f.name, color: f.color, count: counts.byFolder[f.id] ?? 0 }))}
        activeKey={activeKey}
        onSelect={setActiveKey}
        onCreate={handleCreateFolder}
        onRename={handleRenameFolder}
        onRecolor={handleRecolorFolder}
        onDelete={handleDeleteFolder}
      />

      <div
        className={`overflow-y-auto ${previewTemplate ? 'border-r border-gray-200' : 'flex-1'}`}
        style={previewTemplate ? { width: 400, flexShrink: 0 } : {}}
      >
      <div className="max-w-5xl mx-auto px-6 py-8">
      <div className="flex items-center justify-between mb-6">
        <div>
          <h1 className="text-xl font-semibold text-gray-800">Şablonlarım</h1>
          <p className="text-sm text-gray-400 mt-0.5">{templates.length} şablon</p>
        </div>
        <Link
          to="/editor/new"
          className="flex items-center gap-1.5 text-sm font-medium text-white bg-blue-600 hover:bg-blue-700 rounded-lg px-4 py-2 transition-colors"
        >
          <FilePlus size={15} />
          Yeni Şablon
        </Link>
      </div>

      {templates.length === 0 ? (
        <EmptyState />
      ) : (
        <>
          <StorageToolbar search={search} onSearch={setSearch} sort={sort} onSort={setSort} placeholder="Şablonlarda ara…" />

          {someSelected && (
            <div className="flex items-center gap-3 mb-4 px-4 py-2.5 bg-blue-50 border border-blue-200 rounded-xl">
              <span className="text-sm text-blue-700 font-medium flex-1">{selectedIds.size} şablon seçildi</span>
              <div className="relative">
                <button
                  onClick={() => setBulkMoveOpen((v) => !v)}
                  className="flex items-center gap-1.5 text-sm font-medium text-blue-700 hover:bg-blue-100 rounded-lg px-3 py-1.5 transition-colors"
                >
                  <FolderInput size={14} /> Klasöre taşı
                </button>
                {bulkMoveOpen && (
                  <MoveToFolderMenu
                    folders={sortedFolders}
                    onMove={handleBulkMove}
                    onClose={() => setBulkMoveOpen(false)}
                  />
                )}
              </div>
              {bulkConfirm ? (
                <div className="flex items-center gap-2 text-sm">
                  <span className="text-red-600 font-medium">Emin misin?</span>
                  <button onClick={handleBulkDelete} className="font-medium text-red-600 hover:text-red-700">Evet</button>
                  <span className="text-gray-300">/</span>
                  <button onClick={() => setBulkConfirm(false)} className="text-gray-500 hover:text-gray-700">Hayır</button>
                </div>
              ) : (
                <button
                  onClick={() => setBulkConfirm(true)}
                  className="flex items-center gap-1.5 text-sm font-medium text-white bg-red-500 hover:bg-red-600 rounded-lg px-3 py-1.5 transition-colors"
                >
                  <Trash2 size={14} /> Seçilenleri Sil
                </button>
              )}
            </div>
          )}

          <div className="flex items-center gap-3 px-4 py-2 mb-1">
            <input
              type="checkbox"
              checked={allSelected}
              onChange={toggleAll}
              className="w-4 h-4 rounded border-gray-300 text-blue-600 accent-blue-600 cursor-pointer"
            />
            <span className="text-xs text-gray-500 select-none font-medium">{activeLabel}</span>
            <span className="text-xs text-gray-400 select-none">· {visible.length} şablon</span>
          </div>

          {visible.length === 0 ? (
            <div className="py-16 text-center text-sm text-gray-400">
              {search.trim()
                ? `“${search.trim()}” ile eşleşen şablon bulunamadı.`
                : 'Bu klasörde şablon yok.'}
            </div>
          ) : (
            <div className="flex flex-col gap-2">
              {visible.map((t) => (
                <TemplateRow
                  key={t.id}
                  template={t}
                  folders={sortedFolders}
                  selected={selectedIds.has(t.id)}
                  onToggleSelect={toggleSelect}
                  onClone={handleClone}
                  onDelete={handleDelete}
                  onRename={handleRename}
                  onPreview={setPreviewTemplate}
                  onMove={handleMove}
                  onToggleFavorite={handleToggleFavorite}
                />
              ))}
            </div>
          )}
        </>
      )}
      </div>
      </div>
      {previewTemplate && (
        <TemplatePreviewPanel
          name={previewTemplate.name}
          html={previewHtml}
          loading={previewLoading}
          actionLabel="Editörde Düzenle"
          onAction={() => navigate(`/editor/${previewTemplate.id}`)}
          onClose={() => setPreviewTemplate(null)}
        />
      )}
    </div>
  )
}

function EmptyState() {
  return (
    <div className="flex flex-col items-center py-24 text-center">
      <span className="text-5xl mb-4 select-none">📄</span>
      <p className="text-gray-600 font-medium">Henüz bir şablonunuz yok.</p>
      <p className="text-sm text-gray-400 mt-1">Yeni şablon oluşturun veya hazır temalardan birini kullanın.</p>
      <div className="flex gap-3 mt-5">
        <Link to="/editor/new" className="text-sm font-medium text-white bg-blue-600 hover:bg-blue-700 rounded-lg px-4 py-2 transition-colors">Yeni Şablon Oluştur</Link>
        <Link to="/templates" className="text-sm font-medium text-gray-700 border border-gray-300 hover:bg-gray-50 rounded-lg px-4 py-2 transition-colors">Tema Kütüphanesi</Link>
      </div>
    </div>
  )
}

function TemplateRow({
  template,
  folders,
  selected,
  onToggleSelect,
  onClone,
  onDelete,
  onRename,
  onPreview,
  onMove,
  onToggleFavorite,
}: {
  template: TemplateDetail
  folders: Folder[]
  selected: boolean
  onToggleSelect: (id: string) => void
  onClone: (id: string) => void
  onDelete: (id: string) => void
  onRename: (id: string, name: string) => void
  onPreview: (template: TemplateDetail) => void
  onMove: (id: string, folderId: string | null) => void
  onToggleFavorite: (id: string, value: boolean) => void
}) {
  const navigate = useNavigate()
  const [editing, setEditing] = useState(false)
  const [editName, setEditName] = useState(template.name)
  const [showConfirm, setShowConfirm] = useState(false)
  const [moveOpen, setMoveOpen] = useState(false)

  function commitRename() {
    const trimmed = editName.trim()
    if (trimmed && trimmed !== template.name) onRename(template.id, trimmed)
    else setEditName(template.name)
    setEditing(false)
  }

  return (
    <div
      className={`flex items-center gap-3 px-4 py-3 bg-white border rounded-xl transition-colors ${
        selected ? 'border-blue-300 bg-blue-50/40' : 'border-gray-200 hover:border-gray-300'
      }`}
    >
      <input
        type="checkbox"
        checked={selected}
        onChange={() => onToggleSelect(template.id)}
        className="w-4 h-4 rounded border-gray-300 text-blue-600 accent-blue-600 cursor-pointer flex-shrink-0"
      />

      <button
        onClick={() => onToggleFavorite(template.id, !template.isFavorite)}
        title={template.isFavorite ? 'Favorilerden çıkar' : 'Favorilere ekle'}
        className={`flex-shrink-0 p-0.5 transition-colors ${template.isFavorite ? 'text-amber-400 hover:text-amber-500' : 'text-gray-300 hover:text-amber-400'}`}
      >
        <Star size={15} fill={template.isFavorite ? 'currentColor' : 'none'} />
      </button>

      <div className="flex-1 min-w-0 flex items-center gap-2">
        {editing ? (
          <div className="flex items-center gap-1 flex-1">
            <input
              autoFocus
              className="flex-1 text-sm border border-blue-300 rounded px-2 py-0.5 outline-none focus:ring-1 focus:ring-blue-200"
              value={editName}
              onChange={(e) => setEditName(e.target.value)}
              onBlur={commitRename}
              onKeyDown={(e) => {
                if (e.key === 'Enter') commitRename()
                if (e.key === 'Escape') { setEditName(template.name); setEditing(false) }
              }}
            />
            <button onClick={commitRename} className="text-green-600 hover:text-green-700"><Check size={14} /></button>
            <button onClick={() => { setEditName(template.name); setEditing(false) }} className="text-red-400 hover:text-red-600"><X size={14} /></button>
          </div>
        ) : (
          <button
            onClick={() => navigate(`/editor/${template.id}`)}
            className="text-sm font-medium text-gray-800 hover:text-blue-600 truncate text-left"
          >
            {template.name}
          </button>
        )}
        <span className="flex-shrink-0 text-xs text-blue-500 bg-blue-50 rounded-full px-2 py-0.5">
          {DOC_TYPE_LABEL[template.documentType] ?? template.documentType}
        </span>
      </div>

      <span className="text-xs text-gray-400 flex-shrink-0 hidden sm:block">
        {new Date(template.updatedAt).toLocaleDateString('tr-TR')}
      </span>

      <div className="flex items-center gap-1 flex-shrink-0">
        <button onClick={() => onPreview(template)} title="Önizle" className="p-1.5 rounded text-gray-400 hover:text-blue-600 hover:bg-blue-50 transition-colors"><Eye size={14} /></button>
        <div className="relative">
          <button onClick={() => setMoveOpen((v) => !v)} title="Klasöre taşı" className="p-1.5 rounded text-gray-400 hover:text-blue-600 hover:bg-blue-50 transition-colors"><FolderInput size={14} /></button>
          {moveOpen && (
            <MoveToFolderMenu
              folders={folders}
              currentFolderId={template.folderId}
              onMove={(fid) => onMove(template.id, fid)}
              onClose={() => setMoveOpen(false)}
            />
          )}
        </div>
        <button onClick={() => setEditing(true)} title="Yeniden adlandır" className="p-1.5 rounded text-gray-400 hover:text-gray-700 hover:bg-gray-100 transition-colors"><Pencil size={14} /></button>
        <button onClick={() => onClone(template.id)} title="Kopyala" className="p-1.5 rounded text-gray-400 hover:text-gray-700 hover:bg-gray-100 transition-colors"><Copy size={14} /></button>
        {showConfirm ? (
          <div className="flex items-center gap-1 bg-red-50 rounded px-2 py-1 text-xs">
            <span className="text-red-600">Emin misin?</span>
            <button onClick={() => onDelete(template.id)} className="font-medium text-red-600 hover:text-red-700">Evet</button>
            <span className="text-gray-300">/</span>
            <button onClick={() => setShowConfirm(false)} className="text-gray-500 hover:text-gray-700">Hayır</button>
          </div>
        ) : (
          <button onClick={() => setShowConfirm(true)} title="Sil" className="p-1.5 rounded text-gray-400 hover:text-red-600 hover:bg-red-50 transition-colors"><Trash2 size={14} /></button>
        )}
      </div>
    </div>
  )
}
