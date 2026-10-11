import { useRef, useState, type PointerEvent, type RefObject } from 'react'
import { useElementSize } from './useElementSize'
import {
  clampRect,
  fitsContainer,
  moveRect,
  resizeFromBottomLeft,
  type AnchoredRect,
  type Insets,
  type RectLimits,
} from '../utils/floatingRect'

type Interaction = 'move' | 'resize'

interface PointerHandlers {
  onPointerDown: (e: PointerEvent<HTMLElement>) => void
  onPointerMove: (e: PointerEvent<HTMLElement>) => void
  onPointerUp: (e: PointerEvent<HTMLElement>) => void
  onPointerCancel: (e: PointerEvent<HTMLElement>) => void
  onLostPointerCapture: (e: PointerEvent<HTMLElement>) => void
}

export interface UseFloatingPanelOptions {
  /** position: relative kapsayıcı; pencere bunun dışına taşamaz. */
  containerRef: RefObject<HTMLElement | null>
  initialRect: AnchoredRect
  defaultRect: AnchoredRect
  limits: RectLimits
  insets: Insets
  /** Sürükleme/boyutlandırma bittiğinde (veya sıfırlamada) son dikdörtgenle çağrılır. */
  onCommit?: (rect: AnchoredRect) => void
}

export interface UseFloatingPanelResult {
  rect: AnchoredRect
  /** Kapsayıcı en küçük pencereyi alamıyorsa (ya da henüz ölçülmediyse) false. */
  fits: boolean
  interacting: Interaction | null
  moveHandleProps: PointerHandlers
  resizeHandleProps: PointerHandlers
  reset: () => void
}

interface DragState {
  mode: Interaction
  x: number
  y: number
  start: AnchoredRect
  last: AnchoredRect
  moved: boolean
}

/**
 * Kapsayıcısının sağ-üst köşesine çapalı, sürüklenebilir ve sol-alt köşeden
 * boyutlandırılabilir yüzen panel. Kullanıcının istediği dikdörtgen saklanır;
 * kapsayıcıya sığdırma render'da türetilir — kapsayıcı tekrar büyüyünce panel
 * eski yerine döner.
 */
export function useFloatingPanel({
  containerRef, initialRect, defaultRect, limits, insets, onCommit,
}: UseFloatingPanelOptions): UseFloatingPanelResult {
  const containerSize = useElementSize(containerRef)
  const [intent, setIntent] = useState(initialRect)
  const [interacting, setInteracting] = useState<Interaction | null>(null)
  const dragRef = useRef<DragState | null>(null)

  const rect = containerSize ? clampRect(intent, containerSize, limits, insets) : intent
  const fits = containerSize != null && fitsContainer(containerSize, limits, insets)

  // Panel sürükleme sırasında sığmaz olup DOM'dan düşerse pointerup hiç gelmez; takılı kalmasın.
  if (!fits && interacting) setInteracting(null)

  function begin(mode: Interaction) {
    return (e: PointerEvent<HTMLElement>) => {
      if (e.button !== 0) return
      if (mode === 'move' && (e.target as HTMLElement).closest('button')) return
      e.preventDefault()
      // Yakalama, imleç önizleme iframe'inin üzerine gelse de olayların bu elemana akmasını sağlar.
      e.currentTarget.setPointerCapture(e.pointerId)
      dragRef.current = { mode, x: e.clientX, y: e.clientY, start: rect, last: rect, moved: false }
      setInteracting(mode)
    }
  }

  function handleMove(e: PointerEvent<HTMLElement>) {
    const drag = dragRef.current
    if (!drag || !containerSize) return
    // Bırakma olayı kaçtıysa (tuş artık basılı değil) sürüklemeyi burada bitir.
    if (!(e.buttons & 1)) { handleEnd(e); return }
    const dx = e.clientX - drag.x
    const dy = e.clientY - drag.y
    const next = drag.mode === 'move'
      ? clampRect(moveRect(drag.start, dx, dy), containerSize, limits, insets)
      : resizeFromBottomLeft(drag.start, dx, dy, containerSize, limits, insets)
    drag.last = next
    drag.moved = true
    setIntent(next)
  }

  function handleEnd(e: PointerEvent<HTMLElement>) {
    const drag = dragRef.current
    if (!drag) return
    dragRef.current = null
    if (e.currentTarget.hasPointerCapture(e.pointerId)) e.currentTarget.releasePointerCapture(e.pointerId)
    setInteracting(null)
    if (drag.moved) onCommit?.(drag.last)
  }

  function handlers(mode: Interaction): PointerHandlers {
    return {
      onPointerDown: begin(mode),
      onPointerMove: handleMove,
      onPointerUp: handleEnd,
      onPointerCancel: handleEnd,
      onLostPointerCapture: handleEnd,
    }
  }

  function reset() {
    setIntent(defaultRect)
    onCommit?.(defaultRect)
  }

  return {
    rect,
    fits,
    interacting,
    moveHandleProps: handlers('move'),
    resizeHandleProps: handlers('resize'),
    reset,
  }
}
