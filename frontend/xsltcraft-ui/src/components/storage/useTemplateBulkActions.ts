import { useState, type Dispatch, type SetStateAction } from 'react'
import {
  bulkMoveUserXslt, deleteUserXsltTemplate, moveUserXsltToFolder, MAX_TEMPLATES_PER_FOLDER,
  type UserXsltTemplateSummary,
} from '../../services/userXsltService'
import { parseGateError } from '../../services/entitlementService'
import { runPool } from '../../utils/bulkRun'
import { toast } from '../../store/toastStore'

export interface BulkOp {
  label: string
  done: number
  total: number
}

interface Deps {
  templates: UserXsltTemplateSummary[]
  setTemplates: Dispatch<SetStateAction<UserXsltTemplateSummary[]>>
  selectedIds: Set<string>
  setSelectedIds: Dispatch<SetStateAction<Set<string>>>
  /** Klasör başına mevcut şablon sayısı (kapasite ön kontrolü). */
  byFolder: Record<string, number>
}

/** 409 folder_full ise sunucunun mesajı (kalan kapasite dahil), değilse genel hata. */
async function moveErrorMessage(err: unknown): Promise<string> {
  const { status, message } = await parseGateError(err)
  return status === 409 && message ? message : 'Taşıma başarısız.'
}

/** Şablonlarım: tekli/toplu taşıma ve toplu silme (ilerleme şeridi durumu dahil). */
export function useTemplateBulkActions({ templates, setTemplates, selectedIds, setSelectedIds, byFolder }: Deps) {
  const [bulkOp, setBulkOp] = useState<BulkOp | null>(null)

  async function moveOne(id: string, folderId: string | null) {
    try {
      await moveUserXsltToFolder(id, folderId)
      setTemplates((prev) => prev.map((t) => (t.id === id ? { ...t, folderId } : t)))
      toast.success(folderId ? 'Klasöre taşındı.' : 'Klasörden çıkarıldı.', { durationMs: 4000 })
    } catch (err: unknown) {
      toast.error(await moveErrorMessage(err), { durationMs: 6000 })
    }
  }

  async function bulkMove(folderId: string | null) {
    const ids = [...selectedIds]
    if (ids.length === 0) return
    if (folderId) {
      // Ön kontrol yalnız kullanıcı dostu uyarı içindir; otorite sunucudaki tek kapasite kontrolü.
      const folderOf = new Map(templates.map((t) => [t.id, t.folderId]))
      const incoming = ids.filter((id) => folderOf.get(id) !== folderId).length
      const current = byFolder[folderId] ?? 0
      if (current + incoming > MAX_TEMPLATES_PER_FOLDER) {
        const remaining = Math.max(0, MAX_TEMPLATES_PER_FOLDER - current)
        toast.warning(
          `Bir klasörde en fazla ${MAX_TEMPLATES_PER_FOLDER} şablon olabilir. Bu klasöre en fazla ${remaining} şablon daha eklenebilir.`,
          { durationMs: 6000 },
        )
        return
      }
    }
    const label = folderId ? 'Şablonlar taşınıyor' : 'Şablonlar klasörden çıkarılıyor'
    setBulkOp({ label, done: 0, total: ids.length })
    try {
      const { movedIds } = await bulkMoveUserXslt(ids, folderId)
      setBulkOp({ label, done: ids.length, total: ids.length })
      const moved = new Set(movedIds)
      setTemplates((prev) => prev.map((t) => (moved.has(t.id) ? { ...t, folderId } : t)))
      // Taşınamayanlar (ör. bu arada silinenler) seçili kalır.
      const notMoved = ids.filter((id) => !moved.has(id))
      setSelectedIds(new Set(notMoved))
      if (notMoved.length === 0) {
        toast.success(folderId ? 'Seçilenler taşındı.' : 'Seçilenler klasörden çıkarıldı.', { durationMs: 4000 })
      } else {
        toast.error(`${ids.length - notMoved.length} şablon taşındı, ${notMoved.length} şablon taşınamadı. Taşınamayanlar seçili bırakıldı.`, { durationMs: 6000 })
      }
    } catch (err: unknown) {
      toast.error(await moveErrorMessage(err), { durationMs: 6000 })
    } finally {
      setBulkOp(null)
    }
  }

  async function bulkDelete() {
    const ids = [...selectedIds]
    const label = 'Şablonlar siliniyor'
    setBulkOp({ label, done: 0, total: ids.length })
    try {
      const { succeeded, failed } = await runPool(
        ids,
        (id) => deleteUserXsltTemplate(id),
        (done) => setBulkOp({ label, done, total: ids.length }),
      )
      const deleted = new Set(succeeded)
      setTemplates((prev) => prev.filter((t) => !deleted.has(t.id)))
      setSelectedIds(new Set(failed))
      if (failed.length === 0) {
        toast.success(`${succeeded.length} şablon silindi.`, { durationMs: 4000 })
      } else {
        toast.error(`${succeeded.length} şablon silindi, ${failed.length} şablon silinemedi. Silinemeyenler seçili bırakıldı.`, { durationMs: 6000 })
      }
    } finally {
      setBulkOp(null)
    }
  }

  return { bulkOp, moveOne, bulkMove, bulkDelete }
}
