import { useMemo, type CSSProperties } from 'react'
import type { InvoiceTotalsBlockConfig } from '../../../types/blocks'
import { evalNodes, evalString, textOf, type Resolved, type XmlContext } from './xmlEval'
import { Val } from './parts'
import { currencyLabel, formatAmount } from './format'
import { DOC } from './invoiceStyle'

interface Row { key: string; label: string; value: Resolved; highlight: boolean; bold: boolean }

const KDV = "/n1:Invoice/cac:TaxTotal/cac:TaxSubtotal[cac:TaxCategory/cac:TaxScheme/cbc:TaxTypeCode='0015']"
const OTV = "/n1:Invoice/cac:TaxTotal/cac:TaxSubtotal[cac:TaxCategory/cac:TaxScheme/cbc:TaxTypeCode!='0015']"

function money(raw: string, currency: string): Resolved {
  const text = formatAmount(raw)
  if (!text) return { kind: 'empty' }
  return { kind: 'value', text: currency ? `${text} ${currency}` : text }
}

/** Dip toplamlar: alan listesi + orana göre KDV/ÖTV dinamik satırları (generator'daki taxBaseGroups/taxSubtotalGroups). */
export default function InvoiceTotalsView({ config, ctx }: { config: InvoiceTotalsBlockConfig; ctx: XmlContext | null }) {
  const rows = useMemo(() => {
    const currencyCode = config.showCurrency ? textOf(evalString(ctx, config.currencyXpath || '//cbc:DocumentCurrencyCode')) : ''
    const currency = currencyLabel(currencyCode)
    const out: Row[] = []
    const fields = [...(config.fields ?? [])].filter((f) => f.visible).sort((a, b) => a.order - b.order)
    for (const f of fields) {
      const base = { highlight: f.highlight, bold: f.bold }
      if (f.key === 'taxBaseGroups' || f.key === 'taxSubtotalGroups') {
        if (ctx?.isDespatch) continue
        const amountXpath = f.key === 'taxBaseGroups' ? 'cbc:TaxableAmount' : 'cbc:TaxAmount'
        const kdvLabel = f.key === 'taxBaseGroups' ? 'KDV Matrahı' : 'Hesaplanan KDV'
        ;(evalNodes(ctx, KDV) ?? []).forEach((n, i) => {
          const pct = textOf(evalString(ctx, 'cbc:Percent', n))
          out.push({ ...base, key: `${f.key}${i}`, label: `${kdvLabel}${pct ? ` (%${pct})` : ''}`, value: money(textOf(evalString(ctx, amountXpath, n)), currency) })
        })
        const otv = evalNodes(ctx, OTV) ?? []
        if (otv.length > 0) {
          const sum = otv.reduce((acc, n) => acc + (Number(textOf(evalString(ctx, amountXpath, n))) || 0), 0)
          out.push({ ...base, key: `${f.key}otv`, label: f.key === 'taxBaseGroups' ? 'ÖTV Matrahı' : 'Hesaplanan ÖTV', value: money(String(sum), currency) })
        }
        continue
      }
      const r = evalString(ctx, f.xpath)
      out.push({ ...base, key: f.key, label: f.label, value: r.kind === 'value' ? money(r.text, currency) : r })
    }
    return out
  }, [config.fields, config.showCurrency, config.currencyXpath, ctx])

  const td: CSSProperties = { border: `1px solid ${DOC.totalsBorder}`, padding: '2px 8px', whiteSpace: 'nowrap', textAlign: 'right' }
  return (
    <table style={{ borderCollapse: 'collapse', marginLeft: 'auto', fontSize: config.fontSize || '9.5pt', minWidth: 'min(280px, 100%)' }}>
      <tbody>
        {rows.map((r) => {
          const row: CSSProperties = { background: r.highlight ? DOC.highlight : undefined, fontWeight: r.bold ? 'bold' : undefined }
          return (
            <tr key={r.key} style={row}>
              <td style={{ ...td, paddingRight: 12, width: config.labelWidth || undefined }}>{r.label}</td>
              <td style={{ ...td, minWidth: 120 }}><Val r={r.value} /></td>
            </tr>
          )
        })}
      </tbody>
    </table>
  )
}
