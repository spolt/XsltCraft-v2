// AI sohbet yanıtındaki XSLT kod bloğunu editördeki dokümana uygulamak için saf yardımcılar.
// Model yanıtı serbest metindir; bloğu güvenle uygulayabilmek için hedefi (tüm doküman /
// seçim / imzası eşleşen XSLT elemanı) tespit eder. Tahmin yapmaz — belirsizse 'no-match' döner.

export type ApplyKind = 'whole-doc' | 'selection' | 'element' | 'no-match'

export interface ApplyTarget {
  kind: ApplyKind
  /** Değişiklik uygulandığında oluşacak TAM doküman (no-match'te undefined). */
  newDoc?: string
  /** Diff sol tarafı: değişen bölge (tüm doküman, seçim ya da eşleşen eleman). */
  oldText: string
  /** Diff sağ tarafı: uygulanacak blok. */
  newText: string
  /** no-match'te neden hedef bulunamadığını açıklayan tanı metni. */
  reason?: string
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

// ── XML/XSLT ayrıştırma yardımcıları (regex değil, alıntı-duyarlı tarama) ──────

function escapeRegExp(s: string): string {
  return s.replace(/[.*+?^${}()|[\]\\]/g, '\\$&')
}

/** `<` konumundan başlayıp etiketi kapatan `>`'ı bulur; attribute değeri içindeki `>`'ı atlar. */
function findTagEnd(doc: string, tagStart: number): number {
  let quote = ''
  for (let i = tagStart; i < doc.length; i++) {
    const c = doc[i]
    if (quote) {
      if (c === quote) quote = ''
    } else if (c === '"' || c === "'") {
      quote = c
    } else if (c === '>') {
      return i
    }
  }
  return -1
}

/** Bir açılış etiketinin attribute metninden verilen attribute değerini okur. */
function readAttr(attrs: string, name: string): string | undefined {
  const re = new RegExp(`\\b${escapeRegExp(name)}\\s*=\\s*("([^"]*)"|'([^']*)')`)
  const m = re.exec(attrs)
  return m ? (m[2] ?? m[3]) : undefined
}

// Kök elemanı tanımlayan ayırt edici attribute'lar, öncelik sırasıyla.
// template→match/name, for-each→select, if/when→test, variable/param/call-template→name.
const SIGNATURE_ATTRS = ['match', 'name', 'select', 'test'] as const

/** Bloğun başındaki boşluk ve XML yorumlarını atlar (kök elemanı bulmak için). */
function stripLeading(block: string): string {
  let s = block.trimStart()
  while (s.startsWith('<!--')) {
    const end = s.indexOf('-->')
    if (end === -1) break
    s = s.slice(end + 3).trimStart()
  }
  return s
}

/** Bloğun kök XSLT elemanının adını ve ayırt edici imza attribute'unu çıkarır. */
function parseRootSignature(block: string): { name: string; attr: string; value: string } | null {
  const head = stripLeading(block)
  const m = /^<(xsl:[\w-]+)\b/.exec(head)
  if (!m) return null
  const tagEnd = findTagEnd(head, 0)
  if (tagEnd === -1) return null
  const attrs = head.slice(m[0].length, tagEnd)
  for (const a of SIGNATURE_ATTRS) {
    const v = readAttr(attrs, a)?.trim()
    if (v) return { name: m[1], attr: a, value: v }
  }
  return null
}

/**
 * `openStart`'taki (açılış etiketinin `<`'ı) elemanın kapanışını dengeli olarak bulur.
 * Aynı adlı iç içe elemanları (ör. for-each içinde for-each) doğru sayar; kendinden-kapanan
 * (`<.../>`) elemanlar derinliği artırmaz. Elemanın bittiği (kapanış `>`'ından sonraki) indeksi döner.
 */
function findElementEnd(doc: string, name: string, openStart: number): number | null {
  const re = new RegExp(`<(/?)${escapeRegExp(name)}(?=[\\s/>])`, 'g')
  re.lastIndex = openStart
  let depth = 0
  let m: RegExpExecArray | null
  while ((m = re.exec(doc)) !== null) {
    if (m[1] === '/') {
      depth--
      if (depth === 0) {
        const gt = doc.indexOf('>', m.index)
        return gt === -1 ? null : gt + 1
      }
    } else {
      const gt = findTagEnd(doc, m.index)
      if (gt === -1) return null
      if (doc[gt - 1] !== '/') depth++ // kendinden-kapanan derinliği artırmaz
      re.lastIndex = gt + 1            // attribute bölgesini atla
    }
  }
  return null
}

/** Dokümanda `name` elemanı + `attr="value"` imzasına sahip TÜM aralıkları bulur. */
function findElementRegions(
  doc: string,
  name: string,
  attr: string,
  value: string,
): { start: number; end: number }[] {
  const openNeedle = '<' + name
  const hits: { start: number; end: number }[] = []
  let idx = 0
  while ((idx = doc.indexOf(openNeedle, idx)) !== -1) {
    const after = doc[idx + openNeedle.length]
    // Gerçek etiket sınırı mı? (ör. <xsl:for-each ama <xsl:for-each-group değil)
    if (after === undefined || /[\s/>]/.test(after)) {
      const tagEnd = findTagEnd(doc, idx)
      if (tagEnd !== -1) {
        const selfClosing = doc[tagEnd - 1] === '/'
        const attrs = doc.slice(idx + openNeedle.length, selfClosing ? tagEnd - 1 : tagEnd)
        if (readAttr(attrs, attr)?.trim() === value) {
          if (selfClosing) {
            hits.push({ start: idx, end: tagEnd + 1 })
          } else {
            const end = findElementEnd(doc, name, idx)
            if (end !== null) hits.push({ start: idx, end })
          }
        }
        idx = tagEnd + 1
        continue
      }
    }
    idx += openNeedle.length
  }
  return hits
}

/**
 * Bloğun dokümana nasıl uygulanacağını hesaplar. Öncelik sırası:
 * 1) Tam stylesheet → tüm doküman
 * 2) Aktif seçim dokümanda birebir varsa → seçimi değiştir
 * 3) Aynı imzalı tek XSLT elemanı (for-each/template/if/when/variable…) → o elemanı değiştir
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

  // 3) İmza eşleşen tek XSLT elemanı (kullanıcı seçim yapmasa da bulur)
  const sig = parseRootSignature(block)
  if (sig) {
    const regions = findElementRegions(doc, sig.name, sig.attr, sig.value)
    if (regions.length === 1) {
      const { start, end } = regions[0]
      const oldText = doc.slice(start, end)
      const newDoc = doc.slice(0, start) + block + doc.slice(end)
      return { kind: 'element', newDoc, oldText, newText: block }
    }
    // Tanı: dosyayı okuduk (docLen), aradık ama eşleşmedi → nedenini söyle.
    const anchor = `<${sig.name} ${sig.attr}="${sig.value}">`
    const reason = regions.length === 0
      ? `Önerinin en dış elemanı ${anchor}, ${doc.length.toLocaleString('tr-TR')} karakterlik şablonda bulunamadı. Model bloğu yeniden yapılandırmış olabilir (mevcut bloğu birebir korumamış). Editörde ilgili bloğu seçip tekrar sorabilir ya da bloğu elle yapıştırabilirsin.`
      : `${anchor} imzası şablonda ${regions.length} kez geçiyor; hangisinin değiştirileceği belirsiz. İlgili bloğu editörde seçip tekrar sor.`
    return { kind: 'no-match', oldText: '', newText: block, reason }
  }

  // 4) Tanınabilir bir kök eleman yok — dürüstçe elle uygulamaya bırak.
  return {
    kind: 'no-match',
    oldText: '',
    newText: block,
    reason: 'Öneri tanınabilir bir XSLT elemanıyla (xsl:for-each / xsl:template / xsl:if…) başlamıyor, otomatik hedeflenemiyor.',
  }
}
