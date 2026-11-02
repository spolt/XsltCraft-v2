import { useEffect, useState, useCallback } from 'react'
import { Check, ChevronDown, Globe, Loader2, Search, ThumbsDown, ThumbsUp, Trash2 } from 'lucide-react'
import {
  getAdminAiFeedback,
  setAiFeedbackGlobal,
  deleteAiFeedback,
  type AdminAiFeedbackItem,
} from '../../services/aiAssistantService'
import PageHeader from '../../components/ui/PageHeader'
import { fnStyle } from '../../theme/uiThemes'
import { AdminPage, Banner, BTN_DANGER_SOFT, BTN_SECONDARY, INPUT, Pill } from '../../components/admin/adminUi'

type RatingFilter = 'positive' | 'negative' | 'all'

/** Görselli soru işareti (backend AiFeedbackMarkers.ImageAttached) — bu kayıtlar global yapılamaz. */
const IMAGE_MARKER = '[ekran görüntüsü ekli]'
const hasImage = (item: AdminAiFeedbackItem) => item.userMessage.includes(IMAGE_MARKER)

const GLOBAL_ON =
  'xc-focus inline-flex h-9 items-center justify-center gap-1.5 rounded-lg border border-ui-soft-line bg-ui-soft px-3 text-[12.5px] font-semibold text-ui-ink transition-opacity hover:opacity-90 disabled:cursor-not-allowed disabled:opacity-50'

const TABS: { key: RatingFilter; label: string }[] = [
  { key: 'positive', label: 'İşe yaradı' },
  { key: 'negative', label: 'İşe yaramadı' },
  { key: 'all', label: 'Tümü' },
]

export default function AdminAiFeedbackPage() {
  const [items, setItems] = useState<AdminAiFeedbackItem[]>([])
  const [total, setTotal] = useState(0)
  const [rating, setRating] = useState<RatingFilter>('positive')
  const [q, setQ] = useState('')
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)
  const [expanded, setExpanded] = useState<string | null>(null)
  const [busyId, setBusyId] = useState<string | null>(null)

  const load = useCallback(async () => {
    setLoading(true)
    setError(null)
    try {
      const data = await getAdminAiFeedback({ rating, q: q.trim() || undefined, pageSize: 100 })
      setItems(data.items)
      setTotal(data.total)
    } catch {
      setError('Geri bildirimler yüklenemedi.')
    } finally {
      setLoading(false)
    }
  }, [rating, q])

  // Filtre değişince hemen; arama için kısa debounce.
  useEffect(() => {
    const t = setTimeout(load, 300)
    return () => clearTimeout(t)
  }, [load])

  async function toggleGlobal(item: AdminAiFeedbackItem) {
    setBusyId(item.id)
    try {
      await setAiFeedbackGlobal(item.id, !item.isGlobal)
      setItems(prev => prev.map(i => (i.id === item.id ? { ...i, isGlobal: !i.isGlobal } : i)))
    } catch {
      setError('Terfi işlemi başarısız.')
    } finally {
      setBusyId(null)
    }
  }

  async function remove(id: string) {
    if (!confirm('Bu geri bildirim kalıcı olarak silinsin mi?')) return
    setBusyId(id)
    try {
      await deleteAiFeedback(id)
      setItems(prev => prev.filter(i => i.id !== id))
      setTotal(t => t - 1)
    } catch {
      setError('Silme başarısız.')
    } finally {
      setBusyId(null)
    }
  }

  return (
    <AdminPage>
      <PageHeader
        title="AI Geri Bildirim Havuzu"
        description={
          <>
            Kaliteli pozitif örnekleri <strong className="font-semibold text-tx-2">global havuza</strong> terfi edin — global örnekler
            tüm kullanıcıların prompt'una few-shot olarak eklenir.
          </>
        }
      />

      <Banner tone="orange">
        Global yapmadan önce içeriği kontrol edin: soru/cevap metni kişisel veri veya gerçek fatura bilgisi içermemeli.
        Global örnekler diğer kullanıcıların modeline gönderilir.
      </Banner>

      <div className="flex flex-wrap items-center gap-2">
        <label className="relative min-w-[220px] flex-[0_1_320px]">
          <span className="sr-only">Soru metninde ara</span>
          <Search size={15} aria-hidden="true" className="pointer-events-none absolute top-1/2 left-3 -translate-y-1/2 text-tx-subtle" />
          <input type="search" value={q} onChange={e => setQ(e.target.value)} placeholder="Soru metninde ara…" className={`${INPUT} pl-9!`} />
        </label>
        <div role="group" aria-label="Değerlendirme filtresi" className="flex flex-wrap gap-2">
          {TABS.map(t => {
            const on = rating === t.key
            return (
              <button
                key={t.key}
                type="button"
                aria-pressed={on}
                onClick={() => setRating(t.key)}
                className={`xc-focus h-[34px] rounded-full border px-3.5 text-[12.5px] font-medium transition-colors ${
                  on ? 'border-ui-soft-line bg-ui-soft text-ui-ink' : 'border-ui-line bg-ui-card text-tx-2 hover:bg-ui-hover'
                }`}
              >
                {t.label}
              </button>
            )
          })}
        </div>
        <span className="ml-auto text-[12.5px] text-tx-muted" aria-live="polite">{total} kayıt</span>
      </div>

      {error && <Banner>{error}</Banner>}
      {loading && (
        <div className="flex items-center justify-center gap-2 py-12 text-[13px] text-tx-muted">
          <Loader2 size={15} className="animate-spin" aria-hidden="true" /> Yükleniyor…
        </div>
      )}
      {!loading && items.length === 0 && !error && (
        <p className="m-0 py-10 text-center text-[13px] text-tx-muted">Kayıt yok.</p>
      )}

      {!loading && items.length > 0 && (
        <div className="flex flex-col gap-2.5">
          {items.map(item => {
            const open = expanded === item.id
            const positive = item.rating === 'Positive'
            const VoteIcon = positive ? ThumbsUp : ThumbsDown
            const globalBlocked = !item.isGlobal && hasImage(item)
            const panelId = `fb-${item.id}`
            return (
              <article
                key={item.id}
                className={`rounded-xl border bg-ui-card shadow-[var(--ui-shadow-rest)] transition-[border-color,box-shadow] hover:shadow-[var(--ui-shadow-lift)] ${
                  open ? 'border-ui-soft-line' : 'border-ui-line hover:border-ui-soft-line'
                }`}
              >
                <div className="flex flex-wrap items-center gap-3 px-4 py-3">
                  <button
                    type="button"
                    aria-expanded={open}
                    aria-controls={panelId}
                    onClick={() => setExpanded(open ? null : item.id)}
                    title="Detayı aç/kapat"
                    className="xc-focus flex min-w-0 flex-[1_1_320px] items-center gap-3 rounded-lg text-left"
                  >
                    <span
                      style={fnStyle(positive ? 'green' : 'red')}
                      className="flex h-[34px] w-[34px] flex-none items-center justify-center rounded-[9px] bg-(--fn-tint) text-(--fn-ink)"
                    >
                      <VoteIcon size={17} strokeWidth={1.8} role="img" aria-label={positive ? 'İşe yaradı' : 'İşe yaramadı'} />
                    </span>
                    <span className="flex min-w-0 flex-1 flex-col gap-0.5">
                      <span className="truncate text-[13.5px] font-semibold text-tx">{item.userMessage}</span>
                      <span className="text-[12px] text-tx-muted">{item.username || item.email}</span>
                    </span>
                    <ChevronDown
                      size={15}
                      aria-hidden="true"
                      className={`flex-none text-tx-subtle transition-transform ${open ? 'rotate-180' : ''}`}
                    />
                  </button>
                  <div className="flex flex-wrap items-center gap-2">
                    {item.applied && <Pill tone="green"><Check size={11} aria-hidden="true" />Uygulandı</Pill>}
                    {item.isGlobal && <Pill tone="blue"><Globe size={11} aria-hidden="true" />Global</Pill>}
                    {positive && (
                      <button
                        type="button"
                        aria-pressed={item.isGlobal}
                        onClick={() => toggleGlobal(item)}
                        disabled={busyId === item.id || globalBlocked}
                        title={globalBlocked
                          ? 'Ekran görüntülü sorular global yapılamaz: cevap görseldeki kullanıcıya özel verileri içerebilir.'
                          : undefined}
                        className={item.isGlobal ? GLOBAL_ON : BTN_SECONDARY}
                      >
                        <Globe size={13} aria-hidden="true" />
                        {item.isGlobal ? 'Global (kaldır)' : 'Global yap'}
                      </button>
                    )}
                    <button
                      type="button"
                      onClick={() => remove(item.id)}
                      disabled={busyId === item.id}
                      className={BTN_DANGER_SOFT}
                    >
                      <Trash2 size={13} aria-hidden="true" />
                      Sil
                    </button>
                  </div>
                </div>
                {open && (
                  <div id={panelId} className="flex flex-col gap-1.5 px-4 pb-4 sm:pl-[62px]">
                    <span className="text-[12.5px] font-semibold text-tx-2">Cevap:</span>
                    <pre className="m-0 max-h-64 overflow-auto rounded-lg border border-ed-border bg-ed-bg p-3 font-mono text-[11.5px] leading-[1.6] break-words whitespace-pre-wrap text-ed-text">
                      {item.assistantAnswer}
                    </pre>
                  </div>
                )}
              </article>
            )
          })}
        </div>
      )}
    </AdminPage>
  )
}
