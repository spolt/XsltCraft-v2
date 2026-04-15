/**
 * V1 (section-based) → V2 (grid-based) BlockTree migrasyon yardimcisi.
 *
 * Strateji:
 * - Bolumleri `order` alanina gore sirala
 * - Her bolumun bloklarini usttan asagi istifle
 * - Sutun bilgisine gore yatay pozisyon ata
 * - Her blok tipi icin tahmini yukseklik kullan
 */

import type { BlockTreeV1, BlockTreeV2 } from '../types/template'
import type { GridBlock, BlockType } from '../types/blocks'
import { PAGE_WIDTH_MM } from '../store/editorStore'

const LEFT_MARGIN  = 14 // mm
const TOP_MARGIN   = 12 // mm
const CONTENT_WIDTH = PAGE_WIDTH_MM - LEFT_MARGIN * 2 // 182mm

/** Tip bazli tahmini blok yukseklikleri (mm) */
const HEIGHT_ESTIMATE: Partial<Record<BlockType, number>> = {
  Text:             10,
  Heading:          12,
  Paragraph:        25,
  Table:            60,
  ForEach:          40,
  Conditional:      30,
  Image:            30,
  DocumentInfo:     40,
  Totals:           50,
  Notes:            30,
  BankInfo:         25,
  ETTN:             40,
  Divider:           3,
  Spacer:           10,
  Variable:         10,
  ConditionalText:  15,
  TaxSummary:       50,
  GibKarekod:       40,
  PartyInfo:        55,
  InvoiceLineTable: 80,
  InvoiceHeader:    50,
  InvoiceTotals:    50,
  GibLogo:          30,
}

/** Blosun spacer icin config'den yukseklik al */
function estimateHeight(block: { type: BlockType; config: Record<string, unknown> }): number {
  if (block.type === 'Spacer') {
    const raw = block.config.height as string | undefined
    if (raw) {
      const num = parseFloat(raw)
      if (!isNaN(num) && raw.endsWith('px')) {
        return Math.max(3, num * 0.2646) // px → mm (96dpi)
      }
      if (!isNaN(num) && raw.endsWith('mm')) return num
    }
  }
  return HEIGHT_ESTIMATE[block.type] ?? 20
}

export function migrateV1toV2(v1: BlockTreeV1): BlockTreeV2 {
  const newBlocks: Record<string, GridBlock> = {}

  // Bolumleri sirala
  const sortedSections = [...v1.sections].sort((a, b) => a.order - b.order)

  let currentY = TOP_MARGIN

  for (const section of sortedSections) {
    const isTwoCol   = section.layout === 'two-column'
    const isThreeCol = section.layout === 'three-column'

    if (!isTwoCol && !isThreeCol) {
      // ── Tek sutun ──────────────────────────────────────────────────────────
      let maxH = 0
      for (const blockId of section.blockIds) {
        const block = v1.blocks[blockId]
        if (!block) continue
        const h = estimateHeight(block as unknown as { type: BlockType; config: Record<string, unknown> })
        const newId = blockId // ID'yi koru (config referanslari icin)
        newBlocks[newId] = {
          id: newId,
          type: block.type as BlockType,
          config: block.config,
          gridLayout: {
            x: LEFT_MARGIN,
            y: currentY,
            width: CONTENT_WIDTH,
            height: h,
            zIndex: 0,
            autoHeight: ['Table', 'InvoiceLineTable', 'ForEach'].includes(block.type),
          },
        }
        currentY += h + 2
        maxH = Math.max(maxH, h)
      }
      currentY += 4 // section sonrasi bosluk

    } else {
      // ── Cok sutun ──────────────────────────────────────────────────────────
      const colCount = isThreeCol ? 3 : 2

      // Sutun X pozisyonlari ve genislikleri
      let colXs: number[]
      let colWidths: number[]

      if (isTwoCol) {
        const half = CONTENT_WIDTH / 2
        colXs     = [LEFT_MARGIN, LEFT_MARGIN + half]
        colWidths = [half - 1, half - 1]
      } else {
        // 3 sutun: yaklasik 2/5, 3/10, 3/10 orani (mevcut sisteme benzer)
        const w0 = Math.round(CONTENT_WIDTH * 2 / 5)
        const w1 = Math.round(CONTENT_WIDTH * 3 / 10)
        const w2 = CONTENT_WIDTH - w0 - w1
        colXs     = [LEFT_MARGIN, LEFT_MARGIN + w0 + 1, LEFT_MARGIN + w0 + w1 + 2]
        colWidths = [w0 - 1, w1 - 1, w2 - 1]
      }

      // Sutun bazli bloklar
      const colBlocks: Array<typeof section.blockIds> = Array.from({ length: colCount }, () => [])
      for (const blockId of section.blockIds) {
        const block = v1.blocks[blockId]
        const col = (block?.layout as { col?: number } | undefined)?.col ?? 0
        const clampedCol = Math.min(col, colCount - 1)
        colBlocks[clampedCol].push(blockId)
      }

      // Her sutunu istifle, en yuksek sutun section yuksekligi
      const colEndY: number[] = Array(colCount).fill(currentY)

      for (let ci = 0; ci < colCount; ci++) {
        let colY = currentY
        for (const blockId of colBlocks[ci]) {
          const block = v1.blocks[blockId]
          if (!block) continue
          const h = estimateHeight(block as unknown as { type: BlockType; config: Record<string, unknown> })
          newBlocks[blockId] = {
            id: blockId,
            type: block.type as BlockType,
            config: block.config,
            gridLayout: {
              x: colXs[ci],
              y: colY,
              width: colWidths[ci],
              height: h,
              zIndex: 0,
              autoHeight: ['Table', 'InvoiceLineTable', 'ForEach'].includes(block.type),
            },
          }
          colY += h + 2
        }
        colEndY[ci] = colY
      }

      currentY = Math.max(...colEndY) + 4
    }
  }

  return { version: 2, blocks: newBlocks }
}
