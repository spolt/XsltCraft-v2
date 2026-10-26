/*
 * Tasarım Alanı blok çizimi için tarayıcı içi XPath değerlendirici.
 * - Örnek XML her içerik için bir kez DOMParser ile ayrıştırılır (modül önbelleği).
 * - Sonuçlar bağlam düğümü × ifade bazında önbelleklenir (WeakMap → XML değişince kendiliğinden düşer).
 * - document.evaluate XPath 1.0'dır: XSLT fonksiyonları (current(), format-number(), key() …) ya da
 *   XPath 2.0 ifadeleri hata verir → `unresolved` döner; çağıran ifadeyi soluk mono metin olarak gösterir.
 * XML yalnız DOMParser ile okunur (DTD/dış varlık çözülmez, ağ isteği yok).
 */

const UBL_NS: Record<string, string> = {
  cbc: 'urn:oasis:names:specification:ubl:schema:xsd:CommonBasicComponents-2',
  cac: 'urn:oasis:names:specification:ubl:schema:xsd:CommonAggregateComponents-2',
  ext: 'urn:oasis:names:specification:ubl:schema:xsd:CommonExtensionComponents-2',
}

export type Resolved =
  | { kind: 'value'; text: string }
  | { kind: 'empty' }
  | { kind: 'unresolved'; xpath: string }

export interface XmlContext {
  doc: Document
  root: Element
  resolver: XPathNSResolver
  /** n1 kök elemanı DespatchAdvice ise true. */
  isDespatch: boolean
}

type NodeCache = WeakMap<Node, Map<string, unknown>>

const nodeCaches = new WeakMap<Document, NodeCache>()

// Son birkaç XML içeriği → bağlam (aynı içerik tüm bloklar için tek kez ayrıştırılır).
const CONTEXT_CACHE_LIMIT = 4
const contextCache = new Map<string, XmlContext | null>()

export function getXmlContext(content: string | undefined | null): XmlContext | null {
  if (!content) return null
  const hit = contextCache.get(content)
  if (hit !== undefined) {
    contextCache.delete(content)
    contextCache.set(content, hit)
    return hit
  }
  const ctx = buildContext(content)
  contextCache.set(content, ctx)
  if (contextCache.size > CONTEXT_CACHE_LIMIT) {
    const oldest = contextCache.keys().next().value
    if (oldest !== undefined) contextCache.delete(oldest)
  }
  return ctx
}

function buildContext(content: string): XmlContext | null {
  try {
    const doc = new DOMParser().parseFromString(content, 'application/xml')
    const root = doc.documentElement
    if (!root || doc.getElementsByTagName('parsererror').length > 0) return null
    const rootNs = root.namespaceURI
    const resolver: XPathNSResolver = (prefix: string | null) => {
      if (!prefix) return null
      if (prefix === 'n1') return rootNs
      return UBL_NS[prefix] ?? root.lookupNamespaceURI(prefix)
    }
    return { doc, root, resolver, isDespatch: root.localName === 'DespatchAdvice' }
  } catch {
    return null
  }
}

function cacheFor(ctx: XmlContext, node: Node): Map<string, unknown> {
  let byNode = nodeCaches.get(ctx.doc)
  if (!byNode) {
    byNode = new WeakMap()
    nodeCaches.set(ctx.doc, byNode)
  }
  let map = byNode.get(node)
  if (!map) {
    map = new Map()
    byNode.set(node, map)
  }
  return map
}

const UNRESOLVED = Symbol('unresolved')

function cached<T>(ctx: XmlContext, node: Node, key: string, compute: () => T): T | typeof UNRESOLVED {
  const map = cacheFor(ctx, node)
  if (map.has(key)) return map.get(key) as T | typeof UNRESOLVED
  let value: T | typeof UNRESOLVED
  try {
    value = compute()
  } catch {
    value = UNRESOLVED
  }
  map.set(key, value)
  return value
}

/** Tekil değer (XSLT `value-of select="(x)[1]"` karşılığı — düğüm kümesinde ilk düğümün metni). */
export function evalString(ctx: XmlContext | null, xpath: string | undefined, node?: Node): Resolved {
  const expr = xpath?.trim()
  if (!expr) return { kind: 'empty' }
  if (!ctx) return { kind: 'unresolved', xpath: expr }
  const result = cached(ctx, node ?? ctx.doc, `s:${expr}`, () =>
    ctx.doc.evaluate(expr, node ?? ctx.doc, ctx.resolver, XPathResult.STRING_TYPE, null).stringValue,
  )
  if (result === UNRESOLVED) return { kind: 'unresolved', xpath: expr }
  const text = (result as string).trim()
  return text ? { kind: 'value', text } : { kind: 'empty' }
}

/** Düğüm listesi (for-each). Çözülemezse null. */
export function evalNodes(ctx: XmlContext | null, xpath: string | undefined, node?: Node): Node[] | null {
  const expr = xpath?.trim()
  if (!expr || !ctx) return null
  const result = cached(ctx, node ?? ctx.doc, `n:${expr}`, () => {
    const snap = ctx.doc.evaluate(expr, node ?? ctx.doc, ctx.resolver, XPathResult.ORDERED_NODE_SNAPSHOT_TYPE, null)
    const out: Node[] = []
    for (let i = 0; i < snap.snapshotLength; i++) {
      const n = snap.snapshotItem(i)
      if (n) out.push(n)
    }
    return out
  })
  return result === UNRESOLVED ? null : (result as Node[])
}

/** XSLT `test` (EBV). Çözülemezse null. */
export function evalBoolean(ctx: XmlContext | null, xpath: string | undefined, node?: Node): boolean | null {
  const expr = xpath?.trim()
  if (!expr || !ctx) return null
  const result = cached(ctx, node ?? ctx.doc, `b:${expr}`, () =>
    ctx.doc.evaluate(expr, node ?? ctx.doc, ctx.resolver, XPathResult.BOOLEAN_TYPE, null).booleanValue,
  )
  return result === UNRESOLVED ? null : (result as boolean)
}

/** İlk eşleşen düğüm (göreli alanlar için bağlam). */
export function firstNode(ctx: XmlContext | null, xpath: string | undefined, node?: Node): Node | null {
  return evalNodes(ctx, xpath, node)?.[0] ?? null
}

/** Resolved → düz metin (çözülemeyen/boş için ''). */
export function textOf(r: Resolved): string {
  return r.kind === 'value' ? r.text : ''
}
