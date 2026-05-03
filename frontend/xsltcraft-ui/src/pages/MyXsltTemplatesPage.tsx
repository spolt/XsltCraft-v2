import { useEffect, useState } from 'react'
import { Link, useNavigate } from 'react-router-dom'
import { Code2, Eye, Pencil, Trash2, Check, X } from 'lucide-react'
import {
  getUserXsltTemplates,
  getUserXsltTemplate,
  deleteUserXsltTemplate,
  updateUserXsltTemplate,
  type UserXsltTemplateSummary,
} from '../services/userXsltService'
import { previewFromRawXslt } from '../services/previewService'
import TemplatePreviewPanel from '../components/TemplatePreviewPanel'
import defaultInvoiceXml from '../assets/default-invoice.xml?raw'

export default function MyXsltTemplatesPage() {
  const [templates, setTemplates] = useState<UserXsltTemplateSummary[]>([])
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)
  const [selectedIds, setSelectedIds] = useState<Set<string>>(new Set())
  const [bulkConfirm, setBulkConfirm] = useState(false)
  const [previewId, setPreviewId] = useState<string | null>(null)
  const [previewName, setPreviewName] = useState('')
  const [previewHtml, setPreviewHtml] = useState('')
  const [previewLoading, setPreviewLoading] = useState(false)
  const navigate = useNavigate()

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
      setTemplates(await getUserXsltTemplates())
    } catch {
      setError('Şablonlar yüklenirken bir hata oluştu.')
    } finally {
      setLoading(false)
    }
  }

  async function handleDelete(id: string) {
    try {
      await deleteUserXsltTemplate(id)
      setTemplates((prev) => prev.filter((t) => t.id !== id))
      setSelectedIds((prev) => {
        const next = new Set(prev)
        next.delete(id)
        return next
      })
    } catch {
      alert('Silme başarısız.')
    }
  }

  async function handleRename(id: string, name: string) {
    try {
      const updated = await updateUserXsltTemplate(id, { name })
      setTemplates((prev) => prev.map((t) => (t.id === id ? { ...t, name: updated.name, updatedAt: updated.updatedAt } : t)))
    } catch {
      alert('Yeniden adlandırma başarısız.')
    }
  }

  function toggleSelect(id: string) {
    setSelectedIds((prev) => {
      const next = new Set(prev)
      if (next.has(id)) next.delete(id)
      else next.add(id)
      return next
    })
  }

  function toggleAll() {
    if (selectedIds.size === templates.length) {
      setSelectedIds(new Set())
    } else {
      setSelectedIds(new Set(templates.map((t) => t.id)))
    }
  }

  async function handleBulkDelete() {
    const ids = [...selectedIds]
    try {
      await Promise.all(ids.map((id) => deleteUserXsltTemplate(id)))
      setTemplates((prev) => prev.filter((t) => !ids.includes(t.id)))
      setSelectedIds(new Set())
    } catch {
      alert('Toplu silme sırasında hata oluştu.')
    } finally {
      setBulkConfirm(false)
    }
  }

  if (loading) {
    return (
      <div className="flex items-center justify-center py-24">
        <p className="text-gray-400 text-sm">Yükleniyor...</p>
      </div>
    )
  }

  if (error) {
    return (
      <div className="flex items-center justify-center py-24">
        <p className="text-red-500 text-sm">{error}</p>
      </div>
    )
  }

  const allSelected = templates.length > 0 && selectedIds.size === templates.length
  const someSelected = selectedIds.size > 0

  return (
    <div className="flex h-full overflow-hidden">
      <div
        className={`overflow-y-auto ${previewId ? 'border-r border-gray-200' : 'flex-1'}`}
        style={previewId ? { width: 400, flexShrink: 0 } : {}}
      >
      <div className="max-w-5xl mx-auto px-6 py-8">
      <div className="flex items-center justify-between mb-6">
        <div>
          <h1 className="text-xl font-semibold text-gray-800">Şablonlarım</h1>
          <p className="text-sm text-gray-400 mt-0.5">{templates.length} XSLT şablon</p>
        </div>
        <Link
          to="/xslt-editor"
          className="flex items-center gap-1.5 text-sm font-medium text-white bg-blue-600 hover:bg-blue-700 rounded-lg px-4 py-2 transition-colors"
        >
          <Code2 size={15} />
          XSLT Editör
        </Link>
      </div>

      {someSelected && (
        <div className="flex items-center gap-3 mb-4 px-4 py-2.5 bg-blue-50 border border-blue-200 rounded-xl">
          <span className="text-sm text-blue-700 font-medium flex-1">
            {selectedIds.size} şablon seçildi
          </span>
          {bulkConfirm ? (
            <div className="flex items-center gap-2 text-sm">
              <span className="text-red-600 font-medium">Emin misin?</span>
              <button
                onClick={handleBulkDelete}
                className="font-medium text-red-600 hover:text-red-700"
              >
                Evet
              </button>
              <span className="text-gray-300">/</span>
              <button
                onClick={() => setBulkConfirm(false)}
                className="text-gray-500 hover:text-gray-700"
              >
                Hayır
              </button>
            </div>
          ) : (
            <button
              onClick={() => setBulkConfirm(true)}
              className="flex items-center gap-1.5 text-sm font-medium text-white bg-red-500 hover:bg-red-600 rounded-lg px-3 py-1.5 transition-colors"
            >
              <Trash2 size={14} />
              Seçilenleri Sil
            </button>
          )}
        </div>
      )}

      {templates.length === 0 ? (
        <EmptyState />
      ) : (
        <>
          <div className="flex items-center gap-3 px-4 py-2 mb-1">
            <input
              type="checkbox"
              checked={allSelected}
              onChange={toggleAll}
              className="w-4 h-4 rounded border-gray-300 text-blue-600 accent-blue-600 cursor-pointer"
            />
            <span className="text-xs text-gray-400 select-none">Tümünü seç</span>
          </div>
          <div className="flex flex-col gap-2">
            {templates.map((t) => (
              <TemplateRow
                key={t.id}
                template={t}
                selected={selectedIds.has(t.id)}
                onToggleSelect={toggleSelect}
                onOpen={() => navigate(`/xslt-editor/${t.id}`)}
                onDelete={handleDelete}
                onRename={handleRename}
                onPreview={(id, name) => { setPreviewId(id); setPreviewName(name) }}
              />
            ))}
          </div>
        </>
      )}
      </div>
      </div>
      {previewId && (
        <TemplatePreviewPanel
          name={previewName}
          html={previewHtml}
          loading={previewLoading}
          actionLabel="XSLT Editörde Aç"
          onAction={() => navigate(`/xslt-editor/${previewId}`)}
          onClose={() => setPreviewId(null)}
        />
      )}
    </div>
  )
}

function EmptyState() {
  return (
    <div className="flex flex-col items-center py-24 text-center">
      <div className="w-16 h-16 rounded-full bg-gray-100 flex items-center justify-center mb-4">
        <Code2 size={28} className="text-gray-400" />
      </div>
      <p className="text-gray-600 font-medium">Henüz kayıtlı XSLT şablonunuz yok.</p>
      <p className="text-sm text-gray-400 mt-1">
        XSLT Editör'de bir dosya düzenleyip kaydedin.
      </p>
      <Link
        to="/xslt-editor"
        className="mt-5 text-sm font-medium text-white bg-blue-600 hover:bg-blue-700 rounded-lg px-4 py-2 transition-colors"
      >
        XSLT Editör'e Git
      </Link>
    </div>
  )
}

function TemplateRow({
  template,
  selected,
  onToggleSelect,
  onOpen,
  onDelete,
  onRename,
  onPreview,
}: {
  template: UserXsltTemplateSummary
  selected: boolean
  onToggleSelect: (id: string) => void
  onOpen: () => void
  onDelete: (id: string) => void
  onRename: (id: string, name: string) => void
  onPreview: (id: string, name: string) => void
}) {
  const [editing, setEditing] = useState(false)
  const [editName, setEditName] = useState(template.name)
  const [showConfirm, setShowConfirm] = useState(false)

  function commitRename() {
    const trimmed = editName.trim()
    if (trimmed && trimmed !== template.name) {
      onRename(template.id, trimmed)
    } else {
      setEditName(template.name)
    }
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
          <button onClick={onOpen} className="text-sm font-medium text-gray-800 hover:text-blue-600 truncate text-left">
            {template.name}
          </button>
        )}
        <span className="flex-shrink-0 text-xs text-purple-500 bg-purple-50 rounded-full px-2 py-0.5">XSLT</span>
      </div>

      <span className="text-xs text-gray-400 flex-shrink-0 hidden sm:block">
        {new Date(template.updatedAt).toLocaleDateString('tr-TR')}
      </span>

      <div className="flex items-center gap-1 flex-shrink-0">
        <button onClick={() => onPreview(template.id, template.name)} title="Önizle" className="p-1.5 rounded text-gray-400 hover:text-blue-600 hover:bg-blue-50 transition-colors">
          <Eye size={14} />
        </button>
        <button onClick={() => setEditing(true)} title="Yeniden adlandır" className="p-1.5 rounded text-gray-400 hover:text-gray-700 hover:bg-gray-100 transition-colors">
          <Pencil size={14} />
        </button>
        {showConfirm ? (
          <div className="flex items-center gap-1 bg-red-50 rounded px-2 py-1 text-xs">
            <span className="text-red-600">Emin misin?</span>
            <button onClick={() => onDelete(template.id)} className="font-medium text-red-600 hover:text-red-700">Evet</button>
            <span className="text-gray-300">/</span>
            <button onClick={() => setShowConfirm(false)} className="text-gray-500 hover:text-gray-700">Hayır</button>
          </div>
        ) : (
          <button onClick={() => setShowConfirm(true)} title="Sil" className="p-1.5 rounded text-gray-400 hover:text-red-600 hover:bg-red-50 transition-colors">
            <Trash2 size={14} />
          </button>
        )}
      </div>
    </div>
  )
}
