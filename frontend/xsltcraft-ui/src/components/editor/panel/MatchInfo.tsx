import { useMemo } from 'react'
import { AlertTriangle, Check, CornerDownRight, Info } from 'lucide-react'
import { useXmlContext } from '../blockRender/useXmlContext'
import { evalNodes, evalString, firstNode } from '../blockRender/xmlEval'

/**
 * XPath alanı altındaki eşleşme bilgisi — Tasarım Alanı çizimindeki değerlendiriciyle (önbellekli) hesaplanır.
 * `nodes`: "Örnek XML'de N satır eşleşti"; `value`: çözülen değer. Göreli ifadeler `contextXPath`'in ilk düğümüne göre.
 */
export default function MatchInfo({
  xpath,
  mode = 'value',
  contextXPath,
  noun = 'satır',
}: {
  xpath: string
  mode?: 'value' | 'nodes'
  contextXPath?: string
  noun?: string
}) {
  const ctx = useXmlContext()

  const state = useMemo(() => {
    const expr = xpath.trim()
    if (!ctx || !expr) return null
    let node: Node | undefined
    if (contextXPath?.trim()) {
      const n = firstNode(ctx, contextXPath)
      if (!n) return { tone: 'muted' as const, text: 'Bağlam (satır kaynağı) örnek XML\'de eşleşmedi' }
      node = n
    }
    if (mode === 'nodes') {
      const nodes = evalNodes(ctx, expr, node)
      if (nodes === null) return { tone: 'muted' as const, text: 'Tarayıcıda değerlendirilemedi (XSLT/XPath 2.0 ifadesi olabilir)' }
      return nodes.length > 0
        ? { tone: 'ok' as const, text: `Örnek XML'de ${nodes.length} ${noun} eşleşti` }
        : { tone: 'warn' as const, text: "Örnek XML'de eşleşme yok" }
    }
    const r = evalString(ctx, expr, node)
    if (r.kind === 'unresolved') return { tone: 'muted' as const, text: 'Tarayıcıda değerlendirilemedi (XSLT/XPath 2.0 ifadesi olabilir)' }
    if (r.kind === 'empty') return { tone: 'warn' as const, text: "Örnek XML'de eşleşme yok" }
    return { tone: 'value' as const, text: r.text }
  }, [ctx, xpath, mode, contextXPath, noun])

  if (!state) return null
  const cls =
    state.tone === 'ok' || state.tone === 'value'
      ? 'text-fn-green-ink'
      : state.tone === 'warn'
        ? 'text-fn-orange-ink'
        : 'text-tx-subtle'
  const Icon = state.tone === 'ok' ? Check : state.tone === 'value' ? CornerDownRight : state.tone === 'warn' ? AlertTriangle : Info
  return (
    <span role="status" className={`flex min-w-0 items-center gap-1.5 px-0.5 text-[12px] ${cls}`} title={state.text}>
      <Icon size={13} strokeWidth={state.tone === 'ok' ? 2.4 : 2} aria-hidden="true" className="flex-none" />
      <span className="truncate">{state.text}</span>
    </span>
  )
}
