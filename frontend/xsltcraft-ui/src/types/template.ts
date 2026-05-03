// PRD §9.3 — Section & BlockTree tipleri

import type { Block, GridBlock } from './blocks'

// ── V1 (section-based, backward compat) ──────────────────────────────────────

export type SectionLayout = 'single-column' | 'two-column' | 'three-column'

export interface Section {
  id: string
  name: string
  order: number
  layout: SectionLayout
  blockIds: string[] // sıralı block ID listesi
}

export interface BlockTreeV1 {
  version?: 1
  sections: Section[]
  blocks: Record<string, Block> // id → Block (normalize edilmiş)
}

// ── V2 (grid-based A4 canvas) ────────────────────────────────────────────────

export interface GridBlockLayout {
  x: number       // mm (sol kenardan)
  y: number       // mm (üst kenardan)
  width: number   // mm
  height: number  // mm
  zIndex?: number  // katman sırası (varsayılan 0)
  autoHeight?: boolean // dinamik içerikli bloklar için (InvoiceLineTable, ForEach, vb.)
  // Görsel kenar boşlukları (mm) — yalnızca önizleme çıktısına uygulanır.
  // Canvas konumunu ve sweep algoritmasını etkilemez (padding olarak render edilir).
  marginTop?: number
  marginBottom?: number
  marginLeft?: number
  marginRight?: number
}

export interface BlockTreeV2 {
  version: 2
  blocks: Record<string, GridBlock>
}

// ── Union type ───────────────────────────────────────────────────────────────

export type BlockTree = BlockTreeV1 | BlockTreeV2
