import { useMemo, type CSSProperties } from 'react'
import type { ColumnFormat_ILT, InvoiceLineColumn, InvoiceLineTableBlockConfig } from '../../../types/blocks'
import { evalNodes, evalString, textOf, type Resolved, type XmlContext } from './xmlEval'
import { Unresolved, Val } from './parts'
import { currencyLabel, formatAmount, formatNumber, formatPercent, unitLabel } from './format'
import { borderOf, DOC, P_RESET } from './invoiceStyle'

/** Çok satırlı faturalarda tuvali hafif tutmak için çizilen en fazla satır. */
const MAX_RENDERED_ROWS = 40

const NBSP = String.fromCharCode(0xa0)

const NUMERIC: ColumnFormat_ILT[] = ['currency', 'number', 'percent', 'percentDirect', 'quantityWithUnit']

function cellText(
  col: InvoiceLineColumn,
  ctx: XmlContext | null,
  line: Node,
  docCurrency: string,
  showCurrency: boolean,
  isDespatch: boolean,
): Resolved {
  const r = evalString(ctx, col.relativeXpath, line)
  if (r.kind !== 'value') return r
  const format = col.format ?? 'text'
  let text = r.text
  switch (format) {
    case 'currency': {
      text = formatAmount(r.text)
      const cur = isDespatch ? textOf(evalString(ctx, `${col.relativeXpath}/@currencyID`, line)) : docCurrency
      if (text && showCurrency && cur) text = `${text} ${currencyLabel(cur)}`
      break
    }
    case 'number': text = formatNumber(r.text); break
    case 'percent': text = formatPercent(r.text, true); break
    case 'percentDirect': text = formatPercent(r.text, false); break
    case 'unitCode': text = unitLabel(r.text); break
    case 'quantityWithUnit': {
      const unit = textOf(evalString(ctx, `${col.relativeXpath}/@unitCode`, line))
      text = [formatNumber(r.text), unitLabel(unit)].filter(Boolean).join(NBSP)
      break
    }
  }
  return text ? { kind: 'value', text } : { kind: 'empty' }
}

/** Fatura/İrsaliye satırları: satır XPath'i üzerinden döner, kolon XPath'leri her satıra göre değerlendirilir. */
export default function InvoiceLineTableView({ config, ctx }: { config: InvoiceLineTableBlockConfig; ctx: XmlContext | null }) {
  const columns = useMemo(
    () => [...(config.columns ?? [])].filter((c) => c.visible).sort((a, b) => a.order - b.order),
    [config.columns],
  )

  const body = useMemo(() => {
    const lines = evalNodes(ctx, config.iterateOver)
    if (lines === null) return { unresolved: true as const, rows: [], total: 0 }
    const isDespatch = ctx?.isDespatch ?? false
    const docCurrency = textOf(evalString(ctx, '//cbc:DocumentCurrencyCode'))
    const showCurrency = config.showCurrency ?? true
    const rows = lines.slice(0, MAX_RENDERED_ROWS).map((line) =>
      columns.map((col) => cellText(col, ctx, line, docCurrency, showCurrency, isDespatch)),
    )
    return { unresolved: false as const, rows, total: lines.length }
  }, [ctx, config.iterateOver, config.showCurrency, columns])

  const fontSize = config.fontSize || '9pt'
  const border = config.bordered ? borderOf(config.borderStyle) : 'none'
  const headerBg = config.headerBackgroundColor || DOC.headerBg
  const altBg = config.alternateRowColor || DOC.altRow
  const cell: CSSProperties = { border, padding: '2px 4px' }

  return (
    <div>
      {config.showTitle && config.title && (
        <p style={{ ...P_RESET, fontWeight: 'bold', marginBottom: 4, fontSize }}>{config.title}</p>
      )}
      <table style={{ width: '100%', borderCollapse: 'collapse', fontSize }}>
        {config.showHeader && (
          <thead>
            <tr style={{ background: headerBg }}>
              {config.showRowNumber && <th style={{ ...cell, textAlign: 'center', width: '4%' }}>#</th>}
              {columns.map((c) => (
                <th key={c.key} style={{ ...cell, textAlign: 'center', width: c.width || undefined }}>{c.header}</th>
              ))}
            </tr>
          </thead>
        )}
        <tbody>
          {body.unresolved ? (
            <tr>
              <td colSpan={columns.length + (config.showRowNumber ? 1 : 0)} style={cell}>
                <Unresolved xpath={config.iterateOver} />
              </td>
            </tr>
          ) : (
            body.rows.map((cells, i) => (
              <tr key={i} style={{ background: i % 2 === 1 ? altBg : '#FFFFFF' }}>
                {config.showRowNumber && <td style={{ ...cell, textAlign: 'center' }}>{i + 1}</td>}
                {cells.map((r, ci) => (
                  <td key={columns[ci].key} style={{ ...cell, textAlign: NUMERIC.includes(columns[ci].format ?? 'text') ? 'right' : undefined }}>
                    <Val r={r} />
                  </td>
                ))}
              </tr>
            ))
          )}
        </tbody>
      </table>
      {body.total > MAX_RENDERED_ROWS && (
        <p style={{ ...P_RESET, fontSize: '8pt', color: DOC.placeholderInk, textAlign: 'center', padding: '2px 0' }}>
          + {body.total - MAX_RENDERED_ROWS} satır daha
        </p>
      )}
    </div>
  )
}
