import { useMemo } from 'react'
import type { StorageItem } from '../../services/folderService'

export type SortKey = 'updated' | 'created' | 'name'

/**
 * Taslaklarım / Şablonlarım listelerini istemci tarafında filtreler ve sıralar.
 * - `match`: aktif klasör/favori/paylaşılan seçimine göre öğe filtresi (sayfa belirler; kararlı olması için useCallback ile sarmalayın).
 * - `search`: ada göre büyük/küçük harf duyarsız anlık arama.
 */
export function useTemplateLibrary<T extends StorageItem>(
  items: T[],
  opts: { search: string; match: (item: T) => boolean; sort: SortKey },
): T[] {
  const { search, match, sort } = opts
  return useMemo(() => {
    const q = search.trim().toLowerCase()
    const filtered = items.filter(
      (it) => match(it) && (q === '' || it.name.toLowerCase().includes(q)),
    )
    return [...filtered].sort((a, b) => {
      if (sort === 'name') return a.name.localeCompare(b.name, 'tr')
      const ka = sort === 'created' ? a.createdAt : a.updatedAt
      const kb = sort === 'created' ? b.createdAt : b.updatedAt
      return kb.localeCompare(ka) // ISO 8601 → ters sözlüksel = en yeni önce
    })
  }, [items, search, match, sort])
}
