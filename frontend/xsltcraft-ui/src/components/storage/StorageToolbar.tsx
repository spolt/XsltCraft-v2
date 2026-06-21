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
}

export default function StorageToolbar({ search, onSearch, sort, onSort, placeholder }: StorageToolbarProps) {
  return (
    <div className="flex items-center gap-3 mb-4">
      <div className="relative flex-1">
        <Search size={15} className="absolute left-3 top-1/2 -translate-y-1/2 text-gray-400 pointer-events-none" />
        <input
          type="text"
          value={search}
          onChange={(e) => onSearch(e.target.value)}
          placeholder={placeholder ?? 'Şablonlarda ara…'}
          className="w-full border border-gray-300 rounded-lg pl-9 pr-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500 focus:border-blue-500"
        />
      </div>
      <label className="flex items-center gap-1.5 text-xs text-gray-400 flex-shrink-0">
        <span className="hidden sm:inline">Sırala</span>
        <select
          value={sort}
          onChange={(e) => onSort(e.target.value as SortKey)}
          className="border border-gray-300 rounded-lg px-2 py-2 text-sm text-gray-700 bg-white focus:outline-none focus:ring-2 focus:ring-blue-500 focus:border-blue-500 cursor-pointer"
        >
          {(Object.keys(SORT_LABELS) as SortKey[]).map((key) => (
            <option key={key} value={key}>{SORT_LABELS[key]}</option>
          ))}
        </select>
      </label>
    </div>
  )
}
