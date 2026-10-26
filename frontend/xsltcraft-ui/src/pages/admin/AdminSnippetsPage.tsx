import { useEffect, useState } from 'react'
import { Loader2, Pencil, Plus, Save, Trash2 } from 'lucide-react'
import {
  listAdminSnippets,
  createAdminSnippet,
  updateAdminSnippet,
  deleteAdminSnippet,
  type UserSnippet,
  type CreateSnippetRequest,
} from '../../services/snippetService'
import PageHeader from '../../components/ui/PageHeader'
import Card from '../../components/ui/Card'
import type { FnColor } from '../../theme/uiThemes'
import {
  AdminPage, Banner, BTN_DANGER, BTN_DANGER_SOFT, BTN_ICON, BTN_ICON_DANGER, BTN_PRIMARY, BTN_SECONDARY, INPUT, LABEL,
  Modal, Pill, SectionHeader, SegmentGroup, TABLE, TD, TH,
} from '../../components/admin/adminUi'

type Scope = 'xslt' | 'xpath' | 'html'

const SCOPE_LABELS: Record<string, string> = { xslt: 'XSLT', xpath: 'XPath', html: 'HTML' }
const SCOPE_TONES: Record<string, FnColor> = { xslt: 'purple', xpath: 'blue', html: 'orange' }
const SCOPE_OPTIONS = [
  { value: 'xslt', label: 'XSLT' },
  { value: 'xpath', label: 'XPath' },
  { value: 'html', label: 'HTML' },
] as const

interface FormState {
  prefix: string
  body: string
  description: string
  scope: Scope
}

const EMPTY_FORM: FormState = { prefix: '', body: '', description: '', scope: 'xslt' }

// ---- Snippet form (oluştur / düzenle) ----
function SnippetForm({ initial, onSave, onCancel, onDelete }: {
  initial?: UserSnippet
  onSave: (s: UserSnippet) => void
  onCancel: () => void
  onDelete?: () => void
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
    <Card as="section" padding="none" aria-labelledby="snippet-form-title" className="flex min-w-0 flex-[1_1_340px] flex-col gap-3 p-[18px] lg:sticky lg:top-4">
      <h2 id="snippet-form-title" className="m-0 text-[15px] font-semibold text-tx">
        {initial ? 'Snippet düzenle' : 'Yeni kütüphane snippet\'i'}
      </h2>

      {error && <Banner>{error}</Banner>}

      <label className={LABEL}>
        Prefix (tetikleyici kelime)
        <input
          value={form.prefix}
          onChange={e => setForm(f => ({ ...f, prefix: e.target.value }))}
          placeholder="örn: foreach-inv"
          autoFocus
          className={`${INPUT} font-mono`}
        />
      </label>

      <label className={LABEL}>
        Açıklama (opsiyonel)
        <input
          value={form.description}
          onChange={e => setForm(f => ({ ...f, description: e.target.value }))}
          placeholder="Snippet'in ne yaptığını kısaca açıklayın"
          className={INPUT}
        />
      </label>

      <div className={LABEL}>
        <span>Kapsam</span>
        <SegmentGroup label="Kapsam" options={SCOPE_OPTIONS} value={form.scope} onChange={scope => setForm(f => ({ ...f, scope }))} />
      </div>

      <label className={LABEL}>
        İçerik
        <textarea
          value={form.body}
          onChange={e => setForm(f => ({ ...f, body: e.target.value }))}
          rows={8}
          spellCheck={false}
          placeholder={'<xsl:for-each select="$1">\n  $0\n</xsl:for-each>'}
          className="w-full resize-y rounded-lg border border-ed-border bg-ed-bg px-3 py-2.5 font-mono text-[12px] leading-[1.7] text-ed-text outline-none placeholder:text-ed-muted focus:ring-2 focus:ring-ui-soft-line"
        />
        <span className="text-[11.5px] font-normal text-tx-muted">Tab durağı için $1, $2 … — son konum $0</span>
      </label>

      <div className="flex flex-wrap gap-2">
        <button type="button" onClick={handleSave} disabled={saving} className={`${BTN_PRIMARY} flex-1`}>
          {saving ? <Loader2 size={14} className="animate-spin" aria-hidden="true" /> : <Save size={14} aria-hidden="true" />}
          Kaydet
        </button>
        <button type="button" onClick={onCancel} className={`${BTN_SECONDARY} h-10! px-4!`}>İptal</button>
        {onDelete && (
          <button type="button" onClick={onDelete} className={`${BTN_DANGER_SOFT} h-10!`}>
            <Trash2 size={14} aria-hidden="true" />Snippet'i Sil
          </button>
        )}
      </div>
    </Card>
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
      if (editingSnippet?.id === deletingSnippet.id) setEditingSnippet(null)
      setDeletingSnippet(null)
    } catch {
      // modal açık kalır
    } finally {
      setDeleteLoading(false)
    }
  }

  const formOpen = showForm || editingSnippet !== null

  return (
    <AdminPage>
      <PageHeader
        title="Snippet Kütüphanesi"
        description="Tüm kullanıcılara görünecek ortak snippet'ları yönetin — XSLT editöründe prefix yazarak eklenir."
        actions={
          <button
            type="button"
            onClick={() => { setEditingSnippet(null); setShowForm(true) }}
            disabled={showForm}
            className={BTN_PRIMARY}
          >
            <Plus size={15} aria-hidden="true" />
            Yeni Snippet Ekle
          </button>
        }
      />

      <div className="flex flex-wrap items-start gap-4">
        <Card as="section" padding="none" aria-labelledby="snippet-list-title" className="min-w-0 flex-[999_1_520px]">
          <SectionHeader
            id="snippet-list-title"
            title="Kütüphane snippet'ları"
            aside={snippets.length > 0 && <span className="text-[12.5px] text-tx-muted">{snippets.length} snippet</span>}
          />

          {error && <div className="px-4 pt-3"><Banner>{error}</Banner></div>}

          {loading && (
            <div className="flex items-center justify-center gap-2 py-12 text-[13px] text-tx-muted">
              <Loader2 size={15} className="animate-spin" aria-hidden="true" /> Yükleniyor…
            </div>
          )}

          {!loading && !error && snippets.length === 0 && (
            <p className="m-0 px-4 py-10 text-center text-[13px] text-tx-muted">
              Henüz kütüphane snippet'ı yok. "Yeni Snippet Ekle" ile başlayın.
            </p>
          )}

          {!loading && snippets.length > 0 && (
            <div className="overflow-x-auto">
              <table className={TABLE}>
                <thead>
                  <tr>
                    <th scope="col" className={`${TH} px-4!`}>Prefix</th>
                    <th scope="col" className={TH}>Açıklama</th>
                    <th scope="col" className={TH}>Kapsam</th>
                    <th scope="col" className={TH}>Eklenme</th>
                    <th scope="col" className={`${TH} px-4! text-right!`}><span className="sr-only">İşlem</span></th>
                  </tr>
                </thead>
                <tbody>
                  {snippets.map(s => {
                    const selected = editingSnippet?.id === s.id
                    return (
                      <tr
                        key={s.id}
                        onClick={() => { setEditingSnippet(s); setShowForm(false) }}
                        className={`cursor-pointer border-t border-ui-line transition-colors ${selected ? 'bg-ui-soft' : 'hover:bg-ui-hover'}`}
                      >
                        <td className={`${TD} px-4!`}>
                          <span className="rounded-md bg-fn-slate-tint px-2 py-0.5 font-mono text-[12px] text-fn-slate-ink">{s.prefix}</span>
                        </td>
                        <td className={`${TD} max-w-[280px] truncate text-tx-2`}>
                          {s.description || <span className="text-tx-subtle">—</span>}
                        </td>
                        <td className={TD}>
                          <Pill tone={SCOPE_TONES[s.scope] ?? 'gray'}>{SCOPE_LABELS[s.scope] ?? s.scope}</Pill>
                        </td>
                        <td className={`${TD} text-[12px] text-tx-muted`}>{new Date(s.createdAt).toLocaleDateString('tr-TR')}</td>
                        <td className={`${TD} px-4!`} onClick={e => e.stopPropagation()}>
                          <span className="flex justify-end gap-0.5">
                            <button
                              type="button"
                              aria-label={`${s.prefix}: düzenle`}
                              aria-pressed={selected}
                              title="Düzenle"
                              onClick={() => { setEditingSnippet(s); setShowForm(false) }}
                              className={BTN_ICON}
                            >
                              <Pencil size={15} aria-hidden="true" />
                            </button>
                            <button
                              type="button"
                              aria-label={`${s.prefix}: sil`}
                              title="Sil"
                              onClick={() => setDeletingSnippet(s)}
                              className={BTN_ICON_DANGER}
                            >
                              <Trash2 size={15} aria-hidden="true" />
                            </button>
                          </span>
                        </td>
                      </tr>
                    )
                  })}
                </tbody>
              </table>
            </div>
          )}
        </Card>

        {formOpen && (
          <SnippetForm
            key={editingSnippet?.id ?? 'new'}
            initial={editingSnippet ?? undefined}
            onSave={handleSaved}
            onCancel={() => { setShowForm(false); setEditingSnippet(null) }}
            onDelete={editingSnippet ? () => setDeletingSnippet(editingSnippet) : undefined}
          />
        )}
      </div>

      {deletingSnippet && (
        <Modal
          titleId="delete-snippet-title"
          title="Snippet'i Sil"
          onClose={() => setDeletingSnippet(null)}
          footer={
            <>
              <button type="button" onClick={() => setDeletingSnippet(null)} className={`${BTN_SECONDARY} h-10! px-4!`}>İptal</button>
              <button type="button" onClick={() => void handleDeleteConfirm()} disabled={deleteLoading} className={BTN_DANGER}>
                {deleteLoading ? 'Siliniyor…' : 'Evet, Sil'}
              </button>
            </>
          }
        >
          <p className="m-0 text-[13px] text-tx-2">
            <span className="font-mono font-semibold text-tx">{deletingSnippet.prefix}</span> snippet'i kütüphaneden kalıcı olarak silinecek.
          </p>
        </Modal>
      )}
    </AdminPage>
  )
}
