import { useRef } from 'react'
import { snapToGrid, clampToPage, pxToMm } from '../../utils/gridSnap'
import type { GridBlockLayout } from '../../types/template'

type HandleDir = 'nw' | 'n' | 'ne' | 'e' | 'se' | 's' | 'sw' | 'w'

interface ResizeHandlesProps {
  layout: GridBlockLayout
  scale: number
  onResize: (patch: Partial<GridBlockLayout>) => void
}

const HANDLE_SIZE = 8

const HANDLE_POSITIONS: Record<HandleDir, { top?: string; bottom?: string; left?: string; right?: string; transform?: string; cursor: string }> = {
  nw: { top: `${-HANDLE_SIZE / 2}px`, left: `${-HANDLE_SIZE / 2}px`, cursor: 'nw-resize' },
  n:  { top: `${-HANDLE_SIZE / 2}px`, left: '50%', transform: 'translateX(-50%)', cursor: 'n-resize' },
  ne: { top: `${-HANDLE_SIZE / 2}px`, right: `${-HANDLE_SIZE / 2}px`, cursor: 'ne-resize' },
  e:  { top: '50%', right: `${-HANDLE_SIZE / 2}px`, transform: 'translateY(-50%)', cursor: 'e-resize' },
  se: { bottom: `${-HANDLE_SIZE / 2}px`, right: `${-HANDLE_SIZE / 2}px`, cursor: 'se-resize' },
  s:  { bottom: `${-HANDLE_SIZE / 2}px`, left: '50%', transform: 'translateX(-50%)', cursor: 's-resize' },
  sw: { bottom: `${-HANDLE_SIZE / 2}px`, left: `${-HANDLE_SIZE / 2}px`, cursor: 'sw-resize' },
  w:  { top: '50%', left: `${-HANDLE_SIZE / 2}px`, transform: 'translateY(-50%)', cursor: 'w-resize' },
}

function computeNewLayout(
  dir: HandleDir,
  startLayout: GridBlockLayout,
  dxMm: number,
  dyMm: number,
): Partial<GridBlockLayout> {
  let { x, y, width, height } = startLayout

  switch (dir) {
    case 'nw': x += dxMm; y += dyMm; width -= dxMm; height -= dyMm; break
    case 'n':  y += dyMm; height -= dyMm; break
    case 'ne': width += dxMm; y += dyMm; height -= dyMm; break
    case 'e':  width += dxMm; break
    case 'se': width += dxMm; height += dyMm; break
    case 's':  height += dyMm; break
    case 'sw': x += dxMm; width -= dxMm; height += dyMm; break
    case 'w':  x += dxMm; width -= dxMm; break
  }

  const snappedW = snapToGrid(width)
  const snappedH = snapToGrid(height)
  const snappedX = snapToGrid(x)
  const snappedY = snapToGrid(y)

  const clamped = clampToPage(snappedX, snappedY, snappedW, snappedH)
  return { x: clamped.x, y: clamped.y, width: clamped.width, height: clamped.height }
}

function ResizeHandle({
  dir,
  layout,
  scale,
  onResize,
}: {
  dir: HandleDir
  layout: GridBlockLayout
  scale: number
  onResize: (patch: Partial<GridBlockLayout>) => void
}) {
  const startPosRef = useRef<{ clientX: number; clientY: number } | null>(null)
  const startLayoutRef = useRef<GridBlockLayout>(layout)

  function handlePointerDown(e: React.PointerEvent<HTMLDivElement>) {
    e.stopPropagation()
    e.preventDefault()
    e.currentTarget.setPointerCapture(e.pointerId)
    startPosRef.current = { clientX: e.clientX, clientY: e.clientY }
    startLayoutRef.current = layout
  }

  function handlePointerMove(e: React.PointerEvent<HTMLDivElement>) {
    if (!startPosRef.current) return
    const dxPx = e.clientX - startPosRef.current.clientX
    const dyPx = e.clientY - startPosRef.current.clientY
    const dxMm = pxToMm(dxPx, scale)
    const dyMm = pxToMm(dyPx, scale)
    const patch = computeNewLayout(dir, startLayoutRef.current, dxMm, dyMm)
    onResize(patch)
  }

  function handlePointerUp(e: React.PointerEvent<HTMLDivElement>) {
    e.currentTarget.releasePointerCapture(e.pointerId)
    startPosRef.current = null
  }

  const pos = HANDLE_POSITIONS[dir]

  return (
    <div
      onPointerDown={handlePointerDown}
      onPointerMove={handlePointerMove}
      onPointerUp={handlePointerUp}
      style={{
        position: 'absolute',
        width: HANDLE_SIZE,
        height: HANDLE_SIZE,
        background: '#185FA5',
        border: '1.5px solid #fff',
        borderRadius: 2,
        zIndex: 10,
        cursor: pos.cursor,
        ...pos,
      }}
    />
  )
}

export default function ResizeHandles({ layout, scale, onResize }: ResizeHandlesProps) {
  const dirs: HandleDir[] = ['nw', 'n', 'ne', 'e', 'se', 's', 'sw', 'w']

  return (
    <>
      {dirs.map((dir) => (
        <ResizeHandle key={dir} dir={dir} layout={layout} scale={scale} onResize={onResize} />
      ))}
    </>
  )
}
