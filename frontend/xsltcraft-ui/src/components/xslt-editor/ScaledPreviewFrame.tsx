import { useEffect, useMemo, useRef, useState } from 'react'
import {
  PREVIEW_BASE_HEIGHT,
  PREVIEW_BASE_WIDTH,
  PREVIEW_CLICK_MESSAGE,
  PREVIEW_IFRAME_TITLE,
  PREVIEW_SANDBOX,
  buildPreviewSrcDoc,
  measurePreviewContentHeight,
} from './previewFrame'

interface Props {
  html: string
  scale: number
  onElementClick?: (text: string, opts?: { exact?: boolean }) => void
  /** true ise iframe yüksekliği yüklenen içeriğe göre ayarlanır (sabit tampon yerine). */
  fitContentHeight?: boolean
}

export default function ScaledPreviewFrame({ html, scale, onElementClick, fitContentHeight = false }: Props) {
  const iframeRef = useRef<HTMLIFrameElement>(null)
  const [contentHeight, setContentHeight] = useState<number | null>(null)
  const srcDoc = useMemo(() => buildPreviewSrcDoc(html), [html])

  useEffect(() => {
    if (!onElementClick) return
    function handler(e: MessageEvent) {
      // Yalnız bu çerçeveden gelen mesajlar: sayfada başka bir önizleme olsa da tıklama tek kez işlenir.
      if (e.source !== iframeRef.current?.contentWindow) return
      if (e.origin !== window.location.origin) return
      if (e.data?.type === PREVIEW_CLICK_MESSAGE && typeof e.data.text === 'string' && e.data.text) {
        onElementClick!(e.data.text, { exact: !!e.data.exact })
      }
    }
    window.addEventListener('message', handler)
    return () => window.removeEventListener('message', handler)
  }, [onElementClick])

  const baseHeight = fitContentHeight && contentHeight ? contentHeight : PREVIEW_BASE_HEIGHT

  return (
    /*
      Wrapper reports scaled dimensions to layout engine.
      iframe inside is full-size, scaled via transform.
      overflow:hidden clips any rendering outside the scaled bounds.
    */
    <div
      style={{
        width: Math.round(PREVIEW_BASE_WIDTH * scale),
        height: Math.round(baseHeight * scale),
        overflow: 'hidden',
        flexShrink: 0,
      }}
    >
      <iframe
        ref={iframeRef}
        srcDoc={srcDoc}
        sandbox={PREVIEW_SANDBOX}
        onLoad={fitContentHeight ? (e) => setContentHeight(measurePreviewContentHeight(e.currentTarget)) : undefined}
        style={{
          width: PREVIEW_BASE_WIDTH,
          height: baseHeight,
          transform: `scale(${scale})`,
          transformOrigin: 'top left',
          border: 'none',
          display: 'block',
          background: 'white',
        }}
        title={PREVIEW_IFRAME_TITLE}
      />
    </div>
  )
}
