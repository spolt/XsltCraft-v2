import { createElement } from 'react'
import {
  Calculator,
  CircleHelp,
  FileText,
  Heading,
  Image,
  Landmark,
  Minus,
  MoveVertical,
  PanelTop,
  Percent,
  Pilcrow,
  QrCode,
  Sigma,
  Stamp,
  StickyNote,
  Table,
  Table2,
  Truck,
  Type,
  UserRound,
  type LucideIcon,
  type LucideProps,
} from 'lucide-react'
import type { BlockType } from '../../types/blocks'
import type { DocumentType } from '../../types/template'
import type { FnColor } from '../../theme/uiThemes'

/*
 * Tasarım Alanı blok kataloğu: palet öğeleri, blok tipi ikonları, Türkçe adlar ve işlev renkleri.
 * Palet öğesi `id`'leri DnD kimliğidir (`palette-{id}`) — DEĞİŞTİRME.
 */

export interface PaletteItem {
  id: string
  type: BlockType
  label: string
  icon: LucideIcon
  configOverride?: Record<string, unknown>
}

export interface PaletteGroup {
  id: string
  title: string
  items: PaletteItem[]
}

const INVOICE_READY_BLOCKS: PaletteItem[] = [
  {
    id: 'SupplierInfo', type: 'PartyInfo', label: 'Satıcı Bilgileri', icon: UserRound,
    configOverride: { partyType: 'SupplierParty', title: 'SATICI' },
  },
  {
    id: 'BuyerInfo', type: 'PartyInfo', label: 'Alıcı Bilgileri', icon: UserRound,
    configOverride: { partyType: 'CustomerParty', title: 'ALICI' },
  },
  { id: 'InvoiceHeader',    type: 'InvoiceHeader',    label: 'Fatura Başlığı',        icon: PanelTop },
  { id: 'InvoiceLineTable', type: 'InvoiceLineTable', label: 'Fatura Satırları',      icon: Table },
  { id: 'InvoiceTotals',    type: 'InvoiceTotals',    label: 'Fatura Dip Toplamları', icon: Sigma },
  { id: 'Notes',            type: 'Notes',            label: 'Notlar',                icon: StickyNote },
  { id: 'BankInfo',         type: 'BankInfo',         label: 'Banka Bilgisi',         icon: Landmark },
]

const DESPATCH_READY_BLOCKS: PaletteItem[] = [
  {
    id: 'DespatchSupplierInfo', type: 'PartyInfo', label: 'Gönderici Bilgileri', icon: UserRound,
    configOverride: { partyType: 'DespatchSupplierParty', title: 'GÖNDERİCİ' },
  },
  {
    id: 'DeliveryCustomerInfo', type: 'PartyInfo', label: 'Alıcı Bilgileri', icon: UserRound,
    configOverride: { partyType: 'DeliveryCustomerParty', title: 'SAYIN' },
  },
  { id: 'DespatchHeader',    type: 'InvoiceHeader',    label: 'İrsaliye Başlığı',   icon: PanelTop },
  { id: 'DespatchLineTable', type: 'InvoiceLineTable', label: 'İrsaliye Satırları', icon: Table },
  { id: 'DespatchTotals',    type: 'InvoiceTotals',    label: 'Toplam Tutar',       icon: Sigma },
  { id: 'ShipmentInfo',      type: 'ShipmentInfo',     label: 'Taşıyıcı Bilgileri', icon: Truck },
  { id: 'Notes',             type: 'Notes',            label: 'Notlar',             icon: StickyNote },
  { id: 'BankInfo',          type: 'BankInfo',         label: 'Banka Bilgisi',      icon: Landmark },
]

const COMMON_GROUPS: PaletteGroup[] = [
  {
    id: 'basic',
    title: 'Temel',
    items: [
      { id: 'Text',            type: 'Text',            label: 'Metin',         icon: Type },
      { id: 'Heading',         type: 'Heading',         label: 'Başlık',        icon: Heading },
      { id: 'Paragraph',       type: 'Paragraph',       label: 'Paragraf',      icon: Pilcrow },
      { id: 'ConditionalText', type: 'ConditionalText', label: 'Koşullu Metin', icon: CircleHelp },
    ],
  },
  {
    id: 'structure',
    title: 'Yapı',
    items: [
      { id: 'Table',        type: 'Table',        label: 'Tablo',         icon: Table2 },
      { id: 'DocumentInfo', type: 'DocumentInfo', label: 'Belge Bilgisi', icon: FileText },
    ],
  },
  {
    id: 'media',
    title: 'Medya',
    items: [
      { id: 'Image',      type: 'Image',      label: 'Görsel',          icon: Image },
      { id: 'ETTN',       type: 'ETTN',       label: 'Dinamik Karekod', icon: QrCode },
      { id: 'GibKarekod', type: 'GibKarekod', label: 'GİB Karekod',     icon: QrCode },
      { id: 'GibLogo',    type: 'GibLogo',    label: 'GİB Logo',        icon: Stamp },
    ],
  },
  {
    id: 'layout',
    title: 'Düzen',
    items: [
      { id: 'Divider', type: 'Divider', label: 'Ayırıcı', icon: Minus },
      { id: 'Spacer',  type: 'Spacer',  label: 'Boşluk',  icon: MoveVertical },
    ],
  },
]

/** Belge türüne göre palet grupları (ilk grup belgeye özel hazır bloklar). */
export function paletteGroups(documentType: DocumentType): PaletteGroup[] {
  const ready: PaletteGroup = documentType === 'Despatch'
    ? { id: 'ready', title: 'e-İrsaliye blokları', items: DESPATCH_READY_BLOCKS }
    : { id: 'ready', title: 'e-Fatura blokları', items: INVOICE_READY_BLOCKS }
  return [ready, ...COMMON_GROUPS]
}

/** DnD kimliğinden (`palette-{id}`) palet öğesi — DragOverlay etiketi için. */
export function findPaletteItem(dndId: string): PaletteItem | undefined {
  if (!dndId.startsWith('palette-')) return undefined
  const id = dndId.slice('palette-'.length)
  return [...INVOICE_READY_BLOCKS, ...DESPATCH_READY_BLOCKS, ...COMMON_GROUPS.flatMap((g) => g.items)]
    .find((i) => i.id === id)
}

/** Blok tipi → ikon (canvas bloğu ve özellik paneli başlığı). */
const BLOCK_TYPE_ICON: Record<string, LucideIcon> = {
  Text: Type,
  Heading: Heading,
  Paragraph: Pilcrow,
  Table: Table2,
  Image: Image,
  DocumentInfo: FileText,
  Totals: Calculator,
  Notes: StickyNote,
  BankInfo: Landmark,
  ETTN: QrCode,
  GibKarekod: QrCode,
  Divider: Minus,
  Spacer: MoveVertical,
  ConditionalText: CircleHelp,
  TaxSummary: Percent,
  InvoiceLineTable: Table,
  PartyInfo: UserRound,
  InvoiceHeader: PanelTop,
  InvoiceTotals: Sigma,
  GibLogo: Stamp,
  ShipmentInfo: Truck,
}

export function blockTypeIcon(type: string): LucideIcon {
  return BLOCK_TYPE_ICON[type] ?? FileText
}

/** Blok tipinin ikonunu çizer (render sırasında bileşen türetmeden). */
export function BlockTypeIcon({ type, ...props }: { type: string } & LucideProps) {
  return createElement(blockTypeIcon(type), props)
}

/** Blok tipi → işlev rengi (canvas bloğundaki ikon kutusu). */
const BLOCK_TYPE_COLOR: Record<string, FnColor> = {
  PartyInfo: 'blue',
  InvoiceHeader: 'accent',
  InvoiceTotals: 'accent',
  InvoiceLineTable: 'green',
  Table: 'green',
  ShipmentInfo: 'green',
  TaxSummary: 'orange',
  Totals: 'orange',
  GibLogo: 'orange',
  Notes: 'gray',
  BankInfo: 'slate',
  Image: 'purple',
  ETTN: 'violet',
  GibKarekod: 'violet',
  Divider: 'gray',
  Spacer: 'gray',
}

export function blockTypeColor(type: string): FnColor {
  return BLOCK_TYPE_COLOR[type] ?? 'accent'
}

const BLOCK_LABEL_TR: Record<string, string> = {
  Text:             'Metin',
  Heading:          'Başlık',
  Paragraph:        'Paragraf',
  Table:            'Tablo',
  Image:            'Görsel',
  DocumentInfo:     'Belge Bilgisi',
  Totals:           'Toplamlar',
  Notes:            'Notlar',
  BankInfo:         'Banka Bilgisi',
  ETTN:             'Dinamik Karekod',
  Divider:          'Ayırıcı',
  Spacer:           'Boşluk',
  ConditionalText:  'Koşullu Metin',
  TaxSummary:       'KDV Özeti',
  GibKarekod:       'GİB Karekod',
  PartyInfo:        'Taraf Bilgisi',
  InvoiceLineTable: 'Fatura Satırları',
  InvoiceHeader:    'Fatura Başlığı',
  InvoiceTotals:    'Fatura Dip Toplamları',
  GibLogo:          'GİB Logo',
  ShipmentInfo:     'Taşıyıcı Bilgileri',
}

// e-İrsaliye şablonlarında aynı blok tiplerinin görünen adları
const DESPATCH_LABEL_TR: Record<string, string> = {
  InvoiceLineTable: 'İrsaliye Satırları',
  InvoiceHeader:    'İrsaliye Başlığı',
  InvoiceTotals:    'Toplam Tutar',
}

/** Bloğun belge türüne göre Türkçe görünen adı. */
export function blockLabel(type: string, documentType: DocumentType): string {
  return (documentType === 'Despatch' ? DESPATCH_LABEL_TR[type] : undefined) ?? BLOCK_LABEL_TR[type] ?? type
}

export const DOCUMENT_TYPE_LABEL: Record<DocumentType, string> = {
  Invoice: 'e-Fatura / e-Arşiv',
  Despatch: 'e-İrsaliye',
}
