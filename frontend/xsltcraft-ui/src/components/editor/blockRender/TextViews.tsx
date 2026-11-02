import { useMemo, type CSSProperties } from 'react'
import type {
  ConditionalCondition,
  ConditionalTextBlockConfig,
  HeadingBlockConfig,
  ParagraphBlockConfig,
  TextBlockConfig,
} from '../../../types/blocks'
import { evalBoolean, evalString, type Resolved, type XmlContext } from './xmlEval'
import { EmptyHint, Val } from './parts'
import { DOC, P_RESET } from './invoiceStyle'

type Ctx = { ctx: XmlContext | null }

/** Yalnız tanımlı stil anahtarları (undefined, başlık varsayılanlarını ezmesin — generator'daki BuildStyleAttr gibi). */
function textStyle(c: { fontWeight?: string; fontStyle?: string; fontSize?: string; color?: string }): CSSProperties {
  const s: CSSProperties = {}
  if (c.fontWeight && c.fontWeight !== 'normal') s.fontWeight = c.fontWeight as CSSProperties['fontWeight']
  if (c.fontStyle && c.fontStyle !== 'normal') s.fontStyle = c.fontStyle
  if (c.fontSize) s.fontSize = c.fontSize
  if (c.color) s.color = c.color
  return s
}

export function TextView({ config, ctx }: { config: TextBlockConfig } & Ctx) {
  const value = useMemo<Resolved>(() => {
    if (config.isStatic) return config.content ? { kind: 'value', text: config.content } : { kind: 'empty' }
    const r = evalString(ctx, config.binding?.xpath)
    if (r.kind === 'empty' && config.binding?.fallback) return { kind: 'value', text: config.binding.fallback }
    return r
  }, [config.isStatic, config.content, config.binding, ctx])

  if (value.kind === 'empty') return <EmptyHint>{config.isStatic ? 'Metin' : 'XPath bağlı metin'}</EmptyHint>
  return <p style={{ ...P_RESET, ...textStyle(config), whiteSpace: 'pre-wrap' }}><Val r={value} /></p>
}

const HEADING_STYLE: Record<HeadingBlockConfig['level'], CSSProperties> = {
  H1: { fontSize: '1.4em', fontFamily: DOC.headingFont, padding: '3px 0' },
  H2: { fontSize: '1em', color: 'brown', textTransform: 'uppercase', fontFamily: DOC.headingFont, padding: '3px 0' },
  H3: { fontSize: '1em' },
  H4: { fontSize: '1.1em', fontWeight: 'bold', fontFamily: DOC.headingFont },
}

export function HeadingView({ config, ctx }: { config: HeadingBlockConfig } & Ctx) {
  const value = useMemo<Resolved>(
    () => (config.isStatic
      ? (config.content ? { kind: 'value', text: config.content } : { kind: 'empty' })
      : evalString(ctx, config.binding?.xpath)),
    [config.isStatic, config.content, config.binding, ctx],
  )
  if (value.kind === 'empty') return <EmptyHint>Başlık ({config.level})</EmptyHint>
  const Tag = (config.level?.toLowerCase() ?? 'h2') as 'h1' | 'h2' | 'h3' | 'h4'
  return (
    <Tag style={{ margin: 0, fontWeight: 'bold', ...HEADING_STYLE[config.level ?? 'H2'], ...textStyle(config) }}>
      <Val r={value} />
    </Tag>
  )
}

export function ParagraphView({ config, ctx }: { config: ParagraphBlockConfig } & Ctx) {
  const parts = useMemo(
    () => (config.lines ?? []).map<Resolved>((l) =>
      l.isStatic ? (l.content ? { kind: 'value', text: l.content } : { kind: 'empty' }) : evalString(ctx, l.xpath)),
    [config.lines, ctx],
  )
  if (parts.every((p) => p.kind === 'empty')) return <EmptyHint>Paragraf</EmptyHint>
  return (
    <p style={{ ...P_RESET, fontSize: config.fontSize || undefined, whiteSpace: 'pre-wrap' }}>
      {parts.map((r, i) => <Val key={i} r={r} />)}
    </p>
  )
}

function conditionTest(c: ConditionalCondition): string {
  const x = c.xpath?.trim()
  const v = c.value ?? ''
  switch (c.operator) {
    case 'equals': return `${x} = '${v}'`
    case 'notEquals': return `${x} != '${v}'`
    case 'contains': return `contains((${x})[1], '${v}')`
    case 'greaterThan': return `number((${x})[1]) > ${v || 0}`
    case 'lessThan': return `number((${x})[1]) < ${v || 0}`
    case 'notExists': return `not(${x})`
    default: return x ?? ''
  }
}

export function ConditionalTextView({ config, ctx }: { config: ConditionalTextBlockConfig } & Ctx) {
  const value = useMemo<Resolved>(() => {
    const branch = (isStatic: boolean, content: string): Resolved =>
      isStatic ? (content ? { kind: 'value', text: content } : { kind: 'empty' }) : evalString(ctx, content)
    if (!config.condition?.xpath?.trim()) return branch(config.thenIsStatic, config.thenContent)
    const ok = evalBoolean(ctx, conditionTest(config.condition))
    if (ok === null) return { kind: 'unresolved', xpath: config.condition.xpath }
    return ok ? branch(config.thenIsStatic, config.thenContent) : branch(config.elseIsStatic, config.elseContent)
  }, [config, ctx])

  if (value.kind === 'empty') return <EmptyHint>Koşullu metin</EmptyHint>
  return <span style={{ fontSize: config.fontSize || undefined }}><Val r={value} /></span>
}
