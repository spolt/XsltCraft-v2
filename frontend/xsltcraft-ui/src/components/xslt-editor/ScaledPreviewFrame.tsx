import { useEffect, useLayoutEffect, useMemo, useRef, useState } from 'react'
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
  /** fitContentHeight ile: içerik kısa olsa da çerçevenin en az bu yükseklikte (px) olması. */
  minContentHeight?: number
}

/** Belge köküne ölçeği yazar; erişilemezse sessizce geçer. */
function applyZoom(iframe: HTMLIFrameElement | null, scale: number) {
  const root = iframe?.contentDocument?.documentElement
  if (root) root.style.zoom = String(scale)
}

export default function ScaledPreviewFrame({ html, scale, onElementClick, fitContentHeight = false, minContentHeight = 0 }: Props) {
  const iframeRef = useRef<HTMLIFrameElement>(null)
  const scaleRef = useRef(scale)
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

  // Ölçek, transform yerine belge içinde CSS zoom ile uygulanır: metin her ölçekte yeniden
  // çizilir ve küçültünce bulanıklaşmaz. iframe kutusu ölçeklenmiş boyutta durur.
  useLayoutEffect(() => {
    scaleRef.current = scale
    applyZoom(iframeRef.current, scale)
  }, [scale])

  function handleLoad(e: React.SyntheticEvent<HTMLIFrameElement>) {
    const iframe = e.currentTarget
    if (fitContentHeight) {
      // Yükseklik tam genişlik ve ölçek 1'de ölçülür, sonra ikisi de geri verilir.
      const previousWidth = iframe.style.width
      iframe.style.width = `${PREVIEW_BASE_WIDTH}px`
      applyZoom(iframe, 1)
      setContentHeight(measurePreviewContentHeight(iframe))
      iframe.style.width = previousWidth
    }
    applyZoom(iframe, scaleRef.current)
  }

  const baseHeight = fitContentHeight && contentHeight ? Math.max(contentHeight, minContentHeight) : PREVIEW_BASE_HEIGHT
  const width = Math.round(PREVIEW_BASE_WIDTH * scale)
  const height = Math.round(baseHeight * scale)

  return (
    <div style={{ width, height, overflow: 'hidden', flexShrink: 0 }}>
      <iframe
        ref={iframeRef}
        srcDoc={srcDoc}
        sandbox={PREVIEW_SANDBOX}
        onLoad={handleLoad}
        style={{
          width,
          height,
          border: 'none',
          display: 'block',
          background: 'white',
        }}
        title={PREVIEW_IFRAME_TITLE}
      />
    </div>
  )
}
