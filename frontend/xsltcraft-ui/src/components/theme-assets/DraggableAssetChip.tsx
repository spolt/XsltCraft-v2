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
      className={`flex items-center gap-1 border border-gray-200 rounded p-1 bg-gray-50 cursor-grab active:cursor-grabbing touch-none hover:border-blue-400 ${
        isDragging ? 'opacity-40' : ''
      }`}
      title="Önizlemedeki bir konuma sürükleyin"
      aria-label={`${ASSET_LABEL[kind]} — önizlemedeki bir konuma sürükleyin`}
    >
      <GripVertical size={12} className="text-gray-400 flex-shrink-0" />
      <img src={dataUrl} alt="" className="max-h-14 max-w-[140px] object-contain" />
    </button>
  )
}

/** Sürükleme sırasında imleci izleyen küçük görünüm. */
export function AssetDragPreview({ kind, dataUrl }: { kind: AssetKind; dataUrl: string }) {
  return (
    <div className="flex items-center gap-1.5 bg-white border-2 border-blue-500 rounded-md shadow-xl px-2 py-1 pointer-events-none">
      <img src={dataUrl} alt="" className="max-h-10 max-w-[100px] object-contain" />
      <span className="text-xs font-medium text-blue-700">{ASSET_LABEL[kind]}</span>
    </div>
  )
}
