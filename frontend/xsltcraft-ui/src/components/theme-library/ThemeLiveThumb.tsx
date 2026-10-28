import { useEffect, useRef, useState, type ReactNode } from 'react'
import { loadThemePreview, type ThemePreviewKey } from './themePreviewCache'

/** Önizlemenin çizildiği sanal sayfa genişliği (büyük önizleme ile aynı); kart genişliğine ölçeklenir. */
const PAGE_WIDTH = 860
const PAGE_HEIGHT = Math.round((PAGE_WIDTH * 230) / 210)
const NO_SCROLL = '<style>html,body{overflow:hidden!important}</style>'

/**
 * Tema kartı küçük resmi: temanın örnek XML ile gerçek render'ı, kart görünür olunca yüklenir.
 * Yüklenene kadar ve hata durumunda `fallback` (stilize sayfa) gösterilir.
 */
export default function ThemeLiveThumb({ theme, fallback }: { theme: ThemePreviewKey; fallback: ReactNode }) {
  const boxRef = useRef<HTMLDivElement>(null)
  const [visible, setVisible] = useState(false)
  const [scale, setScale] = useState(0)
  const [html, setHtml] = useState<string | null>(null)

  useEffect(() => {
    const el = boxRef.current
    if (!el) return
    const io = new IntersectionObserver(([entry]) => {
      if (entry.isIntersecting) {
        setVisible(true)
        io.disconnect()
      }
    }, { rootMargin: '200px' })
    const ro = new ResizeObserver(([entry]) => setScale(entry.contentRect.width / PAGE_WIDTH))
    io.observe(el)
    ro.observe(el)
    return () => { io.disconnect(); ro.disconnect() }
  }, [])

  useEffect(() => {
    if (!visible) return
    let cancelled = false
    loadThemePreview(theme)
      .then((res) => { if (!cancelled) setHtml(NO_SCROLL + res) })
      .catch(() => { /* fallback görünür kalır */ })
    return () => { cancelled = true }
  }, [visible, theme])

  return (
    <div ref={boxRef} aria-hidden="true" className="relative aspect-[210/230] w-full">
      {html && scale > 0 ? (
        <div className="xc-paper absolute inset-0 overflow-hidden bg-white shadow-[var(--ui-shadow-rest)]">
          <iframe
            srcDoc={html}
            sandbox="allow-scripts"
            tabIndex={-1}
            title=""
            className="pointer-events-none origin-top-left border-none bg-white"
            style={{ width: PAGE_WIDTH, height: PAGE_HEIGHT, transform: `scale(${scale})` }}
          />
        </div>
      ) : (
        fallback
      )}
    </div>
  )
}
