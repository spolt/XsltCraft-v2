import { useRef, useState } from 'react'
import {
  downloadFolderZip, downloadUserXsltZip, getUserXsltTemplate, MAX_DOWNLOAD_ITEMS, type DownloadProgressFn,
} from '../../services/userXsltService'
import { parseGateError } from '../../services/entitlementService'
import { reportDownload } from '../../services/activityService'
import { fileStem, localDateStamp, saveBlob } from '../../utils/saveBlob'
import { toast } from '../../store/toastStore'

async function downloadErrorMessage(err: unknown): Promise<string> {
  const { status, message } = await parseGateError(err)
  if ((status === 413 || status === 404 || status === 429) && message) return message
  if (status === 429) return 'Çok fazla indirme isteği; biraz bekleyip tekrar deneyin.'
  return 'İndirme başarısız.'
}

/** İndirme durumu: sunucu ZIP'i kurarken `preparing` (ilk bayt arşiv bitince gelir), sonra `downloading`. */
export interface DownloadProgressState {
  phase: 'preparing' | 'downloading'
  /** Arşivdeki şablon sayısı (bilinmiyorsa 0). */
  count: number
  loaded: number
  total?: number
}

/**
 * Şablonlarım indirmeleri: tek seçim → doğrudan `.xslt`, çoklu seçim / klasör → sunucuda üretilen ZIP.
 * ZIP adı istemcide belirlenir (Content-Disposition CORS'ta okunmaz). İlerleme `progress` ile ekranda gösterilir.
 */
export function useTemplateDownload() {
  const [progress, setProgress] = useState<DownloadProgressState | null>(null)
  const busy = useRef(false)

  async function run(count: number, task: (onProgress: DownloadProgressFn) => Promise<void>) {
    if (busy.current) return
    busy.current = true
    setProgress({ phase: 'preparing', count, loaded: 0 })
    const onProgress: DownloadProgressFn = (loaded, total) => {
      if (loaded > 0) setProgress({ phase: 'downloading', count, loaded, total })
    }
    try {
      await task(onProgress)
      toast.success(count === 1 ? 'Şablon indirildi.' : `${count > 0 ? `${count} şablon` : 'Klasör'} indirildi.`, { durationMs: 4000 })
    } catch (err: unknown) {
      toast.error(await downloadErrorMessage(err), { durationMs: 6000 })
    } finally {
      busy.current = false
      setProgress(null)
    }
  }

  /** `activeFolderName`: aktif görünüm gerçek bir klasörse adı (ZIP adı için). */
  function downloadSelected(ids: string[], activeFolderName?: string) {
    if (ids.length === 0) return Promise.resolve()
    if (ids.length > MAX_DOWNLOAD_ITEMS) {
      toast.warning(`Tek seferde en fazla ${MAX_DOWNLOAD_ITEMS} şablon indirilebilir.`, { durationMs: 6000 })
      return Promise.resolve()
    }
    return run(ids.length, async (onProgress) => {
      if (ids.length === 1) {
        const t = await getUserXsltTemplate(ids[0])
        saveBlob(new Blob([t.xsltContent], { type: 'application/xslt+xml' }), `${fileStem(t.name)}.xslt`)
        reportDownload('Xslt', t.id)
        return
      }
      const blob = await downloadUserXsltZip(ids, onProgress)
      const prefix = activeFolderName ? fileStem(activeFolderName) : 'sablonlarim'
      saveBlob(blob, `${prefix}-${localDateStamp()}.zip`)
    })
  }

  /** `count`: klasördeki şablon sayısı (gösterge ve bildirim metni için). */
  function downloadFolder(folder: { id: string; name: string }, count = 0) {
    return run(count, async (onProgress) => {
      const blob = await downloadFolderZip(folder.id, onProgress)
      saveBlob(blob, `${fileStem(folder.name)}.zip`)
    })
  }

  return { downloading: progress !== null, progress, downloadSelected, downloadFolder }
}
