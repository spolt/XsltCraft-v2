import { useState } from 'react'
import { ZoomIn, ZoomOut, RotateCcw } from 'lucide-react'
import ScaledPreviewFrame from './ScaledPreviewFrame'

interface Props {
  html: string
  onElementClick?: (text: string, opts?: { exact?: boolean }) => void
}

const ZOOM_LEVELS = [0.5, 0.6, 0.75, 1.0, 1.25, 1.5, 2.0]
const DEFAULT_ZOOM_IDX = 3 // 1.0

export default function XsltEditorPreview({ html, onElementClick }: Props) {
  const [zoomIdx, setZoomIdx] = useState(DEFAULT_ZOOM_IDX)
  const zoom = ZOOM_LEVELS[zoomIdx]

  const canZoomIn  = zoomIdx < ZOOM_LEVELS.length - 1
  const canZoomOut = zoomIdx > 0
  const isDefault  = zoomIdx === DEFAULT_ZOOM_IDX

  return (
    <div className="h-full flex flex-col">
      {/* Header with zoom controls */}
      <div className="px-3 py-1.5 bg-gray-800 border-b border-gray-700 flex-shrink-0 flex items-center justify-between">
        <span className="text-xs text-gray-400 font-mono uppercase tracking-wide">Önizleme</span>
        <div className="flex items-center gap-1">
          <button
            onClick={() => setZoomIdx(i => Math.max(0, i - 1))}
            disabled={!canZoomOut}
            className="h-6 w-6 flex items-center justify-center rounded text-gray-400 hover:bg-gray-700 hover:text-gray-200 disabled:opacity-30 transition-colors"
            title="Uzaklaştır"
          >
            <ZoomOut size={13} />
          </button>

          <button
            onClick={() => setZoomIdx(DEFAULT_ZOOM_IDX)}
            disabled={isDefault}
            className="h-6 px-2 flex items-center justify-center rounded text-gray-400 hover:bg-gray-700 hover:text-gray-200 disabled:opacity-30 transition-colors text-[11px] font-mono min-w-[44px]"
            title="Varsayılan büyüklüğe sıfırla (%100)"
          >
            {Math.round(zoom * 100)}%
          </button>

          <button
            onClick={() => setZoomIdx(i => Math.min(ZOOM_LEVELS.length - 1, i + 1))}
            disabled={!canZoomIn}
            className="h-6 w-6 flex items-center justify-center rounded text-gray-400 hover:bg-gray-700 hover:text-gray-200 disabled:opacity-30 transition-colors"
            title="Yakınlaştır"
          >
            <ZoomIn size={13} />
          </button>

          {!isDefault && (
            <button
              onClick={() => setZoomIdx(DEFAULT_ZOOM_IDX)}
              className="h-6 w-6 flex items-center justify-center rounded text-gray-400 hover:bg-gray-700 hover:text-gray-200 transition-colors"
              title="Sıfırla"
            >
              <RotateCcw size={11} />
            </button>
          )}
        </div>
      </div>

      {/* Scrollable preview area.
          `justify-content: safe center` centers the scaled page when it fits, but
          falls back to start-alignment when it overflows — otherwise a centered
          flex item that is wider than the container clips its left edge and the
          horizontal scrollbar can't reach it. */}
      <div
        className="flex-1 overflow-auto bg-gray-300 flex py-2"
        style={{ justifyContent: 'safe center' }}
      >
        <ScaledPreviewFrame html={html} scale={zoom} onElementClick={onElementClick} />
      </div>
    </div>
  )
}
