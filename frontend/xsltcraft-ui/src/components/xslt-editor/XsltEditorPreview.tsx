import { useRef, useEffect } from 'react'

interface Props {
  html: string
  onElementClick?: (text: string) => void
}

const CLICK_SCRIPT = `
<script>
document.addEventListener('click', function(e) {
  e.preventDefault();
  var el = e.target;
  while (el && el !== document.body) {
    var text = el.textContent ? el.textContent.trim().substring(0, 120) : '';
    if (text) {
      window.parent.postMessage({ type: 'xslt-preview-click', text: text }, '*');
      return;
    }
    el = el.parentElement;
  }
});
</script>
`

export default function XsltEditorPreview({ html, onElementClick }: Props) {
  const iframeRef = useRef<HTMLIFrameElement>(null)

  // Inject click handler script into preview HTML
  const injectedHtml = html
    ? html.replace('</body>', CLICK_SCRIPT + '</body>')
    : '<html><body style="background:#1f2937;margin:0"></body></html>'

  // Listen for postMessage from iframe
  useEffect(() => {
    if (!onElementClick) return

    function handler(e: MessageEvent) {
      if (e.origin !== window.location.origin) return
      if (e.data?.type === 'xslt-preview-click' && e.data.text) {
        onElementClick!(e.data.text)
      }
    }

    window.addEventListener('message', handler)
    return () => window.removeEventListener('message', handler)
  }, [onElementClick])

  return (
    <div className="h-full flex flex-col">
      <div className="px-3 py-1.5 bg-gray-800 border-b border-gray-700 flex-shrink-0">
        <span className="text-xs text-gray-400 font-mono uppercase tracking-wide">Önizleme</span>
      </div>
      <div className="flex-1 overflow-auto bg-gray-300 flex justify-center">
        <iframe
          ref={iframeRef}
          srcDoc={injectedHtml}
          sandbox="allow-same-origin allow-scripts allow-modals"
          className="border-none bg-white shadow-lg"
          style={{ width: 860, minHeight: '100%', height: 'max(100%, 1200px)' }}
          title="Canlı Önizleme"
        />
      </div>
    </div>
  )
}

// eslint-disable-next-line react-refresh/only-export-components
export function printPreview(iframeEl: HTMLIFrameElement | null) {
  iframeEl?.contentWindow?.print()
}
