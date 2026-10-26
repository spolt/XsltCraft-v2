import { useState, useEffect, useCallback } from 'react'
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
  xslt: 'bg-fn-blue-tint text-fn-blue-ink',
  xpath: 'bg-fn-purple-tint text-fn-purple-ink',
  html: 'bg-fn-green-tint text-fn-green-ink',
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

  const load = useCallback(async () => {
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
  }, [onSnippetsChanged])

  useEffect(() => {
    load()
  }, [load])

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
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/50">
      <div className="w-full max-w-2xl bg-ui-card border border-ui-line rounded-xl shadow-2xl flex flex-col max-h-[85vh]">
        {/* Header */}
        <div className="flex items-center gap-2 px-5 py-4 border-b border-ui-line flex-shrink-0">
          <h2 className="text-sm font-semibold text-tx flex-1">Snippet Kütüphanesi</h2>
          <button
            onClick={openCreate}
            className="flex items-center gap-1.5 h-7 px-3 rounded bg-ui-accent hover:brightness-110 text-white text-xs font-medium transition-colors"
          >
            <Plus size={13} />
            Yeni Snippet
          </button>
          <button onClick={onClose} className="text-tx-muted hover:text-tx ml-1" title="Kapat">
            <X size={16} />
          </button>
        </div>

        {/* Form */}
        {showForm && (
          <div className="px-5 py-4 border-b border-ui-line bg-ui-side flex-shrink-0">
            <p className="text-xs font-medium text-tx-2 mb-3">
              {editingId ? 'Snippet Düzenle' : 'Yeni Snippet'}
            </p>
            <div className="grid grid-cols-2 gap-3 mb-3">
              <div>
                <label className="block text-[11px] text-tx-muted mb-1">Prefix (tetikleyici kelime)</label>
                <input
                  value={form.prefix}
                  onChange={e => setForm(f => ({ ...f, prefix: e.target.value }))}
                  placeholder="örn: foreach-inv"
                  className="w-full bg-ui-card border border-field-line rounded px-2 py-1.5 text-xs text-tx font-mono focus:outline-none focus:border-ui-accent"
                  autoFocus
                />
              </div>
              <div>
                <label className="block text-[11px] text-tx-muted mb-1">Kapsam</label>
                <select
                  value={form.scope}
                  onChange={e => setForm(f => ({ ...f, scope: e.target.value as FormState['scope'] }))}
                  className="w-full bg-ui-card border border-field-line rounded px-2 py-1.5 text-xs text-tx focus:outline-none focus:border-ui-accent"
                >
                  <option value="xslt">XSLT</option>
                  <option value="xpath">XPath</option>
                  <option value="html">HTML</option>
                </select>
              </div>
            </div>
            <div className="mb-3">
              <label className="block text-[11px] text-tx-muted mb-1">
                İçerik <span className="text-tx-subtle">(tab durağı için $1, $2 … kullanın)</span>
              </label>
              <textarea
                value={form.body}
                onChange={e => setForm(f => ({ ...f, body: e.target.value }))}
                rows={5}
                placeholder={'<xsl:for-each select="$1">\n  $0\n</xsl:for-each>'}
                className="w-full bg-ui-card border border-field-line rounded px-2 py-1.5 text-xs text-tx font-mono resize-y focus:outline-none focus:border-ui-accent"
              />
            </div>
            <div className="mb-3">
              <label className="block text-[11px] text-tx-muted mb-1">Açıklama (opsiyonel)</label>
              <input
                value={form.description}
                onChange={e => setForm(f => ({ ...f, description: e.target.value }))}
                placeholder="Snippet'in ne yaptığını kısaca açıklayın"
                className="w-full bg-ui-card border border-field-line rounded px-2 py-1.5 text-xs text-tx focus:outline-none focus:border-ui-accent"
              />
            </div>
            {formError && (
              <div className="flex items-center gap-1.5 text-xs text-fn-red-ink mb-3">
                <AlertCircle size={13} />
                {formError}
              </div>
            )}
            <div className="flex gap-2 justify-end">
              <button onClick={cancelForm} className="h-7 px-3 text-xs text-tx-muted hover:text-tx border border-field-line rounded transition-colors">
                İptal
              </button>
              <button
                onClick={handleSave}
                disabled={saving}
                className="h-7 px-3 text-xs font-medium text-white bg-ui-accent hover:brightness-110 rounded disabled:opacity-40 flex items-center gap-1.5 transition-colors"
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
            <div className="px-5 py-8 text-center text-xs text-tx-muted">Yükleniyor…</div>
          )}
          {!loading && error && (
            <div className="px-5 py-4 text-xs text-fn-red-ink flex items-center gap-2">
              <AlertCircle size={14} />
              {error}
            </div>
          )}
          {!loading && !error && snippets.length === 0 && (
            <div className="px-5 py-10 text-center text-xs text-tx-subtle">
              Henüz snippet yok. "Yeni Snippet" ile başlayın.
            </div>
          )}
          {!loading && snippets.length > 0 && (() => {
            const personal = snippets.filter(s => !s.isPublic)
            const library = snippets.filter(s => s.isPublic)
            return (
              <table className="w-full text-xs">
                <thead className="sticky top-0 bg-ui-side border-b border-ui-line">
                  <tr>
                    <th className="px-4 py-2 text-left text-tx-muted font-medium">Prefix</th>
                    <th className="px-4 py-2 text-left text-tx-muted font-medium w-16">Kapsam</th>
                    <th className="px-4 py-2 text-left text-tx-muted font-medium">Açıklama</th>
                    <th className="px-4 py-2 text-right text-tx-muted font-medium w-20"></th>
                  </tr>
                </thead>
                <tbody className="divide-y divide-ui-line">
                  {personal.map(s => (
                    <tr key={s.id} className="hover:bg-ui-hover group">
                      <td className="px-4 py-2.5 font-mono text-fn-blue-ink">{s.prefix}</td>
                      <td className="px-4 py-2.5">
                        <span className={`inline-block px-1.5 py-0.5 rounded text-[10px] font-medium ${SCOPE_COLORS[s.scope] ?? 'bg-ui-card text-tx-2'}`}>
                          {SCOPE_LABELS[s.scope] ?? s.scope}
                        </span>
                      </td>
                      <td className="px-4 py-2.5 text-tx-muted truncate max-w-[200px]">
                        {s.description || <span className="text-tx-subtle italic">—</span>}
                      </td>
                      <td className="px-4 py-2.5 text-right">
                        <div className="flex items-center justify-end gap-1 opacity-0 group-hover:opacity-100 transition-opacity">
                          <button
                            onClick={() => openEdit(s)}
                            className="p-1 text-tx-muted hover:text-tx rounded hover:bg-ui-hover transition-colors"
                            title="Düzenle"
                          >
                            <Pencil size={12} />
                          </button>
                          <button
                            onClick={() => handleDelete(s.id)}
                            disabled={deletingId === s.id}
                            className="p-1 text-tx-muted hover:text-fn-red-ink rounded hover:bg-ui-hover transition-colors disabled:opacity-40"
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
                        <td colSpan={4} className="px-4 py-1.5 bg-ui-side border-t border-ui-line">
                          <div className="flex items-center gap-1.5 text-[10px] font-medium text-fn-violet-ink tracking-wide">
                            <Library size={11} />
                            XsltCraft Kütüphanesi
                          </div>
                        </td>
                      </tr>
                      {library.map(s => (
                        <tr key={s.id} className="hover:bg-ui-hover">
                          <td className="px-4 py-2.5 font-mono text-fn-violet-ink">{s.prefix}</td>
                          <td className="px-4 py-2.5">
                            <span className={`inline-block px-1.5 py-0.5 rounded text-[10px] font-medium ${SCOPE_COLORS[s.scope] ?? 'bg-ui-card text-tx-2'}`}>
                              {SCOPE_LABELS[s.scope] ?? s.scope}
                            </span>
                          </td>
                          <td className="px-4 py-2.5 text-tx-muted truncate max-w-[200px]">
                            {s.description || <span className="text-tx-subtle italic">—</span>}
                          </td>
                          <td className="px-4 py-2.5 text-right">
                            <span title="Sistem yöneticisi tarafından tanımlanmıştır.">
                              <Star size={14} className="text-fn-purple fill-fn-purple cursor-default" />
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
        <div className="px-5 py-3 border-t border-ui-line flex-shrink-0">
          <p className="text-[11px] text-tx-subtle">
            Snippet'ler editörde prefix yazınca otomatik tamamlamada görünür. Tab duraklarını <span className="font-mono text-tx-muted">$1</span>, <span className="font-mono text-tx-muted">$2</span> ile işaretleyin; son konum için <span className="font-mono text-tx-muted">$0</span>.
          </p>
        </div>
      </div>
    </div>
  )
}
