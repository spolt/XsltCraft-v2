import { useEffect, useMemo, useState } from 'react'
import { useDraggable, useDroppable } from '@dnd-kit/core'
import { GripVertical } from 'lucide-react'
import { PREVIEW_BASE_WIDTH, PREVIEW_IFRAME_TITLE, PREVIEW_SANDBOX } from '../xslt-editor/previewFrame'
import type { ThemeSlotOption } from '../../services/templateService'
import type { AssetKind } from '../../services/themeComposeService'
import { ASSET_LABEL } from './imageState'
import { assetDragId, slotDropId } from './slotDnd'
import { useSlotRects, type Box } from './useSlotRects'

// Düzenleme modunda yuvaları görünür kılan stil. Sınıf iframe'in kök elemanına konur (srcDoc yeniden
// yüklenmez); @media screen → yazdırmada hiç görünmez.
const EDIT_CSS =
  '<style data-xc-edit-css>@media screen{' +
  'html.xc-edit [data-xc-slot]{outline:2px dashed #60a5fa;outline-offset:-2px;border-radius:6px}' +
  'html.xc-edit [data-xc-slot]:not(img){display:block;min-height:44px;min-width:110px}' +
  'html.xc-edit [data-xc-slot]:not(img):not(:has([data-xc-asset])){background:rgba(219,234,254,.55)}' +
  // Temadaki görsel yuvası: yerini kullanıcının görseli aldıysa gizlenir; düzenlerken soluk görünsün ki geri bırakılabilsin.
  'html.xc-edit img[data-xc-hidden]{visibility:visible!important;opacity:.25}' +
  // Kutu, görselin nihai yerleşimiyle aynı akışta dursun (runtime aynı kuralları sarmalayıcıya uygular).
  'html.xc-edit [data-xc-slot][data-xc-layout="block-clear"]{clear:both}' +
  'html.xc-edit [data-xc-slot][data-xc-layout="float-left"]{float:left;margin:0 8px 4px 0}' +
  '}</style>'

function withEditCss(html: string): string {
  if (!html) return '<html><body style="background:#d1d5db;margin:0"></body></html>'
  const head = html.search(/<\/head>/i)
  return head >= 0 ? html.slice(0, head) + EDIT_CSS + html.slice(head) : EDIT_CSS + html
}

interface Props {
  html: string
  slots: ThemeSlotOption[]
  /** Konum kutularını göster (düzenleme paneli açıkken). */
  showSlots: boolean
  /** Sürükleme sürüyor: iframe imleç olaylarını yutmasın, kutular bırakma hedefi olsun. */
  dragging: boolean
  /** Hangi yuvada hangi görsel var (etiket için). */
  placements: Partial<Record<AssetKind, string | null>>
  /** false → salt-okunur (admin raporu): yalnız etiketler. */
  interactive?: boolean
}

/**
 * Tema önizlemesi + yuva katmanı. Yuvalar (<c>data-xc-slot</c>) iframe içinde kesikli kutular olarak görünür;
 * üstteki katmanda her yuva bir bırakma hedefi, yerleşmiş her görsel bir sürükleme tutamağıdır.
 */
export default function ThemeSlotPreview({ html, slots, showSlots, dragging, placements, interactive = true }: Props) {
  const [iframe, setIframe] = useState<HTMLIFrameElement | null>(null)
  const srcDoc = useMemo(() => withEditCss(html), [html])
  const geometry = useSlotRects(iframe, srcDoc)

  // Kök elemanın sınıfı — srcDoc'u değiştirmeden kutuları aç/kapat.
  useEffect(() => {
    if (!iframe) return
    // srcdoc değişirken belge geçici olarak boş olabilir (documentElement null); sınıf 'load'da yeniden uygulanır.
    const apply = () => iframe.contentDocument?.documentElement?.classList.toggle('xc-edit', showSlots)
    apply()
    iframe.addEventListener('load', apply)
    return () => iframe.removeEventListener('load', apply)
  }, [iframe, showSlots, srcDoc])

  const labelOf = (id: string) => slots.find((s) => s.id === id)?.label ?? id
  const height = Math.max(geometry.contentHeight, 600)

  return (
    <div className="relative bg-white shadow-lg" style={{ width: PREVIEW_BASE_WIDTH, height }}>
      <iframe
        ref={setIframe}
        srcDoc={srcDoc}
        sandbox={PREVIEW_SANDBOX}
        title={PREVIEW_IFRAME_TITLE}
        className="block border-none"
        style={{ width: PREVIEW_BASE_WIDTH, height, pointerEvents: dragging ? 'none' : 'auto' }}
      />
      {showSlots && (
        <div className="absolute inset-0 pointer-events-none" aria-hidden={!interactive}>
          {Object.entries(geometry.slots).map(([id, box]) => {
            const kinds = (Object.keys(placements) as AssetKind[]).filter((k) => placements[k] === id)
            return interactive
              ? <SlotTarget key={id} id={id} box={box} label={labelOf(id)} filledBy={kinds} dragging={dragging} />
              : <SlotLabel key={id} box={box} label={labelOf(id)} />
          })}
          {interactive && (Object.entries(geometry.assets) as [AssetKind, Box][]).map(([kind, box]) => (
            <AssetHandle key={kind} kind={kind} box={box} />
          ))}
        </div>
      )}
    </div>
  )
}

function SlotTarget({ id, box, label, filledBy, dragging }: {
  id: string; box: Box; label: string; filledBy: AssetKind[]; dragging: boolean
}) {
  const { setNodeRef, isOver } = useDroppable({ id: slotDropId(id), data: { slotId: id, label } })
  const empty = filledBy.length === 0
  return (
    <div
      ref={setNodeRef}
      className={`absolute rounded-md transition-colors ${
        isOver ? 'bg-blue-500/25 ring-2 ring-blue-600' : dragging ? 'ring-1 ring-blue-300/70' : ''
      }`}
      style={{ left: box.x, top: box.y, width: Math.max(box.w, 110), height: Math.max(box.h, 44) }}
    >
      {(empty || isOver) && (
        <span className={`absolute inset-0 flex items-center justify-center text-[11px] font-semibold text-center px-1 ${
          isOver ? 'text-blue-800' : 'text-blue-700/80'
        }`}>
          {label}
        </span>
      )}
      {!empty && !isOver && (
        <span className="absolute -top-2 left-1 text-[10px] leading-none bg-blue-600 text-white rounded px-1 py-0.5">
          {label}
        </span>
      )}
    </div>
  )
}

function SlotLabel({ box, label }: { box: Box; label: string }) {
  return (
    <div className="absolute" style={{ left: box.x, top: box.y, width: Math.max(box.w, 110), height: Math.max(box.h, 44) }}>
      <span className="absolute inset-0 flex items-center justify-center text-[11px] font-semibold text-blue-700/80 text-center px-1">
        {label}
      </span>
    </div>
  )
}

/** Önizlemedeki görselin üstünde sürükleme tutamağı. */
function AssetHandle({ kind, box }: { kind: AssetKind; box: Box }) {
  const { setNodeRef, attributes, listeners, isDragging } = useDraggable({ id: assetDragId(kind), data: { kind } })
  return (
    <button
      ref={setNodeRef}
      type="button"
      {...attributes}
      {...listeners}
      className={`absolute pointer-events-auto rounded cursor-grab active:cursor-grabbing touch-none group ${
        isDragging ? 'opacity-40' : 'hover:ring-2 hover:ring-blue-500'
      }`}
      style={{ left: box.x, top: box.y, width: box.w, height: box.h }}
      title={`${ASSET_LABEL[kind]} — başka bir konuma sürükleyin`}
      aria-label={`${ASSET_LABEL[kind]} konumunu değiştir`}
    >
      <span className="absolute -top-2 -left-2 hidden group-hover:flex items-center bg-blue-600 text-white rounded p-0.5 shadow">
        <GripVertical size={12} />
      </span>
    </button>
  )
}
