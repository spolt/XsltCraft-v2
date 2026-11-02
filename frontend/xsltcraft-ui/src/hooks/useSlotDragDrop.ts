import { useCallback, useState } from 'react'
import {
  PointerSensor,
  TouchSensor,
  useSensor,
  useSensors,
  type DragEndEvent,
  type DragStartEvent,
} from '@dnd-kit/core'
import { kindFromDragId, slotFromDropId } from '../components/theme-assets/slotDnd'
import type { AssetKind } from '../services/themeComposeService'
import { useDraftField } from '../store/themeDraftStore'

/** Görseli yuvaya sürükleme: bırakınca taslakta `slot` yazılır ve beklemeden metne işlenir. */
export function useSlotDragDrop(composeNow: () => void) {
  const [logo, setLogo] = useDraftField('logo')
  const [signature, setSignature] = useDraftField('signature')
  const [dragKind, setDragKind] = useState<AssetKind | null>(null)

  const sensors = useSensors(
    useSensor(PointerSensor, { activationConstraint: { distance: 4 } }),
    useSensor(TouchSensor, { activationConstraint: { delay: 150, tolerance: 5 } }),
  )

  const onDragStart = useCallback((e: DragStartEvent) => setDragKind(kindFromDragId(e.active.id)), [])
  const onDragCancel = useCallback(() => setDragKind(null), [])
  const onDragEnd = useCallback((e: DragEndEvent) => {
    setDragKind(null)
    const kind = kindFromDragId(e.active.id)
    const slot = slotFromDropId(e.over?.id)
    if (!kind || !slot) return
    const setImage = kind === 'logo' ? setLogo : setSignature
    setImage((p) => ({ ...p, slot }))
    composeNow()
  }, [composeNow, setLogo, setSignature])

  const dragged = dragKind === 'logo' ? logo : dragKind === 'signature' ? signature : null

  return {
    sensors,
    dragKind,
    draggedDataUrl: dragged?.dataUrl || null,
    onDragStart,
    onDragEnd,
    onDragCancel,
  }
}
