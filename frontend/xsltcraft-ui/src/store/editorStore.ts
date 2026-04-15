import { create } from 'zustand'
import { v4 as uuidv4 } from 'uuid'
import { DEFAULT_BLOCK_SIZE, DEFAULT_PARTY_FIELDS, DEFAULT_INVOICE_LINE_COLUMNS, DEFAULT_INVOICE_TOTALS_FIELDS } from '../types/blocks'
import type { BlockType, BlockConfig, GridBlock } from '../types/blocks'
import type { GridBlockLayout, BlockTreeV2 } from '../types/template'

const MAX_HISTORY = 20

/** A4 sayfa boyutlari (mm) */
export const PAGE_WIDTH_MM = 210
export const PAGE_HEIGHT_MM = 297

function defaultConfig(type: BlockType): BlockConfig['config'] {
  switch (type) {
    case 'Text':
      return { isStatic: true, content: '', fontSize: '11px' }
    case 'Heading':
      return { level: 'H2', isStatic: true, content: '', fontSize: '11px' }
    case 'Paragraph':
      return { lines: [], fontSize: '11px' }
    case 'Table':
      return { iterateOver: '', columns: [], showHeader: true }
    case 'ForEach':
      return { iterateOver: '', children: [] }
    case 'Conditional':
      return {
        condition: { xpath: '', operator: 'equals', value: '' },
        thenBlockIds: [],
        elseBlockIds: [],
      }
    case 'Image':
      return { assetType: 'logo', alignment: 'center', width: '100px', height: '80px', editableOnFreeTheme: false }
    case 'DocumentInfo':
      return { rows: [], fontSize: '11px' }
    case 'Totals':
      return { rows: [], alignment: 'right', fontSize: '11px' }
    case 'Notes':
      return { iterateOver: '/n1:Invoice/cbc:Note', prefix: 'Not: ', staticLines: [], staticPosition: 'after', bordered: true, borderColor: '#555555', borderStyle: 'solid', fontSize: '11px' }
    case 'BankInfo':
      return { bankName: '', iban: '', ibanLabel: 'IBAN: ', bordered: true, borderColor: '#555555', borderStyle: 'solid', fontSize: '11px' }
    case 'ETTN':
      return { ettnXpath: '', showEttn: true, showQR: false, qrWidth: 80, qrHeight: 80, qrAlignment: 'right' }
    case 'Divider':
      return { style: 'solid', color: '#CCCCCC', thickness: '1px' }
    case 'Spacer':
      return { height: '24px' }
    case 'Variable':
      return { name: '', xpath: '' }
    case 'ConditionalText':
      return {
        condition: { xpath: '', operator: 'equals', value: '' },
        thenIsStatic: true,
        thenContent: '',
        elseIsStatic: true,
        elseContent: '',
        fontSize: '11px',
      }
    case 'TaxSummary':
      return {
        taxTotalXpath: '//cac:TaxTotal/cac:TaxSubtotal',
        percentXpath: 'cac:TaxCategory/cbc:Percent',
        taxableAmountXpath: 'cbc:TaxableAmount',
        taxAmountXpath: 'cbc:TaxAmount',
        showPercent: true,
      }
    case 'GibKarekod':
      return { qrWidth: 150, qrHeight: 150, qrAlignment: 'right' }
    case 'PartyInfo':
      return {
        partyType: 'SupplierParty',
        fields: DEFAULT_PARTY_FIELDS.map((f) => ({ ...f })),
        title: 'SATICI',
        showTitle: true,
        bordered: true,
        borderStyle: 'solid',
        labelStyle: 'inline',
        fontSize: '11px',
      }
    case 'InvoiceLineTable':
      return {
        iterateOver: '//cac:InvoiceLine',
        columns: DEFAULT_INVOICE_LINE_COLUMNS.map((c) => ({ ...c })),
        title: 'MAL HİZMET TABLOSU',
        showTitle: false,
        showHeader: true,
        showRowNumber: true,
        bordered: true,
        borderStyle: 'solid',
        headerBackgroundColor: '#E0E0E0',
        alternateRowColor: '#F9F9F9',
        fontSize: '11px',
      }
    case 'InvoiceHeader':
      return {
        fields: [],
        title: 'FATURA BİLGİLERİ',
        showTitle: false,
        bordered: true,
        borderStyle: 'solid',
        labelStyle: 'table',
        fontSize: '10.4px',
      }
    case 'InvoiceTotals':
      return {
        fields: DEFAULT_INVOICE_TOTALS_FIELDS.map((f) => ({ ...f })),
        showCurrency: true,
        currencyXpath: '//cbc:DocumentCurrencyCode',
        fontSize: '11px',
      }
    case 'GibLogo':
      return { width: '80px', height: '80px', alignment: 'center' }
  }
}

interface EditorState {
  templateId: string | null
  templateName: string
  hasStoredXslt: boolean
  blocks: Record<string, GridBlock>
  selectedBlockId: string | null
  isDirty: boolean

  // Sürükleme sırasında kılavuz çizgisi (UI-only, undo dışı)
  dragGuide: { x: number; y: number } | null
  setDragGuide: (guide: { x: number; y: number } | null) => void

  // undo/redo
  past: Array<{ blocks: Record<string, GridBlock> }>
  future: Array<{ blocks: Record<string, GridBlock> }>

  // actions
  setTemplateId: (id: string | null) => void
  setTemplateName: (name: string) => void
  setHasStoredXslt: (val: boolean) => void

  addBlock: (type: BlockType, position: { x: number; y: number }, configOverride?: Record<string, unknown>) => void
  removeBlock: (blockId: string) => void
  moveBlock: (blockId: string, position: { x: number; y: number }) => void
  resizeBlock: (blockId: string, size: { width: number; height: number }) => void
  updateBlockGridLayout: (blockId: string, patch: Partial<GridBlockLayout>) => void
  updateBlockConfig: (blockId: string, config: Partial<BlockConfig['config']>) => void
  duplicateBlock: (blockId: string) => void
  selectBlock: (blockId: string | null) => void
  bringForward: (blockId: string) => void
  sendBackward: (blockId: string) => void

  loadTree: (tree: BlockTreeV2) => void
  resetTree: () => void

  undo: () => void
  redo: () => void
}

function snapshot(state: Pick<EditorState, 'blocks'>) {
  return {
    blocks: JSON.parse(JSON.stringify(state.blocks)),
  }
}


export const useEditorStore = create<EditorState>((set, get) => ({
  templateId: null,
  templateName: 'Yeni Şablon',
  hasStoredXslt: false,
  blocks: {},
  selectedBlockId: null,
  isDirty: false,
  dragGuide: null,
  setDragGuide: (guide) => set({ dragGuide: guide }),
  past: [],
  future: [],

  setTemplateId: (id) => set({ templateId: id }),
  setTemplateName: (name) => set({ templateName: name, isDirty: true }),
  setHasStoredXslt: (val) => set({ hasStoredXslt: val }),

  // ---- Block actions ----

  addBlock(type, position, configOverride?) {
    const state = get()
    const prev = snapshot(state)
    const id = uuidv4()
    const defaults = DEFAULT_BLOCK_SIZE[type]
    const block: GridBlock = {
      id,
      type,
      config: configOverride
        ? { ...defaultConfig(type), ...configOverride }
        : defaultConfig(type),
      gridLayout: {
        x: position.x,
        y: position.y,
        width: defaults.width,
        height: defaults.height,
        zIndex: 0,
        autoHeight: defaults.autoHeight,
      },
    }

    set((s) => ({
      blocks: { ...s.blocks, [id]: block },
      selectedBlockId: id,
      isDirty: true,
      past: [...s.past.slice(-(MAX_HISTORY - 1)), prev],
      future: [],
    }))
  },

  removeBlock(blockId) {
    const state = get()
    const prev = snapshot(state)
    const { [blockId]: _, ...remainingBlocks } = state.blocks

    set((s) => ({
      blocks: remainingBlocks,
      selectedBlockId: s.selectedBlockId === blockId ? null : s.selectedBlockId,
      isDirty: true,
      past: [...s.past.slice(-(MAX_HISTORY - 1)), prev],
      future: [],
    }))
  },

  moveBlock(blockId, position) {
    const state = get()
    const prev = snapshot(state)
    const existing = state.blocks[blockId]
    if (!existing) return

    set((s) => ({
      blocks: {
        ...s.blocks,
        [blockId]: {
          ...existing,
          gridLayout: { ...existing.gridLayout, x: position.x, y: position.y },
        },
      },
      isDirty: true,
      past: [...s.past.slice(-(MAX_HISTORY - 1)), prev],
      future: [],
    }))
  },

  resizeBlock(blockId, size) {
    const state = get()
    const prev = snapshot(state)
    const existing = state.blocks[blockId]
    if (!existing) return

    set((s) => ({
      blocks: {
        ...s.blocks,
        [blockId]: {
          ...existing,
          gridLayout: { ...existing.gridLayout, width: size.width, height: size.height },
        },
      },
      isDirty: true,
      past: [...s.past.slice(-(MAX_HISTORY - 1)), prev],
      future: [],
    }))
  },

  updateBlockGridLayout(blockId, patch) {
    const state = get()
    const prev = snapshot(state)
    const existing = state.blocks[blockId]
    if (!existing) return

    set((s) => ({
      blocks: {
        ...s.blocks,
        [blockId]: {
          ...existing,
          gridLayout: { ...existing.gridLayout, ...patch },
        },
      },
      isDirty: true,
      past: [...s.past.slice(-(MAX_HISTORY - 1)), prev],
      future: [],
    }))
  },

  updateBlockConfig(blockId, config) {
    const state = get()
    const prev = snapshot(state)
    const existing = state.blocks[blockId]
    if (!existing) return

    set((s) => ({
      blocks: {
        ...s.blocks,
        [blockId]: { ...existing, config: { ...existing.config, ...config } as BlockConfig['config'] },
      },
      isDirty: true,
      past: [...s.past.slice(-(MAX_HISTORY - 1)), prev],
      future: [],
    }))
  },

  duplicateBlock(blockId) {
    const state = get()
    const prev = snapshot(state)
    const existing = state.blocks[blockId]
    if (!existing) return

    const newId = uuidv4()
    const newBlock: GridBlock = {
      id: newId,
      type: existing.type,
      config: JSON.parse(JSON.stringify(existing.config)),
      gridLayout: {
        ...existing.gridLayout,
        x: Math.min(existing.gridLayout.x + 5, PAGE_WIDTH_MM - existing.gridLayout.width),
        y: Math.min(existing.gridLayout.y + 5, PAGE_HEIGHT_MM - existing.gridLayout.height),
      },
    }

    set((s) => ({
      blocks: { ...s.blocks, [newId]: newBlock },
      selectedBlockId: newId,
      isDirty: true,
      past: [...s.past.slice(-(MAX_HISTORY - 1)), prev],
      future: [],
    }))
  },

  selectBlock(blockId) {
    set({ selectedBlockId: blockId })
  },

  bringForward(blockId) {
    const state = get()
    const existing = state.blocks[blockId]
    if (!existing) return

    const maxZ = Math.max(0, ...Object.values(state.blocks).map((b) => b.gridLayout.zIndex ?? 0))
    const currentZ = existing.gridLayout.zIndex ?? 0
    if (currentZ >= maxZ && maxZ > 0) return

    const prev = snapshot(state)
    set((s) => ({
      blocks: {
        ...s.blocks,
        [blockId]: {
          ...existing,
          gridLayout: { ...existing.gridLayout, zIndex: currentZ + 1 },
        },
      },
      isDirty: true,
      past: [...s.past.slice(-(MAX_HISTORY - 1)), prev],
      future: [],
    }))
  },

  sendBackward(blockId) {
    const state = get()
    const existing = state.blocks[blockId]
    if (!existing) return

    const currentZ = existing.gridLayout.zIndex ?? 0
    if (currentZ <= 0) return

    const prev = snapshot(state)
    set((s) => ({
      blocks: {
        ...s.blocks,
        [blockId]: {
          ...existing,
          gridLayout: { ...existing.gridLayout, zIndex: currentZ - 1 },
        },
      },
      isDirty: true,
      past: [...s.past.slice(-(MAX_HISTORY - 1)), prev],
      future: [],
    }))
  },

  loadTree(tree) {
    set({
      blocks: tree.blocks,
      selectedBlockId: null,
      isDirty: false,
      past: [],
      future: [],
    })
  },

  resetTree() {
    set({
      templateId: null,
      templateName: 'Yeni Şablon',
      hasStoredXslt: false,
      blocks: {},
      selectedBlockId: null,
      isDirty: false,
      past: [],
      future: [],
    })
  },

  undo() {
    const { past, blocks, future } = get()
    if (past.length === 0) return
    const prev = past[past.length - 1]
    set({
      blocks: prev.blocks,
      past: past.slice(0, -1),
      future: [snapshot({ blocks }), ...future.slice(0, MAX_HISTORY - 1)],
      isDirty: true,
    })
  },

  redo() {
    const { future, blocks, past } = get()
    if (future.length === 0) return
    const next = future[0]
    set({
      blocks: next.blocks,
      future: future.slice(1),
      past: [...past.slice(-(MAX_HISTORY - 1)), snapshot({ blocks })],
      isDirty: true,
    })
  },
}))
