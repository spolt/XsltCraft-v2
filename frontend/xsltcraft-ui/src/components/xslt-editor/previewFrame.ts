// Önizleme iframe'inin ortak sabitleri ve saf yardımcıları.
// iframe yalnız ScaledPreviewFrame içinde render edilir; sandbox değeri buradan gelir
// ve çağıran tarafından genişletilemez.

export const PREVIEW_BASE_WIDTH = 860
// 96 dpi'de yaklaşık 3,5 A4 sayfalık tampon
export const PREVIEW_BASE_HEIGHT = 4000
export const PREVIEW_IFRAME_TITLE = 'Canlı Önizleme'
export const PREVIEW_SANDBOX = 'allow-same-origin allow-scripts allow-modals'
export const PREVIEW_CLICK_MESSAGE = 'xslt-preview-click'

const MIN_FIT_SCALE = 0.2
const MIN_CONTENT_HEIGHT = 200
const MAX_CONTENT_HEIGHT = 20000

const CLICK_SCRIPT = `
<script>
document.addEventListener('click', function(e) {
  e.preventDefault();
  var el = e.target;
  // Görsel tıklaması: base64 src'den ayırt edici bir parça çıkar
  var img = el;
  while (img && img !== document.body) {
    if (img.tagName === 'IMG') {
      var src = img.getAttribute('src') || '';
      var idx = src.indexOf('base64,');
      if (idx !== -1) {
        var b64 = src.substring(idx + 7).replace(/\\s/g, '');
        // İlk ~48 karakter çoğu formatta ortak başlık olduğundan atla,
        // sonraki ~80 karakteri ayırt edici imza olarak kullan
        var start = b64.length > 128 ? 48 : 0;
        var fragment = b64.substr(start, 80);
        if (fragment) {
          window.parent.postMessage({ type: '${PREVIEW_CLICK_MESSAGE}', text: fragment, exact: true }, '*');
          return;
        }
      }
    }
    img = img.parentElement;
  }
  // Metin tıklaması
  while (el && el !== document.body) {
    var text = el.textContent ? el.textContent.trim().substring(0, 120) : '';
    if (text) {
      window.parent.postMessage({ type: '${PREVIEW_CLICK_MESSAGE}', text: text }, '*');
      return;
    }
    el = el.parentElement;
  }
});
</script>
`

/** Önizleme HTML'ine tıkla→satıra git betiğini ekler; HTML boşsa koyu yer tutucu döner. */
export function buildPreviewSrcDoc(html: string): string {
  return html
    ? html.replace('</body>', CLICK_SCRIPT + '</body>')
    : '<html><body style="background:#1f2937;margin:0"></body></html>'
}

/** Önizlemeyi verilen genişliğe sığdıran ölçek (en fazla 1). */
export function fitScale(availableWidth: number, baseWidth: number = PREVIEW_BASE_WIDTH): number {
  return Math.min(1, Math.max(MIN_FIT_SCALE, availableWidth / baseWidth))
}

/**
 * Yüklenmiş önizleme belgesinin gerçek içerik yüksekliği (px). Kök elemanın
 * scrollHeight'i en az viewport kadar olduğundan, ölçüm sırasında iframe geçici
 * olarak 0 yüksekliğe indirilir. Belgeye erişilemezse null döner.
 */
export function measurePreviewContentHeight(iframe: HTMLIFrameElement | null): number | null {
  const doc = iframe?.contentDocument
  if (!iframe || !doc?.documentElement) return null
  const previous = iframe.style.height
  iframe.style.height = '0px'
  const measured = Math.max(doc.documentElement.scrollHeight, doc.body?.scrollHeight ?? 0)
  iframe.style.height = previous
  if (!Number.isFinite(measured)) return null
  return Math.min(MAX_CONTENT_HEIGHT, Math.max(MIN_CONTENT_HEIGHT, Math.ceil(measured)))
}

/** Sayfadaki önizleme iframe'ini yazdırır; açık önizleme yoksa false döner. */
export function printActivePreview(root: ParentNode = document): boolean {
  const iframe = root.querySelector<HTMLIFrameElement>(`iframe[title="${PREVIEW_IFRAME_TITLE}"]`)
  if (!iframe?.contentWindow) return false
  iframe.contentWindow.print()
  return true
}
