import { useRef, type RefObject } from 'react'
import { PanelRightOpen, X } from 'lucide-react'
import ScaledPreviewFrame from './ScaledPreviewFrame'
import { fitScale } from './previewFrame'
import { DEFAULT_MINI_RECT, MINI_INSETS, MINI_LIMITS } from './miniPreviewPrefs'
import { useElementSize } from '../../hooks/useElementSize'
import { useFloatingPanel } from '../../hooks/useFloatingPanel'
import type { AnchoredRect } from '../../utils/floatingRect'

type ElementClickHandler = (text: string, opts?: { exact?: boolean }) => void

interface Props {
  /** position: relative kapsayıcı (Monaco alanı). */
  containerRef: RefObject<HTMLElement | null>
  html: string
  status: 'idle' | 'loading' | 'error'
  initialRect: AnchoredRect
  onRectCommit: (rect: AnchoredRect) => void
  onElementClick?: ElementClickHandler
  /** Tam ekrandan çıkıp önizlemeyi sağ panele geri alır. */
  onDock: () => void
  onClose: () => void
}

const headerBtn = 'h-5 w-5 flex items-center justify-center rounded text-gray-500 hover:bg-gray-300 hover:text-gray-800 transition-colors'

// Ayrı bileşen: pencere sığmadığı için gizlenip yeniden göründüğünde ölçüm baştan kurulur.
function MiniPreviewBody({ html, frozen, onElementClick }: {
  html: string
  frozen: boolean
  onElementClick?: ElementClickHandler
}) {
  const bodyRef = useRef<HTMLDivElement>(null)
  const bodySize = useElementSize(bodyRef)

  return (
    // overflow-y: scroll (auto değil) — çubuk belirip kayboldukça genişlik ve ölçek salınmasın.
    <div
      ref={bodyRef}
      className={`flex-1 min-h-0 overflow-y-scroll overflow-x-hidden bg-white ${frozen ? 'pointer-events-none' : ''}`}
    >
      {!html && (
        <p className="h-full flex items-center justify-center px-4 text-center text-xs text-gray-400">
          Henüz önizleme yok
        </p>
      )}
      {html && bodySize && (
        <ScaledPreviewFrame
          html={html}
          scale={fitScale(bodySize.width)}
          onElementClick={onElementClick}
          fitContentHeight
        />
      )}
    </div>
  )
}

/**
 * Tam ekran editörde kodun üzerinde yüzen mini önizleme. Tutamak şeridinden
 * sürüklenir, sol-alt köşeden boyutlandırılır; önizleme pencere genişliğine sığdırılır.
 */
export default function FloatingPreviewWindow({
  containerRef, html, status, initialRect, onRectCommit, onElementClick, onDock, onClose,
}: Props) {
  const panel = useFloatingPanel({
    containerRef,
    initialRect,
    defaultRect: DEFAULT_MINI_RECT,
    limits: MINI_LIMITS,
    insets: MINI_INSETS,
    onCommit: onRectCommit,
  })

  if (!panel.fits) return null

  const { rect, interacting } = panel

  return (
    // z-30: Monaco kaydırma çubuğunun üstünde; bul kutusu, öneri listesi ve hover'ın altında.
    <div
      role="region"
      aria-label="Mini önizleme"
      className="absolute z-30 flex flex-col rounded-xl overflow-hidden bg-white shadow-2xl ring-1 ring-black/30"
      style={{ top: rect.top, right: rect.right, width: rect.width, height: rect.height }}
    >
      <div
        {...panel.moveHandleProps}
        onDoubleClick={panel.reset}
        className={`relative h-7 flex-shrink-0 flex items-center gap-1.5 px-2.5 bg-gray-100 border-b border-gray-200 select-none touch-none ${
          interacting === 'move' ? 'cursor-grabbing' : 'cursor-grab'
        }`}
        title="Sürükleyerek taşı · çift tık: varsayılan konum ve boyut"
      >
        <span className="text-[10px] font-mono uppercase tracking-wide text-gray-500">Önizleme</span>
        {status === 'loading' && (
          <span
            role="status"
            aria-label="Derleniyor"
            className="inline-block w-3 h-3 border-2 border-blue-500 border-t-transparent rounded-full animate-spin"
            title="Derleniyor"
          />
        )}
        {status === 'error' && (
          <span
            role="status"
            aria-label="Önizleme hatası — son başarılı çıktı gösteriliyor"
            className="inline-block w-2 h-2 rounded-full bg-red-500"
            title="Önizleme hatası — son başarılı çıktı gösteriliyor"
          />
        )}
        <span className="absolute left-1/2 top-1/2 -translate-x-1/2 -translate-y-1/2 w-10 h-1 rounded-full bg-gray-300 pointer-events-none" />
        <div className="ml-auto flex items-center gap-0.5">
          <button
            onClick={onDock}
            className={headerBtn}
            title="Sağ panele geri al (tam ekrandan çık)"
            aria-label="Sağ panele geri al (tam ekrandan çık)"
          >
            <PanelRightOpen size={13} />
          </button>
          <button
            onClick={onClose}
            className={headerBtn}
            title="Mini önizlemeyi kapat"
            aria-label="Mini önizlemeyi kapat"
          >
            <X size={13} />
          </button>
        </div>
      </div>

      <MiniPreviewBody html={html} frozen={interacting !== null} onElementClick={onElementClick} />

      <div
        {...panel.resizeHandleProps}
        className="absolute bottom-0 left-0 w-4 h-4 cursor-nesw-resize touch-none"
        title="Sürükleyerek boyutlandır"
      >
        <span className="absolute bottom-1 left-1 w-2 h-2 border-l-2 border-b-2 border-gray-400" />
      </div>
    </div>
  )
}
