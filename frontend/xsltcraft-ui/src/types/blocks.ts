// PRD §9.2 — 14 block tipi için TypeScript interface'leri

export type BlockType =
  | 'Text'
  | 'Heading'
  | 'Paragraph'
  | 'Table'
  | 'ForEach'
  | 'Conditional'
  | 'Image'
  | 'DocumentInfo'
  | 'Totals'
  | 'Notes'
  | 'BankInfo'
  | 'ETTN'
  | 'Divider'
  | 'Spacer'

export interface Binding {
  xpath: string
  fallback?: string
}

// BLOCK-01: Text
export interface TextBlockConfig {
  content?: string
  binding?: Binding
  isStatic: boolean
}

// BLOCK-02: Heading
export interface HeadingBlockConfig {
  level: 'H1' | 'H2' | 'H3' | 'H4'
  content?: string
  binding?: Binding
  isStatic: boolean
}

// BLOCK-03: Paragraph
export interface ParagraphLine {
  isStatic: boolean
  content?: string
  xpath?: string
}

export interface ParagraphBlockConfig {
  lines: ParagraphLine[]
}

// BLOCK-04: Table
export interface TableColumn {
  header: string
  xpath: string
  width?: string
}

export interface TableBlockConfig {
  iterateOver: string
  columns: TableColumn[]
  showHeader: boolean
  alternateRowColor?: string
  headerBackgroundColor?: string
}

// BLOCK-05: ForEach (Loop Container)
export interface ForEachBlockConfig {
  iterateOver: string
  children: string[] // child block ID'leri
}

// BLOCK-06: Conditional
export type ConditionalOperator =
  | 'equals'
  | 'notEquals'
  | 'contains'
  | 'greaterThan'
  | 'lessThan'
  | 'exists'
  | 'notExists'

export interface ConditionalCondition {
  xpath: string
  operator: ConditionalOperator
  value?: string
}

export interface ConditionalBlockConfig {
  condition: ConditionalCondition
  thenBlockIds: string[]
  elseBlockIds: string[]
}

// BLOCK-07: Image
export type AssetType = 'logo' | 'signature' | 'other'
export type Alignment = 'left' | 'center' | 'right'

export interface ImageBlockConfig {
  assetId?: string
  assetType: AssetType
  altText?: string
  width?: string
  height?: string
  alignment: Alignment
  editableOnFreeTheme: boolean
}

// BLOCK-08: DocumentInfo
export interface DocumentInfoRow {
  label: string
  xpath: string
}

export interface DocumentInfoBlockConfig {
  rows: DocumentInfoRow[]
}

// BLOCK-09: Totals
export interface TotalsRow {
  label: string
  xpath: string
  highlight?: boolean
}

export interface TotalsBlockConfig {
  rows: TotalsRow[]
  alignment: Alignment
}

// BLOCK-10: Notes
export interface NotesBlockConfig {
  iterateOver: string
  prefix?: string
  staticLines: string[]
}

// BLOCK-11: BankInfo
export interface BankInfoBlockConfig {
  bankNameXpath?: string
  ibanXpath?: string
  paymentTermsXpath?: string
}

// BLOCK-12: ETTN
export interface ETTNBlockConfig {
  ettnXpath: string
  showQR: boolean
  qrAssetId?: string | null
}

// BLOCK-13: Divider
export type DividerStyle = 'solid' | 'dashed' | 'dotted'

export interface DividerBlockConfig {
  style: DividerStyle
  color?: string
  thickness?: string
  marginTop?: string
  marginBottom?: string
}

// BLOCK-14: Spacer
export interface SpacerBlockConfig {
  height: string
}

// Discriminated union: her block tipini config'iyle eşleştir
export type BlockConfig =
  | { type: 'Text'; config: TextBlockConfig }
  | { type: 'Heading'; config: HeadingBlockConfig }
  | { type: 'Paragraph'; config: ParagraphBlockConfig }
  | { type: 'Table'; config: TableBlockConfig }
  | { type: 'ForEach'; config: ForEachBlockConfig }
  | { type: 'Conditional'; config: ConditionalBlockConfig }
  | { type: 'Image'; config: ImageBlockConfig }
  | { type: 'DocumentInfo'; config: DocumentInfoBlockConfig }
  | { type: 'Totals'; config: TotalsBlockConfig }
  | { type: 'Notes'; config: NotesBlockConfig }
  | { type: 'BankInfo'; config: BankInfoBlockConfig }
  | { type: 'ETTN'; config: ETTNBlockConfig }
  | { type: 'Divider'; config: DividerBlockConfig }
  | { type: 'Spacer'; config: SpacerBlockConfig }

export interface Block {
  id: string
  type: BlockType
  config: BlockConfig['config']
}
