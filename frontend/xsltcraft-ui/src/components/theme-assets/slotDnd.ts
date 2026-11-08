import type { Announcements, ScreenReaderInstructions } from '@dnd-kit/core'
import type { AssetKind } from '../../services/themeComposeService'
import { ASSET_LABEL } from './imageState'
import { FREE_IMAGE_LABEL } from './freeImageState'

/** Sürüklenen öğe: logo/imza ya da (yalnız doc) serbest görsel listesindeki `index`. öğe. */
export type DragSource = { type: 'asset'; kind: AssetKind } | { type: 'free'; index: number }

/** Sürükle-bırak kimlikleri: görsel `asset:logo`, serbest görsel `free:0`, yuva `slot:qr-below`. */
export const assetDragId = (kind: AssetKind) => `asset:${kind}`
/** Paneldeki sürükleme çipi (önizlemedeki tutamaktan ayrı kimlik). */
export const chipDragId = (kind: AssetKind) => `asset-chip:${kind}`
export const freeDragId = (index: number) => `free:${index}`
export const freeChipDragId = (index: number) => `free-chip:${index}`
export const slotDropId = (slotId: string) => `slot:${slotId}`

export function kindFromDragId(id: string | number): AssetKind | null {
  const m = /^asset(?:-chip)?:(logo|signature)$/.exec(String(id))
  return m ? (m[1] as AssetKind) : null
}

export function sourceFromDragId(id: string | number): DragSource | null {
  const kind = kindFromDragId(id)
  if (kind) return { type: 'asset', kind }
  const m = /^free(?:-chip)?:(\d{1,2})$/.exec(String(id))
  return m ? { type: 'free', index: Number(m[1]) } : null
}

export function slotFromDropId(id: string | number | undefined | null): string | null {
  const v = id == null ? '' : String(id)
  return v.startsWith('slot:') ? v.slice(5) : null
}

const label = (id: string | number) => {
  const source = sourceFromDragId(id)
  if (!source) return 'Görsel'
  return source.type === 'asset' ? ASSET_LABEL[source.kind] : FREE_IMAGE_LABEL
}
const slotLabel = (data: unknown) => (data as { label?: string } | undefined)?.label ?? 'konum'

export const screenReaderInstructions: ScreenReaderInstructions = {
  draggable: 'Görseli taşımak için boşluk ya da Enter ile alın; konumu ayrıca paneldeki "Konum" listesinden de seçebilirsiniz.',
}

export const announcements: Announcements = {
  onDragStart: ({ active }) => `${label(active.id)} alındı.`,
  onDragOver: ({ active, over }) => (over ? `${label(active.id)}: ${slotLabel(over.data.current)} üzerinde.` : undefined),
  onDragEnd: ({ active, over }) =>
    over ? `${label(active.id)} ${slotLabel(over.data.current)} konumuna bırakıldı.` : `${label(active.id)} bırakıldı; konum değişmedi.`,
  onDragCancel: ({ active }) => `${label(active.id)} taşıma iptal edildi.`,
}
