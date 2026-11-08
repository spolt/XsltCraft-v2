import { useState } from 'react'
import { Link } from 'react-router-dom'
import { ArrowRight, Clock3, FileCode2, FileText, LayoutTemplate, Truck, type LucideIcon } from 'lucide-react'
import type { WorkItem } from '../../hooks/useRecentWork'
import { DOC_TYPE_LABEL } from '../../utils/workRoutes'
import { formatWhen } from './formatWhen'

type Filter = 'all' | 'xslt' | 'draft'

const FILTERS: { id: Filter; label: string }[] = [
  { id: 'all', label: 'Tümü' },
  { id: 'xslt', label: 'XSLT' },
  { id: 'draft', label: 'Taslak' },
]

const DOC_TYPE_SHORT: Record<string, string> = { Invoice: 'e-Fatura', Despatch: 'e-İrsaliye' }

/** Liste kendi içinde kayar; tamamı "Tümünü gör"de. */
const MAX_ROWS = 40

interface Props {
  /** Yeniden eskiye taslak + XSLT. */
  items: WorkItem[]
  loading: boolean
  error: boolean
  className?: string
}

function iconOf(item: WorkItem): LucideIcon {
  if (item.kind === 'xslt') return FileCode2
  if (item.source === 'theme') return LayoutTemplate
  return item.documentType === 'Despatch' ? Truck : FileText
}

/** Alt satır: tür + bağlam ("XSLT · Kolay düzenle", "e-Fatura · Temadan"). */
function metaOf(item: WorkItem): string {
  const parts: string[] = []
  if (item.kind === 'xslt') {
    parts.push('XSLT')
    if (item.lastEditMode === 'easyEdit') parts.push('Kolay düzenle')
    if (item.isShared) parts.push('Paylaşılan')
  } else {
    parts.push(item.documentType ? DOC_TYPE_SHORT[item.documentType] : 'Taslak')
    if (item.source === 'theme') parts.push('Temadan')
  }
  return parts.join(' · ')
}

/** Son düzenlenenler: nötr ikon · ad/tür · tarih; üzerine gelince ok. */
export default function RecentList({ items, loading, error, className = '' }: Props) {
  const [filter, setFilter] = useState<Filter>('all')
  const rows = (filter === 'all' ? items : items.filter((i) => i.kind === filter)).slice(0, MAX_ROWS)

  const empty = error
    ? 'Liste yüklenemedi.'
    : filter === 'all'
      ? 'Henüz bir çalışman yok.'
      : filter === 'xslt'
        ? 'Kayıtlı XSLT dosyan yok.'
        : 'Kayıtlı taslağın yok.'

  return (
    <section
      aria-labelledby="dash-recent-title"
      aria-busy={loading}
      className={`flex min-w-0 flex-col overflow-hidden rounded-xl border border-ui-line bg-ui-card ${className}`}
    >
      <div className="flex flex-wrap items-center justify-between gap-x-3 gap-y-2 border-b border-ui-line px-4 py-3">
        <h2 id="dash-recent-title" className="m-0 text-[14px] font-semibold text-tx">Son düzenlenenler</h2>
        <div role="group" aria-label="Listeyi filtrele" className="flex rounded-lg bg-ui-side p-0.5">
          {FILTERS.map((f) => {
            const on = f.id === filter
            return (
              <button
                key={f.id}
                type="button"
                aria-pressed={on}
                onClick={() => setFilter(f.id)}
                className={`xc-focus h-7 cursor-pointer rounded-md px-3 text-[12.5px] transition-colors ${
                  on
                    ? 'bg-ui-card font-semibold text-tx shadow-[0_1px_2px_rgba(15,36,64,0.10)] ring-1 ring-ui-line'
                    : 'font-medium text-tx-muted hover:text-tx'
                }`}
              >
                {f.label}
              </button>
            )
          })}
        </div>
      </div>

      <div className="max-h-[480px] min-h-0 flex-1 overflow-y-auto p-2 xl:max-h-none">
        {loading && rows.length === 0 ? (
          <ul aria-hidden="true" className="m-0 flex list-none flex-col gap-0.5 p-0">
            {[0, 1, 2, 3, 4].map((i) => (
              <li key={i} className="flex h-[52px] items-center gap-3 px-2.5">
                <span className="h-8 w-8 flex-none animate-pulse rounded-lg bg-ui-side" />
                <span className="flex flex-1 flex-col gap-1.5">
                  <span className="h-2.5 w-2/5 animate-pulse rounded bg-ui-side" />
                  <span className="h-2 w-1/4 animate-pulse rounded bg-ui-side" />
                </span>
              </li>
            ))}
          </ul>
        ) : rows.length === 0 ? (
          <div className="flex h-full min-h-[160px] flex-col items-center justify-center gap-2.5 px-4 text-center">
            <span aria-hidden="true" className="flex h-10 w-10 items-center justify-center rounded-lg bg-ui-side text-tx-muted">
              <Clock3 size={18} strokeWidth={1.75} />
            </span>
            <p className="m-0 text-[13px] text-tx-muted">{empty}</p>
          </div>
        ) : (
          <ul className="m-0 flex list-none flex-col gap-0.5 p-0">
            {rows.map((r) => {
              const Icon = iconOf(r)
              return (
                <li key={r.key}>
                  <Link
                    to={r.to}
                    title={r.name}
                    className="xc-focus group flex h-[52px] min-w-0 items-center gap-3 rounded-lg px-2.5 text-tx no-underline transition-colors duration-150 hover:bg-ui-hover"
                  >
                    <span
                      aria-hidden="true"
                      className="flex h-8 w-8 flex-none items-center justify-center rounded-lg bg-ui-side text-tx-muted transition-colors duration-150 group-hover:bg-ui-card group-hover:text-tx"
                    >
                      <Icon size={16} strokeWidth={1.75} />
                    </span>
                    <span className="flex min-w-0 flex-1 flex-col gap-0.5">
                      <span className="truncate text-[13px] font-medium leading-tight text-tx">{r.name}</span>
                      <span
                        className="truncate text-[11.5px] leading-tight text-tx-muted"
                        title={r.documentType ? DOC_TYPE_LABEL[r.documentType] : undefined}
                      >
                        {metaOf(r)}
                      </span>
                    </span>
                    <span className="flex-none text-[12px] tabular-nums text-tx-muted">{formatWhen(r.updatedAt)}</span>
                    <ArrowRight
                      aria-hidden="true"
                      size={15}
                      strokeWidth={2}
                      className="flex-none -translate-x-1 text-tx-subtle opacity-0 transition-[opacity,translate,color] duration-150 group-hover:translate-x-0 group-hover:text-tx group-hover:opacity-100 group-focus-visible:translate-x-0 group-focus-visible:opacity-100"
                    />
                  </Link>
                </li>
              )
            })}
          </ul>
        )}
        {loading && <span className="sr-only">Yükleniyor…</span>}
      </div>

      <Link
        to="/drafts"
        className="xc-focus group flex h-11 flex-none items-center justify-between border-t border-ui-line px-4 text-[12.5px] font-medium text-tx-muted no-underline transition-colors duration-150 hover:bg-ui-hover hover:text-tx"
      >
        Tüm çalışmaları gör
        <ArrowRight
          aria-hidden="true"
          size={15}
          strokeWidth={2}
          className="transition-[translate] duration-150 group-hover:translate-x-0.5"
        />
      </Link>
    </section>
  )
}
