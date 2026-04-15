import { createRef } from 'react'

export const CANVAS_DROPPABLE_ID = 'grid-canvas'

/**
 * Module-level ref'ler — EditorPage'in drop koordinat hesaplamasi icin.
 * Canvas bileseninin mount ettiginde doldurulur.
 */
export const canvasPageRef = createRef<HTMLDivElement>()
export const canvasScaleRef: { current: number } = { current: 2.835 }
