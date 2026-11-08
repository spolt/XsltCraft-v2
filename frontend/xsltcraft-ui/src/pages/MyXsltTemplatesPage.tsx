import { useCallback, useEffect, useMemo, useState } from 'react'
import { Link, useNavigate, useSearchParams } from 'react-router-dom'
import { Code2, Users, FolderInput, LayoutGrid, Star, Upload, StickyNote } from 'lucide-react'
import {
  getUserXsltTemplates,
  getUserXsltTemplate,
  deleteUserXsltTemplate,
  updateUserXsltTemplate,
  moveUserXsltToFolder,
  setUserXsltFavorite,
  bulkAddFixedNote,
  type UserXsltTemplateSummary,
  type FixedNoteMode,
  type BulkAddFixedNoteResult,
} from '../services/userXsltService'
import {
  createFolder,
  deleteFolder,
  getFolders,
  updateFolder,
  type Folder,
} from '../services/folderService'
import { previewFromRawXslt } from '../services/previewService'
import LibraryPreviewPanel from '../components/storage/LibraryPreviewPanel'
import LibraryTabsHeader from '../components/storage/LibraryTabsHeader'
import XsltRow from '../components/storage/XsltRow'
import {
  BulkBar, BulkButton, BulkDelete, LibraryEmpty, LibraryStatus, PRIMARY_LINK, SECONDARY_LINK, SelectBox, TH,
} from '../components/storage/LibraryParts'

import ShareTemplateDialog from '../components/xslt-editor/ShareTemplateDialog'
import FolderSidebar from '../components/storage/FolderSidebar'
import StorageToolbar from '../components/storage/StorageToolbar'
import MoveToFolderMenu from '../components/storage/MoveToFolderMenu'
import BulkUploadModal from '../components/storage/BulkUploadModal'
import BulkAddNoteModal from '../components/storage/BulkAddNoteModal'
import BulkNoteResultDialog from '../components/storage/BulkNoteResultDialog'
import { useTemplateLibrary, type SortKey } from '../components/storage/useTemplateLibrary'
import { toast } from '../store/toastStore'
import { useEntitlementStore } from '../store/entitlementStore'
import { isProOrAbove } from '../services/entitlementService'
import { xsltOpenPath } from '../utils/workRoutes'
import defaultInvoiceXml from '../assets/default-invoice.xml?raw'

export default function MyXsltTemplatesPage() {
  const [templates, setTemplates] = useState<UserXsltTemplateSummary[]>([])
  const [folders, setFolders] = useState<Folder[]>([])
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)
  const [selectedIds, setSelectedIds] = useState<Set<string>>(new Set())
  const [bulkConfirm, setBulkConfirm] = useState(false)
  const [bulkMoveOpen, setBulkMoveOpen] = useState(false)
  const [bulkUploadOpen, setBulkUploadOpen] = useState(false)
  const [bulkNoteOpen, setBulkNoteOpen] = useState(false)
  const [noteResult, setNoteResult] = useState<BulkAddFixedNoteResult | null>(null)
  const [search, setSearch] = useState('')
  const [sort, setSort] = useState<SortKey>('updated')
  const [previewId, setPreviewId] = useState<string | null>(null)
  const [previewName, setPreviewName] = useState('')
  const [previewHtml, setPreviewHtml] = useState('')
  const [previewLoading, setPreviewLoading] = useState(false)
  const [shareTarget, setShareTarget] = useState<{ id: string; name: string } | null>(null)
  const [searchParams, setSearchParams] = useSearchParams()
  const navigate = useNavigate()
  const isPro = useEntitlementStore((s) => isProOrAbove(s.entitlements))

  const activeKey = searchParams.get('folder') ?? 'all'
  function setActiveKey(key: string) {
    setSearchParams(key === 'all' ? {} : { folder: key }, { replace: true })
  }

  useEffect(() => {
    load()
  }, [])

  useEffect(() => {
    if (!previewId) return
    let cancelled = false
    setPreviewHtml('')
    setPreviewLoading(true)
    getUserXsltTemplate(previewId)
      .then((t) => previewFromRawXslt(t.xsltContent, t.xmlContent || defaultInvoiceXml))
      .then((res) => { if (!cancelled) setPreviewHtml(res.html) })
      .catch(() => { if (!cancelled) setPreviewHtml('<html><body style="display:flex;align-items:center;justify-content:center;height:100%;color:#ef4444;font-family:sans-serif;font-size:13px">Önizleme alınamadı.</body></html>') })
      .finally(() => { if (!cancelled) setPreviewLoading(false) })
    return () => { cancelled = true }
  }, [previewId])

  async function load() {
    setLoading(true)
    setError(null)
    try {
      const [tpls, fldrs] = await Promise.all([getUserXsltTemplates(), getFolders('XsltTemplate')])
      setTemplates(tpls)
      setFolders(fldrs)
    } catch {
      setError('Şablonlar yüklenirken bir hata oluştu.')
    } finally {
      setLoading(false)
    }
  }

  const match = useCallback((t: UserXsltTemplateSummary) => {
    if (activeKey === 'all') return true
    if (activeKey === 'favorites') return t.isFavorite
    if (activeKey === 'shared') return t.isShared
    return t.folderId === activeKey
  }, [activeKey])

  const visible = useTemplateLibrary(templates, { search, match, sort })

  const counts = useMemo(() => {
    const byFolder: Record<string, number> = {}
    let fav = 0, shared = 0
    for (const t of templates) {
      if (t.folderId) byFolder[t.folderId] = (byFolder[t.folderId] ?? 0) + 1
      if (t.isFavorite) fav++
      if (t.isShared) shared++
    }
    return { byFolder, fav, shared, all: templates.length }
  }, [templates])

  const sortedFolders = useMemo(
    () => [...folders].sort((a, b) => a.name.localeCompare(b.name, 'tr')),
    [folders],
  )

  // --- Klasör CRUD ---
  async function handleCreateFolder(name: string, color: string | null) {
    try {
      const f = await createFolder({ name, kind: 'XsltTemplate', color })
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
      await moveUserXsltToFolder(id, folderId)
      setTemplates((prev) => prev.map((t) => (t.id === id ? { ...t, folderId } : t)))
      toast.success(folderId ? 'Klasöre taşındı.' : 'Klasörden çıkarıldı.', { durationMs: 4000 })
    } catch { toast.error('Taşıma başarısız.') }
  }
  async function handleToggleFavorite(id: string, value: boolean) {
    setTemplates((prev) => prev.map((t) => (t.id === id ? { ...t, isFavorite: value } : t)))
    try {
      await setUserXsltFavorite(id, value)
    } catch {
      setTemplates((prev) => prev.map((t) => (t.id === id ? { ...t, isFavorite: !value } : t)))
      toast.error('İşlem başarısız.')
    }
  }
  async function handleBulkMove(folderId: string | null) {
    const ids = [...selectedIds]
    try {
      await Promise.all(ids.map((id) => moveUserXsltToFolder(id, folderId)))
      setTemplates((prev) => prev.map((t) => (ids.includes(t.id) ? { ...t, folderId } : t)))
      setSelectedIds(new Set())
      toast.success(folderId ? 'Seçilenler taşındı.' : 'Seçilenler klasörden çıkarıldı.', { durationMs: 4000 })
    } catch { toast.error('Toplu taşıma başarısız.') } finally { setBulkMoveOpen(false) }
  }

  async function handleDelete(id: string) {
    try {
      await deleteUserXsltTemplate(id)
      setTemplates((prev) => prev.filter((t) => t.id !== id))
      setSelectedIds((prev) => { const next = new Set(prev); next.delete(id); return next })
    } catch { toast.error('Silme başarısız.') }
  }

  async function handleRename(id: string, name: string) {
    try {
      const updated = await updateUserXsltTemplate(id, { name })
      setTemplates((prev) => prev.map((t) => (t.id === id ? { ...t, name: updated.name, updatedAt: updated.updatedAt } : t)))
    } catch { toast.error('Yeniden adlandırma başarısız.') }
  }

  function toggleSelect(id: string) {
    setSelectedIds((prev) => { const next = new Set(prev); if (next.has(id)) next.delete(id); else next.add(id); return next })
  }

  function toggleAll() {
    const ownedVisible = visible.filter((t) => t.isOwner).map((t) => t.id)
    if (ownedVisible.length > 0 && ownedVisible.every((id) => selectedIds.has(id))) {
      setSelectedIds(new Set())
    } else {
      setSelectedIds(new Set(ownedVisible))
    }
  }

  async function handleBulkDelete() {
    const ids = [...selectedIds]
    try {
      await Promise.all(ids.map((id) => deleteUserXsltTemplate(id)))
      setTemplates((prev) => prev.filter((t) => !ids.includes(t.id)))
      setSelectedIds(new Set())
    } catch { toast.error('Toplu silme sırasında hata oluştu.') } finally { setBulkConfirm(false) }
  }

  async function handleBulkUploaded(folderId: string | null) {
    await load()
    if (folderId) setActiveKey(folderId)
  }

  async function handleBulkAddNote(noteText: string, mode: FixedNoteMode) {
    const ids = [...selectedIds]
    try {
      const res = await bulkAddFixedNote(ids, noteText, mode)
      setBulkNoteOpen(false)
      setNoteResult(res)
      // Etkilenen şablonların updatedAt'ı değişti; listeyi tazele.
      const updatedIds = new Set(res.results.filter((r) => r.status === 'updated').map((r) => r.id))
      if (updatedIds.size > 0) {
        const tpls = await getUserXsltTemplates()
        setTemplates(tpls)
        setSelectedIds(new Set())
      }
    } catch (err: unknown) {
      const status = (err as { response?: { status?: number } })?.response?.status
      toast.error(status === 402 ? 'Sabit not ekleme XsltCraft Pro üyeliği gerektirir.' : 'Sabit not eklenemedi.', { durationMs: 5000 })
    }
  }

  if (loading) return <LibraryStatus>Yükleniyor...</LibraryStatus>
  if (error) return <LibraryStatus tone="error">{error}</LibraryStatus>

  const activeLabel = activeKey === 'all' ? 'Tümü'
    : activeKey === 'favorites' ? 'Favoriler'
    : activeKey === 'shared' ? 'Paylaşılan'
    : folders.find((f) => f.id === activeKey)?.name ?? 'Tümü'
  const ownedVisible = visible.filter((t) => t.isOwner).map((t) => t.id)
  const allSelected = ownedVisible.length > 0 && ownedVisible.every((id) => selectedIds.has(id))
  const someSelected = selectedIds.size > 0

  const specials = [
    { key: 'all', label: 'Tümü', icon: <LayoutGrid size={15} />, count: counts.all },
    { key: 'favorites', label: 'Favoriler', icon: <Star size={15} />, count: counts.fav },
    ...(counts.shared > 0 ? [{ key: 'shared', label: 'Paylaşılan', icon: <Users size={15} />, count: counts.shared }] : []),
  ]

  return (
    <div className="flex h-full overflow-hidden">
      <div className="min-w-0 flex-1 overflow-y-auto">
        <div className="flex flex-col gap-4 px-8 pt-7 pb-9">
          <LibraryTabsHeader
            active="xslt"
            actions={
              <Link to="/xslt-editor" className={PRIMARY_LINK}>
                <Code2 size={15} aria-hidden="true" /> XSLT Editör
              </Link>
            }
          />

          <div className="flex flex-wrap items-start gap-[18px]">
            <FolderSidebar
              specials={specials}
              folders={sortedFolders.map((f) => ({ id: f.id, name: f.name, color: f.color, count: counts.byFolder[f.id] ?? 0 }))}
              activeKey={activeKey}
              onSelect={setActiveKey}
              onCreate={handleCreateFolder}
              onRename={handleRenameFolder}
              onRecolor={handleRecolorFolder}
              onDelete={handleDeleteFolder}
            />

            <section
              aria-label={`${activeLabel} XSLT şablonları`}
              className="min-w-0 flex-[999_1_520px] rounded-xl border border-ui-line bg-ui-card shadow-[var(--ui-shadow-rest)]"
            >
              {templates.length === 0 ? (
                <>
                  <div className="flex justify-end border-b border-ui-line px-3.5 py-3">
                    <BulkUploadButton onClick={() => setBulkUploadOpen(true)} />
                  </div>
                  <EmptyState />
                </>
              ) : (
                <>
                  <StorageToolbar search={search} onSearch={setSearch} sort={sort} onSort={setSort} placeholder="Şablonlarda ara…">
                    <span className="text-[12.5px] text-tx-muted">
                      <span className="font-medium text-tx-2">{activeLabel}</span> · {visible.length} şablon
                    </span>
                    <BulkUploadButton onClick={() => setBulkUploadOpen(true)} />
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
                          <MoveToFolderMenu folders={sortedFolders} onMove={handleBulkMove} onClose={() => setBulkMoveOpen(false)} />
                        )}
                      </div>
                      <BulkButton icon={StickyNote} onClick={() => setBulkNoteOpen(true)}>Sabit Not Ekle</BulkButton>
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
                              <SelectBox checked={allSelected} onChange={toggleAll} label="Görünen tüm şablonları seç" />
                            </th>
                            <th scope="col" className={`${TH} pl-1`}>Ad</th>
                            <th scope="col" className={TH}>Klasör</th>
                            <th scope="col" className={TH}>Güncellendi</th>
                            <th scope="col" className={`${TH} pr-3.5 text-right`}>İşlemler</th>
                          </tr>
                        </thead>
                        <tbody>
                          {visible.map((t) => (
                            <XsltRow
                              key={t.id}
                              template={t}
                              folders={sortedFolders}
                              selected={selectedIds.has(t.id)}
                              onToggleSelect={toggleSelect}
                              onOpen={() => navigate(xsltOpenPath(t.id, t.lastEditMode, isPro))}
                              onDelete={handleDelete}
                              onRename={handleRename}
                              onPreview={(id, name) => { setPreviewId(id); setPreviewName(name) }}
                              onShare={(id, name) => setShareTarget({ id, name })}
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
      {previewId && (
        <LibraryPreviewPanel
          name={previewName}
          html={previewHtml}
          loading={previewLoading}
          actionLabel="XSLT Editörde Aç"
          onAction={() => navigate(`/xslt-editor/${previewId}`)}
          onClose={() => setPreviewId(null)}
        />
      )}
      {shareTarget && (
        <ShareTemplateDialog
          templateId={shareTarget.id}
          templateName={shareTarget.name}
          onClose={() => setShareTarget(null)}
        />
      )}
      {bulkUploadOpen && (
        <BulkUploadModal
          folders={sortedFolders}
          defaultFolderId={activeKey !== 'all' && activeKey !== 'favorites' && activeKey !== 'shared' ? activeKey : null}
          onClose={() => setBulkUploadOpen(false)}
          onUploaded={handleBulkUploaded}
        />
      )}
      {bulkNoteOpen && (
        <BulkAddNoteModal
          count={selectedIds.size}
          onClose={() => setBulkNoteOpen(false)}
          onConfirm={handleBulkAddNote}
        />
      )}
      {noteResult && (
        <BulkNoteResultDialog
          result={noteResult}
          onClose={() => setNoteResult(null)}
          onPreview={(id) => {
            const r = noteResult.results.find((x) => x.id === id)
            setPreviewId(id)
            setPreviewName(r?.name ?? '')
            setNoteResult(null)
          }}
        />
      )}
    </div>
  )
}

function BulkUploadButton({ onClick }: { onClick: () => void }) {
  return (
    <button type="button" onClick={onClick} className={SECONDARY_LINK}>
      <Upload size={14} aria-hidden="true" /> Toplu yükle
    </button>
  )
}

function EmptyState() {
  return (
    <LibraryEmpty
      icon={Code2}
      title="Henüz kayıtlı XSLT şablonunuz yok."
      hint="XSLT Editör'de bir dosya düzenleyip kaydedin."
    >
      <Link to="/xslt-editor" className={PRIMARY_LINK}>XSLT Editör'e Git</Link>
    </LibraryEmpty>
  )
}
