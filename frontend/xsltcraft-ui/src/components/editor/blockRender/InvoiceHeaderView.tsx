import { useMemo } from 'react'
import type { InvoiceHeaderBlockConfig } from '../../../types/blocks'
import { evalNodes, evalString, textOf, type Resolved, type XmlContext } from './xmlEval'
import { Val } from './parts'
import { borderOf, P_RESET } from './invoiceStyle'

interface Row { key: string; label: string; value: Resolved }

const val = (text: string): Resolved => (text ? { kind: 'value', text } : { kind: 'empty' })

/** Sabit satır: koşullu (`optional`) satır, değer boşsa çıktıdaki `xsl:if` gibi gizlenir. */
function push(rows: Row[], key: string, label: string, value: Resolved, optional = false) {
  if (optional && value.kind === 'empty') return
  rows.push({ key, label, value })
}

function customRows(rows: Row[], config: InvoiceHeaderBlockConfig, ctx: XmlContext | null) {
  ;[...(config.fields ?? [])]
    .filter((f) => f.visible && f.isCustom)
    .sort((a, b) => a.order - b.order)
    .forEach((f) => push(rows, f.key, f.label, evalString(ctx, f.xpath)))
}

function invoiceRows(config: InvoiceHeaderBlockConfig, ctx: XmlContext | null): Row[] {
  const rows: Row[] = []
  const inv = '/n1:Invoice'
  const isPusula = textOf(evalString(ctx, `${inv}/cbc:ProfileID`)) === 'EGIDERPUSULASI'
  push(rows, 'customizationId', 'Özelleştirme No:', evalString(ctx, `${inv}/cbc:CustomizationID`))
  push(rows, 'profileId', 'Senaryo:', evalString(ctx, `${inv}/cbc:ProfileID`))
  if (!isPusula) push(rows, 'typeCode', 'Fatura Tipi:', evalString(ctx, `${inv}/cbc:InvoiceTypeCode`))
  push(rows, 'accountingCost', 'İlave Fatura Tipi:', evalString(ctx, `${inv}/cbc:AccountingCost`), true)
  push(rows, 'id', isPusula ? 'Pusula No:' : 'Fatura No:', evalString(ctx, `${inv}/cbc:ID`))
  push(rows, 'issueDate', isPusula ? 'Pusula Tarihi:' : 'Fatura Tarihi:', evalString(ctx, `${inv}/cbc:IssueDate`))
  ;(evalNodes(ctx, `${inv}/cac:DespatchDocumentReference`) ?? []).slice(0, 5).forEach((n, i) => {
    push(rows, `despatchId${i}`, 'İrsaliye No:', evalString(ctx, 'cbc:ID', n))
    push(rows, `despatchDate${i}`, 'İrsaliye Tarihi:', evalString(ctx, 'cbc:IssueDate', n))
  })
  push(rows, 'orderId', 'Sipariş No:', evalString(ctx, `${inv}/cac:OrderReference/cbc:ID`), true)
  push(rows, 'orderDate', 'Sipariş Tarihi:', evalString(ctx, `${inv}/cac:OrderReference/cbc:IssueDate`), true)
  push(rows, 'paymentDue', 'Ödeme Tarihi:', evalString(ctx, `${inv}/cac:PaymentMeans/cbc:PaymentDueDate`), true)
  customRows(rows, config, ctx)
  return rows
}

function despatchRows(config: InvoiceHeaderBlockConfig, ctx: XmlContext | null): Row[] {
  const rows: Row[] = []
  const d = '/n1:DespatchAdvice'
  const dn = `${d}/cac:Shipment/cac:Delivery/cac:Despatch`
  const withTime = (date: string, time: string) =>
    val([textOf(evalString(ctx, date)), textOf(evalString(ctx, time)).slice(0, 5)].filter(Boolean).join(' '))
  push(rows, 'customizationId', 'Özelleştirme No:', evalString(ctx, `${d}/cbc:CustomizationID`))
  push(rows, 'profileId', 'Senaryo:', evalString(ctx, `${d}/cbc:ProfileID`))
  push(rows, 'typeCode', 'İrsaliye Tipi:', evalString(ctx, `${d}/cbc:DespatchAdviceTypeCode`))
  push(rows, 'id', 'İrsaliye No:', evalString(ctx, `${d}/cbc:ID`))
  push(rows, 'issueDate', 'İrsaliye Tarihi:', withTime(`${d}/cbc:IssueDate`, `${d}/cbc:IssueTime`))
  push(rows, 'despatchDate', 'Sevk Tarihi:', withTime(`${dn}/cbc:ActualDespatchDate`, `${dn}/cbc:ActualDespatchTime`), true)
  push(rows, 'orderId', 'Sipariş No:', evalString(ctx, `${d}/cac:OrderReference/cbc:ID`), true)
  push(rows, 'orderDate', 'Sipariş Tarihi:', evalString(ctx, `${d}/cac:OrderReference/cbc:IssueDate`), true)
  customRows(rows, config, ctx)
  return rows
}

function shipmentRows(config: InvoiceHeaderBlockConfig, ctx: XmlContext | null): Row[] {
  const rows: Row[] = []
  const each = (key: string, label: string, xpath: string, value: (n: Node) => string) =>
    (evalNodes(ctx, xpath) ?? []).slice(0, 3).forEach((n, i) => push(rows, `${key}${i}`, label, val(value(n))))
  const s = (n: Node, x: string) => textOf(evalString(ctx, x, n))
  each('carrier', 'Taşıyıcı Firma:', '//cac:Shipment/cac:Delivery/cac:CarrierParty',
    (n) => `${s(n, 'cac:PartyName/cbc:Name')} (VKN: ${s(n, 'cac:PartyIdentification/cbc:ID')})`)
  each('plate', 'Araç Plaka No:', '//cac:ShipmentStage/cac:TransportMeans/cac:RoadTransport/cbc:LicensePlateID', (n) => s(n, '.'))
  each('trailer', 'Dorse Plaka No:', "//cac:TransportHandlingUnit/cac:TransportEquipment/cbc:ID[@schemeID='DORSEPLAKA']", (n) => s(n, '.'))
  each('driver', 'Şoför:', '//cac:ShipmentStage/cac:DriverPerson[cbc:FirstName]', (n) => {
    const tckn = s(n, 'cbc:NationalityID')
    return `${s(n, 'cbc:FirstName')} ${s(n, 'cbc:FamilyName')}${tckn ? `, TCKN: ${tckn}` : ''}`
  })
  each('deliverer', 'Teslim Eden:', '//cac:DespatchSupplierParty/cac:DespatchContact/cbc:Name', (n) => s(n, '.'))
  customRows(rows, config, ctx)
  return rows
}

/** Fatura/İrsaliye Başlığı (sabit UBL satırları + özel alanlar) ve Taşıyıcı Bilgileri. */
export default function InvoiceHeaderView({
  config,
  ctx,
  variant,
}: {
  config: InvoiceHeaderBlockConfig
  ctx: XmlContext | null
  variant: 'invoice' | 'despatch' | 'shipment'
}) {
  const rows = useMemo(
    () => (variant === 'shipment' ? shipmentRows : variant === 'despatch' ? despatchRows : invoiceRows)(config, ctx),
    [config, ctx, variant],
  )
  const fontSize = config.fontSize || '10.4px'

  if (variant === 'shipment') {
    return (
      <div style={{ fontSize, ...(config.bordered ? { border: borderOf(config.borderStyle), padding: '4px 8px' } : {}) }}>
        {config.showTitle && config.title && <p style={{ ...P_RESET, fontWeight: 'bold', marginBottom: 4 }}>{config.title}</p>}
        <table style={{ width: '100%', borderCollapse: 'collapse' }}>
          <tbody>
            {rows.map((r) => (
              <tr key={r.key}>
                <td style={{ fontWeight: 'bold', padding: '1px 4px', whiteSpace: 'nowrap', width: '35%', verticalAlign: 'top' }}>{r.label}</td>
                <td style={{ padding: '1px 4px' }}><Val r={r.value} /></td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    )
  }

  const cell = { padding: '2px 6px', width: '50%', ...(config.bordered ? { border: borderOf(config.borderStyle) } : {}) }
  return (
    <div style={{ fontSize }}>
      {variant === 'despatch' && config.showTitle && config.title && (
        <p style={{ ...P_RESET, fontWeight: 'bold', marginBottom: 4 }}>{config.title}</p>
      )}
      <table style={{ width: '100%', borderCollapse: 'collapse', ...(config.bordered ? { border: borderOf(config.borderStyle) } : {}) }}>
        <tbody>
          {rows.map((r) => (
            <tr key={r.key}>
              <td style={{ ...cell, fontWeight: 'bold' }}>{r.label}</td>
              <td style={cell}><Val r={r.value} /></td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  )
}
