/** XML ağacını tarayarak { element → children, attributes } indeksi kurar.
 *  XPath mini-parser ile o anki context node'u bulur, tamamlama önerir.
 */

export interface XmlNodeInfo {
  childNames: Set<string>               // prefix:local biçimindeki çocuk etiketler
  children: Map<string, XmlNodeInfo>    // tagName veya localName → node
  attributes: Set<string>              // attribute isimleri (xmlns hariç)
}

export interface XmlIndex {
  root: XmlNodeInfo | null
  rootName: string | null              // ör. "n1:Invoice"
}

export interface XPathSuggestResult {
  suggestions: string[]
  partialSegment: string               // kullanıcının şu an yazdığı kısım (replace edilecek)
  isAttr: boolean                      // @ bağlamı mı?
}

// ── Index builder ──────────────────────────────────────────────────────────────

function buildNode(el: Element): XmlNodeInfo {
  const node: XmlNodeInfo = {
    childNames: new Set(),
    children: new Map(),
    attributes: new Set(),
  }

  for (let i = 0; i < el.attributes.length; i++) {
    const attr = el.attributes[i]
    if (!attr.name.startsWith('xmlns')) node.attributes.add(attr.name)
  }

  for (let i = 0; i < el.children.length; i++) {
    const child = el.children[i]
    const tag = child.tagName
    const local = child.localName
    node.childNames.add(tag)

    if (!node.children.has(tag)) {
      const childNode = buildNode(child)
      node.children.set(tag, childNode)
      if (local !== tag) node.children.set(local, childNode)
    } else {
      // Aynı isimde kardeş: sadece üst-düzey çocukları birleştir
      const existing = node.children.get(tag)!
      for (let j = 0; j < child.children.length; j++) {
        const gc = child.children[j]
        existing.childNames.add(gc.tagName)
        if (!existing.children.has(gc.tagName)) {
          const gcNode = buildNode(gc)
          existing.children.set(gc.tagName, gcNode)
          if (gc.localName !== gc.tagName) existing.children.set(gc.localName, gcNode)
        }
      }
      for (let k = 0; k < child.attributes.length; k++) {
        const a = child.attributes[k]
        if (!a.name.startsWith('xmlns')) existing.attributes.add(a.name)
      }
    }
  }

  return node
}

export function buildXmlIndex(xmlContent: string): XmlIndex {
  try {
    const parser = new DOMParser()
    const doc = parser.parseFromString(xmlContent, 'text/xml')
    if (doc.querySelector('parsererror')) return { root: null, rootName: null }
    const rootEl = doc.documentElement
    if (!rootEl) return { root: null, rootName: null }
    return { root: buildNode(rootEl), rootName: rootEl.tagName }
  } catch {
    return { root: null, rootName: null }
  }
}

// ── XPath mini-parser ──────────────────────────────────────────────────────────

/** "/" karakterine böler; "[...]" içindeki "/" karakterlerini görmezden gelir. */
function splitSegments(path: string): string[] {
  const segs: string[] = []
  let depth = 0
  let cur = ''
  for (const ch of path) {
    if (ch === '[') depth++
    else if (ch === ']') depth--
    else if (ch === '/' && depth === 0) {
      if (cur) segs.push(cur)
      cur = ''
      continue
    }
    cur += ch
  }
  if (cur) segs.push(cur)
  return segs
}

/**
 * path: "//n1:Invoice/cac:AccountingSupplierParty/" gibi cursor öncesi path.
 * Kök elemana kadar gezinerek ilgili XmlNodeInfo'yu döner.
 */
function navigateTo(path: string, index: XmlIndex): XmlNodeInfo | null {
  if (!index.root) return null

  const isDescendant = path.startsWith('//')
  const normalized = path.replace(/^\/+/, '').replace(/\/$/, '')

  if (!normalized) return index.root  // bare // veya /

  const segs = splitSegments(normalized)
  let current: XmlNodeInfo = index.root

  for (let i = 0; i < segs.length; i++) {
    // Predicate'leri sil: cac:InvoiceLine[1] → cac:InvoiceLine
    const segment = segs[i].replace(/\[.*?\]/g, '')
    if (!segment || segment === '.') continue
    if (segment === '..') return null  // üste çıkma desteklenmiyor
    if (segment === '*') {
      const first = current.children.values().next().value as XmlNodeInfo | undefined
      if (!first) return null
      current = first
      continue
    }

    // child::foo → foo
    const name = segment.includes('::') ? segment.split('::')[1] : segment

    // "//" + ilk segment root elemana eşleşiyorsa kökta kal
    if (i === 0 && isDescendant && index.rootName) {
      const rootLocal = index.rootName.includes(':') ? index.rootName.split(':')[1] : index.rootName
      const nameLocal = name.includes(':') ? name.split(':')[1] : name
      if (name === index.rootName || nameLocal === rootLocal) continue
    }

    // Önce tam isimle (prefix:local), sonra localName ile ara
    if (current.children.has(name)) {
      current = current.children.get(name)!
    } else {
      const local = name.includes(':') ? name.split(':')[1] : name
      if (current.children.has(local)) {
        current = current.children.get(local)!
      } else {
        return null
      }
    }
  }

  return current
}

// ── Public API ─────────────────────────────────────────────────────────────────

/**
 * xpathSoFar: kullanıcının attribute değerinde cursor'a kadar yazdığı XPath ifadesi.
 * Dönüş: sunulacak öneriler + cursor'da replace edilecek kısım.
 */
export function getXmlPathSuggestions(xpathSoFar: string, index: XmlIndex): XPathSuggestResult {
  const empty: XPathSuggestResult = { suggestions: [], partialSegment: '', isAttr: false }
  if (!index.root) return empty

  // Attribute bağlamı: ifade @partial ile bitiyorsa
  const attrMatch = /@([\w:.-]*)$/.exec(xpathSoFar)
  if (attrMatch) {
    const partial = attrMatch[1]
    const beforeAt = xpathSoFar.slice(0, xpathSoFar.lastIndexOf('@'))
    const contextNode = navigateTo(beforeAt, index)
    if (!contextNode) return { suggestions: [], partialSegment: partial, isAttr: true }
    const sug = Array.from(contextNode.attributes)
      .filter(a => !partial || a.startsWith(partial))
      .sort()
    return { suggestions: sug, partialSegment: partial, isAttr: true }
  }

  // "/" içermiyorsa path bağlamı yok — öneride bulunma
  if (!xpathSoFar.includes('/')) return empty

  // Son "/" dan sonrası partial segment
  const lastSlash = xpathSoFar.lastIndexOf('/')
  const partial = xpathSoFar.slice(lastSlash + 1)
  const contextPath = xpathSoFar.slice(0, lastSlash + 1)

  const contextNode = navigateTo(contextPath, index)
  if (!contextNode) return empty

  const sug = Array.from(contextNode.childNames)
    .filter(n => {
      if (!partial) return true
      if (n.startsWith(partial)) return true
      const local = n.includes(':') ? n.split(':')[1] : n
      return local.startsWith(partial)
    })
    .sort()

  return { suggestions: sug, partialSegment: partial, isAttr: false }
}
