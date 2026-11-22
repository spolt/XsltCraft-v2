import { useId } from 'react'
import { ChevronLeft, ChevronRight } from 'lucide-react'
import { PAGE_SIZES } from './usePagedList'

interface LibraryPagerProps {
  total: number
  page: number
  pageSize: number
  totalPages: number
  onPage: (page: number) => void
  onPageSize: (size: number) => void
}

const NAV_BTN =
  'xc-focus inline-flex h-8 items-center justify-center gap-1 rounded-lg border border-ui-line bg-ui-card px-2.5 text-[12.5px] font-medium text-tx-2 transition-colors hover:bg-ui-hover disabled:cursor-not-allowed disabled:opacity-50'

/**
 * Şablon listesi sayfalayıcısı. Boyut seçici için en küçük sayfa boyutunu aşan listelerde görünür;
 * sayfa düğmeleri yalnız birden fazla sayfa varken.
 */
export default function LibraryPager({ total, page, pageSize, totalPages, onPage, onPageSize }: LibraryPagerProps) {
  const sizeId = useId()
  if (total <= PAGE_SIZES[0]) return null

  const from = (page - 1) * pageSize + 1
  const to = Math.min(page * pageSize, total)
  const pageStart = Math.max(1, Math.min(page - 2, totalPages - 4))

  return (
    <nav aria-label="Sayfalama" className="flex flex-wrap items-center justify-between gap-2 border-t border-ui-line px-3.5 py-3">
      <div className="text-[12.5px] text-tx-muted" aria-live="polite">
        {total} şablondan {from}–{to} arası
      </div>
      <div className="flex flex-wrap items-center gap-3">
        {totalPages > 1 && (
          <div className="flex items-center gap-1">
            <button type="button" disabled={page <= 1} onClick={() => onPage(page - 1)} className={NAV_BTN}>
              <ChevronLeft size={14} aria-hidden="true" /> Önceki
            </button>
            {Array.from({ length: Math.min(5, totalPages) }, (_, i) => {
              const p = pageStart + i
              const on = p === page
              return (
                <button
                  key={p}
                  type="button"
                  aria-current={on ? 'page' : undefined}
                  aria-label={`Sayfa ${p}`}
                  onClick={() => onPage(p)}
                  className={`xc-focus h-8 min-w-8 rounded-lg px-2 text-[12.5px] transition-colors ${
                    on ? 'bg-ui-accent font-semibold text-ui-accent-fg' : 'border border-ui-line bg-ui-card text-tx-2 hover:bg-ui-hover'
                  }`}
                >
                  {p}
                </button>
              )
            })}
            <button type="button" disabled={page >= totalPages} onClick={() => onPage(page + 1)} className={NAV_BTN}>
              Sonraki <ChevronRight size={14} aria-hidden="true" />
            </button>
          </div>
        )}
        <div className="flex items-center gap-1.5">
          <label htmlFor={sizeId} className="text-[12.5px] text-tx-muted">Sayfa başına</label>
          <select
            id={sizeId}
            value={pageSize}
            onChange={(e) => onPageSize(Number(e.target.value))}
            className="xc-focus h-8 cursor-pointer rounded-[7px] border border-ui-line bg-ui-card px-2 text-[12.5px] text-tx-2"
          >
            {PAGE_SIZES.map((n) => <option key={n} value={n}>{n}</option>)}
          </select>
        </div>
      </div>
    </nav>
  )
}
