/**
 * AI yanıtları için minimal markdown ayrıştırıcı — saf, React'sız.
 *
 * Neden kendi ayrıştırıcımız: react-markdown bundle'a ~60-100KB gzip ekler ve mevcut
 * chunk zaten 500KB uyarı eşiğinin üzerinde. Model yanıtlarında pratikte görülen alt
 * küme (başlık, liste, kalın/italik, inline kod, link) bunun çok altında bir kodla
 * karşılanıyor.
 *
 * Bilinçli olarak DESTEKLENMEYEN: tablo, blockquote, iç içe liste, referans link,
 * HTML passthrough. Her biri ayrıştırıcıyı ikiye katlar, karşılığında kazanç düşük.
 */

// ─── Tipler ──────────────────────────────────────────────────────────────────

export type Segment =
  | { kind: 'text'; content: string }
  /** `open: true` → kapanış fence'i henüz gelmedi (yanıt akıyor). */
  | { kind: 'code'; lang: string; content: string; open: boolean }

export type Inline =
  | { t: 'text'; v: string }
  | { t: 'strong'; c: Inline[] }
  | { t: 'em'; c: Inline[] }
  | { t: 'code'; v: string }
  | { t: 'link'; href: string; c: Inline[] }

export type Block =
  | { t: 'h'; level: 1 | 2 | 3; c: Inline[] }
  /** Paragraf satırları ayrı tutulur; render'da aralarına <br/> girer (bkz. aşağıdaki not). */
  | { t: 'p'; lines: Inline[][] }
  | { t: 'ul'; items: Inline[][] }
  | { t: 'ol'; start: number; items: Inline[][] }
  | { t: 'hr' }

// ─── Segment ayrıştırma (kod bloğu / metin) ──────────────────────────────────

const FENCE_LINE_RE = /^\s*```(\w*)\s*$/
/** Henüz tamamlanmamış açılış fence'i: "`", "``", "```", "```xs" gibi. */
const PARTIAL_FENCE_RE = /^\s*`{1,3}\w*$/

/**
 * Metni metin/kod segmentlerine böler. Regex yerine satır bazlı durum makinesi:
 * streaming sırasında kapanış fence'i henüz gelmemiş olabilir ve tembel bir regex
 * (```(\w*)\n([\s\S]*?)\n```) böyle bir girdide bloğu hiç görmez — kullanıcı ham
 * backtick metni görürdü.
 */
export function parseSegments(text: string): Segment[] {
  const segments: Segment[] = []
  const lines = text.split('\n')

  let inCode = false
  let lang = ''
  let buf: string[] = []

  const flushText = () => {
    const content = buf.join('\n')
    if (content.trim()) segments.push({ kind: 'text', content })
    buf = []
  }

  const flushCode = (open: boolean) => {
    // Açık blokta trimEnd YAPMA: yazılmakta olan son satır kırpılırsa imleç titrer.
    const content = open ? buf.join('\n') : buf.join('\n').trimEnd()
    if (content) segments.push({ kind: 'code', lang: lang || 'plaintext', content, open })
    buf = []
  }

  for (let i = 0; i < lines.length; i++) {
    const line = lines[i]
    const fence = FENCE_LINE_RE.exec(line)

    if (fence) {
      if (inCode) {
        flushCode(false)
        inCode = false
        lang = ''
      } else {
        flushText()
        inCode = true
        lang = fence[1]
      }
      continue
    }

    // Metin içindeyken son satır yarım bir açılış fence'i ise render'dan düş; aksi
    // halde kullanıcı bir an ham ``` görür, blok kapanınca ekran zıplar.
    if (!inCode && i === lines.length - 1 && PARTIAL_FENCE_RE.test(line)) continue

    buf.push(line)
  }

  if (inCode) flushCode(true)
  else flushText()

  return segments
}

// ─── Blok düzeyi ─────────────────────────────────────────────────────────────

const H_RE = /^(#{1,3})\s+(.+)$/
const UL_RE = /^\s*[-*+]\s+(.+)$/
const OL_RE = /^\s*(\d{1,9})[.)]\s+(.+)$/
const HR_RE = /^\s*(-{3,}|_{3,}|\*{3,})\s*$/

/**
 * Bir metin segmentini bloklara ayırır.
 *
 * CommonMark'tan KASITLI SAPMA: paragraf içindeki tek `\n` korunur (`lines` dizisi).
 * Standart markdown bunu boşluğa çevirir; model yanıtları satır sonlarını anlamlı
 * kullandığı için bu davranış burada okunabilirliği bozuyordu.
 */
export function parseBlocks(text: string): Block[] {
  const blocks: Block[] = []
  const lines = text.split('\n')

  let para: Inline[][] = []
  let ul: Inline[][] = []
  let ol: Inline[][] = []
  let olStart = 1

  const flushPara = () => {
    if (para.length) blocks.push({ t: 'p', lines: para })
    para = []
  }
  const flushUl = () => {
    if (ul.length) blocks.push({ t: 'ul', items: ul })
    ul = []
  }
  const flushOl = () => {
    if (ol.length) blocks.push({ t: 'ol', start: olStart, items: ol })
    ol = []
  }
  const flushAll = () => {
    flushPara()
    flushUl()
    flushOl()
  }

  for (const line of lines) {
    if (!line.trim()) {
      flushAll()
      continue
    }

    const hr = HR_RE.exec(line)
    if (hr) {
      flushAll()
      blocks.push({ t: 'hr' })
      continue
    }

    const h = H_RE.exec(line)
    if (h) {
      flushAll()
      blocks.push({ t: 'h', level: h[1].length as 1 | 2 | 3, c: parseInline(h[2]) })
      continue
    }

    const olm = OL_RE.exec(line)
    if (olm) {
      flushPara()
      flushUl()
      if (!ol.length) olStart = parseInt(olm[1], 10)
      ol.push(parseInline(olm[2]))
      continue
    }

    // UL kontrolü OL'den sonra: "1. madde" OL_RE'ye takılır, buraya düşmez.
    const ulm = UL_RE.exec(line)
    if (ulm) {
      flushPara()
      flushOl()
      ul.push(parseInline(ulm[1]))
      continue
    }

    flushUl()
    flushOl()
    para.push(parseInline(line))
  }

  flushAll()
  return blocks
}

// ─── Satır içi ───────────────────────────────────────────────────────────────

const MAX_DEPTH = 3
const SAFE_HREF_RE = /^(https?:\/\/|mailto:)/i

/**
 * `*` bir XPath/XSLT ifadesinin parçası mı? Yanıtlar `select="//*"`, `@*`, `node()*`
 * gibi ifadeler içeriyor; bunları italiğe çevirmek kodu okunamaz hâle getirir.
 * Bu yüzden tek `*` yalnızca ardından "sözcük gibi" bir karakter geliyorsa açılış
 * sayılır. Az biçimlendirmek, XPath'i bozmaktan iyidir.
 */
const XPATH_NEIGHBORS = new Set([
  ' ', '\t', '\n', '/', '[', ']', '=', '"', "'", '*', '(', ')', ',', ';', '@',
])

function isEmphasisOpener(ch: string | undefined): boolean {
  if (!ch) return false
  return !XPATH_NEIGHBORS.has(ch)
}

function isAlnum(ch: string | undefined): boolean {
  return !!ch && /[\p{L}\p{N}_]/u.test(ch)
}

/**
 * Satır içi işaretleyicileri ayrıştırır.
 *
 * Regex DEĞİL, elle yazılmış soldan-sağa tarayıcı. Gerekçe: streaming sırasında bu
 * fonksiyon her token'da uzayan bir metin üzerinde yeniden çalışır; kalın/italik için
 * tembel (lazy) bir regex, kapanış işaretleyicisi henüz gelmemişken katastrofik
 * backtracking'e girebilir. Elle tarama O(n) kalır.
 *
 * Kapanışı olmayan her işaretleyici düz metne düşer — yarım markdown zarifçe bozulmaz.
 */
export function parseInline(src: string, depth = 0): Inline[] {
  if (depth > MAX_DEPTH) return [{ t: 'text', v: src }]

  const out: Inline[] = []
  let buf = ''
  let i = 0

  const pushText = (s: string) => { buf += s }
  const flush = () => {
    if (buf) out.push({ t: 'text', v: buf })
    buf = ''
  }

  while (i < src.length) {
    const ch = src[i]

    // 1) Inline kod — içerik OPAK, içinde başka işaretleyici aranmaz.
    if (ch === '`') {
      let ticks = 0
      while (src[i + ticks] === '`') ticks++
      const fence = '`'.repeat(ticks)
      const close = src.indexOf(fence, i + ticks)
      if (close !== -1) {
        flush()
        out.push({ t: 'code', v: src.slice(i + ticks, close) })
        i = close + ticks
        continue
      }
      pushText(src.slice(i, i + ticks))
      i += ticks
      continue
    }

    // 2) [metin](url)
    if (ch === '[') {
      const closeBracket = src.indexOf(']', i + 1)
      if (closeBracket !== -1 && src[closeBracket + 1] === '(') {
        const closeParen = src.indexOf(')', closeBracket + 2)
        if (closeParen !== -1) {
          const href = src.slice(closeBracket + 2, closeParen).trim()
          const label = src.slice(i + 1, closeBracket)
          // javascript:/data: gibi şemalar reddedilir → tüm ifade düz metin kalır.
          if (SAFE_HREF_RE.test(href)) {
            flush()
            out.push({ t: 'link', href, c: parseInline(label, depth + 1) })
            i = closeParen + 1
            continue
          }
        }
      }
      pushText(ch)
      i++
      continue
    }

    // 3) **kalın**
    if (ch === '*' && src[i + 1] === '*') {
      let close = src.indexOf('**', i + 2)
      // "**kalın *ve italik***" gibi durumlarda kapanış 3+ yıldızlı bir dizinin BAŞINA
      // düşer ve içteki italik kapanışsız kalırdı. Strong'u dizinin son iki yıldızına
      // kaydır ki içerideki tek yıldız kendi kapanışını bulsun.
      if (close !== -1) {
        let run = 0
        while (src[close + run] === '*') run++
        if (run >= 3) close += run - 2
      }
      if (close !== -1 && close > i + 2) {
        flush()
        out.push({ t: 'strong', c: parseInline(src.slice(i + 2, close), depth + 1) })
        i = close + 2
        continue
      }
      pushText('**')
      i += 2
      continue
    }

    // 4) *italik* / _italik_
    if ((ch === '*' || ch === '_') && isEmphasisOpener(src[i + 1])) {
      // `_` sözcük içindeyse atla: xsl_foo_bar bozulmasın.
      const prev = src[i - 1]
      if (ch === '_' && isAlnum(prev)) {
        pushText(ch)
        i++
        continue
      }
      const close = src.indexOf(ch, i + 1)
      if (close !== -1 && close > i + 1 && (ch !== '_' || !isAlnum(src[close + 1]))) {
        flush()
        out.push({ t: 'em', c: parseInline(src.slice(i + 1, close), depth + 1) })
        i = close + 1
        continue
      }
      pushText(ch)
      i++
      continue
    }

    pushText(ch)
    i++
  }

  flush()
  return out
}
