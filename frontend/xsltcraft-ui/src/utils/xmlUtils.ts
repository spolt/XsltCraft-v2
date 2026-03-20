// UBL 2.1 namespace URI → prefix
const NS_TO_PREFIX: Record<string, string> = {
  'urn:oasis:names:specification:ubl:schema:xsd:CommonBasicComponents-2': 'cbc',
  'urn:oasis:names:specification:ubl:schema:xsd:CommonAggregateComponents-2': 'cac',
  'urn:oasis:names:specification:ubl:schema:xsd:CommonExtensionComponents-2': 'ext',
}

// prefix → namespace URI (for XPath evaluation)
const PREFIX_TO_NS: Record<string, string> = Object.fromEntries(
  Object.entries(NS_TO_PREFIX).map(([uri, prefix]) => [prefix, uri])
)

export interface XmlNode {
  id: string
  name: string     // display name: prefix:localName
  xpath: string    // absolute XPath from document root
  value: string    // trimmed text content (leaf nodes)
  isLeaf: boolean
  depth: number
  children: XmlNode[]
}

function resolvePrefix(el: Element): string | null {
  if (el.namespaceURI && NS_TO_PREFIX[el.namespaceURI]) {
    return NS_TO_PREFIX[el.namespaceURI]
  }
  return el.prefix || null
}

function qname(el: Element): string {
  const prefix = resolvePrefix(el)
  return prefix ? `${prefix}:${el.localName}` : el.localName
}

function xpathName(el: Element): string {
  const prefix = resolvePrefix(el)
  // Elements in unrecognized namespaces (e.g. Invoice-2 root) use local-name() so
  // the generated XPath works in both browser document.evaluate and C# XslCompiledTransform.
  return prefix ? `${prefix}:${el.localName}` : `*[local-name()='${el.localName}']`
}

function xpathSegment(el: Element): string {
  const name = xpathName(el)
  const siblings = Array.from(el.parentElement?.children ?? []).filter(
    (s) => s.localName === el.localName && s.namespaceURI === el.namespaceURI
  )
  if (siblings.length === 1) return name
  return `${name}[${siblings.indexOf(el) + 1}]`
}

function buildXPath(el: Element): string {
  const parts: string[] = []
  let node: Element | null = el
  while (node) {
    parts.unshift(node.parentElement ? xpathSegment(node) : xpathName(node))
    node = node.parentElement
  }
  return '/' + parts.join('/')
}

let _counter = 0

function walkElement(el: Element, depth: number): XmlNode {
  const childEls = Array.from(el.children)
  const isLeaf = childEls.length === 0
  return {
    id: `xn-${++_counter}`,
    name: qname(el),
    xpath: buildXPath(el),
    value: isLeaf ? (el.textContent?.trim() ?? '') : '',
    isLeaf,
    depth,
    children: childEls.map((c) => walkElement(c, depth + 1)),
  }
}

export function parseXmlToTree(xmlContent: string): XmlNode | null {
  try {
    _counter = 0
    const doc = new DOMParser().parseFromString(xmlContent, 'application/xml')
    const root = doc.documentElement
    if (!root || root.tagName === 'parsererror') return null
    return walkElement(root, 0)
  } catch {
    return null
  }
}

export function evaluateXPathValue(xmlContent: string, xpath: string): string {
  if (!xpath.trim()) return ''
  try {
    const doc = new DOMParser().parseFromString(xmlContent, 'application/xml')
    const resolver = (prefix: string | null): string | null =>
      prefix ? (PREFIX_TO_NS[prefix] ?? null) : null
    const result = document.evaluate(xpath, doc, resolver, XPathResult.STRING_TYPE, null)
    return result.stringValue
  } catch {
    return ''
  }
}
