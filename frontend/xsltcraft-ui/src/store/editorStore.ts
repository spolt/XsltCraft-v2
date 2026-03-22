import { create } from 'zustand'
import { v4 as uuidv4 } from 'uuid'
import { DEFAULT_BLOCK_LAYOUT, DEFAULT_PARTY_FIELDS, DEFAULT_INVOICE_LINE_COLUMNS, DEFAULT_INVOICE_HEADER_FIELDS, DEFAULT_INVOICE_TOTALS_FIELDS } from '../types/blocks'
import type { Block, BlockType, BlockConfig, BlockLayout } from '../types/blocks'
import type { BlockTree, Section } from '../types/template'

const MAX_HISTORY = 20

function defaultConfig(type: BlockType): BlockConfig['config'] {
  switch (type) {
    case 'Text':
      return { isStatic: true, content: '' }
    case 'Heading':
      return { level: 'H2', isStatic: true, content: '' }
    case 'Paragraph':
      return { lines: [] }
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
      return { assetType: 'logo', alignment: 'center', editableOnFreeTheme: false }
    case 'DocumentInfo':
      return { rows: [] }
    case 'Totals':
      return { rows: [], alignment: 'right' }
    case 'Notes':
      return { iterateOver: '/n1:Invoice/cbc:Note', prefix: 'Not: ', staticLines: [], bordered: true, borderColor: '#555555' }
    case 'BankInfo':
      return { bankName: '', iban: '', ibanLabel: 'IBAN: ', bordered: true, borderColor: '#555555' }
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
        labelStyle: 'inline',
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
        headerBackgroundColor: '#E0E0E0',
        alternateRowColor: '#F9F9F9',
      }
    case 'InvoiceHeader':
      return {
        fields: DEFAULT_INVOICE_HEADER_FIELDS.map((f) => ({ ...f })),
        title: 'FATURA BİLGİLERİ',
        showTitle: false,
        bordered: true,
        labelStyle: 'table',
      }
    case 'InvoiceTotals':
      return {
        fields: DEFAULT_INVOICE_TOTALS_FIELDS.map((f) => ({ ...f })),
        showCurrency: true,
        currencyXpath: '//cbc:DocumentCurrencyCode',
      }
    case 'GibLogo':
      return { width: '80px', height: '80px', alignment: 'center' }
  }
}

interface EditorState {
  templateId: string | null
  templateName: string
  xsltStoragePath: string | null
  sections: Section[]
  blocks: Record<string, Block>
  selectedBlockId: string | null
  isDirty: boolean

  // undo/redo
  past: Array<{ sections: Section[]; blocks: Record<string, Block> }>
  future: Array<{ sections: Section[]; blocks: Record<string, Block> }>

  // actions
  setTemplateId: (id: string | null) => void
  setTemplateName: (name: string) => void
  setXsltStoragePath: (path: string | null) => void
  addBlock: (sectionId: string, type: BlockType, atIndex?: number) => void
  removeBlock: (sectionId: string, blockId: string) => void
  moveBlock: (fromSectionId: string, toSectionId: string, blockId: string, toIndex: number) => void
  updateBlockConfig: (blockId: string, config: Partial<BlockConfig['config']>) => void
  updateBlockLayout: (blockId: string, layout: Partial<BlockLayout>) => void
  selectBlock: (blockId: string | null) => void

  addSection: (name?: string) => void
  removeSection: (sectionId: string) => void
  updateSection: (sectionId: string, patch: Partial<Pick<Section, 'name' | 'layout'>>) => void
  reorderSections: (fromIndex: number, toIndex: number) => void
  duplicateSection: (sectionId: string) => void

  loadTree: (tree: BlockTree) => void
  resetTree: () => void

  undo: () => void
  redo: () => void
}

function snapshot(state: Pick<EditorState, 'sections' | 'blocks'>) {
  return {
    sections: JSON.parse(JSON.stringify(state.sections)),
    blocks: JSON.parse(JSON.stringify(state.blocks)),
  }
}

export const useEditorStore = create<EditorState>((set, get) => ({
  templateId: null,
  templateName: 'Yeni Şablon',
  xsltStoragePath: null,
  sections: [],
  blocks: {},
  selectedBlockId: null,
  isDirty: false,
  past: [],
  future: [],

  setTemplateId: (id) => set({ templateId: id }),
  setTemplateName: (name) => set({ templateName: name, isDirty: true }),
  setXsltStoragePath: (path) => set({ xsltStoragePath: path }),

  // ---- helpers ----

  addBlock(sectionId, type, atIndex) {
    const state = get()
    const section = state.sections.find((s) => s.id === sectionId)
    if (!section) return

    const prev = snapshot(state)
    const id = uuidv4()
    const layoutWidth = section.layout === 'three-column' ? '1/3'
      : section.layout === 'two-column' ? '1/2'
      : 'full'
    const block: Block = { id, type, config: defaultConfig(type), layout: { ...DEFAULT_BLOCK_LAYOUT, width: layoutWidth } }

    const newBlockIds = [...section.blockIds]
    if (atIndex !== undefined) {
      newBlockIds.splice(atIndex, 0, id)
    } else {
      newBlockIds.push(id)
    }

    set((s) => ({
      blocks: { ...s.blocks, [id]: block },
      sections: s.sections.map((sec) =>
        sec.id === sectionId ? { ...sec, blockIds: newBlockIds } : sec
      ),
      selectedBlockId: id,
      isDirty: true,
      past: [...s.past.slice(-(MAX_HISTORY - 1)), prev],
      future: [],
    }))
  },

  removeBlock(sectionId, blockId) {
    const state = get()
    const prev = snapshot(state)

    const { [blockId]: _removed, ...remainingBlocks } = state.blocks

    set((s) => ({
      blocks: remainingBlocks,
      sections: s.sections.map((sec) =>
        sec.id === sectionId
          ? { ...sec, blockIds: sec.blockIds.filter((id) => id !== blockId) }
          : sec
      ),
      selectedBlockId: s.selectedBlockId === blockId ? null : s.selectedBlockId,
      isDirty: true,
      past: [...s.past.slice(-(MAX_HISTORY - 1)), prev],
      future: [],
    }))
  },

  moveBlock(fromSectionId, toSectionId, blockId, toIndex) {
    const state = get()
    const prev = snapshot(state)

    set((s) => {
      const newSections = s.sections.map((sec) => {
        if (sec.id === fromSectionId && sec.id === toSectionId) {
          const ids = sec.blockIds.filter((id) => id !== blockId)
          ids.splice(toIndex, 0, blockId)
          return { ...sec, blockIds: ids }
        }
        if (sec.id === fromSectionId) {
          return { ...sec, blockIds: sec.blockIds.filter((id) => id !== blockId) }
        }
        if (sec.id === toSectionId) {
          const ids = [...sec.blockIds]
          ids.splice(toIndex, 0, blockId)
          return { ...sec, blockIds: ids }
        }
        return sec
      })
      return {
        sections: newSections,
        isDirty: true,
        past: [...s.past.slice(-(MAX_HISTORY - 1)), prev],
        future: [],
      }
    })
  },

  updateBlockConfig(blockId, config) {
    const state = get()
    const prev = snapshot(state)
    const existing = state.blocks[blockId]
    if (!existing) return

    set((s) => ({
      blocks: {
        ...s.blocks,
        [blockId]: { ...existing, config: { ...existing.config, ...config } },
      },
      isDirty: true,
      past: [...s.past.slice(-(MAX_HISTORY - 1)), prev],
      future: [],
    }))
  },

  updateBlockLayout(blockId, layoutPatch) {
    const state = get()
    const prev = snapshot(state)
    const existing = state.blocks[blockId]
    if (!existing) return

    set((s) => ({
      blocks: {
        ...s.blocks,
        [blockId]: {
          ...existing,
          layout: { ...DEFAULT_BLOCK_LAYOUT, ...existing.layout, ...layoutPatch },
        },
      },
      isDirty: true,
      past: [...s.past.slice(-(MAX_HISTORY - 1)), prev],
      future: [],
    }))
  },

  selectBlock(blockId) {
    set({ selectedBlockId: blockId })
  },

  addSection(name) {
    const state = get()
    const prev = snapshot(state)
    const id = uuidv4()
    const order = state.sections.length + 1
    const section: Section = {
      id,
      name: name ?? `Bölüm ${order}`,
      order,
      layout: 'single-column',
      blockIds: [],
    }
    set((s) => ({
      sections: [...s.sections, section],
      isDirty: true,
      past: [...s.past.slice(-(MAX_HISTORY - 1)), prev],
      future: [],
    }))
  },

  removeSection(sectionId) {
    const state = get()
    const prev = snapshot(state)
    const section = state.sections.find((s) => s.id === sectionId)
    if (!section) return

    const remainingBlocks = { ...state.blocks }
    for (const blockId of section.blockIds) {
      delete remainingBlocks[blockId]
    }

    set((s) => ({
      sections: s.sections.filter((sec) => sec.id !== sectionId),
      blocks: remainingBlocks,
      selectedBlockId: section.blockIds.includes(s.selectedBlockId ?? '')
        ? null
        : s.selectedBlockId,
      isDirty: true,
      past: [...s.past.slice(-(MAX_HISTORY - 1)), prev],
      future: [],
    }))
  },

  updateSection(sectionId, patch) {
    set((s) => ({
      sections: s.sections.map((sec) =>
        sec.id === sectionId ? { ...sec, ...patch } : sec
      ),
      isDirty: true,
    }))
  },

  reorderSections(fromIndex, toIndex) {
    const state = get()
    const prev = snapshot(state)
    const sections = [...state.sections]
    const [moved] = sections.splice(fromIndex, 1)
    sections.splice(toIndex, 0, moved)
    set((s) => ({
      sections,
      isDirty: true,
      past: [...s.past.slice(-(MAX_HISTORY - 1)), prev],
      future: [],
    }))
  },

  duplicateSection(sectionId) {
    const state = get()
    const prev = snapshot(state)
    const source = state.sections.find((s) => s.id === sectionId)
    if (!source) return

    const newSectionId = uuidv4()
    const newBlockIds: string[] = []
    const newBlocks: Record<string, Block> = {}

    for (const oldId of source.blockIds) {
      const oldBlock = state.blocks[oldId]
      if (!oldBlock) continue
      const newId = uuidv4()
      newBlockIds.push(newId)
      newBlocks[newId] = {
        id: newId,
        type: oldBlock.type,
        config: JSON.parse(JSON.stringify(oldBlock.config)),
        layout: { ...oldBlock.layout },
      }
    }

    const newSection: Section = {
      id: newSectionId,
      name: `${source.name} (kopya)`,
      order: state.sections.length + 1,
      layout: source.layout,
      blockIds: newBlockIds,
    }

    const srcIndex = state.sections.findIndex((s) => s.id === sectionId)
    const sections = [...state.sections]
    sections.splice(srcIndex + 1, 0, newSection)

    set((s) => ({
      sections,
      blocks: { ...s.blocks, ...newBlocks },
      isDirty: true,
      past: [...s.past.slice(-(MAX_HISTORY - 1)), prev],
      future: [],
    }))
  },

  loadTree(tree) {
    set({
      sections: tree.sections,
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
      xsltStoragePath: null,
      sections: [],
      blocks: {},
      selectedBlockId: null,
      isDirty: false,
      past: [],
      future: [],
    })
  },

  undo() {
    const { past, sections, blocks, future } = get()
    if (past.length === 0) return
    const prev = past[past.length - 1]
    set({
      sections: prev.sections,
      blocks: prev.blocks,
      past: past.slice(0, -1),
      future: [snapshot({ sections, blocks }), ...future.slice(0, MAX_HISTORY - 1)],
      isDirty: true,
    })
  },

  redo() {
    const { future, sections, blocks, past } = get()
    if (future.length === 0) return
    const next = future[0]
    set({
      sections: next.sections,
      blocks: next.blocks,
      future: future.slice(1),
      past: [...past.slice(-(MAX_HISTORY - 1)), snapshot({ sections, blocks })],
      isDirty: true,
    })
  },
}))
