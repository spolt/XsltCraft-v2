import { useState } from 'react'
import { Link } from 'react-router-dom'
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

/** Tür sütunu: XSLT → "XSLT"; taslak → belge türü (+ temadansa soluk " · Temadan"). */
function TypeCell({ item }: { item: WorkItem }) {
  if (item.kind === 'xslt') return <>XSLT</>
  const doc = item.documentType ? DOC_TYPE_SHORT[item.documentType] : 'Taslak'
  return (
    <>
      {doc}
      {item.source === 'theme' && <span className="text-tx-subtle"> · Temadan</span>}
    </>
  )
}

/** Son düzenlenenler: kompakt tek liste (Ad · Tür · Güncellendi). */
export default function RecentList({ items, loading, error, className = '' }: Props) {
  const [filter, setFilter] = useState<Filter>('all')
  const rows = (filter === 'all' ? items : items.filter((i) => i.kind === filter)).slice(0, MAX_ROWS)

  const empty = loading
    ? 'Yükleniyor…'
    : error
      ? 'Liste yüklenemedi.'
      : filter === 'all'
        ? 'Henüz bir çalışman yok.'
        : filter === 'xslt'
          ? 'Kayıtlı XSLT dosyan yok.'
          : 'Kayıtlı taslağın yok.'

  return (
    <section
      aria-labelledby="dash-recent-title"
      className={`flex min-w-0 flex-col overflow-hidden rounded-xl border border-ui-line bg-ui-card ${className}`}
    >
      <div className="flex items-center justify-between gap-3 px-4 pt-3.5 pb-2.5">
        <h2 id="dash-recent-title" className="m-0 text-[14px] font-semibold text-tx">Son düzenlenenler</h2>
        <Link to="/drafts" className="xc-focus rounded text-[12.5px] font-medium text-ui-ink no-underline hover:underline">
          Tümünü gör
        </Link>
      </div>

      <div role="group" aria-label="Listeyi filtrele" className="flex gap-1 px-4 pb-2.5">
        {FILTERS.map((f) => {
          const on = f.id === filter
          return (
            <button
              key={f.id}
              type="button"
              aria-pressed={on}
              onClick={() => setFilter(f.id)}
              className={`xc-focus h-7 cursor-pointer rounded-md px-2.5 text-[12.5px] transition-colors ${
                on ? 'bg-ui-hover font-semibold text-tx' : 'font-medium text-tx-muted hover:text-tx'
              }`}
            >
              {f.label}
            </button>
          )
        })}
      </div>

      {rows.length === 0 ? (
        <p className="m-0 flex-1 border-t border-ui-line px-4 py-6 text-center text-[13px] text-tx-subtle">{empty}</p>
      ) : (
        <div className="max-h-[480px] min-h-0 flex-1 overflow-y-auto border-t border-ui-line xl:max-h-none">
          <table className="w-full table-fixed border-collapse text-[13px]">
            <colgroup>
              <col />
              <col className="hidden w-[132px] sm:table-column" />
              <col className="w-[96px]" />
            </colgroup>
            <thead className="sr-only">
              <tr>
                <th scope="col">Ad</th>
                <th scope="col" className="hidden sm:table-cell">Tür</th>
                <th scope="col">Güncellendi</th>
              </tr>
            </thead>
            <tbody>
              {rows.map((r) => (
                <tr key={r.key} className="relative h-[40px] border-b border-ui-line transition-colors hover:bg-ui-hover">
                  <td className="truncate pr-2 pl-4">
                    <Link
                      to={r.to}
                      title={r.name}
                      className={`xc-focus rounded-sm text-tx no-underline after:absolute after:inset-0 after:content-[''] ${
                        r.kind === 'xslt' ? 'font-mono text-[12.5px]' : 'font-medium'
                      }`}
                    >
                      {r.name}
                    </Link>
                  </td>
                  <td
                    className="hidden truncate px-1.5 text-[12px] text-tx-2 sm:table-cell"
                    title={r.documentType ? DOC_TYPE_LABEL[r.documentType] : undefined}
                  >
                    <TypeCell item={r} />
                  </td>
                  <td className="truncate pr-4 pl-1.5 text-right text-[12px] tabular-nums text-tx-muted">
                    {formatWhen(r.updatedAt)}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </section>
  )
}
