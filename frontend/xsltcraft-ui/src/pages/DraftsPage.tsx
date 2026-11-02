import { useCallback, useEffect, useMemo, useState } from 'react'
import { Link, useNavigate, useSearchParams } from 'react-router-dom'
import { BookOpen, FilePlus2, FileText, FolderInput, LayoutGrid, Star, Truck } from 'lucide-react'
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
import LibraryPreviewPanel from '../components/storage/LibraryPreviewPanel'
import LibraryTabsHeader from '../components/storage/LibraryTabsHeader'
import DraftRow from '../components/storage/DraftRow'
import {
  BulkBar, BulkButton, BulkDelete, LibraryEmpty, LibraryStatus, PRIMARY_LINK, SECONDARY_LINK, SelectBox, TH,
} from '../components/storage/LibraryParts'
import FolderSidebar from '../components/storage/FolderSidebar'
import StorageToolbar from '../components/storage/StorageToolbar'
import MoveToFolderMenu from '../components/storage/MoveToFolderMenu'
import { useTemplateLibrary, type SortKey } from '../components/storage/useTemplateLibrary'
import { toast } from '../store/toastStore'
import defaultInvoiceXml from '../assets/default-invoice.xml?raw'
import defaultDespatchXml from '../assets/default-despatch.xml?raw'

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
    previewFromUserTemplate(previewTemplate.id, previewTemplate.documentType === 'Despatch' ? defaultDespatchXml : defaultInvoiceXml)
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

  if (loading) return <LibraryStatus>Yükleniyor...</LibraryStatus>
  if (error) return <LibraryStatus tone="error">{error}</LibraryStatus>

  const activeLabel = activeKey === 'all' ? 'Tümü'
    : activeKey === 'favorites' ? 'Favoriler'
    : folders.find((f) => f.id === activeKey)?.name ?? 'Tümü'
  const visibleIds = visible.map((t) => t.id)
  const allSelected = visibleIds.length > 0 && visibleIds.every((id) => selectedIds.has(id))
  const someSelected = selectedIds.size > 0

  return (
    <div className="flex h-full overflow-hidden">
      <div className="min-w-0 flex-1 overflow-y-auto">
        <div className="flex flex-col gap-4 px-8 pt-7 pb-9">
          <LibraryTabsHeader
            active="drafts"
            actions={
              <>
                <Link to="/editor/new" className={PRIMARY_LINK}>
                  <FilePlus2 size={15} aria-hidden="true" /> Yeni e-Fatura / e-Arşiv
                </Link>
                <Link to="/editor/new?type=despatch" className={SECONDARY_LINK}>
                  <Truck size={15} aria-hidden="true" /> Yeni e-İrsaliye
                </Link>
              </>
            }
          />

          <div className="flex flex-wrap items-start gap-[18px]">
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

            <section
              aria-label={`${activeLabel} taslakları`}
              className="min-w-0 flex-[999_1_520px] rounded-xl border border-ui-line bg-ui-card shadow-[var(--ui-shadow-rest)]"
            >
              {templates.length === 0 ? (
                <EmptyState />
              ) : (
                <>
                  <StorageToolbar search={search} onSearch={setSearch} sort={sort} onSort={setSort} placeholder="Şablonlarda ara…">
                    <span className="text-[12.5px] text-tx-muted">
                      <span className="font-medium text-tx-2">{activeLabel}</span> · {visible.length} şablon
                    </span>
                  </StorageToolbar>

                  {someSelected && (
                    <BulkBar count={selectedIds.size}>
                      <div className="relative">
                        <BulkButton
                          icon={FolderInput}
                          aria-haspopup="menu"
                          aria-expanded={bulkMoveOpen}
                          onClick={() => setBulkMoveOpen((v) => !v)}
                        >
                          Klasöre taşı
                        </BulkButton>
                        {bulkMoveOpen && (
                          <MoveToFolderMenu
                            folders={sortedFolders}
                            onMove={handleBulkMove}
                            onClose={() => setBulkMoveOpen(false)}
                          />
                        )}
                      </div>
                      <BulkDelete
                        confirming={bulkConfirm}
                        onAsk={() => setBulkConfirm(true)}
                        onYes={handleBulkDelete}
                        onNo={() => setBulkConfirm(false)}
                      />
                    </BulkBar>
                  )}

                  {visible.length === 0 ? (
                    <p className="m-0 py-16 text-center text-[13px] text-tx-subtle">
                      {search.trim()
                        ? `“${search.trim()}” ile eşleşen şablon bulunamadı.`
                        : 'Bu klasörde şablon yok.'}
                    </p>
                  ) : (
                    <div className="overflow-x-auto">
                      <table className="w-full border-collapse text-[13px]">
                        <thead>
                          <tr>
                            <th scope="col" className="w-10 py-2.5 pl-3.5 pr-1 text-left">
                              <SelectBox checked={allSelected} onChange={toggleAll} label="Görünen tüm taslakları seç" />
                            </th>
                            <th scope="col" className={`${TH} pl-1`}>Ad</th>
                            <th scope="col" className={TH}>Tür</th>
                            <th scope="col" className={TH}>Klasör</th>
                            <th scope="col" className={TH}>Güncellendi</th>
                            <th scope="col" className={`${TH} pr-3.5 text-right`}>İşlemler</th>
                          </tr>
                        </thead>
                        <tbody>
                          {visible.map((t) => (
                            <DraftRow
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
                        </tbody>
                      </table>
                    </div>
                  )}
                </>
              )}
            </section>
          </div>
        </div>
      </div>
      {previewTemplate && (
        <LibraryPreviewPanel
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
    <LibraryEmpty
      icon={FileText}
      title="Henüz bir şablonunuz yok."
      hint="Yeni şablon oluşturun veya hazır temalardan birini kullanın."
    >
      <Link to="/editor/new" className={PRIMARY_LINK}>Yeni e-Fatura / e-Arşiv</Link>
      <Link to="/editor/new?type=despatch" className={SECONDARY_LINK}>Yeni e-İrsaliye</Link>
      <Link to="/templates" className={SECONDARY_LINK}><BookOpen size={15} aria-hidden="true" /> Tema Kütüphanesi</Link>
    </LibraryEmpty>
  )
}
