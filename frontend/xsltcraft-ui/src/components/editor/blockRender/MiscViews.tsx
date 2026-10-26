import { useMemo, type CSSProperties } from 'react'
import type {
  BankInfoBlockConfig,
  DividerBlockConfig,
  ETTNBlockConfig,
  NotesBlockConfig,
  SpacerBlockConfig,
} from '../../../types/blocks'
import { evalNodes, evalString, textOf, type Resolved, type XmlContext } from './xmlEval'
import { EmptyHint, Unresolved, Val } from './parts'
import { QrPlaceholder } from './MediaViews'
import { borderOf, DOC, P_RESET } from './invoiceStyle'

type Ctx = { ctx: XmlContext | null }

export function NotesView({ config, ctx }: { config: NotesBlockConfig } & Ctx) {
  const xmlNotes = useMemo<Resolved[] | null>(() => {
    if (!config.iterateOver?.trim()) return []
    const nodes = evalNodes(ctx, config.iterateOver)
    if (nodes === null) return null
    return nodes.slice(0, 20).map((n) => evalString(ctx, '.', n)).filter((r) => r.kind !== 'empty')
  }, [config.iterateOver, ctx])

  const staticLines = (config.staticLines ?? []).filter((l) => l.trim())
  const prefix = config.prefix ?? ''
  const p: CSSProperties = { ...P_RESET, lineHeight: 1.6, fontSize: config.fontSize || undefined }
  const staticEls = staticLines.map((line, i) => <p key={`s${i}`} style={p}><strong>{prefix}</strong>{line}</p>)
  const xmlEls = xmlNotes === null
    ? [<p key="u" style={p}><strong>{prefix}</strong><Unresolved xpath={config.iterateOver} /></p>]
    : xmlNotes.map((r, i) => <p key={`x${i}`} style={p}><strong>{prefix}</strong><Val r={r} /></p>)
  const before = config.staticPosition !== 'after'

  const outer: CSSProperties | undefined = config.bordered
    ? { border: borderOf(config.borderStyle, config.borderColor || DOC.border), padding: '4px 8px' }
    : undefined
  if (staticEls.length === 0 && xmlEls.length === 0) {
    return <div style={{ ...outer, height: '100%' }}><EmptyHint>Notlar</EmptyHint></div>
  }
  return <div style={outer}>{before ? [...staticEls, ...xmlEls] : [...xmlEls, ...staticEls]}</div>
}

export function BankInfoView({ config }: { config: BankInfoBlockConfig }) {
  const bordered = config.bordered === true
  const table: CSSProperties = {
    width: '100%',
    borderCollapse: 'collapse',
    fontSize: config.fontSize || undefined,
    border: bordered ? borderOf(config.borderStyle, config.borderColor || DOC.border) : undefined,
  }
  const empty = !config.bankName && !config.iban
  return (
    <table style={table}>
      <tbody>
        <tr>
          <td style={{ padding: '4px 8px', fontWeight: 'bold', color: empty ? DOC.placeholderInk : undefined }}>
            {config.bankName || (empty ? '[BANKA ADI]' : '')}
          </td>
          <td style={{ padding: '4px 8px', textAlign: 'right', color: empty ? DOC.placeholderInk : undefined }}>
            {config.ibanLabel ?? 'IBAN: '}{config.iban || (empty ? 'TR00 0000 0000 0000 0000 0000 00' : '')}
          </td>
        </tr>
      </tbody>
    </table>
  )
}

const JUSTIFY = { left: 'flex-start', center: 'center', right: 'flex-end' } as const

export function EttnView({ config, ctx }: { config: ETTNBlockConfig } & Ctx) {
  const ettn = useMemo(() => evalString(ctx, config.ettnXpath), [config.ettnXpath, ctx])
  if (!config.showEttn && !config.showQR) return <EmptyHint>ETTN</EmptyHint>
  return (
    <div>
      {config.showEttn && (
        <p style={{ ...P_RESET, marginBottom: 4 }}>
          <strong>ETTN:</strong>{' '}
          {config.ettnXpath?.trim() ? <Val r={ettn} /> : <span style={{ color: DOC.placeholderInk }}>XPath seçin</span>}
        </p>
      )}
      {config.showQR && (
        <div style={{ display: 'flex', justifyContent: JUSTIFY[config.qrAlignment ?? 'right'] }}>
          <QrPlaceholder width={config.qrWidth || 80} height={config.qrHeight || 80} label={textOf(ettn) ? 'ETTN karekodu' : 'Karekod'} />
        </div>
      )}
    </div>
  )
}

export function DividerView({ config }: { config: DividerBlockConfig }) {
  return (
    <div
      style={{
        borderTopStyle: config.style || 'solid',
        borderTopWidth: config.thickness || '1px',
        borderTopColor: config.color || DOC.dividerColor,
        marginTop: config.marginTop || '8px',
        marginBottom: config.marginBottom || '8px',
      }}
    />
  )
}

export function SpacerView({ config }: { config: SpacerBlockConfig }) {
  return <EmptyHint>Boşluk · {config.height || '—'}</EmptyHint>
}
