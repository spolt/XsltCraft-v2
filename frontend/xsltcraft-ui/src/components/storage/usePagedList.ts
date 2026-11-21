import { useMemo, useState } from 'react'

export const PAGE_SIZES = [50, 100, 150, 200] as const
const DEFAULT_PAGE_SIZE = 100
const STORAGE_KEY = 'xsltcraft-library-page-size'

const isPageSize = (n: number) => (PAGE_SIZES as readonly number[]).includes(n)

function readPageSize(): number {
  try {
    const n = Number(localStorage.getItem(STORAGE_KEY))
    return isPageSize(n) ? n : DEFAULT_PAGE_SIZE
  } catch {
    return DEFAULT_PAGE_SIZE
  }
}

/**
 * İstemci tarafı sayfalama. `resetKeys` (ör. klasör / arama / sıralama) veya sayfa boyutu değişince 1. sayfaya döner;
 * liste kısalırsa (silme/taşıma) son sayfaya kıstırılır. Boyut tercihi kişisel kolaylık olarak localStorage'da.
 */
export function usePagedList<T>(items: readonly T[], resetKeys: readonly unknown[]) {
  const [page, setPage] = useState(1)
  const [pageSize, setPageSizeState] = useState(readPageSize)
  const totalPages = Math.max(1, Math.ceil(items.length / pageSize))

  // Render sırasında türetilmiş durum düzeltmesi (effect'te setState yerine React'in önerdiği desen).
  const resetSig = JSON.stringify(resetKeys)
  const [prevSig, setPrevSig] = useState(resetSig)
  if (resetSig !== prevSig) {
    setPrevSig(resetSig)
    setPage(1)
  } else if (page > totalPages) {
    setPage(totalPages)
  }

  const current = Math.min(page, totalPages)
  const pageItems = useMemo(
    () => items.slice((current - 1) * pageSize, current * pageSize),
    [items, current, pageSize],
  )

  function setPageSize(size: number) {
    if (!isPageSize(size)) return
    setPageSizeState(size)
    setPage(1)
    try { localStorage.setItem(STORAGE_KEY, String(size)) } catch { /* özel pencere / engelli depolama */ }
  }

  return { page: current, setPage, pageSize, setPageSize, pageItems, totalPages }
}
