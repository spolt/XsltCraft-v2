import { useCallback, useEffect, useRef, useState } from 'react'
import { useDroppable } from '@dnd-kit/core'
import { useEditorStore } from '../../store/editorStore'
import GridBlockItem from './GridBlockItem'
import { PAGE_WIDTH_MM, PAGE_HEIGHT_MM } from '../../store/editorStore'
import { CANVAS_DROPPABLE_ID, canvasPageRef, canvasScaleRef } from './canvasRefs'

const GRID_SIZE_PX_PER_5MM = 5 // 5mm grid

export default function Canvas() {
  const blocks       = useEditorStore((s) => s.blocks)
  const selectedBlockId = useEditorStore((s) => s.selectedBlockId)
  const selectBlock  = useEditorStore((s) => s.selectBlock)
  const removeBlock  = useEditorStore((s) => s.removeBlock)
  const moveBlock    = useEditorStore((s) => s.moveBlock)
  const dragGuide    = useEditorStore((s) => s.dragGuide)
  const undo         = useEditorStore((s) => s.undo)
  const redo         = useEditorStore((s) => s.redo)

  // Ref'ler — event listener kapanımında her zaman güncel değeri okur
  const blocksRef = useRef(blocks)
  const selectedBlockIdRef = useRef(selectedBlockId)
  useEffect(() => { blocksRef.current = blocks }, [blocks])
  useEffect(() => { selectedBlockIdRef.current = selectedBlockId }, [selectedBlockId])

  const scrollContainerRef = useRef<HTMLDivElement>(null)
  const [scale, setScale] = useState(2.835) // varsayilan: ~1mm = 2.835px (96dpi)

  // Canvas genisligine gore scale hesapla
  useEffect(() => {
    if (!scrollContainerRef.current) return

    const observer = new ResizeObserver((entries) => {
      for (const entry of entries) {
        const available = entry.contentRect.width - 64 // 32px her yandan padding
        const newScale = Math.max(0.5, available / PAGE_WIDTH_MM)
        setScale(newScale)
        canvasScaleRef.current = newScale
      }
    })

    observer.observe(scrollContainerRef.current)
    return () => observer.disconnect()
  }, [])

  // Klavye kisayollari — stable ref'ler ile, listener yeniden kaydedilmez
  const handleKeyDown = useCallback(
    (e: KeyboardEvent) => {
      const tag = (e.target as HTMLElement).tagName
      const isInput = tag === 'INPUT' || tag === 'TEXTAREA' || tag === 'SELECT'

      if ((e.ctrlKey || e.metaKey) && e.key === 'z' && !e.shiftKey) {
        e.preventDefault()
        undo()
      }
      if (
        ((e.ctrlKey || e.metaKey) && e.key === 'y') ||
        ((e.ctrlKey || e.metaKey) && e.shiftKey && e.key === 'z')
      ) {
        e.preventDefault()
        redo()
      }
      // Secili blogu Delete veya Backspace ile sil
      const currentSelected = selectedBlockIdRef.current
      if (!isInput && (e.key === 'Delete' || e.key === 'Backspace') && currentSelected) {
        e.preventDefault()
        removeBlock(currentSelected)
      }
      // Ok tuslari ile secili blok hareketi (1mm, Shift ile 5mm)
      if (!isInput && currentSelected && ['ArrowLeft', 'ArrowRight', 'ArrowUp', 'ArrowDown'].includes(e.key)) {
        const block = blocksRef.current[currentSelected]
        if (block) {
          e.preventDefault()
          const step = e.shiftKey ? 5 : 1
          const { x, y, width, height } = block.gridLayout
          let nx = x
          let ny = y
          if (e.key === 'ArrowLeft')  nx = Math.max(0, x - step)
          if (e.key === 'ArrowRight') nx = Math.min(PAGE_WIDTH_MM - width, x + step)
          if (e.key === 'ArrowUp')    ny = Math.max(0, y - step)
          if (e.key === 'ArrowDown')  ny = Math.min(PAGE_HEIGHT_MM - height, y + step)
          if (nx !== x || ny !== y) moveBlock(currentSelected, { x: nx, y: ny })
        }
      }
    },
    [undo, redo, removeBlock, moveBlock], // blocks ve selectedBlockId ref üzerinden okunur
  )

  useEffect(() => {
    window.addEventListener('keydown', handleKeyDown)
    return () => window.removeEventListener('keydown', handleKeyDown)
  }, [handleKeyDown])

  // dnd-kit droppable (palette -> canvas icin)
  const { setNodeRef: setDropRef, isOver } = useDroppable({
    id: CANVAS_DROPPABLE_ID,
    data: { type: 'grid-canvas' },
  })

  const pageWidthPx  = PAGE_WIDTH_MM  * scale
  const pageHeightPx = PAGE_HEIGHT_MM * scale
  const gridCellPx   = GRID_SIZE_PX_PER_5MM * scale // 5mm'lik hucre boyutu

  const blockList = Object.values(blocks).sort(
    (a, b) => ((a.gridLayout.zIndex ?? 0) - (b.gridLayout.zIndex ?? 0)) || 0,
  )

  return (
    <main
      ref={scrollContainerRef}
      className="flex-1 overflow-auto"
      style={{ background: '#F1F0EC' }}
      onClick={() => {
        // Sayfa arkaplanina tiklamak secimi kaldirir
        if (selectedBlockId) selectBlock(null)
      }}
    >
      {/* Sayfa kapsayicisi — scroll ve hizalama icin */}
      <div
        style={{
          minHeight: '100%',
          display: 'flex',
          alignItems: 'flex-start',
          justifyContent: 'center',
          padding: '32px',
        }}
      >
        {/* A4 sayfasi */}
        <div
          ref={(node) => {
            // hem module-level canvasPageRef hem de dnd-kit droppable ref
            ;(canvasPageRef as React.MutableRefObject<HTMLDivElement | null>).current = node
            setDropRef(node)
          }}
          onClick={(e) => e.stopPropagation()} // sayfa icini tiklamak deselect etmesin
          style={{
            position: 'relative',
            width: pageWidthPx,
            height: pageHeightPx,
            background: '#ffffff',
            flexShrink: 0,
            boxShadow: isOver
              ? '0 0 0 2px #185FA5, 0 4px 24px rgba(0,0,0,0.18)'
              : '0 2px 16px rgba(0,0,0,0.12), 0 0 0 1px rgba(0,0,0,0.05)',
            borderRadius: 2,
            // Izgara arkaplan - 5mm kucuk + 10mm buyuk cizgiler
            backgroundImage: [
              `linear-gradient(rgba(0,100,200,0.055) 1px, transparent 1px)`,
              `linear-gradient(90deg, rgba(0,100,200,0.055) 1px, transparent 1px)`,
              `linear-gradient(rgba(0,100,200,0.1) 1px, transparent 1px)`,
              `linear-gradient(90deg, rgba(0,100,200,0.1) 1px, transparent 1px)`,
            ].join(','),
            backgroundSize: [
              `${gridCellPx}px ${gridCellPx}px`,
              `${gridCellPx}px ${gridCellPx}px`,
              `${gridCellPx * 2}px ${gridCellPx * 2}px`,
              `${gridCellPx * 2}px ${gridCellPx * 2}px`,
            ].join(','),
          }}
        >
          {/* Marj kilavuzlari (14mm her taraftan) */}
          <div
            style={{
              position: 'absolute',
              top: 14 * scale,
              left: 14 * scale,
              right: 14 * scale,
              bottom: 14 * scale,
              border: '1px dashed rgba(24,95,165,0.18)',
              borderRadius: 0,
              pointerEvents: 'none',
              zIndex: 0,
            }}
          />

          {/* Bos canvas mesaji */}
          {blockList.length === 0 && (
            <div
              style={{
                position: 'absolute',
                inset: 0,
                display: 'flex',
                flexDirection: 'column',
                alignItems: 'center',
                justifyContent: 'center',
                gap: 6,
                pointerEvents: 'none',
              }}
            >
              <span style={{ fontSize: 13, color: 'rgba(0,0,0,0.2)', fontWeight: 500 }}>
                Soldaki listeden blok sürükle
              </span>
              <span style={{ fontSize: 11, color: 'rgba(0,0,0,0.15)' }}>
                A4 — 210 × 297 mm
              </span>
            </div>
          )}

          {/* Sürükleme kılavuz çizgileri */}
          {dragGuide && (
            <>
              {/* Dikey çizgi — X mesafesi */}
              <div
                style={{
                  position: 'absolute',
                  left: dragGuide.x * scale,
                  top: 0,
                  width: 1,
                  height: pageHeightPx,
                  background: 'rgba(220,30,30,0.7)',
                  pointerEvents: 'none',
                  zIndex: 9999,
                }}
              />
              {/* Yatay çizgi — Y mesafesi */}
              <div
                style={{
                  position: 'absolute',
                  left: 0,
                  top: dragGuide.y * scale,
                  width: pageWidthPx,
                  height: 1,
                  background: 'rgba(220,30,30,0.7)',
                  pointerEvents: 'none',
                  zIndex: 9999,
                }}
              />
              {/* X etiketi */}
              <div
                style={{
                  position: 'absolute',
                  left: dragGuide.x * scale + 4,
                  top: 4,
                  fontSize: 9,
                  fontWeight: 600,
                  color: 'rgba(220,30,30,0.9)',
                  background: 'rgba(255,255,255,0.85)',
                  padding: '1px 4px',
                  borderRadius: 3,
                  pointerEvents: 'none',
                  zIndex: 9999,
                  whiteSpace: 'nowrap',
                }}
              >
                x: {dragGuide.x.toFixed(1)} mm
              </div>
              {/* Y etiketi */}
              <div
                style={{
                  position: 'absolute',
                  left: 4,
                  top: dragGuide.y * scale + 4,
                  fontSize: 9,
                  fontWeight: 600,
                  color: 'rgba(220,30,30,0.9)',
                  background: 'rgba(255,255,255,0.85)',
                  padding: '1px 4px',
                  borderRadius: 3,
                  pointerEvents: 'none',
                  zIndex: 9999,
                  whiteSpace: 'nowrap',
                }}
              >
                y: {dragGuide.y.toFixed(1)} mm
              </div>
            </>
          )}

          {/* Bloklar */}
          {blockList.map((block) => (
            <GridBlockItem
              key={block.id}
              block={block}
              scale={scale}
            />
          ))}
        </div>
      </div>

      {/* Scale bilgisi — debug icin gizli tutulabilir */}
      <div
        style={{
          position: 'fixed',
          bottom: 8,
          left: '50%',
          transform: 'translateX(-50%)',
          fontSize: 9,
          color: 'rgba(0,0,0,0.25)',
          pointerEvents: 'none',
          zIndex: 10,
          background: 'rgba(255,255,255,0.6)',
          padding: '2px 8px',
          borderRadius: 4,
        }}
      >
        {Math.round(scale * 25.4 / 96 * 100)}% · A4 {PAGE_WIDTH_MM}×{PAGE_HEIGHT_MM} mm · 5mm ızgara
      </div>
    </main>
  )
}

