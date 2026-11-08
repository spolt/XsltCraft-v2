import { useEffect, useMemo, useState } from 'react'
import { useDraggable, useDroppable } from '@dnd-kit/core'
import { GripVertical } from 'lucide-react'
import { PREVIEW_BASE_WIDTH, PREVIEW_IFRAME_TITLE, PREVIEW_SANDBOX } from '../xslt-editor/previewFrame'
import type { ThemeSlotOption } from '../../services/templateService'
import type { AssetKind } from '../../services/themeComposeService'
import { ASSET_LABEL } from './imageState'
import { FREE_IMAGE_LABEL } from './freeImageState'
import { assetDragId, freeDragId, slotDropId, type DragSource } from './slotDnd'
import { useSlotRects, type Box } from './useSlotRects'

// Düzenleme modunda yuvaları görünür kılan stil. Sınıf iframe'in kök elemanına konur (srcDoc yeniden
// yüklenmez); @media screen → yazdırmada hiç görünmez.
const EDIT_CSS =
  '<style data-xc-edit-css>@media screen{' +
  'html.xc-edit [data-xc-slot]{outline:2px dashed #60a5fa;outline-offset:-2px;border-radius:6px}' +
  'html.xc-edit [data-xc-slot]:not(img){display:block;min-height:44px;min-width:110px}' +
  'html.xc-edit [data-xc-slot]:not(img):not(:has([data-xc-asset],[data-xc-static-wrap])){background:rgba(219,234,254,.55)}' +
  // Temadaki görsel kaldırıldıysa/yerini kullanıcının görseli aldıysa çıktıdan tamamen çıkar (display:none);
  // düzenlerken soluk görünsün ki geri bırakılabilsin.
  'html.xc-edit img[data-xc-hidden]{display:inline-block!important;opacity:.25}' +
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
  /**
   * Çalışma betiğinin eklemediği (dosyanın kendi) görseller için tutamak: tür → tutamağın aranacağı yuvalar (sırayla).
   * Önce yuvadaki o türden görsel aranır; hiçbirinde yoksa ilk bulunan yuvanın kutusu kullanılır.
   */
  staticHandles?: Partial<Record<AssetKind, readonly string[]>>
  /**
   * Yalnız doc: serbest görsellerin yuvaları (liste sırasıyla; null → yerleşmedi). Verilirse yerleşmiş serbest
   * görsellere sürükleme tutamağı eklenir.
   */
  freePlacements?: readonly (string | null)[]
}

/**
 * Tema önizlemesi + yuva katmanı. Yuvalar (<c>data-xc-slot</c>) iframe içinde kesikli kutular olarak görünür;
 * üstteki katmanda her yuva bir bırakma hedefi, yerleşmiş her görsel bir sürükleme tutamağıdır.
 */
export default function ThemeSlotPreview({
  html, slots, showSlots, dragging, placements, interactive = true, staticHandles, freePlacements,
}: Props) {
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
  const kinds = Object.keys(placements) as AssetKind[]
  const handles = { ...geometry.assets }
  for (const [kind, candidates] of Object.entries(staticHandles ?? {}) as [AssetKind, readonly string[]][]) {
    if (handles[kind]) continue
    const image = candidates.map((c) => geometry.slotImages[c]?.[kind]).find((b) => b)
    const id = candidates.find((c) => geometry.slots[c])
    if (image) handles[kind] = image
    else if (id) handles[kind] = geometry.slots[id]
  }
  // Runtime serbest görselleri liste sırasıyla yerleştirir; yerleşmeyen (yuvası bulunamayan) öğe sırayı kaydırmasın.
  const placedFree = (freePlacements ?? [])
    .map((slot, index) => ({ slot, index }))
    .filter((f): f is { slot: string; index: number } => !!f.slot && !!geometry.slots[f.slot])
  const freeHandles = placedFree.flatMap((f, i) => (geometry.free[i] ? [{ index: f.index, box: geometry.free[i] }] : []))

  return (
    <div className="relative bg-white shadow-[var(--ui-shadow-lift)]" style={{ width: PREVIEW_BASE_WIDTH, height }}>
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
            const filled = kinds.some((k) => placements[k] === id) || placedFree.some((f) => f.slot === id)
            return interactive
              ? <SlotTarget key={id} id={id} box={box} label={labelOf(id)} filled={filled} dragging={dragging} />
              : <SlotLabel key={id} box={box} label={labelOf(id)} />
          })}
          {interactive && (Object.entries(handles) as [AssetKind, Box][]).map(([kind, box]) => (
            <AssetHandle key={kind} source={{ type: 'asset', kind }} box={box} />
          ))}
          {interactive && freeHandles.map(({ index, box }) => (
            <AssetHandle key={`free-${index}`} source={{ type: 'free', index }} box={box} />
          ))}
        </div>
      )}
    </div>
  )
}

function SlotTarget({ id, box, label, filled, dragging }: {
  id: string; box: Box; label: string; filled: boolean; dragging: boolean
}) {
  const { setNodeRef, isOver } = useDroppable({ id: slotDropId(id), data: { slotId: id, label } })
  const empty = !filled
  return (
    <div
      ref={setNodeRef}
      className={`absolute rounded-md transition-colors ${
        isOver ? 'bg-ui-accent/25 ring-2 ring-ui-accent' : dragging ? 'ring-1 ring-ui-soft-line' : ''
      }`}
      style={{ left: box.x, top: box.y, width: Math.max(box.w, 110), height: Math.max(box.h, 44) }}
    >
      {(empty || isOver) && (
        <span className={`absolute inset-0 flex items-center justify-center text-[11px] font-semibold text-center px-1 ${
          isOver ? 'text-ui-ink' : 'text-ui-ink/80'
        }`}>
          {label}
        </span>
      )}
      {!empty && !isOver && (
        <span className="absolute -top-2 left-1 text-[10px] leading-none bg-ui-accent text-ui-accent-fg rounded px-1 py-0.5">
          {label}
        </span>
      )}
    </div>
  )
}

function SlotLabel({ box, label }: { box: Box; label: string }) {
  return (
    <div className="absolute" style={{ left: box.x, top: box.y, width: Math.max(box.w, 110), height: Math.max(box.h, 44) }}>
      <span className="absolute inset-0 flex items-center justify-center text-[11px] font-semibold text-ui-ink/80 text-center px-1">
        {label}
      </span>
    </div>
  )
}

/** Önizlemedeki görselin (logo/imza ya da serbest görsel) üstünde sürükleme tutamağı. */
function AssetHandle({ source, box }: { source: DragSource; box: Box }) {
  const { setNodeRef, attributes, listeners, isDragging } = useDraggable(source.type === 'asset'
    ? { id: assetDragId(source.kind), data: { kind: source.kind } }
    : { id: freeDragId(source.index), data: { freeIndex: source.index } })
  const label = source.type === 'asset' ? ASSET_LABEL[source.kind] : FREE_IMAGE_LABEL
  return (
    <button
      ref={setNodeRef}
      type="button"
      {...attributes}
      {...listeners}
      className={`absolute pointer-events-auto rounded cursor-grab active:cursor-grabbing touch-none group ${
        isDragging ? 'opacity-40' : 'hover:ring-2 hover:ring-ui-accent focus-visible:ring-2 focus-visible:ring-ui-accent'
      }`}
      style={{ left: box.x, top: box.y, width: box.w, height: box.h }}
      title={`${label} — başka bir konuma sürükleyin`}
      aria-label={`${label} konumunu değiştir`}
    >
      <span className="absolute -top-2 -left-2 hidden group-hover:flex group-focus-visible:flex items-center bg-ui-accent text-ui-accent-fg rounded p-0.5 shadow">
        <GripVertical size={12} aria-hidden="true" />
      </span>
    </button>
  )
}
