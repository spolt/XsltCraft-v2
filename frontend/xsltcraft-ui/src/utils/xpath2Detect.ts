// XPath ifadesinde XPath/XSLT 2.0'a özgü yapıları bulan hafif tarayıcı.
// Tam bir XPath ayrıştırıcısı değildir: string literal'leri ve (: :) yorumlarını atlar,
// fonksiyon çağrılarını ve 2.0 sözdizimi anahtar kelimelerini konumlarıyla döndürür.

export interface XPathCall { prefix: string | null; local: string; index: number; length: number }
export interface XPathSyntaxHit { label: string; index: number; length: number }
export interface XPathScanResult { calls: XPathCall[]; syntax: XPathSyntaxHit[] }

type TokType = 'name' | 'str' | 'num' | 'var' | 'op' | '(' | ')' | '[' | ']' | ',' | '@' | '::' | '/' | '.' | '*'
interface Tok { t: TokType; v: string; i: number }

const NAME_RE = /(?:\*:)?[\p{L}_][\p{L}\p{N}_.-]*(?::(?!:)(?:[\p{L}_][\p{L}\p{N}_.-]*|\*))?/uy

const NODE_TESTS_1 = new Set(['node', 'text', 'comment', 'processing-instruction'])
const KIND_TESTS_2 = new Set(['element', 'attribute', 'document-node', 'schema-element', 'schema-attribute', 'item', 'empty-sequence'])

// Operatör konumunda görülen 2.0 anahtar kelimeleri → gösterilecek etiket
const OPS_2: Record<string, string> = {
  eq: 'eq', ne: 'ne', lt: 'lt', le: 'le', gt: 'gt', ge: 'ge', is: 'is', idiv: 'idiv',
  to: 'to (aralık)', except: 'except', intersect: 'intersect', union: 'union',
  instance: 'instance of', treat: 'treat as', castable: 'castable as', cast: 'cast as',
}
const QUANTIFIERS = new Set(['for', 'some', 'every'])

function tokenize(s: string): Tok[] {
  const toks: Tok[] = []
  const n = s.length
  let i = 0
  while (i < n) {
    const c = s[i]
    if (c === ' ' || c === '\t' || c === '\n' || c === '\r') { i++; continue }
    if (c === '(' && s[i + 1] === ':') { i = skipComment(s, i); continue }
    if (c === "'" || c === '"') {
      let j = i + 1
      while (j < n) {
        if (s[j] === c) { if (s[j + 1] === c) { j += 2; continue } break }
        j++
      }
      toks.push({ t: 'str', v: '', i }); i = j + 1; continue
    }
    if ((c >= '0' && c <= '9') || (c === '.' && s[i + 1] >= '0' && s[i + 1] <= '9')) {
      let j = i + 1
      while (j < n && /[0-9.eE]/.test(s[j])) j++
      toks.push({ t: 'num', v: '', i }); i = j; continue
    }
    if (c === '$') {
      let j = i + 1
      while (j < n && /\s/.test(s[j])) j++
      NAME_RE.lastIndex = j
      const m = NAME_RE.exec(s)
      toks.push({ t: 'var', v: m?.[0] ?? '', i }); i = m ? j + m[0].length : j; continue
    }
    NAME_RE.lastIndex = i
    const m = NAME_RE.exec(s)
    if (m) { toks.push({ t: 'name', v: m[0], i }); i += m[0].length; continue }
    if (c === ':' && s[i + 1] === ':') { toks.push({ t: '::', v: '::', i }); i += 2; continue }
    if (c === '.') { const two = s[i + 1] === '.'; toks.push({ t: '.', v: '.', i }); i += two ? 2 : 1; continue }
    if (c === '/') { toks.push({ t: '/', v: '/', i }); i += s[i + 1] === '/' ? 2 : 1; continue }
    if ('()[],@*'.includes(c)) { toks.push({ t: c as TokType, v: c, i }); i++; continue }
    const two = s.slice(i, i + 2)
    if (two === '!=' || two === '<=' || two === '>=' || two === '<<' || two === '>>') {
      toks.push({ t: 'op', v: two, i }); i += 2; continue
    }
    if ('=<>|+-'.includes(c)) { toks.push({ t: 'op', v: c, i }); i++; continue }
    i++
  }
  return toks
}

function skipComment(s: string, start: number): number {
  let depth = 0
  let i = start
  while (i < s.length) {
    if (s[i] === '(' && s[i + 1] === ':') { depth++; i += 2; continue }
    if (s[i] === ':' && s[i + 1] === ')') { depth--; i += 2; if (depth === 0) return i; continue }
    i++
  }
  return i
}

export function scanXPath(expr: string): XPathScanResult {
  const toks = tokenize(expr)
  const calls: XPathCall[] = []
  const syntax: XPathSyntaxHit[] = []
  // prevEnd: önceki token bir operandı bitirdi mi? (XPath'in ad/operatör ayrımı kuralı)
  let prevEnd = false
  for (let k = 0; k < toks.length; k++) {
    const tok = toks[k]
    const next = toks[k + 1]
    switch (tok.t) {
      case 'name': {
        if (prevEnd) {
          const label = OPS_2[tok.v]
          if (label) syntax.push({ label, index: tok.i, length: tok.v.length })
          prevEnd = false
          break
        }
        const colon = tok.v.indexOf(':')
        const prefix = colon > 0 ? tok.v.slice(0, colon) : null
        const local = colon > 0 ? tok.v.slice(colon + 1) : tok.v
        if (next?.t === '(') {
          if (!prefix && NODE_TESTS_1.has(local)) { /* 1.0 düğüm testi */ }
          else if (!prefix && local === 'if') syntax.push({ label: 'if (…) then … else', index: tok.i, length: 2 })
          else if (!prefix && KIND_TESTS_2.has(local)) syntax.push({ label: `${local}() tür testi`, index: tok.i, length: local.length })
          else calls.push({ prefix, local, index: tok.i, length: tok.v.length })
          prevEnd = false
        } else if (next?.t === 'var' && !prefix && QUANTIFIERS.has(local)) {
          syntax.push({ label: local === 'for' ? 'for $x in … return' : `${local} … satisfies`, index: tok.i, length: local.length })
          prevEnd = false
        } else {
          prevEnd = next?.t !== '::'
        }
        break
      }
      case 'str': case 'num': case 'var': case ')': case ']': case '.':
        prevEnd = true
        break
      case '*':
        prevEnd = !prevEnd
        break
      case 'op':
        if (tok.v === '<<' || tok.v === '>>') syntax.push({ label: `${tok.v} düğüm sırası karşılaştırması`, index: tok.i, length: 2 })
        prevEnd = false
        break
      default:
        prevEnd = false
    }
  }
  return { calls, syntax }
}

/** Öznitelik değer şablonundaki (AVT) `{…}` ifadelerinin [başlangıç, bitiş) aralıkları. */
export function findAvtExpressions(value: string): { start: number; end: number }[] {
  const out: { start: number; end: number }[] = []
  let i = 0
  while (i < value.length) {
    const c = value[i]
    if (c === '{') {
      if (value[i + 1] === '{') { i += 2; continue }
      let j = i + 1
      let quote: string | null = null
      while (j < value.length) {
        const d = value[j]
        if (quote) { if (d === quote) quote = null }
        else if (d === "'" || d === '"') quote = d
        else if (d === '}') break
        j++
      }
      out.push({ start: i + 1, end: j })
      i = j + 1
      continue
    }
    i++
  }
  return out
}
