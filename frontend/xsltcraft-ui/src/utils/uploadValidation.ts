/**
 * XML / XSLT dosya yüklemelerinin doğrulaması.
 *
 * `<input accept>` yalnız dosya seçicide bir ipucudur: kullanıcı "Tüm dosyalar"ı seçebilir ve
 * Windows .xsl/.xslt dosyalarını `text/xml`/`application/xml` MIME tipiyle kaydettiği için MIME
 * tabanlı accept XSLT'yi XML diye geçirir. Bu yüzden uzantı + içerik (kök eleman) yüklemede
 * ayrıca denetlenir.
 */

export type UploadKind = 'xml' | 'xslt'

const XSL_NAMESPACE = 'http://www.w3.org/1999/XSL/Transform'
// XML: mevcut 1 MB sınırı. XSLT: gömülü base64 logo/imza nedeniyle büyüyebilir; ham önizleme
// ucu XSLT+XML için 10 MB kabul ettiği için 5 MB.
const MAX_BYTES: Record<UploadKind, number> = {
  xml: 1 * 1024 * 1024,
  xslt: 5 * 1024 * 1024,
}

/** `<input accept>` değerleri — MIME tipi bilinçli olarak yok (yukarıdaki nota bkz.). */
export const UPLOAD_ACCEPT: Record<UploadKind, string> = {
  xml: '.xml',
  xslt: '.xsl,.xslt',
}

const EXTENSIONS: Record<UploadKind, string[]> = {
  xml: ['.xml'],
  xslt: ['.xsl', '.xslt'],
}

const LABEL: Record<UploadKind, string> = { xml: 'XML', xslt: 'XSLT' }

/** Uzantı ve boyut denetimi — dosya okunmadan önce. Hata mesajı ya da null döner. */
export function validateUploadFile(file: File, kind: UploadKind): string | null {
  const name = file.name.toLowerCase()
  if (!EXTENSIONS[kind].some((ext) => name.endsWith(ext))) {
    return `"${file.name}" bir ${LABEL[kind]} dosyası değil. Yalnız ${EXTENSIONS[kind].join(', ')} uzantılı dosyalar yüklenebilir.`
  }
  if (file.size > MAX_BYTES[kind]) {
    return `${LABEL[kind]} dosyası ${MAX_BYTES[kind] / (1024 * 1024)} MB'dan büyük olamaz.`
  }
  return null
}

/**
 * İçerik denetimi — kök elemanın türü. XML alanına stylesheet, XSLT alanına stylesheet olmayan
 * belge yüklenemez. Ayrıştırılamayan içerik burada reddedilmez: editör hatayı satır/sütunla
 * gösterir ve kullanıcı düzeltebilir.
 */
export function validateUploadContent(content: string, kind: UploadKind): string | null {
  const doc = new DOMParser().parseFromString(content, 'application/xml')
  if (doc.getElementsByTagName('parsererror').length > 0) return null

  const isStylesheet = doc.documentElement?.namespaceURI === XSL_NAMESPACE
  if (kind === 'xml' && isStylesheet) {
    return 'Bu dosya bir XSLT şablonu. XML alanına yalnız veri belgesi (ör. e-Fatura / e-İrsaliye XML\'i) yüklenebilir.'
  }
  if (kind === 'xslt' && !isStylesheet) {
    return 'Bu dosya bir XSLT şablonu değil (kök eleman xsl:stylesheet / xsl:transform olmalı).'
  }
  return null
}

/** Dosyayı doğrulayıp UTF-8 metin olarak okur; geçersizse hata mesajıyla reddeder. */
export async function readUploadFile(file: File, kind: UploadKind): Promise<string> {
  const fileError = validateUploadFile(file, kind)
  if (fileError) throw new Error(fileError)
  const content = await file.text()
  const contentError = validateUploadContent(content, kind)
  if (contentError) throw new Error(contentError)
  return content
}
