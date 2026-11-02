import { memo } from 'react'
import type { BlockConfig, BlockType } from '../../../types/blocks'
import type { XmlContext } from './xmlEval'
import { ROOT_STYLE } from './invoiceStyle'
import PartyInfoView from './PartyInfoView'
import InvoiceHeaderView from './InvoiceHeaderView'
import InvoiceLineTableView from './InvoiceLineTableView'
import InvoiceTotalsView from './InvoiceTotalsView'
import { ConditionalTextView, HeadingView, ParagraphView, TextView } from './TextViews'
import { DocumentInfoView, TableView, TaxSummaryView, TotalsView } from './TableViews'
import { BankInfoView, DividerView, EttnView, NotesView, SpacerView } from './MiscViews'
import { GibKarekodView, GibLogoView, ImageView } from './MediaViews'

interface BlockContentProps {
  type: BlockType
  config: BlockConfig['config']
  ctx: XmlContext | null
  /** Şablon türü e-İrsaliye mi (editorStore.documentType) — generator'daki _isDespatch. */
  despatch: boolean
}

/**
 * Bloğun fatura içeriği (config + etkin örnek XML). `memo`: yalnız tip/config/XML değişince yeniden çizilir —
 * sürükleme/boyutlandırma (gridLayout) içerik değerlendirmesini tetiklemez.
 */
function BlockContent({ type, config, ctx, despatch }: BlockContentProps) {
  const cfg = config as never
  let body: React.ReactNode
  switch (type) {
    case 'PartyInfo': body = <PartyInfoView config={cfg} ctx={ctx} />; break
    case 'InvoiceHeader': body = <InvoiceHeaderView config={cfg} ctx={ctx} variant={despatch ? 'despatch' : 'invoice'} />; break
    case 'ShipmentInfo': body = <InvoiceHeaderView config={cfg} ctx={ctx} variant="shipment" />; break
    case 'InvoiceLineTable': body = <InvoiceLineTableView config={cfg} ctx={ctx} />; break
    case 'InvoiceTotals': body = <InvoiceTotalsView config={cfg} ctx={ctx} />; break
    case 'Text': body = <TextView config={cfg} ctx={ctx} />; break
    case 'Heading': body = <HeadingView config={cfg} ctx={ctx} />; break
    case 'Paragraph': body = <ParagraphView config={cfg} ctx={ctx} />; break
    case 'ConditionalText': body = <ConditionalTextView config={cfg} ctx={ctx} />; break
    case 'Table': body = <TableView config={cfg} ctx={ctx} />; break
    case 'DocumentInfo': body = <DocumentInfoView config={cfg} ctx={ctx} />; break
    case 'Totals': body = <TotalsView config={cfg} ctx={ctx} />; break
    case 'TaxSummary': body = <TaxSummaryView config={cfg} ctx={ctx} />; break
    case 'Notes': body = <NotesView config={cfg} ctx={ctx} />; break
    case 'BankInfo': body = <BankInfoView config={cfg} />; break
    case 'ETTN': body = <EttnView config={cfg} ctx={ctx} />; break
    case 'Divider': body = <DividerView config={cfg} />; break
    case 'Spacer': body = <SpacerView config={cfg} />; break
    case 'Image': body = <ImageView config={cfg} />; break
    case 'GibKarekod': body = <GibKarekodView config={cfg} />; break
    case 'GibLogo': body = <GibLogoView config={cfg} ctx={ctx} despatch={despatch} />; break
    default: body = null
  }
  return <div style={ROOT_STYLE}>{body}</div>
}

export default memo(BlockContent)
