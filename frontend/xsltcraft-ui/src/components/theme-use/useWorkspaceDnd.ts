import { useState } from 'react'
import {
  KeyboardSensor,
  PointerSensor,
  TouchSensor,
  closestCenter,
  pointerWithin,
  useSensor,
  useSensors,
  type CollisionDetection,
  type DragEndEvent,
  type DragStartEvent,
} from '@dnd-kit/core'
import { slotFromDropId, sourceFromDragId, type DragSource } from '../theme-assets/slotDnd'

// Klavyeyle sürüklemede imleç yok: en yakın hedef seçilir. İmleçle aynı kural (pointerWithin) sürer.
const collisionDetection: CollisionDetection = (args) =>
  args.pointerCoordinates ? pointerWithin(args) : closestCenter(args)

/**
 * Kolay düzenle çalışma alanının sürükle-bırakı. Klavye sensörü yalnız doc kaynağında açılır (tema akışı aynı kalır);
 * bırakma kararı çağırana aittir.
 */
export function useWorkspaceDnd({ keyboard, onDrop }: {
  keyboard: boolean
  onDrop: (source: DragSource, slot: string) => void
}) {
  const [dragSource, setDragSource] = useState<DragSource | null>(null)
  const pointer = useSensor(PointerSensor, { activationConstraint: { distance: 4 } })
  const touch = useSensor(TouchSensor, { activationConstraint: { delay: 150, tolerance: 5 } })
  const keys = useSensor(KeyboardSensor)
  const sensors = useSensors(pointer, touch, keyboard ? keys : null)

  function onDragStart(e: DragStartEvent) {
    setDragSource(sourceFromDragId(e.active.id))
  }

  function onDragEnd(e: DragEndEvent) {
    setDragSource(null)
    const source = sourceFromDragId(e.active.id)
    const slot = slotFromDropId(e.over?.id)
    if (!source || !slot) return
    onDrop(source, slot)
  }

  return {
    sensors,
    collisionDetection,
    dragSource,
    onDragStart,
    onDragEnd,
    onDragCancel: () => setDragSource(null),
  }
}
