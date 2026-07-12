// AI sohbet yanıtındaki XSLT kod bloğunu editördeki dokümana uygulamak için saf yardımcılar.
// Model yanıtı serbest metindir; bloğu güvenle uygulayabilmek için hedefi (tüm doküman /
// seçim / imzası eşleşen template) tespit eder. Tahmin yapmaz — belirsizse 'no-match' döner.

export type ApplyKind = 'whole-doc' | 'selection' | 'template' | 'no-match'

export interface ApplyTarget {
  kind: ApplyKind
  /** Değişiklik uygulandığında oluşacak TAM doküman (no-match'te undefined). */
  newDoc?: string
  /** Diff sol tarafı: değişen bölge (tüm doküman, seçim ya da eşleşen template). */
  oldText: string
  /** Diff sağ tarafı: uygulanacak blok. */
  newText: string
}

const FENCE_RE = /```(\w*)\n([\s\S]*?)\n```/g

/**
 * Yanıttaki uygulanabilir XSLT bloğunu çıkarır. Model bazen "önce/sonra" iki blok gösterir;
 * son bloğu (nihai "sonra" hali) tercih ederiz. lang xslt|xml ya da <xsl: içeren etiketsiz blok.
 */
export function extractApplicableBlock(content: string): string | null {
  let match: RegExpExecArray | null
  let last: string | null = null
  FENCE_RE.lastIndex = 0
  while ((match = FENCE_RE.exec(content)) !== null) {
    const lang = (match[1] || '').toLowerCase()
    const body = match[2].trim()
    if (!body) continue
    const looksXslt = lang === 'xslt' || lang === 'xml' || body.includes('<xsl:')
    if (looksXslt) last = body
  }
  return last
}

/** `<xsl:template ...>` açılış etiketinden match/name/mode attribute'larını çıkarır. */
function parseTemplateSignature(block: string): { match?: string; name?: string; mode?: string } | null {
  const open = /<xsl:template\b([^>]*)>/.exec(block.trimStart())
  // Blok bir template ile başlamıyorsa imza yok.
  if (!open || block.trimStart().indexOf('<xsl:template') !== 0) return null
  const attrs = open[1]
  const attr = (n: string) => {
    const m = new RegExp(`\\b${n}\\s*=\\s*["']([^"']*)["']`).exec(attrs)
    return m ? m[1].trim() : undefined
  }
  const sig = { match: attr('match'), name: attr('name'), mode: attr('mode') }
  return sig.match || sig.name ? sig : null
}

/** Dokümanda aynı imzalı `<xsl:template>` bloğunun [start,end) aralığını bulur; tam 1 eşleşme şart. */
function findTemplateRegion(
  doc: string,
  sig: { match?: string; name?: string; mode?: string },
): { start: number; end: number } | null {
  const openRe = /<xsl:template\b([^>]*)>/g
  let m: RegExpExecArray | null
  const hits: { start: number; end: number }[] = []
  while ((m = openRe.exec(doc)) !== null) {
    const attrs = m[1]
    const attr = (n: string) => {
      const a = new RegExp(`\\b${n}\\s*=\\s*["']([^"']*)["']`).exec(attrs)
      return a ? a[1].trim() : undefined
    }
    if (attr('match') !== sig.match || attr('name') !== sig.name || attr('mode') !== sig.mode) continue
    // Template'ler iç içe olamaz → ilk kapanış etiketine kadar.
    const close = doc.indexOf('</xsl:template>', openRe.lastIndex)
    if (close === -1) continue
    hits.push({ start: m.index, end: close + '</xsl:template>'.length })
  }
  return hits.length === 1 ? hits[0] : null
}

/**
 * Bloğun dokümana nasıl uygulanacağını hesaplar. Öncelik sırası:
 * 1) Tam stylesheet → tüm doküman
 * 2) Aktif seçim dokümanda birebir varsa → seçimi değiştir
 * 3) Aynı imzalı tek template → o template'i değiştir
 * 4) Aksi hâlde → no-match (tahmin yok)
 */
export function computeApplyTarget(doc: string, block: string, selection?: string): ApplyTarget {
  const trimmed = block.trimStart()

  // 1) Tam stylesheet / belge
  if (trimmed.startsWith('<?xml') || trimmed.startsWith('<xsl:stylesheet') || trimmed.startsWith('<xsl:transform')) {
    return { kind: 'whole-doc', newDoc: block, oldText: doc, newText: block }
  }

  // 2) Aktif seçim
  const sel = selection?.trim()
  if (sel && doc.includes(sel)) {
    const idx = doc.indexOf(sel)
    const newDoc = doc.slice(0, idx) + block + doc.slice(idx + sel.length)
    return { kind: 'selection', newDoc, oldText: sel, newText: block }
  }

  // 3) Template imza eşleşmesi
  const sig = parseTemplateSignature(block)
  if (sig) {
    const region = findTemplateRegion(doc, sig)
    if (region) {
      const oldText = doc.slice(region.start, region.end)
      const newDoc = doc.slice(0, region.start) + block + doc.slice(region.end)
      return { kind: 'template', newDoc, oldText, newText: block }
    }
  }

  // 4) Hedef bulunamadı — dürüstçe elle uygulamaya bırak.
  return { kind: 'no-match', oldText: '', newText: block }
}
