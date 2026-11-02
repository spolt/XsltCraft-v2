import { useEffect, useMemo, useRef } from 'react'
import { X } from 'lucide-react'
import type { ThemeSlotOption } from '../../services/templateService'
import { useDraftField } from '../../store/themeDraftStore'
import { useUpgradeModalStore } from '../../store/upgradeModalStore'
import ThemeSlotPreview from '../theme-assets/ThemeSlotPreview'
import { effectiveSlot } from '../theme-assets/imageState'

interface Props {
  html: string
  slots: ThemeSlotOption[]
  dragging: boolean
  /** Üstünde sayfa diyaloğu açık: Esc ona aittir. */
  dialogOpen: boolean
  onClose: () => void
  /** Odak açan düğmeye dönemezse (panel kapatıldıysa) buraya gider. */
  focusFallback: () => void
}

/**
 * "Alanları göster": ana bölmeyi örten %100 ölçekli önizleme. Sol panel etkin kalır (görsel oradan sürüklenir), bu yüzden
 * modal değildir. Sayfadaki tek önizleme iframe'i açıkken budur; yazdırma ve bırakma hedefleri onu bulur.
 */
export default function SlotPlacementOverlay({ html, slots, dragging, dialogOpen, onClose, focusFallback }: Props) {
  const [logo] = useDraftField('logo')
  const [signature] = useDraftField('signature')
  const closeRef = useRef<HTMLButtonElement>(null)

  const placements = useMemo(() => ({
    logo: logo.dataUrl ? effectiveSlot('logo', logo, slots) : null,
    signature: signature.dataUrl ? effectiveSlot('signature', signature, slots) : null,
  }), [logo, signature, slots])

  useEffect(() => {
    const opener = document.activeElement as HTMLElement | null
    closeRef.current?.focus()
    return () => {
      opener?.focus()
      if (!opener || opener === document.body || document.activeElement !== opener) focusFallback()
    }
    // Yalnız açılış ve kapanış: odak bir kez alınır ve bir kez geri verilir.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [])

  useEffect(() => {
    function handleKeyDown(e: KeyboardEvent) {
      // Esc önce üstteki diyaloğu kapatır; sürüklerken yalnız sürüklemeyi iptal eder.
      if (e.key !== 'Escape' || e.defaultPrevented || dragging || dialogOpen) return
      if (useUpgradeModalStore.getState().isOpen) return
      onClose()
    }
    document.addEventListener('keydown', handleKeyDown)
    return () => document.removeEventListener('keydown', handleKeyDown)
  }, [dragging, dialogOpen, onClose])

  return (
    <section aria-label="Alan yerleşimi" className="absolute inset-0 z-30 flex flex-col bg-ui-bg">
      <div className="flex h-[36px] flex-none items-center gap-2 border-b border-ui-line bg-ui-card px-3">
        <h2 className="m-0 flex-none text-[13px] font-semibold text-tx">Alanları göster</h2>
        <span className="min-w-0 truncate text-[12px] text-tx-muted">
          Görseli mavi kesikli alanlardan birine sürükleyin.
        </span>
        <div className="flex-1" />
        <button
          ref={closeRef}
          type="button"
          onClick={onClose}
          title="Alan görünümünü kapat (Esc)"
          aria-label="Alan görünümünü kapat"
          className="xc-focus flex h-[26px] w-[26px] flex-none items-center justify-center rounded-md text-tx-muted transition-colors hover:bg-ui-hover hover:text-tx"
        >
          <X size={15} aria-hidden="true" />
        </button>
      </div>
      <div className="flex min-h-0 flex-1 overflow-auto px-6 py-8" style={{ justifyContent: 'safe center' }}>
        <ThemeSlotPreview html={html} slots={slots} showSlots dragging={dragging} placements={placements} />
      </div>
    </section>
  )
}
