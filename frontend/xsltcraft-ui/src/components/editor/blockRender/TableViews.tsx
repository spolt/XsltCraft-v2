import { useMemo, type CSSProperties } from 'react'
import type {
  ColumnFormat,
  DocumentInfoBlockConfig,
  TableBlockConfig,
  TaxSummaryBlockConfig,
  TotalsBlockConfig,
} from '../../../types/blocks'
import { evalNodes, evalString, type Resolved, type XmlContext } from './xmlEval'
import { EmptyHint, Unresolved, Val } from './parts'
import { formatAmount, formatDate, formatNumber } from './format'
import { DOC } from './invoiceStyle'

type Ctx = { ctx: XmlContext | null }

const MAX_ROWS = 40

const DT_TABLE: CSSProperties = { width: '100%', borderCollapse: 'collapse', tableLayout: 'fixed', fontSize: '9pt' }
const DT_TH: CSSProperties = { border: `1px solid ${DOC.borderStrong}`, padding: '3px 6px', fontWeight: 'bold', textAlign: 'center', overflow: 'hidden' }
const DT_TD: CSSProperties = { border: `1px solid ${DOC.border}`, padding: '3px 6px', overflowWrap: 'break-word', overflow: 'hidden' }

function formatCell(r: Resolved, format: ColumnFormat | undefined): Resolved {
  if (r.kind !== 'value') return r
  const f = format ?? 'text'
  const text = f === 'currency' ? formatAmount(r.text) : f === 'number' ? formatNumber(r.text) : f === 'date' ? formatDate(r.text) : r.text
  return text ? { kind: 'value', text } : { kind: 'empty' }
}

export function TableView({ config, ctx }: { config: TableBlockConfig } & Ctx) {
  const rows = useMemo(() => {
    const nodes = evalNodes(ctx, config.iterateOver)
    if (nodes === null) return null
    return nodes.slice(0, MAX_ROWS).map((n) => (config.columns ?? []).map((c) => formatCell(evalString(ctx, c.xpath, n), c.format)))
  }, [config.iterateOver, config.columns, ctx])

  if (!config.columns?.length) return <EmptyHint>Tablo — sütun ekleyin</EmptyHint>
  const alt = config.alternateRowColor || DOC.altRow
  return (
    <table style={DT_TABLE}>
      <colgroup>{config.columns.map((c, i) => <col key={i} style={{ width: c.width || undefined }} />)}</colgroup>
      {config.showHeader && (
        <thead>
          <tr style={{ background: config.headerBackgroundColor || DOC.headerBg }}>
            {config.columns.map((c, i) => <th key={i} style={DT_TH}>{c.header}</th>)}
          </tr>
        </thead>
      )}
      <tbody>
        {rows === null ? (
          <tr><td colSpan={config.columns.length} style={DT_TD}><Unresolved xpath={config.iterateOver || '—'} /></td></tr>
        ) : (
          rows.map((cells, i) => (
            <tr key={i} style={i % 2 === 1 ? { background: alt } : undefined}>
              {cells.map((r, ci) => <td key={ci} style={DT_TD}><Val r={r} /></td>)}
            </tr>
          ))
        )}
      </tbody>
    </table>
  )
}

export function DocumentInfoView({ config, ctx }: { config: DocumentInfoBlockConfig } & Ctx) {
  const values = useMemo(() => (config.rows ?? []).map((r) => evalString(ctx, r.xpath)), [config.rows, ctx])
  if (!config.rows?.length) return <EmptyHint>Belge bilgisi — satır ekleyin</EmptyHint>
  const bordered = config.bordered ?? true
  const td: CSSProperties = { padding: '2px 6px', border: bordered ? `1px ${config.borderStyle || 'solid'} ${DOC.border}` : 'none' }
  return (
    <table style={{ width: '100%', borderCollapse: 'collapse', fontSize: config.fontSize || '9.5pt' }}>
      <tbody>
        {config.rows.map((row, i) => (
          <tr key={i}>
            <td style={{ ...td, fontWeight: 'bold', whiteSpace: 'nowrap', width: '40%' }}>{row.label}</td>
            <td style={td}><Val r={values[i]} /></td>
          </tr>
        ))}
      </tbody>
    </table>
  )
}

export function TotalsView({ config, ctx }: { config: TotalsBlockConfig } & Ctx) {
  const values = useMemo(() => (config.rows ?? []).map((r) => evalString(ctx, r.xpath)), [config.rows, ctx])
  if (!config.rows?.length) return <EmptyHint>Toplamlar — satır ekleyin</EmptyHint>
  const td: CSSProperties = { border: `1px solid ${DOC.totalsBorder}`, padding: '2px 8px', whiteSpace: 'nowrap', textAlign: 'right' }
  const align: CSSProperties = config.alignment === 'left' ? { marginRight: 'auto' } : config.alignment === 'center' ? { margin: '0 auto' } : { marginLeft: 'auto' }
  return (
    <table style={{ borderCollapse: 'collapse', fontSize: config.fontSize || '9.5pt', minWidth: 'min(280px, 100%)', ...align }}>
      <tbody>
        {config.rows.map((row, i) => (
          <tr key={i} style={row.highlight ? { background: DOC.highlight } : undefined}>
            <td style={{ ...td, paddingRight: 12, width: config.labelWidth || undefined }}><strong>{row.label}</strong></td>
            <td style={{ ...td, minWidth: 120 }}><Val r={values[i]} /></td>
          </tr>
        ))}
      </tbody>
    </table>
  )
}

export function TaxSummaryView({ config, ctx }: { config: TaxSummaryBlockConfig } & Ctx) {
  const rows = useMemo(() => {
    const nodes = evalNodes(ctx, config.taxTotalXpath)
    if (nodes === null) return null
    return nodes.slice(0, MAX_ROWS).map((n) => ({
      pct: evalString(ctx, config.percentXpath, n),
      base: formatCell(evalString(ctx, config.taxableAmountXpath, n), 'currency'),
      tax: formatCell(evalString(ctx, config.taxAmountXpath, n), 'currency'),
    }))
  }, [config.taxTotalXpath, config.percentXpath, config.taxableAmountXpath, config.taxAmountXpath, ctx])

  const cols = config.showPercent ? 3 : 2
  return (
    <table style={DT_TABLE}>
      <thead>
        <tr style={{ background: config.headerBackgroundColor || DOC.headerBg }}>
          {config.showPercent && <th style={DT_TH}>KDV Oranı</th>}
          <th style={DT_TH}>Matrah</th>
          <th style={DT_TH}>KDV Tutarı</th>
        </tr>
      </thead>
      <tbody>
        {rows === null ? (
          <tr><td colSpan={cols} style={DT_TD}><Unresolved xpath={config.taxTotalXpath || '—'} /></td></tr>
        ) : (
          rows.map((r, i) => (
            <tr key={i}>
              {config.showPercent && <td style={DT_TD}><Val r={r.pct} />{r.pct.kind === 'value' ? '%' : ''}</td>}
              <td style={DT_TD}><Val r={r.base} /></td>
              <td style={DT_TD}><Val r={r.tax} /></td>
            </tr>
          ))
        )}
      </tbody>
    </table>
  )
}
