import { useState, useEffect } from 'react'
import { X, Plus, Pencil, Trash2, Save, AlertCircle, Library, Star } from 'lucide-react'
import {
  listSnippets,
  createSnippet,
  updateSnippet,
  deleteSnippet,
  type UserSnippet,
  type CreateSnippetRequest,
} from '../../services/snippetService'

const SCOPE_LABELS: Record<string, string> = {
  xslt: 'XSLT',
  xpath: 'XPath',
  html: 'HTML',
}

const SCOPE_COLORS: Record<string, string> = {
  xslt: 'bg-blue-900 text-blue-300',
  xpath: 'bg-purple-900 text-purple-300',
  html: 'bg-green-900 text-green-300',
}

interface FormState {
  prefix: string
  body: string
  description: string
  scope: 'xslt' | 'xpath' | 'html'
}

const EMPTY_FORM: FormState = { prefix: '', body: '', description: '', scope: 'xslt' }

interface Props {
  onClose: () => void
  onSnippetsChanged: (snippets: UserSnippet[]) => void
}

export default function SnippetManagerDialog({ onClose, onSnippetsChanged }: Props) {
  const [snippets, setSnippets] = useState<UserSnippet[]>([])
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)
  const [editingId, setEditingId] = useState<string | null>(null)
  const [showForm, setShowForm] = useState(false)
  const [form, setForm] = useState<FormState>(EMPTY_FORM)
  const [saving, setSaving] = useState(false)
  const [formError, setFormError] = useState<string | null>(null)
  const [deletingId, setDeletingId] = useState<string | null>(null)

  useEffect(() => {
    load()
  }, [])

  async function load() {
    setLoading(true)
    setError(null)
    try {
      const data = await listSnippets()
      setSnippets(data)
      onSnippetsChanged(data)
    } catch {
      setError('Snippet\'ler yüklenemedi.')
    } finally {
      setLoading(false)
    }
  }

  function openCreate() {
    setEditingId(null)
    setForm(EMPTY_FORM)
    setFormError(null)
    setShowForm(true)
  }

  function openEdit(s: UserSnippet) {
    setEditingId(s.id)
    setForm({ prefix: s.prefix, body: s.body, description: s.description ?? '', scope: s.scope })
    setFormError(null)
    setShowForm(true)
  }

  function cancelForm() {
    setShowForm(false)
    setEditingId(null)
    setFormError(null)
  }

  async function handleSave() {
    if (!form.prefix.trim()) { setFormError('Prefix boş olamaz.'); return }
    if (!form.body.trim()) { setFormError('Snippet içeriği boş olamaz.'); return }
    setSaving(true)
    setFormError(null)
    try {
      let updated: UserSnippet[]
      if (editingId) {
        const s = await updateSnippet(editingId, {
          prefix: form.prefix.trim(),
          body: form.body,
          description: form.description.trim() || undefined,
          scope: form.scope,
        })
        updated = snippets.map(x => x.id === editingId ? s : x)
      } else {
        const req: CreateSnippetRequest = {
          prefix: form.prefix.trim(),
          body: form.body,
          description: form.description.trim() || undefined,
          scope: form.scope,
        }
        const s = await createSnippet(req)
        updated = [...snippets, s]
      }
      setSnippets(updated)
      onSnippetsChanged(updated)
      cancelForm()
    } catch {
      setFormError('Kaydedilemedi. Lütfen tekrar deneyin.')
    } finally {
      setSaving(false)
    }
  }

  async function handleDelete(id: string) {
    setDeletingId(id)
    try {
      await deleteSnippet(id)
      const updated = snippets.filter(s => s.id !== id)
      setSnippets(updated)
      onSnippetsChanged(updated)
    } catch {
      setError('Snippet silinemedi.')
    } finally {
      setDeletingId(null)
    }
  }

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/60">
      <div className="w-full max-w-2xl bg-gray-900 border border-gray-700 rounded-xl shadow-2xl flex flex-col max-h-[85vh]">
        {/* Header */}
        <div className="flex items-center gap-2 px-5 py-4 border-b border-gray-700 flex-shrink-0">
          <h2 className="text-sm font-semibold text-white flex-1">Snippet Kütüphanesi</h2>
          <button
            onClick={openCreate}
            className="flex items-center gap-1.5 h-7 px-3 rounded bg-blue-600 hover:bg-blue-700 text-white text-xs font-medium transition-colors"
          >
            <Plus size={13} />
            Yeni Snippet
          </button>
          <button onClick={onClose} className="text-gray-400 hover:text-white ml-1" title="Kapat">
            <X size={16} />
          </button>
        </div>

        {/* Form */}
        {showForm && (
          <div className="px-5 py-4 border-b border-gray-700 bg-gray-800 flex-shrink-0">
            <p className="text-xs font-medium text-gray-300 mb-3">
              {editingId ? 'Snippet Düzenle' : 'Yeni Snippet'}
            </p>
            <div className="grid grid-cols-2 gap-3 mb-3">
              <div>
                <label className="block text-[11px] text-gray-400 mb-1">Prefix (tetikleyici kelime)</label>
                <input
                  value={form.prefix}
                  onChange={e => setForm(f => ({ ...f, prefix: e.target.value }))}
                  placeholder="örn: foreach-inv"
                  className="w-full bg-gray-700 border border-gray-600 rounded px-2 py-1.5 text-xs text-gray-200 font-mono focus:outline-none focus:border-blue-500"
                  autoFocus
                />
              </div>
              <div>
                <label className="block text-[11px] text-gray-400 mb-1">Kapsam</label>
                <select
                  value={form.scope}
                  onChange={e => setForm(f => ({ ...f, scope: e.target.value as FormState['scope'] }))}
                  className="w-full bg-gray-700 border border-gray-600 rounded px-2 py-1.5 text-xs text-gray-200 focus:outline-none focus:border-blue-500"
                >
                  <option value="xslt">XSLT</option>
                  <option value="xpath">XPath</option>
                  <option value="html">HTML</option>
                </select>
              </div>
            </div>
            <div className="mb-3">
              <label className="block text-[11px] text-gray-400 mb-1">
                İçerik <span className="text-gray-500">(tab durağı için $1, $2 … kullanın)</span>
              </label>
              <textarea
                value={form.body}
                onChange={e => setForm(f => ({ ...f, body: e.target.value }))}
                rows={5}
                placeholder={'<xsl:for-each select="$1">\n  $0\n</xsl:for-each>'}
                className="w-full bg-gray-700 border border-gray-600 rounded px-2 py-1.5 text-xs text-gray-200 font-mono resize-y focus:outline-none focus:border-blue-500"
              />
            </div>
            <div className="mb-3">
              <label className="block text-[11px] text-gray-400 mb-1">Açıklama (opsiyonel)</label>
              <input
                value={form.description}
                onChange={e => setForm(f => ({ ...f, description: e.target.value }))}
                placeholder="Snippet'in ne yaptığını kısaca açıklayın"
                className="w-full bg-gray-700 border border-gray-600 rounded px-2 py-1.5 text-xs text-gray-200 focus:outline-none focus:border-blue-500"
              />
            </div>
            {formError && (
              <div className="flex items-center gap-1.5 text-xs text-red-400 mb-3">
                <AlertCircle size={13} />
                {formError}
              </div>
            )}
            <div className="flex gap-2 justify-end">
              <button onClick={cancelForm} className="h-7 px-3 text-xs text-gray-400 hover:text-white border border-gray-600 rounded transition-colors">
                İptal
              </button>
              <button
                onClick={handleSave}
                disabled={saving}
                className="h-7 px-3 text-xs font-medium text-white bg-blue-600 hover:bg-blue-700 rounded disabled:opacity-40 flex items-center gap-1.5 transition-colors"
              >
                {saving
                  ? <span className="inline-block w-3 h-3 border-2 border-white border-t-transparent rounded-full animate-spin" />
                  : <Save size={12} />
                }
                Kaydet
              </button>
            </div>
          </div>
        )}

        {/* List */}
        <div className="flex-1 overflow-y-auto">
          {loading && (
            <div className="px-5 py-8 text-center text-xs text-gray-400">Yükleniyor…</div>
          )}
          {!loading && error && (
            <div className="px-5 py-4 text-xs text-red-400 flex items-center gap-2">
              <AlertCircle size={14} />
              {error}
            </div>
          )}
          {!loading && !error && snippets.length === 0 && (
            <div className="px-5 py-10 text-center text-xs text-gray-500">
              Henüz snippet yok. "Yeni Snippet" ile başlayın.
            </div>
          )}
          {!loading && snippets.length > 0 && (() => {
            const personal = snippets.filter(s => !s.isPublic)
            const library = snippets.filter(s => s.isPublic)
            return (
              <table className="w-full text-xs">
                <thead className="sticky top-0 bg-gray-800 border-b border-gray-700">
                  <tr>
                    <th className="px-4 py-2 text-left text-gray-400 font-medium">Prefix</th>
                    <th className="px-4 py-2 text-left text-gray-400 font-medium w-16">Kapsam</th>
                    <th className="px-4 py-2 text-left text-gray-400 font-medium">Açıklama</th>
                    <th className="px-4 py-2 text-right text-gray-400 font-medium w-20"></th>
                  </tr>
                </thead>
                <tbody className="divide-y divide-gray-800">
                  {personal.map(s => (
                    <tr key={s.id} className="hover:bg-gray-800/50 group">
                      <td className="px-4 py-2.5 font-mono text-blue-300">{s.prefix}</td>
                      <td className="px-4 py-2.5">
                        <span className={`inline-block px-1.5 py-0.5 rounded text-[10px] font-medium ${SCOPE_COLORS[s.scope] ?? 'bg-gray-700 text-gray-300'}`}>
                          {SCOPE_LABELS[s.scope] ?? s.scope}
                        </span>
                      </td>
                      <td className="px-4 py-2.5 text-gray-400 truncate max-w-[200px]">
                        {s.description || <span className="text-gray-600 italic">—</span>}
                      </td>
                      <td className="px-4 py-2.5 text-right">
                        <div className="flex items-center justify-end gap-1 opacity-0 group-hover:opacity-100 transition-opacity">
                          <button
                            onClick={() => openEdit(s)}
                            className="p-1 text-gray-400 hover:text-white rounded hover:bg-gray-700 transition-colors"
                            title="Düzenle"
                          >
                            <Pencil size={12} />
                          </button>
                          <button
                            onClick={() => handleDelete(s.id)}
                            disabled={deletingId === s.id}
                            className="p-1 text-gray-400 hover:text-red-400 rounded hover:bg-gray-700 transition-colors disabled:opacity-40"
                            title="Sil"
                          >
                            <Trash2 size={12} />
                          </button>
                        </div>
                      </td>
                    </tr>
                  ))}
                  {library.length > 0 && (
                    <>
                      <tr>
                        <td colSpan={4} className="px-4 py-1.5 bg-gray-800/80 border-t border-gray-700">
                          <div className="flex items-center gap-1.5 text-[10px] font-medium text-indigo-400 tracking-wide">
                            <Library size={11} />
                            XsltCraft Kütüphanesi
                          </div>
                        </td>
                      </tr>
                      {library.map(s => (
                        <tr key={s.id} className="hover:bg-gray-800/50">
                          <td className="px-4 py-2.5 font-mono text-indigo-300">{s.prefix}</td>
                          <td className="px-4 py-2.5">
                            <span className={`inline-block px-1.5 py-0.5 rounded text-[10px] font-medium ${SCOPE_COLORS[s.scope] ?? 'bg-gray-700 text-gray-300'}`}>
                              {SCOPE_LABELS[s.scope] ?? s.scope}
                            </span>
                          </td>
                          <td className="px-4 py-2.5 text-gray-400 truncate max-w-[200px]">
                            {s.description || <span className="text-gray-600 italic">—</span>}
                          </td>
                          <td className="px-4 py-2.5 text-right">
                            <span title="Sistem yöneticisi tarafından tanımlanmıştır.">
                              <Star size={14} className="text-purple-400 fill-purple-400 cursor-default" />
                            </span>
                          </td>
                        </tr>
                      ))}
                    </>
                  )}
                </tbody>
              </table>
            )
          })()}
        </div>

        {/* Footer */}
        <div className="px-5 py-3 border-t border-gray-700 flex-shrink-0">
          <p className="text-[11px] text-gray-500">
            Snippet'ler editörde prefix yazınca otomatik tamamlamada görünür. Tab duraklarını <span className="font-mono text-gray-400">$1</span>, <span className="font-mono text-gray-400">$2</span> ile işaretleyin; son konum için <span className="font-mono text-gray-400">$0</span>.
          </p>
        </div>
      </div>
    </div>
  )
}
