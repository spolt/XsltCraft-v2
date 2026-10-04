import { useCallback, useEffect, useRef, useState } from 'react'
import { toast } from '../store/toastStore'
import { ImageAttachmentError, prepareImage, type PreparedImage } from '../utils/imageAttachment'

/**
 * Henüz gönderilmemiş ekran görüntüleri. `take()` listeyi mesaja devreder (URL'ler artık mesajın);
 * `remove`/`clear`/unmount ise kendi URL'lerini revoke eder.
 */
export function useImageAttachments(maxCount: number) {
  const [attachments, setAttachments] = useState<PreparedImage[]>([])
  const [processing, setProcessing] = useState(0)
  const currentRef = useRef<PreparedImage[]>([])
  // İşlenmekte olanlar da slot tutar: aynı anda 3 dosya bırakılırsa limit aşılmasın.
  const pendingRef = useRef(0)

  const commit = useCallback((next: PreparedImage[]) => {
    currentRef.current = next
    setAttachments(next)
  }, [])

  useEffect(() => () => currentRef.current.forEach(a => URL.revokeObjectURL(a.previewUrl)), [])

  const add = useCallback(async (files: File[]) => {
    if (files.length === 0) return
    const free = maxCount - currentRef.current.length - pendingRef.current
    if (free <= 0 || files.length > free) {
      toast.warning(`Mesaj başına en fazla ${maxCount} ekran görüntüsü ekleyebilirsiniz.`, { durationMs: 4000 })
    }
    const accepted = files.slice(0, Math.max(0, free))
    pendingRef.current += accepted.length
    setProcessing(p => p + accepted.length)

    await Promise.all(accepted.map(async file => {
      try {
        const img = await prepareImage(file)
        commit([...currentRef.current, img])
      } catch (e) {
        const msg = e instanceof ImageAttachmentError ? e.message : 'Görsel eklenemedi.'
        toast.error(msg, { title: 'Ekran görüntüsü', durationMs: 5000 })
      } finally {
        pendingRef.current -= 1
        setProcessing(p => p - 1)
      }
    }))
  }, [maxCount, commit])

  /** Daha önce hazırlanmış görselleri (ör. "Tekrar ekle") yeniden ekler; URL'ler paylaşılır. */
  const addPrepared = useCallback((images: PreparedImage[]) => {
    const baseId = (id: string) => id.replace(/#re$/, '')
    const existing = new Set(currentRef.current.map(a => baseId(a.id)))
    const free = maxCount - currentRef.current.length - pendingRef.current
    const fresh = images.filter(i => !existing.has(baseId(i.id)))
    if (fresh.length > free)
      toast.warning(`Mesaj başına en fazla ${maxCount} ekran görüntüsü ekleyebilirsiniz.`, { durationMs: 4000 })
    // Mesaj balonu da aynı URL'i kullandığı için kopya oluşturulur; kaldırılınca revoke edilmesin diye id korunur.
    commit([...currentRef.current, ...fresh.slice(0, Math.max(0, free)).map(i => ({ ...i, id: `${baseId(i.id)}#re` }))])
  }, [maxCount, commit])

  const remove = useCallback((id: string) => {
    const target = currentRef.current.find(a => a.id === id)
    // "Tekrar ekle" kopyasının URL'i mesaj balonuna ait — revoke etme.
    if (target && !target.id.endsWith('#re')) URL.revokeObjectURL(target.previewUrl)
    commit(currentRef.current.filter(a => a.id !== id))
  }, [commit])

  /** Gönderimde çağrılır: listeyi boşaltır ama URL'leri revoke ETMEZ (mesaj balonu kullanır). */
  const take = useCallback((): PreparedImage[] => {
    const taken = currentRef.current
    commit([])
    return taken
  }, [commit])

  return { attachments, processing: processing > 0, add, addPrepared, remove, take }
}
