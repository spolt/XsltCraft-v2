import { useRef, useEffect, useState } from 'react'
import { ZoomIn, ZoomOut, RotateCcw } from 'lucide-react'

interface Props {
  html: string
  onElementClick?: (text: string) => void
}

const CLICK_SCRIPT = `
<script>
document.addEventListener('click', function(e) {
  e.preventDefault();
  var el = e.target;
  while (el && el !== document.body) {
    var text = el.textContent ? el.textContent.trim().substring(0, 120) : '';
    if (text) {
      window.parent.postMessage({ type: 'xslt-preview-click', text: text }, '*');
      return;
    }
    el = el.parentElement;
  }
});
</script>
`

const ZOOM_LEVELS = [0.5, 0.6, 0.75, 1.0, 1.25, 1.5, 2.0]
const DEFAULT_ZOOM_IDX = 3 // 1.0
const IFRAME_BASE_WIDTH = 860
// Generous buffer covering ~100 A4 pages at 96dpi
const IFRAME_BASE_HEIGHT = 4000

export default function XsltEditorPreview({ html, onElementClick }: Props) {
  const iframeRef = useRef<HTMLIFrameElement>(null)
  const [zoomIdx, setZoomIdx] = useState(DEFAULT_ZOOM_IDX)
  const zoom = ZOOM_LEVELS[zoomIdx]

  const injectedHtml = html
    ? html.replace('</body>', CLICK_SCRIPT + '</body>')
    : '<html><body style="background:#1f2937;margin:0"></body></html>'

  useEffect(() => {
    if (!onElementClick) return
    function handler(e: MessageEvent) {
      if (e.origin !== window.location.origin) return
      if (e.data?.type === 'xslt-preview-click' && e.data.text) {
        onElementClick!(e.data.text)
      }
    }
    window.addEventListener('message', handler)
    return () => window.removeEventListener('message', handler)
  }, [onElementClick])

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

      {/* Scrollable preview area */}
      <div className="flex-1 overflow-auto bg-gray-300 flex justify-center py-2">
        {/*
          Wrapper reports scaled dimensions to layout engine.
          iframe inside is full-size, scaled via transform.
          overflow:hidden clips any rendering outside the scaled bounds.
        */}
        <div
          style={{
            width: Math.round(IFRAME_BASE_WIDTH * zoom),
            height: Math.round(IFRAME_BASE_HEIGHT * zoom),
            overflow: 'hidden',
            flexShrink: 0,
          }}
        >
          <iframe
            ref={iframeRef}
            srcDoc={injectedHtml}
            sandbox="allow-same-origin allow-scripts allow-modals"
            style={{
              width: IFRAME_BASE_WIDTH,
              height: IFRAME_BASE_HEIGHT,
              transform: `scale(${zoom})`,
              transformOrigin: 'top left',
              border: 'none',
              display: 'block',
              background: 'white',
            }}
            title="Canlı Önizleme"
          />
        </div>
      </div>
    </div>
  )
}

// eslint-disable-next-line react-refresh/only-export-components
export function printPreview(iframeEl: HTMLIFrameElement | null) {
  iframeEl?.contentWindow?.print()
}
