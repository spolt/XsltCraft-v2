import { useState } from 'react'
import { toast } from '../../store/toastStore'

export const MAX_SELECTION = 500

/**
 * Tavanlı çoklu seçim (sayfalar arası korunur). Tek satır tavanda durur; başlık kutusu yalnız verilen
 * sayfa id'lerini birleştirir/çıkarır ve tavan aşılacaksa sayfayı hiç eklemez.
 * `resetKeys` (klasör / arama) değişince seçim temizlenir: görünmeyen şablonlar toplu işlemlere karışmaz.
 */
export function useCappedSelection(resetKeys: readonly unknown[], cap = MAX_SELECTION) {
  const [selectedIds, setSelectedIds] = useState<Set<string>>(new Set())

  // Render sırasında türetilmiş durum düzeltmesi (usePagedList ile aynı desen).
  const resetSig = JSON.stringify(resetKeys)
  const [prevSig, setPrevSig] = useState(resetSig)
  if (resetSig !== prevSig) {
    setPrevSig(resetSig)
    if (selectedIds.size > 0) setSelectedIds(new Set())
  }

  function toggle(id: string) {
    if (!selectedIds.has(id) && selectedIds.size >= cap) {
      toast.warning(`En fazla ${cap} şablon seçilebilir.`, { durationMs: 5000 })
      return
    }
    setSelectedIds((prev) => {
      const next = new Set(prev)
      if (next.has(id)) next.delete(id)
      else next.add(id)
      return next
    })
  }

  function togglePage(pageIds: readonly string[]) {
    if (pageIds.length === 0) return
    if (pageIds.every((id) => selectedIds.has(id))) {
      setSelectedIds((prev) => {
        const next = new Set(prev)
        for (const id of pageIds) next.delete(id)
        return next
      })
      return
    }
    const merged = new Set(selectedIds)
    for (const id of pageIds) merged.add(id)
    if (merged.size > cap) {
      toast.warning(`Bu sayfadakiler eklenince seçim ${cap}'ü aşar. En fazla ${cap} şablon seçilebilir.`, { durationMs: 6000 })
      return
    }
    setSelectedIds(merged)
  }

  return { selectedIds, setSelectedIds, toggle, togglePage }
}
