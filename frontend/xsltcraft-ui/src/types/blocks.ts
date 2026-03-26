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
  | 'PartyInfo'
  | 'InvoiceLineTable'
  | 'InvoiceHeader'
  | 'InvoiceTotals'
  | 'GibLogo'

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
  fontSize?: string
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
  borderStyle?: 'solid' | 'dashed' | 'dotted'
  fontSize?: string
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
  fontSize?: string
}

// BLOCK-10: Notes
export interface NotesBlockConfig {
  iterateOver: string
  prefix?: string
  staticLines: string[]
  staticPosition?: 'before' | 'after'   // XPath notlarına göre sabit notların yeri
  bordered?: boolean
  borderColor?: string
  borderStyle?: 'solid' | 'dashed' | 'dotted'
  fontSize?: string
}

// BLOCK-11: BankInfo
export interface BankInfoBlockConfig {
  bankName: string
  iban: string
  ibanLabel?: string    // varsayılan "IBAN: "
  bordered?: boolean
  borderColor?: string
  borderStyle?: 'solid' | 'dashed' | 'dotted'
  fontSize?: string
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
  fontSize?: string
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

// BLOCK-19: PartyInfo (Satıcı/Alıcı taraf bilgisi — sabit UBL XPath)
export type PartyType =
  | 'SupplierParty'
  | 'CustomerParty'
  | 'DespatchSupplierParty'
  | 'DeliveryCustomerParty'
  | 'BuyerCustomerParty'

export type PartyLabelStyle = 'table' | 'inline' | 'hidden'

export interface PartyInfoField {
  key: string
  label: string
  relativeXpath: string
  visible: boolean
  order: number
  isCustom?: boolean
}

export interface PartyInfoBlockConfig {
  partyType: PartyType
  fields: PartyInfoField[]
  title: string
  showTitle: boolean
  bordered: boolean
  borderStyle?: 'solid' | 'dashed' | 'dotted'
  labelStyle: PartyLabelStyle
  fontSize?: string
}

export const PARTY_TYPE_ROOT_XPATH: Record<PartyType, string> = {
  SupplierParty:         '//cac:AccountingSupplierParty/cac:Party',
  CustomerParty:         '//cac:AccountingCustomerParty/cac:Party',
  DespatchSupplierParty: '//cac:DespatchSupplierParty/cac:Party',
  DeliveryCustomerParty: '//cac:DeliveryCustomerParty/cac:Party',
  BuyerCustomerParty:    '//cac:BuyerCustomerParty/cac:Party',
}

export const PARTY_TYPE_LABELS: Record<PartyType, string> = {
  SupplierParty:         'SATICI',
  CustomerParty:         'ALICI',
  DespatchSupplierParty: 'GÖNDERİCİ',
  DeliveryCustomerParty: 'TESLİM ALICI',
  BuyerCustomerParty:    'ALICI MÜŞTERİ',
}

export const DEFAULT_PARTY_FIELDS: PartyInfoField[] = [
  { key: 'companyName',    label: 'Ünvan',          relativeXpath: 'cac:PartyName/cbc:Name',                     visible: true,  order: 0 },
  { key: 'vkn',            label: 'VKN/TCKN',       relativeXpath: 'cac:PartyIdentification/cbc:ID',             visible: true,  order: 1 },
  { key: 'taxOffice',      label: 'Vergi Dairesi',  relativeXpath: 'cac:PartyTaxScheme/cac:TaxScheme/cbc:Name',  visible: true,  order: 2 },
  { key: 'street',         label: 'Adres',          relativeXpath: 'cac:PostalAddress/cbc:StreetName',           visible: true,  order: 3 },
  { key: 'district',       label: 'İlçe',           relativeXpath: 'cac:PostalAddress/cbc:CitySubdivisionName', visible: true,  order: 4 },
  { key: 'city',           label: 'Şehir',          relativeXpath: 'cac:PostalAddress/cbc:CityName',             visible: true,  order: 5 },
  { key: 'country',        label: 'Ülke',           relativeXpath: 'cac:PostalAddress/cac:Country/cbc:Name',     visible: false, order: 6 },
  { key: 'postalZone',     label: 'Posta Kodu',     relativeXpath: 'cac:PostalAddress/cbc:PostalZone',           visible: false, order: 7 },
  { key: 'buildingNumber', label: 'Bina No',        relativeXpath: 'cac:PostalAddress/cbc:BuildingNumber',       visible: false, order: 8 },
  { key: 'buildingName',   label: 'Bina Adı',       relativeXpath: 'cac:PostalAddress/cbc:BuildingName',         visible: false, order: 9 },
  { key: 'telephone',      label: 'Telefon',        relativeXpath: 'cac:Contact/cbc:Telephone',                  visible: true,  order: 10 },
  { key: 'fax',            label: 'Faks',           relativeXpath: 'cac:Contact/cbc:Telefax',                    visible: false, order: 11 },
  { key: 'email',          label: 'E-Posta',        relativeXpath: 'cac:Contact/cbc:ElectronicMail',             visible: true,  order: 12 },
  { key: 'website',        label: 'Web Sitesi',     relativeXpath: 'cac:Contact/cbc:Note',                       visible: false, order: 13 },
]

// BLOCK-20: InvoiceLineTable (Fatura satır tablosu — hazır UBL XPath)
export type ColumnFormat_ILT = 'text' | 'currency' | 'number' | 'percent' | 'percentDirect' | 'unitCode' | 'quantityWithUnit'

export interface InvoiceLineColumn {
  key: string
  header: string
  relativeXpath: string
  width?: string
  format?: ColumnFormat_ILT
  visible: boolean
  order: number
  isCustom?: boolean
}

export interface InvoiceLineTableBlockConfig {
  iterateOver: string
  columns: InvoiceLineColumn[]
  title: string
  showTitle: boolean
  showHeader: boolean
  showRowNumber: boolean
  bordered: boolean
  borderStyle?: 'solid' | 'dashed' | 'dotted'
  alternateRowColor?: string
  headerBackgroundColor?: string
  fontSize?: string
}

export const DEFAULT_INVOICE_LINE_COLUMNS: InvoiceLineColumn[] = [
  { key: 'productCode',       header: 'Ürün Kodu',              relativeXpath: 'cac:Item/cac:SellersItemIdentification/cbc:ID', width: '10%',  format: 'text',     visible: true,  order: 0 },
  { key: 'description',       header: 'Mal Hizmet',             relativeXpath: 'cac:Item/cbc:Name',                             width: '20%',  format: 'text',     visible: true,  order: 1 },
  { key: 'quantity',           header: 'Miktar',                 relativeXpath: 'cbc:InvoicedQuantity',                          width: '7%',   format: 'quantityWithUnit',   visible: true,  order: 2 },
  { key: 'unitCode',           header: 'Birim',                  relativeXpath: 'cbc:InvoicedQuantity/@unitCode',                width: '5%',   format: 'unitCode', visible: false, order: 3 },
  { key: 'unitPrice',          header: 'Birim Fiyat',            relativeXpath: 'cac:Price/cbc:PriceAmount',                     width: '10%',  format: 'currency', visible: true,  order: 4 },
  { key: 'discountRate',       header: 'İskonto/Artırım Oranı',  relativeXpath: 'cac:AllowanceCharge/cbc:MultiplierFactorNumeric', width: '8%', format: 'percent', visible: true,  order: 5 },
  { key: 'discountAmount',     header: 'İskonto/Artırım Tutarı', relativeXpath: 'cac:AllowanceCharge/cbc:Amount',                width: '10%',  format: 'currency', visible: true,  order: 6 },
  { key: 'taxPercent',         header: 'KDV Oranı',              relativeXpath: 'cac:TaxTotal/cac:TaxSubtotal/cbc:Percent',      width: '6%',   format: 'percentDirect',  visible: true,  order: 7 },
  { key: 'taxAmount',          header: 'KDV Tutarı',             relativeXpath: 'cac:TaxTotal/cac:TaxSubtotal/cbc:TaxAmount',    width: '10%',  format: 'currency', visible: true,  order: 8 },
  { key: 'lineExtension',      header: 'Mal Hizmet Tutarı',     relativeXpath: 'cbc:LineExtensionAmount',                       width: '10%',  format: 'currency', visible: true,  order: 9 },
  { key: 'note',               header: 'Not',                   relativeXpath: 'cbc:Note',                                      width: '10%',  format: 'text',     visible: false, order: 10 },
  { key: 'buyerItemId',        header: 'Alıcı Ürün Kodu',       relativeXpath: 'cac:Item/cac:BuyersItemIdentification/cbc:ID',   width: '10%',  format: 'text',     visible: false, order: 11 },
]

// BLOCK-21: InvoiceHeader (Fatura başlık bilgileri — hazır UBL XPath'li)
export interface InvoiceHeaderField {
  key: string
  label: string
  xpath: string       // mutlak XPath (// ile başlar)
  visible: boolean
  order: number
  isCustom?: boolean
}

export type InvoiceHeaderLabelStyle = 'table' | 'inline'

export interface InvoiceHeaderBlockConfig {
  fields: InvoiceHeaderField[]
  title: string
  showTitle: boolean
  bordered: boolean
  borderStyle?: 'solid' | 'dashed' | 'dotted'
  labelStyle: InvoiceHeaderLabelStyle
  fontSize?: string
}

export const DEFAULT_INVOICE_HEADER_FIELDS: InvoiceHeaderField[] = [
  { key: 'customizationId', label: 'Özelleştirme No', xpath: '//cbc:CustomizationID',                            visible: false, order: 0 },
  { key: 'profileId',       label: 'Senaryo',          xpath: '//cbc:ProfileID',                                   visible: true,  order: 1 },
  { key: 'invoiceTypeCode', label: 'Fatura Tipi',       xpath: '//cbc:InvoiceTypeCode',                             visible: true,  order: 2 },
  { key: 'invoiceId',       label: 'Fatura No',         xpath: '//cbc:ID',                                          visible: true,  order: 3 },
  { key: 'issueDate',       label: 'Fatura Tarihi',     xpath: '//cbc:IssueDate',                                   visible: true,  order: 4 },
  { key: 'issueTime',       label: 'Fatura Saati',      xpath: '//cbc:IssueTime',                                   visible: false, order: 5 },
  { key: 'despatchId',      label: 'İrsaliye No',       xpath: '//cac:DespatchDocumentReference/cbc:ID',            visible: true,  order: 6 },
  { key: 'despatchDate',    label: 'İrsaliye Tarihi',   xpath: '//cac:DespatchDocumentReference/cbc:IssueDate',     visible: true,  order: 7 },
  { key: 'orderId',         label: 'Sipariş No',        xpath: '//cac:OrderReference/cbc:ID',                       visible: false, order: 8 },
  { key: 'orderDate',       label: 'Sipariş Tarihi',    xpath: '//cac:OrderReference/cbc:IssueDate',                visible: false, order: 9 },
  { key: 'contractId',      label: 'Sözleşme No',       xpath: '//cac:ContractDocumentReference/cbc:ID',            visible: false, order: 10 },
  { key: 'currency',        label: 'Para Birimi',       xpath: '//cbc:DocumentCurrencyCode',                        visible: false, order: 11 },
]

// BLOCK-23: GibLogo (GİB — sabit gömülü logo; kullanıcı yalnızca boyut/hizalama seçer)
export interface GibLogoBlockConfig {
  width?: string
  height?: string
  alignment: 'left' | 'center' | 'right'
}

// BLOCK-22: InvoiceTotals (Fatura dip toplamları — hazır UBL XPath'li)
export interface InvoiceTotalsField {
  key: string
  label: string
  xpath: string       // mutlak XPath
  visible: boolean
  highlight: boolean
  order: number
  isCustom?: boolean
}

export interface InvoiceTotalsBlockConfig {
  fields: InvoiceTotalsField[]
  showCurrency: boolean
  currencyXpath: string
  labelWidth?: string
  fontSize?: string
}

export const DEFAULT_INVOICE_TOTALS_FIELDS: InvoiceTotalsField[] = [
  { key: 'lineExtension', label: 'Mal Hizmet Tutarı',      xpath: '//cac:LegalMonetaryTotal/cbc:LineExtensionAmount',   visible: true,  highlight: false, order: 0 },
  { key: 'allowance',     label: 'İskonto Tutarı',          xpath: '//cac:LegalMonetaryTotal/cbc:AllowanceTotalAmount',  visible: true,  highlight: false, order: 1 },
  { key: 'taxExclusive',  label: 'KDV Matrahı',             xpath: '//cac:LegalMonetaryTotal/cbc:TaxExclusiveAmount',    visible: false, highlight: false, order: 2 },
  { key: 'taxAmount',     label: 'Hesaplanan KDV',          xpath: '//cac:TaxTotal/cbc:TaxAmount',                       visible: true,  highlight: false, order: 3 },
  { key: 'taxInclusive',  label: 'Vergiler Dahil Toplam',   xpath: '//cac:LegalMonetaryTotal/cbc:TaxInclusiveAmount',    visible: true,  highlight: true,  order: 4 },
  { key: 'payable',       label: 'Ödenecek Tutar',          xpath: '//cac:LegalMonetaryTotal/cbc:PayableAmount',         visible: true,  highlight: true,  order: 5 },
  { key: 'prepaid',       label: 'Ödenen Tutar',            xpath: '//cac:LegalMonetaryTotal/cbc:PrepaidAmount',         visible: false, highlight: false, order: 6 },
  { key: 'rounding',      label: 'Yuvarlama',               xpath: '//cac:LegalMonetaryTotal/cbc:PayableRoundingAmount', visible: false, highlight: false, order: 7 },
]

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
  | { type: 'PartyInfo'; config: PartyInfoBlockConfig }
  | { type: 'InvoiceLineTable'; config: InvoiceLineTableBlockConfig }
  | { type: 'InvoiceHeader'; config: InvoiceHeaderBlockConfig }
  | { type: 'InvoiceTotals'; config: InvoiceTotalsBlockConfig }
  | { type: 'GibLogo'; config: GibLogoBlockConfig }

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
