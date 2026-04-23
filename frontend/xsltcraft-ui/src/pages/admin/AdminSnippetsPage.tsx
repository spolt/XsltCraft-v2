import { useEffect, useState } from 'react'
import { Link } from 'react-router-dom'
import { Plus, Pencil, Trash2, Save, X, AlertCircle, Library } from 'lucide-react'
import {
  listAdminSnippets,
  createAdminSnippet,
  updateAdminSnippet,
  deleteAdminSnippet,
  type UserSnippet,
  type CreateSnippetRequest,
} from '../../services/snippetService'

const SCOPE_LABELS: Record<string, string> = {
  xslt: 'XSLT',
  xpath: 'XPath',
  html: 'HTML',
}

const SCOPE_COLORS: Record<string, string> = {
  xslt: 'bg-blue-100 text-blue-700',
  xpath: 'bg-purple-100 text-purple-700',
  html: 'bg-green-100 text-green-700',
}

interface FormState {
  prefix: string
  body: string
  description: string
  scope: 'xslt' | 'xpath' | 'html'
}

const EMPTY_FORM: FormState = { prefix: '', body: '', description: '', scope: 'xslt' }

// ---- Silme onay modal'ı ----
function DeleteModal({
  snippet,
  onConfirm,
  onCancel,
  loading,
}: {
  snippet: UserSnippet
  onConfirm: () => void
  onCancel: () => void
  loading: boolean
}) {
  return (
    <div className="fixed inset-0 bg-black/50 flex items-center justify-center z-50">
      <div className="bg-white rounded-xl shadow-xl p-6 max-w-sm w-full mx-4">
        <h3 className="text-base font-semibold text-gray-900 mb-2">Snippet'i Sil</h3>
        <p className="text-sm text-gray-600 mb-6">
          <span className="font-mono font-medium text-gray-900">{snippet.prefix}</span> snippet'i
          kütüphaneden kalıcı olarak silinecek.
        </p>
        <div className="flex gap-3 justify-end">
          <button
            onClick={onCancel}
            className="px-4 py-2 text-sm text-gray-700 border border-gray-300 hover:bg-gray-50 rounded-lg transition"
          >
            İptal
          </button>
          <button
            onClick={onConfirm}
            disabled={loading}
            className="px-4 py-2 text-sm text-white bg-red-600 hover:bg-red-700 disabled:opacity-50 rounded-lg transition"
          >
            {loading ? 'Siliniyor…' : 'Evet, Sil'}
          </button>
        </div>
      </div>
    </div>
  )
}

// ---- Snippet form (oluştur / düzenle) ----
function SnippetForm({
  initial,
  onSave,
  onCancel,
}: {
  initial?: UserSnippet
  onSave: (s: UserSnippet) => void
  onCancel: () => void
}) {
  const [form, setForm] = useState<FormState>(
    initial
      ? { prefix: initial.prefix, body: initial.body, description: initial.description ?? '', scope: initial.scope }
      : EMPTY_FORM,
  )
  const [saving, setSaving] = useState(false)
  const [error, setError] = useState<string | null>(null)

  const handleSave = async () => {
    if (!form.prefix.trim()) { setError('Prefix boş olamaz.'); return }
    if (!form.body.trim()) { setError('Snippet içeriği boş olamaz.'); return }
    setSaving(true)
    setError(null)
    try {
      const req: CreateSnippetRequest = {
        prefix: form.prefix.trim(),
        body: form.body,
        description: form.description.trim() || undefined,
        scope: form.scope,
      }
      const saved = initial
        ? await updateAdminSnippet(initial.id, req)
        : await createAdminSnippet(req)
      onSave(saved)
    } catch {
      setError('Kaydedilemedi. Lütfen tekrar deneyin.')
    } finally {
      setSaving(false)
    }
  }

  return (
    <div className="border border-gray-200 rounded-xl p-6 bg-white shadow-sm mb-8">
      <h2 className="text-base font-semibold text-gray-800 mb-4 flex items-center gap-2">
        <Library size={16} className="text-indigo-600" />
        {initial ? 'Snippet Düzenle' : 'Yeni Kütüphane Snippet\'i'}
      </h2>

      {error && (
        <div className="flex items-center gap-2 text-sm text-red-600 bg-red-50 border border-red-200 rounded-lg px-3 py-2 mb-4">
          <AlertCircle size={14} />
          {error}
        </div>
      )}

      <div className="grid grid-cols-1 sm:grid-cols-2 gap-4 mb-4">
        <div className="flex flex-col gap-1">
          <label className="text-xs font-medium text-gray-600">Prefix (tetikleyici kelime)</label>
          <input
            value={form.prefix}
            onChange={e => setForm(f => ({ ...f, prefix: e.target.value }))}
            placeholder="örn: foreach-inv"
            autoFocus
            className="border border-gray-300 rounded-lg px-3 py-2 text-sm font-mono focus:outline-none focus:ring-2 focus:ring-indigo-500"
          />
        </div>
        <div className="flex flex-col gap-1">
          <label className="text-xs font-medium text-gray-600">Kapsam</label>
          <select
            value={form.scope}
            onChange={e => setForm(f => ({ ...f, scope: e.target.value as FormState['scope'] }))}
            className="border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-indigo-500 bg-white"
          >
            <option value="xslt">XSLT</option>
            <option value="xpath">XPath</option>
            <option value="html">HTML</option>
          </select>
        </div>
      </div>

      <div className="flex flex-col gap-1 mb-4">
        <label className="text-xs font-medium text-gray-600">
          İçerik <span className="text-gray-400">(tab durağı için $1, $2 … — son konum $0)</span>
        </label>
        <textarea
          value={form.body}
          onChange={e => setForm(f => ({ ...f, body: e.target.value }))}
          rows={6}
          placeholder={'<xsl:for-each select="$1">\n  $0\n</xsl:for-each>'}
          className="border border-gray-300 rounded-lg px-3 py-2 text-sm font-mono resize-y focus:outline-none focus:ring-2 focus:ring-indigo-500"
        />
      </div>

      <div className="flex flex-col gap-1 mb-5">
        <label className="text-xs font-medium text-gray-600">Açıklama (opsiyonel)</label>
        <input
          value={form.description}
          onChange={e => setForm(f => ({ ...f, description: e.target.value }))}
          placeholder="Snippet'in ne yaptığını kısaca açıklayın"
          className="border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-indigo-500"
        />
      </div>

      <div className="flex gap-3">
        <button
          onClick={handleSave}
          disabled={saving}
          className="flex items-center gap-1.5 px-4 py-2 bg-indigo-600 hover:bg-indigo-700 disabled:opacity-50 text-white text-sm font-medium rounded-lg transition"
        >
          {saving
            ? <span className="inline-block w-3.5 h-3.5 border-2 border-white border-t-transparent rounded-full animate-spin" />
            : <Save size={14} />}
          Kaydet
        </button>
        <button
          onClick={onCancel}
          className="flex items-center gap-1.5 px-4 py-2 text-sm text-gray-700 border border-gray-300 hover:bg-gray-50 rounded-lg transition"
        >
          <X size={14} />
          İptal
        </button>
      </div>
    </div>
  )
}

// ---- Ana sayfa ----
export default function AdminSnippetsPage() {
  const [snippets, setSnippets] = useState<UserSnippet[]>([])
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)
  const [showForm, setShowForm] = useState(false)
  const [editingSnippet, setEditingSnippet] = useState<UserSnippet | null>(null)
  const [deletingSnippet, setDeletingSnippet] = useState<UserSnippet | null>(null)
  const [deleteLoading, setDeleteLoading] = useState(false)

  useEffect(() => {
    listAdminSnippets()
      .then(setSnippets)
      .catch(() => setError('Snippet\'ler yüklenemedi.'))
      .finally(() => setLoading(false))
  }, [])

  const handleSaved = (saved: UserSnippet) => {
    setSnippets(prev =>
      editingSnippet
        ? prev.map(s => (s.id === saved.id ? saved : s))
        : [saved, ...prev],
    )
    setShowForm(false)
    setEditingSnippet(null)
  }

  const handleDeleteConfirm = async () => {
    if (!deletingSnippet) return
    setDeleteLoading(true)
    try {
      await deleteAdminSnippet(deletingSnippet.id)
      setSnippets(prev => prev.filter(s => s.id !== deletingSnippet.id))
      setDeletingSnippet(null)
    } catch {
      // modal açık kalır
    } finally {
      setDeleteLoading(false)
    }
  }

  return (
    <div className="max-w-5xl mx-auto px-6 py-10">
      {/* Başlık */}
      <div className="flex items-center justify-between mb-6">
        <div>
          <h1 className="text-2xl font-semibold text-gray-900 flex items-center gap-2">
            <Library size={22} className="text-indigo-600" />
            Snippet Kütüphanesi
          </h1>
          <p className="text-gray-500 text-sm mt-1">
            Tüm kullanıcılara görünecek ortak snippet'ları yönetin.
          </p>
        </div>
        <div className="flex items-center gap-3">
          <Link
            to="/admin/themes"
            className="text-sm text-gray-600 hover:text-gray-900 border border-gray-300 px-3 py-1.5 rounded-lg transition"
          >
            ← Admin Paneli
          </Link>
        </div>
      </div>

      {/* Form — oluştur veya düzenle */}
      {(showForm || editingSnippet) && (
        <SnippetForm
          initial={editingSnippet ?? undefined}
          onSave={handleSaved}
          onCancel={() => { setShowForm(false); setEditingSnippet(null) }}
        />
      )}

      {/* Yeni ekle butonu */}
      {!showForm && !editingSnippet && (
        <button
          onClick={() => setShowForm(true)}
          className="flex items-center gap-2 px-4 py-2 bg-indigo-600 hover:bg-indigo-700 text-white text-sm font-medium rounded-lg transition mb-8"
        >
          <Plus size={15} />
          Yeni Snippet Ekle
        </button>
      )}

      {/* Snippet tablosu */}
      <h2 className="text-base font-semibold text-gray-800 mb-3">
        Kütüphane Snippet'ları
        {snippets.length > 0 && (
          <span className="ml-2 text-xs font-normal text-gray-400">({snippets.length})</span>
        )}
      </h2>

      {loading && <p className="text-sm text-gray-500">Yükleniyor…</p>}
      {error && <p className="text-sm text-red-600">{error}</p>}

      {!loading && !error && snippets.length === 0 && (
        <p className="text-sm text-gray-400">Henüz kütüphane snippet'ı yok. "Yeni Snippet Ekle" ile başlayın.</p>
      )}

      {!loading && snippets.length > 0 && (
        <div className="border border-gray-200 rounded-xl overflow-hidden bg-white shadow-sm">
          <table className="w-full text-sm">
            <thead className="bg-gray-50 border-b border-gray-200">
              <tr>
                <th className="text-left px-4 py-3 font-medium text-gray-600">Prefix</th>
                <th className="text-left px-4 py-3 font-medium text-gray-600 w-20">Kapsam</th>
                <th className="text-left px-4 py-3 font-medium text-gray-600">Açıklama</th>
                <th className="text-left px-4 py-3 font-medium text-gray-600">Eklenme</th>
                <th className="px-4 py-3 w-28" />
              </tr>
            </thead>
            <tbody className="divide-y divide-gray-100">
              {snippets.map(s => (
                <tr key={s.id} className="hover:bg-gray-50">
                  <td className="px-4 py-3 font-mono text-indigo-700 font-medium">{s.prefix}</td>
                  <td className="px-4 py-3">
                    <span className={`inline-block px-2 py-0.5 rounded text-xs font-medium ${SCOPE_COLORS[s.scope] ?? 'bg-gray-100 text-gray-600'}`}>
                      {SCOPE_LABELS[s.scope] ?? s.scope}
                    </span>
                  </td>
                  <td className="px-4 py-3 text-gray-500 truncate max-w-[240px]">
                    {s.description || <span className="text-gray-300 italic">—</span>}
                  </td>
                  <td className="px-4 py-3 text-gray-400 text-xs">
                    {new Date(s.createdAt).toLocaleDateString('tr-TR')}
                  </td>
                  <td className="px-4 py-3">
                    <div className="flex gap-2 justify-end">
                      <button
                        onClick={() => { setEditingSnippet(s); setShowForm(false) }}
                        className="flex items-center gap-1 px-2.5 py-1.5 text-xs font-medium text-blue-700 border border-blue-200 hover:bg-blue-50 rounded-lg transition"
                      >
                        <Pencil size={12} />
                        Düzenle
                      </button>
                      <button
                        onClick={() => setDeletingSnippet(s)}
                        className="flex items-center gap-1 px-2.5 py-1.5 text-xs font-medium text-red-700 border border-red-200 hover:bg-red-50 rounded-lg transition"
                      >
                        <Trash2 size={12} />
                        Sil
                      </button>
                    </div>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}

      {/* Silme modal'ı */}
      {deletingSnippet && (
        <DeleteModal
          snippet={deletingSnippet}
          onConfirm={handleDeleteConfirm}
          onCancel={() => setDeletingSnippet(null)}
          loading={deleteLoading}
        />
      )}
    </div>
  )
}
