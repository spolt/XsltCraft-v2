import { useEffect, useRef } from "react"

type Props = {
  html: string
  onTextSelect?: (selectedText: string) => void
}

export default function PreviewPanel({ html, onTextSelect }: Props) {
  const iframeRef = useRef<HTMLIFrameElement>(null)

  useEffect(() => {
    const iframe = iframeRef.current
    if (!iframe) return

    const doc = iframe.contentDocument
    if (!doc) return

    doc.open()
    doc.write(html)
    doc.close()

    if (!onTextSelect) return

    const handler = () => {
      const sel = doc.getSelection()
      const text = sel?.toString().trim()
      if (text && text.length > 0) {
        onTextSelect(text)
      }
    }

    doc.addEventListener("mouseup", handler)
    return () => doc.removeEventListener("mouseup", handler)

  }, [html])

  return (
    <iframe
      ref={iframeRef}
      style={{
        width: "100%",
        height: "100%",
        border: "none",
        background: "white",
      }}
    />
  )
}
