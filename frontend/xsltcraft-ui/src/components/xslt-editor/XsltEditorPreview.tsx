import { useEffect, useRef, useState } from 'react'
import { ZoomIn, ZoomOut } from 'lucide-react'
import ScaledPreviewFrame from './ScaledPreviewFrame'
import { PREVIEW_BASE_WIDTH, fitScale } from './previewFrame'

interface Props {
  html: string
  onElementClick?: (text: string, opts?: { exact?: boolean }) => void
  /**
   * true: varsayılan ölçek "genişliğe sığdır" (sayfa panel daralıp genişledikçe ölçeklenir);
   * yakınlaştırma düğmeleri sabit bir yüzdeye geçer, orta düğme yeniden sığdırır.
   */
  fitToWidth?: boolean
}

const ZOOM_LEVELS = [0.5, 0.6, 0.75, 1.0, 1.25, 1.5, 2.0]
const DEFAULT_ZOOM_IDX = 3 // 1.0
/** Sığdırma modunda sayfanın iki yanındaki boşluk (px, toplam). */
const FIT_GUTTER = 32
/** Sığdırma modunda kâğıt en az A4 oranında görünür (210×297). */
const A4_MIN_HEIGHT = Math.round(PREVIEW_BASE_WIDTH * 297 / 210)

const zoomBtn =
  'xc-focus h-7 w-7 flex items-center justify-center rounded-md text-tx-muted hover:bg-ui-hover hover:text-tx disabled:opacity-35 disabled:hover:bg-transparent transition-colors'

/** Canlı önizleme alanı: ölçekli A4 sayfa (zemin her zaman beyaz) + sağ altta yüzen yakınlaştırma. */
export default function XsltEditorPreview({ html, onElementClick, fitToWidth = false }: Props) {
  // null → sığdır (yalnız fitToWidth'te); sayı → ZOOM_LEVELS indeksi.
  const [zoomIdx, setZoomIdx] = useState<number | null>(fitToWidth ? null : DEFAULT_ZOOM_IDX)
  const scrollRef = useRef<HTMLDivElement>(null)
  const [areaWidth, setAreaWidth] = useState(0)

  useEffect(() => {
    if (!fitToWidth) return
    const el = scrollRef.current
    if (!el) return
    // 2 px altı değişimler yok sayılır: alt piksel yuvarlamaları ölçeği ileri geri oynatmasın.
    const ro = new ResizeObserver(() => setAreaWidth(w => (Math.abs(w - el.clientWidth) < 2 ? w : el.clientWidth)))
    ro.observe(el)
    setAreaWidth(el.clientWidth)
    return () => ro.disconnect()
  }, [fitToWidth])

  const fit = areaWidth > 0 ? fitScale(areaWidth - FIT_GUTTER) : 1
  const zoom = zoomIdx === null ? fit : ZOOM_LEVELS[zoomIdx]

  // Sığdırma modundan çıkarken mevcut ölçeğe en yakın kademeden devam edilir.
  const lowerIdx = (() => {
    for (let i = ZOOM_LEVELS.length - 1; i >= 0; i--) if (ZOOM_LEVELS[i] < zoom - 0.001) return i
    return -1
  })()
  const higherIdx = ZOOM_LEVELS.findIndex(z => z > zoom + 0.001)

  const isFit = zoomIdx === null
  const isDefault = fitToWidth ? isFit : zoomIdx === DEFAULT_ZOOM_IDX
  const pct = Math.round(zoom * 100)

  return (
    <div className="relative h-full flex flex-col bg-ui-bg">
      {/* `justify-content: safe center` sığınca ortalar, taşınca başa yaslar — aksi halde
          kaplardan geniş ortalanmış öğenin sol kenarı kırpılır ve yatay kaydırma ulaşamaz. */}
      <div
        ref={scrollRef}
        className={`flex-1 overflow-auto flex ${fitToWidth ? 'p-4 pb-14' : 'py-4'}`}
        // Kaydırma çubuğu yeri sabit: çubuk görünüp kaybolunca genişlik → ölçek → yükseklik döngüsü (titreme) oluşmaz.
        style={{ justifyContent: 'safe center', alignItems: 'flex-start', scrollbarGutter: 'stable' }}
      >
        <div className={fitToWidth ? 'flex-none shadow-[0_1px_2px_rgba(15,36,64,.08),0_8px_24px_rgba(15,36,64,.10)]' : 'contents'}>
          <ScaledPreviewFrame
            html={html}
            scale={zoom}
            onElementClick={onElementClick}
            fitContentHeight={fitToWidth}
            minContentHeight={fitToWidth ? A4_MIN_HEIGHT : 0}
          />
        </div>
      </div>

      <div
        role="group"
        aria-label="Yakınlaştırma"
        className="absolute bottom-3 right-5 flex items-center gap-0.5 rounded-lg border border-ui-line bg-ui-card p-0.5 shadow-[var(--ui-shadow-lift)]"
      >
        <button
          type="button"
          onClick={() => lowerIdx >= 0 && setZoomIdx(lowerIdx)}
          disabled={lowerIdx < 0}
          className={zoomBtn}
          title="Uzaklaştır"
          aria-label="Uzaklaştır"
        >
          <ZoomOut size={14} aria-hidden="true" />
        </button>
        <button
          type="button"
          onClick={() => setZoomIdx(fitToWidth ? null : DEFAULT_ZOOM_IDX)}
          disabled={isDefault}
          className="xc-focus h-7 min-w-[48px] px-1.5 rounded-md font-mono text-[11px] text-tx-2 hover:bg-ui-hover disabled:hover:bg-transparent transition-colors"
          title={fitToWidth ? 'Genişliğe sığdır' : 'Varsayılan büyüklüğe sıfırla (%100)'}
          aria-label={fitToWidth
            ? `Yakınlaştırma %${pct}${isFit ? ' (sığdırılmış)' : ' — genişliğe sığdır'}`
            : `Yakınlaştırma %${pct} — sıfırla`}
        >
          {pct}%
        </button>
        <button
          type="button"
          onClick={() => higherIdx >= 0 && setZoomIdx(higherIdx)}
          disabled={higherIdx < 0}
          className={zoomBtn}
          title="Yakınlaştır"
          aria-label="Yakınlaştır"
        >
          <ZoomIn size={14} aria-hidden="true" />
        </button>
      </div>
    </div>
  )
}
