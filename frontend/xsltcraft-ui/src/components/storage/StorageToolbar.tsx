import { useId, type ReactNode } from 'react'
import { Search } from 'lucide-react'
import type { SortKey } from './useTemplateLibrary'

const SORT_LABELS: Record<SortKey, string> = {
  updated: 'Son güncelleme',
  created: 'Oluşturulma',
  name: 'Ad (A–Z)',
}

interface StorageToolbarProps {
  search: string
  onSearch: (value: string) => void
  sort: SortKey
  onSort: (sort: SortKey) => void
  placeholder?: string
  /** Sağa hizalanan ek eylemler (ör. Toplu yükle). */
  children?: ReactNode
}

/** Liste kartının üst şeridi: arama + sıralama (+ sağda eylemler). */
export default function StorageToolbar({ search, onSearch, sort, onSort, placeholder, children }: StorageToolbarProps) {
  const sortId = useId()
  const label = placeholder ?? 'Şablonlarda ara…'
  return (
    <div className="flex flex-wrap items-center gap-2 border-b border-ui-line px-3.5 py-3">
      <label className="flex h-9 min-w-0 flex-[0_1_300px] items-center gap-2 rounded-[7px] border border-field-line bg-ui-card px-2.5 text-tx-subtle focus-within:border-ui-accent focus-within:ring-2 focus-within:ring-ui-soft-line">
        <Search size={15} strokeWidth={1.8} aria-hidden="true" className="flex-none" />
        <span className="sr-only">{label.replace(/…$/, '')}</span>
        <input
          type="search"
          value={search}
          onChange={(e) => onSearch(e.target.value)}
          placeholder={label}
          className="min-w-0 flex-1 border-0 bg-transparent text-[13px] text-tx outline-none placeholder:text-tx-subtle"
        />
      </label>
      <div className="flex items-center gap-1.5">
        <label htmlFor={sortId} className="text-[12.5px] text-tx-muted">Sırala</label>
        <select
          id={sortId}
          value={sort}
          onChange={(e) => onSort(e.target.value as SortKey)}
          className="xc-focus h-9 cursor-pointer rounded-[7px] border border-ui-line bg-ui-card px-2 text-[12.5px] text-tx-2"
        >
          {(Object.keys(SORT_LABELS) as SortKey[]).map((key) => (
            <option key={key} value={key}>{SORT_LABELS[key]}</option>
          ))}
        </select>
      </div>
      {children && <div className="ml-auto flex flex-wrap items-center gap-2">{children}</div>}
    </div>
  )
}
