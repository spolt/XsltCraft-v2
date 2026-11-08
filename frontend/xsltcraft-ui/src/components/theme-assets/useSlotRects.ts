import { useEffect, useState } from 'react'
import type { AssetKind } from '../../services/themeComposeService'

export interface Box { x: number; y: number; w: number; h: number }

export interface SlotGeometry {
  /** Yuva kimliği → iframe içeriğindeki kutu (iframe'in sol-üstüne göre, CSS px). */
  slots: Record<string, Box>
  /**
   * Yuva kimliği → yuvadaki dosya görselinin kutusu, türe göre (`img[data-xc-placeholder]`: yuvanın kendisi ya da
   * içindeki görsel). Taşınan statik görsel kendi yuva işaretini bırakır; tutamağı buradan bulunur.
   */
  slotImages: Record<string, Partial<Record<AssetKind, Box>>>
  /** Yerleşmiş görselin kutusu (runtime'ın eklediği <c>[data-xc-asset]</c>). */
  assets: Partial<Record<AssetKind, Box>>
  /** Serbest görsellerin kutuları (<c>[data-xc-asset="free"]</c>), belge sırasıyla. */
  free: Box[]
  /** İçerik yüksekliği — iframe bu yüksekliğe ayarlanır (iç kaydırma olmaz, kutular kaymaz). */
  contentHeight: number
}

const EMPTY: SlotGeometry = { slots: {}, slotImages: {}, assets: {}, free: [], contentHeight: 0 }

const KINDS: readonly AssetKind[] = ['logo', 'signature']

function toBox(r: DOMRect): Box {
  return { x: Math.round(r.left), y: Math.round(r.top), w: Math.round(r.width), h: Math.round(r.height) }
}

function imagesIn(el: HTMLElement): Partial<Record<AssetKind, Box>> {
  const out: Partial<Record<AssetKind, Box>> = {}
  for (const kind of KINDS) {
    const selector = `img[data-xc-placeholder="${kind}"]`
    const img = el.matches(selector) ? el : el.querySelector<HTMLElement>(selector)
    if (img) out[kind] = toBox(img.getBoundingClientRect())
  }
  return out
}

function measure(doc: Document): SlotGeometry {
  const slots: Record<string, Box> = {}
  const slotImages: Record<string, Partial<Record<AssetKind, Box>>> = {}
  doc.querySelectorAll<HTMLElement>('[data-xc-slot]').forEach((el) => {
    const id = el.getAttribute('data-xc-slot')
    if (!id || id in slots) return
    slots[id] = toBox(el.getBoundingClientRect())
    slotImages[id] = imagesIn(el)
  })
  const assets: Partial<Record<AssetKind, Box>> = {}
  const free: Box[] = []
  doc.querySelectorAll<HTMLElement>('[data-xc-asset]').forEach((el) => {
    const kind = el.getAttribute('data-xc-asset')
    // Tutamak yalnız görseli kaplasın (sarmalayıcı satırın tamamını kaplar).
    const target = el.querySelector('img') ?? el
    if (kind === 'free') free.push(toBox(target.getBoundingClientRect()))
    // Tür başına ilk yerleşim tutamak alır.
    else if ((kind === 'logo' || kind === 'signature') && !assets[kind]) assets[kind] = toBox(target.getBoundingClientRect())
  })
  const body = doc.body
  const style = body ? doc.defaultView?.getComputedStyle(body) : null
  const contentHeight = body
    ? Math.ceil(body.getBoundingClientRect().bottom + parseFloat(style?.marginBottom ?? '0'))
    : 0
  return { slots, slotImages, assets, free, contentHeight }
}

/**
 * Önizleme iframe'indeki yuvaların ve yerleşmiş görsellerin kutularını ölçer. Sandbox zaten
 * <c>allow-same-origin</c> (bkz. previewFrame.ts) — parent içeriği okuyabilir; sandbox genişletilmez.
 * Yeniden ölçüm: iframe yüklenince, gövde boyutu değişince (runtime görselleri ekleyince), DOM değişince.
 */
export function useSlotRects(iframe: HTMLIFrameElement | null, version: unknown): SlotGeometry {
  const [geometry, setGeometry] = useState<SlotGeometry>(EMPTY)

  useEffect(() => {
    if (!iframe) return
    let raf = 0
    let observers: { disconnect: () => void }[] = []

    const schedule = () => {
      cancelAnimationFrame(raf)
      raf = requestAnimationFrame(() => {
        const doc = iframe.contentDocument
        if (doc?.body) setGeometry(measure(doc))
      })
    }

    const attach = () => {
      observers.forEach((o) => o.disconnect())
      observers = []
      const doc = iframe.contentDocument
      if (!doc?.body) return
      const ro = new ResizeObserver(schedule)
      ro.observe(doc.body)
      const mo = new MutationObserver(schedule)
      mo.observe(doc.body, { childList: true, subtree: true, attributes: true, attributeFilter: ['class', 'style'] })
      doc.querySelectorAll('img').forEach((img) => img.addEventListener('load', schedule))
      observers = [ro, mo]
      schedule()
    }

    iframe.addEventListener('load', attach)
    window.addEventListener('resize', schedule)
    attach()
    return () => {
      cancelAnimationFrame(raf)
      iframe.removeEventListener('load', attach)
      window.removeEventListener('resize', schedule)
      observers.forEach((o) => o.disconnect())
    }
  }, [iframe, version])

  return geometry
}
