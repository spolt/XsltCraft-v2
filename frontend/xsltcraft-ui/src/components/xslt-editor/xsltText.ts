import xmlFormatter from 'xml-formatter'
import type { ProblemItem } from './ProblemsPanel'

/** Kaydedilmemiş XSLT varken sayfadan ayrılma sorusu. */
export const LEAVE_CONFIRM = 'Kaydedilmemiş değişiklikler var. Sayfadan ayrılmak istediğinize emin misiniz?'

/** Tema taslağı kaydında 409. Yenileme önerilmez: yenilemek taslağı siler, indirilen dosya bu ekranda geri yüklenemez. */
export const SAVE_CONFLICT_MESSAGE = 'Şablon siz düzenlerken değiştirildi. Çalışmanızı kaybetmemek için XSLT\'yi indirin; ardından Şablonlarım\'dan şablonu açıp değişiklikleri uygulayın.'

/** DOMParser parsererror çıktısından satır/kolon çıkarır. */
export function extractXmlParseErrors(xml: string): { message: string; line: number | null; column: number | null }[] {
  const parser = new DOMParser()
  const doc = parser.parseFromString(xml, 'text/xml')
  const err = doc.querySelector('parsererror')
  if (!err) return []
  const raw = (err.textContent ?? 'XML ayrıştırma hatası').replace(/\s+/g, ' ').trim()
  const lineMatch = /line\s*(?:number)?\s*[:\s]\s*(\d+)/i.exec(raw)
  const colMatch = /column\s*(\d+)/i.exec(raw)
  return [{
    message: raw,
    line: lineMatch ? Number(lineMatch[1]) : null,
    column: colMatch ? Number(colMatch[1]) : null,
  }]
}

// İlk satırda XML bildirimi (`<?xml version="1.0" encoding="UTF-8"?>`) var mı?
// Eksikse problem panelinde gösterilecek hata öğesini döndürür.
export const XML_DECL_FIX = '<?xml version="1.0" encoding="UTF-8"?>'

// UBL faturaları genelde tek satıra minify edilmiş gelir; Monaco bunu tek dev satır
// olarak sarmalayınca okunamaz hâle gelir. "Minify edilmiş" sayılma ölçütü: kayda değer
// boyutta olup ya birkaç satırdan ibaret ya da satır başına ortalama çok uzun olması.
export function looksMinified(xml: string): boolean {
  if (xml.length < 2_000) return false
  const lineCount = xml.split('\n').length
  return lineCount <= 3 || xml.length / lineCount > 400
}

// Görüntüleme için pretty-print eder. HAM `xmlContent`'e DOKUNMAZ: ham metin dönüşümün
// (ve XAdES imza sindirimlerinin) tek doğru kaynağıdır — girinti eklemek boşluk metin
// düğümleri yaratıp önizleme çıktısını değiştirebilirdi. Geçersiz XML'de ham metne düşer.
export function formatXmlForDisplay(xml: string): string {
  try {
    return xmlFormatter(xml, {
      indentation: '  ',
      collapseContent: true,
      lineSeparator: '\n',
    })
  } catch {
    return xml
  }
}

// Hata satırının çevresindeki pencereyi SATIR NUMARALARIYLA çıkarır. Modeller ham metinde
// "836. satır"ı sayarak bulamaz; hatalı bölgeyi numaralı ve etiketli vererek AI'nın doğru
// yeri görmesini garanti eder — bağlam özetleme/kırpma ne yaparsa yapsın bu blok korunur.
export function buildErrorRegion(content: string, errorLine: number, radius = 18): string {
  const lines = content.split('\n')
  const start = Math.max(1, errorLine - radius)
  const end = Math.min(lines.length, errorLine + radius)
  const width = String(end).length
  const out: string[] = []
  for (let ln = start; ln <= end; ln++) {
    const marker = ln === errorLine ? '>>' : '  '
    out.push(`${marker} ${String(ln).padStart(width, ' ')} | ${lines[ln - 1] ?? ''}`)
  }
  return out.join('\n')
}

/**
 * Problem için AI'ya sorulacak metin. XML bildirimi eksikse şablonu yeniden yazdırmayan kısa yanıt istenir; satırı
 * bilinen hatada çevresi numaralı olarak gömülür (bağlamdaki dosya kırpılmış olsa da model asıl hatalı kodu görür).
 */
export function aiPromptForProblem(problem: ProblemItem, xslt: string, xml: string | null): string {
  if (problem.ruleId === 'XML_DECL_MISSING') {
    return `XSLT'nin ilk satırında XML bildirimi (\`${XML_DECL_FIX}\`) eksik. ` +
      'Çok kısa açıkla (1-2 cümle) ve yalnızca eklenecek satırı, ardından dosyanın ilk 3-4 satırını içeren küçük bir kod bloğu göster. ' +
      'Tüm şablonu tekrar yazma.'
  }
  const message = `${problem.ruleName ?? 'Hata'}: ${problem.message}` +
    (problem.line != null ? ` (satır ${problem.line}${problem.column ? `:${problem.column}` : ''})` : '')
  const source = problem.source === 'xslt' ? xslt : (xml ?? '')
  if (problem.line == null || !source) return message
  return `${message}\n\nHatanın bulunduğu bölge (">>" ile işaretli satır sorunlu; numaralar dosyadaki gerçek satırlardır):\n` +
    '```xml\n' + buildErrorRegion(source, problem.line) + '\n```'
}

/**
 * Refactor sonucunu seçimin yerine yazar; satırın seçilmemiş kısmına dokunmaz. Seçim konumunda değilse (diyalog açıkken
 * compose metni kaydırdıysa) metinde tek geçiyorsa oraya yazar, bulunamaz ya da belirsizse null döner.
 */
export function replaceSelectionText(
  xslt: string,
  selection: string,
  position: { startLine: number; startColumn: number },
  replacement: string,
): string | null {
  const eol = xslt.includes('\r\n') ? '\r\n' : '\n'
  const doc = xslt.replace(/\r\n?/g, '\n')
  const selected = selection.replace(/\r\n?/g, '\n')
  const lineStart = doc.split('\n', position.startLine - 1).reduce((sum, line) => sum + line.length + 1, 0)
  let at = lineStart + position.startColumn - 1
  if (!doc.startsWith(selected, at)) {
    at = doc.indexOf(selected)
    if (at < 0 || at !== doc.lastIndexOf(selected)) return null
  }
  return (doc.slice(0, at) + replacement.replace(/\r\n?/g, '\n') + doc.slice(at + selected.length)).replace(/\n/g, eol)
}

export function xmlDeclarationProblem(xslt: string): ProblemItem | null {
  if (!xslt.trim()) return null
  const noBom = xslt.charCodeAt(0) === 0xfeff ? xslt.slice(1) : xslt
  const firstLine = noBom.split('\n', 1)[0]
  const hasDecl = /^\s*<\?xml\s+version\s*=\s*["'][^"']*["']/i.test(firstLine)
  if (hasDecl) return null
  return {
    source: 'xslt',
    ruleId: 'XML_DECL_MISSING',
    severity: 'error',
    ruleName: 'XML Bildirimi Eksik',
    message: `XSLT dosyasının ilk satırında XML bildirimi bulunmuyor. İlk satıra \`${XML_DECL_FIX}\` eklenmelidir.`,
    line: 1,
    column: 1,
  }
}

/** xsl:stylesheet / xsl:transform kök öğesinin version özniteliği (başlık çipi için). */
export function stylesheetVersion(xslt: string): string | null {
  const m = /<xsl:(?:stylesheet|transform)\b[^>]*?\bversion\s*=\s*["']([^"']+)["']/.exec(xslt.slice(0, 20_000))
  return m ? m[1] : null
}

export function formatSize(text: string): string {
  const bytes = new Blob([text]).size
  return bytes < 1024 ? `${bytes} B` : `${Math.round(bytes / 1024)} KB`
}

// Snippet gövdesindeki Monaco yer tutucularını (${1:ad}, $1, $0) düz metne indirger.
export function snippetPlainText(body: string): string {
  return body.replace(/\$\{\d+:([^}]*)\}/g, '$1').replace(/\$\{\d+\}|\$\d+/g, '')
}
