// XSLT 1.0 uyumluluk analizi: stylesheet'te FİİLEN kullanılan 2.0'a özgü
// fonksiyon / eleman / sözdizimini bulur (xsl:stylesheet/@version'a bakmaz).
import { parseXmlDocument } from './xmlUtils'
import { findAvtExpressions, scanXPath } from './xpath2Detect'

const XSLT_NS = 'http://www.w3.org/1999/XSL/Transform'
const FN_NS = 'http://www.w3.org/2005/xpath-functions'
const XS_NS = 'http://www.w3.org/2001/XMLSchema'

export type Xslt1CompatCode = 'XSLT1_COMPAT_FUNCTION' | 'XSLT1_COMPAT_ELEMENT' | 'XSLT1_COMPAT_SYNTAX'

export interface Xslt1CompatFinding {
  code: Xslt1CompatCode
  /** Gruplama anahtarı ve görünen ad (ör. `exists()`, `<xsl:function>`). */
  name: string
  message: string
  line: number
  column: number
  length: number
}

const TAIL = "XSLT 1.0 görüntüleyicilerde (ör. tarayıcı, bazı e-Fatura portalları) çalışmaz."

const XPATH1_FUNCTIONS = new Set([
  // XPath 1.0 çekirdek
  'last', 'position', 'count', 'id', 'local-name', 'namespace-uri', 'name', 'string', 'concat',
  'starts-with', 'contains', 'substring-before', 'substring-after', 'substring', 'string-length',
  'normalize-space', 'translate', 'boolean', 'not', 'true', 'false', 'lang', 'number', 'sum',
  'floor', 'ceiling', 'round',
  // XSLT 1.0
  'document', 'key', 'format-number', 'current', 'unparsed-entity-uri', 'generate-id',
  'system-property', 'element-available', 'function-available',
  // yaygın uzantıların öneksiz yazımı (asıl kullanım msxsl:/exsl: önekiyle; onlar zaten atlanır)
  'node-set', 'object-type',
])

const ELEMENTS_2 = new Set([
  'for-each-group', 'function', 'sequence', 'analyze-string', 'matching-substring',
  'non-matching-substring', 'character-map', 'output-character', 'result-document', 'next-match',
  'perform-sort', 'namespace', 'document', 'import-schema',
])

// XSLT elemanlarında XPath ifadesi/deseni taşıyan öznitelikler
const XPATH_ATTRS = new Set([
  'select', 'test', 'match', 'use', 'value', 'count', 'from',
  'group-by', 'group-adjacent', 'group-starting-with', 'group-ending-with',
])

// XSLT elemanlarında AVT kabul eden öznitelikler
const AVT_ATTRS: Record<string, string[]> = {
  element: ['name', 'namespace'],
  attribute: ['name', 'namespace', 'separator'],
  'processing-instruction': ['name'],
  namespace: ['name'],
  sort: ['lang', 'order', 'case-order', 'data-type', 'collation', 'stable'],
  number: ['format', 'lang', 'letter-value', 'grouping-separator', 'grouping-size', 'ordinal'],
  'result-document': ['href', 'format', 'method'],
}

// 2.0'a özgü öznitelikler: eleman yerel adı → öznitelik adları
const ATTRS_2: Record<string, string[]> = {
  output: ['use-character-maps'],
  'value-of': ['separator'],
  variable: ['as'], param: ['as'], 'with-param': ['as'], template: ['as'],
}

interface RawAttr { name: string; valueOffset: number; raw: string }
interface RawTag { qname: string; offset: number; attrs: RawAttr[] }

const XML_NAME_RE = /[\p{L}_:][\p{L}\p{N}_.:-]*/uy

/** Ham metindeki başlangıç etiketlerini (belge sırasıyla) öznitelik değer ofsetleriyle çıkarır. */
function scanStartTags(src: string): RawTag[] {
  const tags: RawTag[] = []
  const n = src.length
  const skipTo = (from: number, end: string) => { const e = src.indexOf(end, from); return e < 0 ? n : e + end.length }
  let i = 0
  while (i < n) {
    const lt = src.indexOf('<', i)
    if (lt < 0) break
    if (src.startsWith('<!--', lt)) { i = skipTo(lt + 4, '-->'); continue }
    if (src.startsWith('<![CDATA[', lt)) { i = skipTo(lt + 9, ']]>'); continue }
    if (src.startsWith('<?', lt)) { i = skipTo(lt + 2, '?>'); continue }
    if (src.startsWith('<!', lt)) {
      let j = lt + 2, depth = 0
      while (j < n && !(src[j] === '>' && depth <= 0)) { if (src[j] === '[') depth++; else if (src[j] === ']') depth--; j++ }
      i = j + 1; continue
    }
    if (src[lt + 1] === '/') { i = skipTo(lt, '>'); continue }
    XML_NAME_RE.lastIndex = lt + 1
    const m = XML_NAME_RE.exec(src)
    if (!m) { i = lt + 1; continue }
    const attrs: RawAttr[] = []
    let j = lt + 1 + m[0].length
    while (j < n) {
      while (j < n && /\s/.test(src[j])) j++
      if (src[j] === '>') { j++; break }
      if (src[j] === '/' && src[j + 1] === '>') { j += 2; break }
      XML_NAME_RE.lastIndex = j
      const a = XML_NAME_RE.exec(src)
      if (!a) { j++; continue }
      j += a[0].length
      while (j < n && /\s/.test(src[j])) j++
      if (src[j] !== '=') continue
      j++
      while (j < n && /\s/.test(src[j])) j++
      const q = src[j]
      if (q !== '"' && q !== "'") continue
      const vs = j + 1
      const ve = src.indexOf(q, vs) < 0 ? n : src.indexOf(q, vs)
      attrs.push({ name: a[0], valueOffset: vs, raw: src.slice(vs, ve) })
      j = ve + 1
    }
    tags.push({ qname: m[0], offset: lt, attrs })
    i = j
  }
  return tags
}

const ENTITIES: Record<string, string> = { lt: '<', gt: '>', amp: '&', quot: '"', apos: "'" }

/** Ham öznitelik değerini çözer; map[i] = çözülmüş i. karakterin ham değerdeki ofseti. */
function decodeWithMap(raw: string): { text: string; map: number[] } {
  let text = ''
  const map: number[] = []
  for (let i = 0; i < raw.length; i++) {
    if (raw[i] === '&') {
      const semi = raw.indexOf(';', i)
      const ent = semi > i ? raw.slice(i + 1, semi) : ''
      let ch: string | undefined = ENTITIES[ent]
      if (!ch && /^#x[0-9a-f]+$/i.test(ent)) ch = String.fromCodePoint(parseInt(ent.slice(2), 16))
      else if (!ch && /^#[0-9]+$/.test(ent)) ch = String.fromCodePoint(parseInt(ent.slice(1), 10))
      if (ch) { text += ch; for (let k = 0; k < ch.length; k++) map.push(i); i = semi; continue }
    }
    text += raw[i]
    map.push(i)
  }
  return { text, map }
}

function makeLocator(src: string) {
  const starts = [0]
  for (let i = 0; i < src.length; i++) if (src[i] === '\n') starts.push(i + 1)
  return (offset: number) => {
    let lo = 0, hi = starts.length - 1
    while (lo < hi) { const mid = (lo + hi + 1) >> 1; if (starts[mid] <= offset) lo = mid; else hi = mid - 1 }
    return { line: lo + 1, column: offset - starts[lo] + 1 }
  }
}

export function analyzeXslt1Compat(xslt: string): Xslt1CompatFinding[] {
  const doc = parseXmlDocument(xslt)
  if (!doc) return []
  const els = Array.from(doc.getElementsByTagName('*'))
  const tags = scanStartTags(xslt)
  // Ham tarama DOM ile hizalı değilse konum veremeyiz; yanlış satır göstermektense sessiz kal.
  if (els.length !== tags.length || els.some((el, k) => el.tagName !== tags[k].qname)) return []

  const locate = makeLocator(xslt)
  const findings: Xslt1CompatFinding[] = []
  const push = (code: Xslt1CompatCode, name: string, message: string, offset: number, length: number) =>
    findings.push({ code, name, message, length, ...locate(offset) })

  // xsl:function ile tanımlı kullanıcı fonksiyonları ({ns}local)
  const userFns = new Set<string>()
  for (const el of els) {
    if (el.namespaceURI !== XSLT_NS || el.localName !== 'function') continue
    const qn = el.getAttribute('name') ?? ''
    const c = qn.indexOf(':')
    if (c > 0) userFns.add(`{${el.lookupNamespaceURI(qn.slice(0, c)) ?? ''}}${qn.slice(c + 1)}`)
  }

  const scanExpr = (el: Element, attr: RawAttr, text: string, map: number[], start: number, end: number) => {
    const { calls, syntax } = scanXPath(text.slice(start, end))
    const at = (idx: number) => attr.valueOffset + (map[start + idx] ?? 0)
    for (const call of calls) {
      const ns = call.prefix
        ? el.lookupNamespaceURI(call.prefix) ?? (call.prefix === 'fn' ? FN_NS : null)
        : FN_NS
      const shown = `${call.prefix ? call.prefix + ':' : ''}${call.local}()`
      if (ns === FN_NS) {
        if (call.prefix || !XPATH1_FUNCTIONS.has(call.local))
          push('XSLT1_COMPAT_FUNCTION', shown, `${shown} XSLT 2.0'a özgüdür; ${TAIL}`, at(call.index), call.length)
      } else if (ns === XS_NS) {
        push('XSLT1_COMPAT_FUNCTION', shown, `${shown} (XML Schema tür dönüşümü) XSLT 2.0'a özgüdür; ${TAIL}`, at(call.index), call.length)
      } else if (userFns.has(`{${ns ?? ''}}${call.local}`)) {
        push('XSLT1_COMPAT_FUNCTION', shown, `${shown} xsl:function ile tanımlı kullanıcı fonksiyonudur (XSLT 2.0'a özgü); ${TAIL}`, at(call.index), call.length)
      }
    }
    for (const hit of syntax) {
      push('XSLT1_COMPAT_SYNTAX', hit.label, `'${hit.label}' ifadesi XSLT 2.0'a (XPath 2.0) özgüdür; ${TAIL}`, at(hit.index), hit.length)
    }
  }

  els.forEach((el, k) => {
    const tag = tags[k]
    const isXsl = el.namespaceURI === XSLT_NS
    const local = el.localName
    if (isXsl && ELEMENTS_2.has(local)) {
      const shown = `<${tag.qname}>`
      push('XSLT1_COMPAT_ELEMENT', shown, `${shown} elemanı XSLT 2.0'a özgüdür; ${TAIL}`, tag.offset + 1, tag.qname.length)
    }
    for (const attr of tag.attrs) {
      if (attr.name === 'xmlns' || attr.name.startsWith('xmlns:')) continue
      const c = attr.name.indexOf(':')
      const attrNs = c > 0 ? el.lookupNamespaceURI(attr.name.slice(0, c)) : null
      if (isXsl && !attrNs && ATTRS_2[local]?.includes(attr.name)) {
        const shown = `${tag.qname}/@${attr.name}`
        push('XSLT1_COMPAT_ELEMENT', shown, `${shown} özniteliği XSLT 2.0'a özgüdür; ${TAIL}`, attr.valueOffset, attr.raw.length || 1)
      }
      const { text, map } = decodeWithMap(attr.raw)
      if (isXsl && !attrNs && XPATH_ATTRS.has(attr.name)) {
        scanExpr(el, attr, text, map, 0, text.length)
      } else if ((isXsl && !attrNs && AVT_ATTRS[local]?.includes(attr.name)) || (!isXsl && attrNs !== XSLT_NS)) {
        for (const r of findAvtExpressions(text)) scanExpr(el, attr, text, map, r.start, r.end)
      }
    }
  })

  return findings.sort((a, b) => a.line - b.line || a.column - b.column)
}

/** Panel için: aynı ad başına en fazla `max` geçiş; fazlası son gösterilen öğede özetlenir. */
export function summarizeXslt1Compat(findings: Xslt1CompatFinding[], max = 3): Xslt1CompatFinding[] {
  const groups = new Map<string, Xslt1CompatFinding[]>()
  for (const f of findings) {
    const key = `${f.code}|${f.name}`
    const list = groups.get(key)
    if (list) list.push(f); else groups.set(key, [f])
  }
  const out: Xslt1CompatFinding[] = []
  for (const list of groups.values()) {
    const shown = list.slice(0, max)
    const rest = list.length - shown.length
    if (rest > 0) {
      const last = shown[shown.length - 1]
      shown[shown.length - 1] = { ...last, message: `${last.message} (ve ${rest} yer daha)` }
    }
    out.push(...shown)
  }
  return out.sort((a, b) => a.line - b.line || a.column - b.column)
}
