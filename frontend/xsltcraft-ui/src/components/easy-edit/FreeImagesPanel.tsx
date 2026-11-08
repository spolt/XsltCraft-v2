import { useMemo } from 'react'
import type { ThemeSlotOption } from '../../services/templateService'
import ImagePanel from '../theme-assets/ImagePanel'
import DraggableAssetChip from '../theme-assets/DraggableAssetChip'
import type { ImgState } from '../theme-assets/imageState'
import { defaultFreeImg, FREE_IMAGE_LABEL, isFreeSlot, toFreePatch, type FreeImgState } from '../theme-assets/freeImageState'

interface Props {
  items: FreeImgState[]
  /** Belgenin tüm konumları; temadaki görsellerin yerleri (`existing-*`) yalnız boşaldıysa (`vacated`) listelenir. */
  slots: ThemeSlotOption[]
  /** Boşalmış temadaki görsel yerleri (`vacatedThemeSlots`). */
  vacated: ReadonlySet<string>
  uploading: boolean
  /** Önizlemede konumlar açık: görsel çipi sürüklenebilir. */
  draggable: boolean
  onUpload: (index: number, e: React.ChangeEvent<HTMLInputElement>) => void
  onChange: (index: number, patch: Partial<FreeImgState>) => void
  onRemove: (index: number) => void
}

/** Yalnız Kolay düzenle (doc): belgede boş bir konuma eklenen serbest görsel (en fazla bir tane). */
export default function FreeImagesPanel({ items, slots, vacated, uploading, draggable, onUpload, onChange, onRemove }: Props) {
  const freeSlots = useMemo(() => slots.filter((s) => isFreeSlot(s.id, vacated)), [slots, vacated])
  const index = 0
  const state = items[index] ?? defaultFreeImg()
  const slotValue = isFreeSlot(state.slot, vacated) ? state.slot : null

  return (
    <div className="flex flex-col gap-2">
      <p className="m-0 text-[12px] leading-normal text-tx-muted">
        Belgede boş bir konuma ek bir görsel (ör. sertifika ya da kampanya görseli) ekle. Konum seçimi zorunlu.
      </p>
      <ImagePanel
        hideTitle
        requireSlot
        label={FREE_IMAGE_LABEL}
        state={state}
        uploading={uploading}
        slots={freeSlots}
        slotValue={slotValue}
        onUpload={(e) => onUpload(index, e)}
        onChange={(patch: Partial<ImgState>) => onChange(index, toFreePatch(patch))}
        onRemove={() => onRemove(index)}
        dragHandle={draggable && state.dataUrl ? <DraggableAssetChip freeIndex={index} dataUrl={state.dataUrl} /> : undefined}
      />
    </div>
  )
}
