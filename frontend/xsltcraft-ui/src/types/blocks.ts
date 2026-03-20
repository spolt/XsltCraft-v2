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
  | 'Variable'
  | 'ConditionalText'
  | 'TaxSummary'
  | 'GibKarekod'

export interface Binding {
  xpath: string
  fallback?: string
}

// BLOCK-01: Text
export interface TextBlockConfig {
  content?: string
  binding?: Binding
  isStatic: boolean
  fontWeight?: 'bold' | 'normal'
  fontStyle?: 'italic' | 'normal'
  fontSize?: string
  color?: string
}

// BLOCK-02: Heading
export interface HeadingBlockConfig {
  level: 'H1' | 'H2' | 'H3' | 'H4'
  content?: string
  binding?: Binding
  isStatic: boolean
  fontWeight?: 'bold' | 'normal'
  fontStyle?: 'italic' | 'normal'
  fontSize?: string
  color?: string
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
export type ColumnFormat = 'text' | 'currency' | 'number' | 'date'

export interface TableColumn {
  header: string
  xpath: string
  width?: string
  format?: ColumnFormat
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
  bordered?: boolean
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
  labelWidth?: string
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
  showEttn: boolean
  showQR: boolean
  qrWidth: number
  qrHeight: number
  qrAlignment: 'left' | 'center' | 'right'
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

// BLOCK-15: Variable (XSL değişken tanımı — görünür çıktı üretmez)
export interface VariableBlockConfig {
  name: string
  xpath: string
}

// BLOCK-16: ConditionalText (koşula göre farklı metin/xpath)
export interface ConditionalTextBlockConfig {
  condition: ConditionalCondition
  thenIsStatic: boolean
  thenContent: string
  elseIsStatic: boolean
  elseContent: string
}

// BLOCK-18: GibKarekod (GİB standart e-Fatura karekodu — sabit XPath)
export interface GibKarekodBlockConfig {
  qrWidth: number
  qrHeight: number
  qrAlignment: 'left' | 'center' | 'right'
}

// BLOCK-17: TaxSummary (KDV özet tablosu — Türk e-Fatura)
export interface TaxSummaryBlockConfig {
  taxTotalXpath: string
  percentXpath: string
  taxableAmountXpath: string
  taxAmountXpath: string
  showPercent: boolean
  headerBackgroundColor?: string
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
  | { type: 'Variable'; config: VariableBlockConfig }
  | { type: 'ConditionalText'; config: ConditionalTextBlockConfig }
  | { type: 'TaxSummary'; config: TaxSummaryBlockConfig }
  | { type: 'GibKarekod'; config: GibKarekodBlockConfig }

// ── Blok düzeni (hizalama + genişlik) ──────────────────────────────────────

export type BlockAlignment = 'left' | 'center' | 'right'
export type BlockWidth = 'full' | '1/2' | '1/3' | '2/3'

export interface BlockLayout {
  alignment: BlockAlignment
  width: BlockWidth
}

export const DEFAULT_BLOCK_LAYOUT: BlockLayout = { alignment: 'left', width: 'full' }

export interface Block {
  id: string
  type: BlockType
  config: BlockConfig['config']
  layout: BlockLayout
}
