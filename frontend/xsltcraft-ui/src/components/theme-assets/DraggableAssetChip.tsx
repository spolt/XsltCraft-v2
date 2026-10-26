import { useDraggable } from '@dnd-kit/core'
import { GripVertical } from 'lucide-react'
import type { AssetKind } from '../../services/themeComposeService'
import { ASSET_LABEL } from './imageState'
import { chipDragId } from './slotDnd'

/** Paneldeki görsel önizlemesi — önizlemedeki bir konuma sürüklenebilir. */
export default function DraggableAssetChip({ kind, dataUrl }: { kind: AssetKind; dataUrl: string }) {
  const { setNodeRef, attributes, listeners, isDragging } = useDraggable({ id: chipDragId(kind), data: { kind } })
  return (
    <button
      ref={setNodeRef}
      type="button"
      {...attributes}
      {...listeners}
      className={`xc-focus flex items-center gap-1 rounded border border-field-line bg-ui-side p-1 cursor-grab active:cursor-grabbing touch-none hover:border-ui-accent ${
        isDragging ? 'opacity-40' : ''
      }`}
      title="Önizlemedeki bir konuma sürükleyin"
      aria-label={`${ASSET_LABEL[kind]} — önizlemedeki bir konuma sürükleyin`}
    >
      <GripVertical size={12} aria-hidden="true" className="flex-none text-tx-subtle" />
      <img src={dataUrl} alt="" className="max-h-14 max-w-[140px] bg-white object-contain" />
    </button>
  )
}

/** Sürükleme sırasında imleci izleyen küçük görünüm. */
export function AssetDragPreview({ kind, dataUrl }: { kind: AssetKind; dataUrl: string }) {
  return (
    <div className="pointer-events-none flex items-center gap-1.5 rounded-md border-2 border-ui-accent bg-ui-card px-2 py-1 shadow-[var(--ui-shadow-pop)]">
      <img src={dataUrl} alt="" className="max-h-10 max-w-[100px] object-contain" />
      <span className="text-xs font-medium text-ui-ink">{ASSET_LABEL[kind]}</span>
    </div>
  )
}
