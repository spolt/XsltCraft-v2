import { useRef, useState } from 'react'
import { useEditorStore } from '../../store/editorStore'
import ResizeHandles from './ResizeHandles'
import { snapToGrid, clampToPage, pxToMm } from '../../utils/gridSnap'
import type { GridBlock } from '../../types/blocks'
import type { GridBlockLayout } from '../../types/template'
import { MoveVertical, X } from 'lucide-react'
import { blockLabel } from './paletteIcons'
import BlockContent from './blockRender/BlockContent'
import { useXmlContext } from './blockRender/useXmlContext'
import { primaryXPath } from './blockRender/blockXPath'
import { CSS_PX_PER_MM } from './blockRender/invoiceStyle'

interface GridBlockItemProps {
  block: GridBlock
  scale: number
}

export default function GridBlockItem({ block, scale }: GridBlockItemProps) {
  const selectedBlockId = useEditorStore((s) => s.selectedBlockId)
  const selectBlock     = useEditorStore((s) => s.selectBlock)
  const moveBlock       = useEditorStore((s) => s.moveBlock)
  const updateBlockGridLayout = useEditorStore((s) => s.updateBlockGridLayout)
  const setDragGuide    = useEditorStore((s) => s.setDragGuide)
  const documentType    = useEditorStore((s) => s.documentType)
  const xmlCtx          = useXmlContext()

  const isSelected = selectedBlockId === block.id
  const gl = block.gridLayout

  // Surukleme sirasinda gorsel pozisyon ofseti (commit edilmeden once)
  const [dragOffset, setDragOffset] = useState<{ dx: number; dy: number } | null>(null)

  const dragStartRef = useRef<{ clientX: number; clientY: number } | null>(null)

  const label = blockLabel(block.type, documentType)
  const boundXPath = isSelected ? primaryXPath(block) : null

  const visualX = gl.x + (dragOffset ? pxToMm(dragOffset.dx, scale) : 0)
  const visualY = gl.y + (dragOffset ? pxToMm(dragOffset.dy, scale) : 0)

  function handlePointerDown(e: React.PointerEvent<HTMLDivElement>) {
    // Resize handle'lardan gelen event'leri yoksay
    if ((e.target as HTMLElement).dataset.resizeHandle) return
    e.stopPropagation()
    e.currentTarget.setPointerCapture(e.pointerId)
    dragStartRef.current = { clientX: e.clientX, clientY: e.clientY }
    selectBlock(block.id)
  }

  function handlePointerMove(e: React.PointerEvent<HTMLDivElement>) {
    if (!dragStartRef.current) return
    const dx = e.clientX - dragStartRef.current.clientX
    const dy = e.clientY - dragStartRef.current.clientY
    // En az 3px hareket sonrasi drag baslat (click ile karistirmasin)
    if (Math.abs(dx) > 3 || Math.abs(dy) > 3) {
      setDragOffset({ dx, dy })
      const guideX = snapToGrid(gl.x + pxToMm(dx, scale))
      const guideY = snapToGrid(gl.y + pxToMm(dy, scale))
      setDragGuide({ x: Math.max(0, guideX), y: Math.max(0, guideY) })
    }
  }

  function handlePointerUp(e: React.PointerEvent<HTMLDivElement>) {
    e.currentTarget.releasePointerCapture(e.pointerId)

    if (dragOffset) {
      const dxMm = pxToMm(dragOffset.dx, scale)
      const dyMm = pxToMm(dragOffset.dy, scale)
      const newX = snapToGrid(gl.x + dxMm)
      const newY = snapToGrid(gl.y + dyMm)
      const clamped = clampToPage(newX, newY, gl.width, gl.height)
      moveBlock(block.id, { x: clamped.x, y: clamped.y })
    }

    dragStartRef.current = null
    setDragOffset(null)
    setDragGuide(null)
  }

  function handleResize(patch: Partial<GridBlockLayout>) {
    updateBlockGridLayout(block.id, patch)
  }

  const isDragging = dragOffset !== null
  // İçerik 96 dpi CSS px'te çizilir, canvas ölçeğine zoom ile getirilir (font/kenarlık/görsel birlikte ölçeklenir).
  const zoom = scale / CSS_PX_PER_MM

  return (
    <div
      onPointerDown={handlePointerDown}
      onPointerMove={handlePointerMove}
      onPointerUp={handlePointerUp}
      aria-label={`${label} bloğu`}
      className={`group ${
        isSelected
          ? 'outline-2 outline-offset-1 outline-ui-accent'
          : 'outline-1 outline-dashed outline-ui-soft-line hover:bg-ui-soft/25 hover:outline-ui-accent'
      }`}
      style={{
        position: 'absolute',
        left: visualX * scale,
        top: visualY * scale,
        width: gl.width * scale,
        height: gl.autoHeight ? undefined : gl.height * scale,
        minHeight: gl.height * scale,
        zIndex: Math.max(0, gl.zIndex ?? 0) + (isSelected ? 1000 : 0),
        cursor: isDragging ? 'grabbing' : 'grab',
        userSelect: 'none',
        touchAction: 'none',
        willChange: isDragging ? 'left, top' : undefined,
        opacity: isDragging ? 0.92 : undefined,
      }}
    >
      {/* Seçili blok etiket çipi: ad · bağlı XPath */}
      {isSelected && (
        <div
          aria-hidden="true"
          style={{
            position: 'absolute',
            top: -21,
            left: -3,
            maxWidth: 'calc(100% - 18px)',
            display: 'flex',
            alignItems: 'center',
            gap: 6,
            height: 18,
            padding: '0 7px',
            borderRadius: '4px 4px 0 0',
            background: 'var(--ui-accent)',
            color: 'white',
            fontSize: 10,
            whiteSpace: 'nowrap',
            overflow: 'hidden',
            pointerEvents: 'none',
          }}
        >
          <b style={{ fontWeight: 600 }}>{label}</b>
          <span className="font-mono" style={{ opacity: 0.85, overflow: 'hidden', textOverflow: 'ellipsis' }}>
            {boundXPath ?? block.type}
          </span>
        </div>
      )}

      {/* Fatura içeriği — çerçeveye kırpılır; olaylar bloğun kendisine gider */}
      <div
        style={{
          width: '100%',
          height: gl.autoHeight ? undefined : '100%',
          overflow: 'hidden',
          pointerEvents: 'none',
        }}
      >
        <div
          style={{
            zoom,
            width: gl.width * CSS_PX_PER_MM,
            height: gl.autoHeight ? undefined : gl.height * CSS_PX_PER_MM,
          }}
        >
          <BlockContent type={block.type} config={block.config} ctx={xmlCtx} despatch={documentType === 'Despatch'} />
        </div>
      </div>

      {/* Silme butonu (seçiliyken) — çipin hizasında sağ üstte, içeriği örtmez */}
      {isSelected && (
        <button
          type="button"
          onPointerDown={(e) => e.stopPropagation()}
          onClick={(e) => {
            e.stopPropagation()
            useEditorStore.getState().removeBlock(block.id)
          }}
          aria-label="Bloğu sil"
          title="Bloğu sil (Delete)"
          className="xc-focus"
          style={{
            position: 'absolute',
            top: -21,
            right: -3,
            width: 18,
            height: 18,
            borderRadius: '4px 4px 0 0',
            background: 'var(--color-fn-red)',
            color: 'white',
            border: 'none',
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'center',
            cursor: 'pointer',
            zIndex: 1001,
            padding: 0,
          }}
        >
          <X size={11} strokeWidth={2.4} aria-hidden="true" />
        </button>
      )}

      {/* Otomatik yükseklik işareti — seçili/üzerindeyken */}
      {gl.autoHeight && (
        <span
          title="Otomatik yükseklik"
          className={`text-ui-ink ${isSelected ? 'opacity-100' : 'opacity-0 group-hover:opacity-100'}`}
          style={{
            position: 'absolute',
            bottom: 2,
            right: 6,
            height: 14,
            padding: '0 4px',
            borderRadius: 3,
            background: 'var(--ui-soft)',
            fontSize: 9,
            display: 'flex',
            alignItems: 'center',
            gap: 2,
            pointerEvents: 'none',
            transition: 'opacity 120ms',
          }}
        >
          <MoveVertical size={9} aria-hidden="true" /> oto
        </span>
      )}

      {/* Resize handles - sadece seciliyken */}
      {isSelected && !isDragging && (
        <ResizeHandles layout={gl} scale={scale} onResize={handleResize} />
      )}
    </div>
  )
}
