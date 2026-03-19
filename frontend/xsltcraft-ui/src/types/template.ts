// PRD §9.3 — Section & BlockTree tipleri

import type { Block } from './blocks'

export type SectionLayout = 'single-column' | 'two-column'

export interface Section {
  id: string
  name: string
  order: number
  layout: SectionLayout
  blockIds: string[] // sıralı block ID listesi
}

export interface BlockTree {
  sections: Section[]
  blocks: Record<string, Block> // id → Block (normalize edilmiş)
}
