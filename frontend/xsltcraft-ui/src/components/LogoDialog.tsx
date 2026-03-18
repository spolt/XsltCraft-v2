import { useState, useRef } from "react"
import { createPortal } from "react-dom"

interface Props {
  open: boolean
  title?: string
  onClose: () => void
  onConfirm: (base64: string, mimeType: string, width: number, height: number) => void
}

export default function LogoDialog({ open, title = "Logo Ayarları", onClose, onConfirm }: Props) {
  const [base64, setBase64] = useState("")
  const [mimeType, setMimeType] = useState("")
  const [previewSrc, setPreviewSrc] = useState("")
  const [width, setWidth] = useState(150)
  const [height, setHeight] = useState(200)
  const fileRef = useRef<HTMLInputElement>(null)

  if (!open) return null

  function handleFileChange(e: React.ChangeEvent<HTMLInputElement>) {
    const file = e.target.files?.[0]
    if (!file) return

    const reader = new FileReader()
    reader.onload = (ev) => {
      const dataUrl = ev.target?.result as string
      setPreviewSrc(dataUrl)

      const [meta, b64] = dataUrl.split(",")
      const mime = meta.replace("data:", "").replace(";base64", "")
      setBase64(b64)
      setMimeType(mime)
    }
    reader.readAsDataURL(file)
    e.target.value = ""
  }

  function handleConfirm() {
    if (!base64) return
    onConfirm(base64, mimeType, width, height)
    setBase64("")
    setMimeType("")
    setPreviewSrc("")
    setWidth(150)
    setHeight(200)
    onClose()
  }

  function handleCancel() {
    setBase64("")
    setMimeType("")
    setPreviewSrc("")
    setWidth(150)
    setHeight(200)
    onClose()
  }

  return createPortal(
    <div
      className="xc-overlay"
      style={{
        position: "fixed",
        inset: 0,
        zIndex: 9999,
        display: "flex",
        alignItems: "center",
        justifyContent: "center",
        background: "var(--xc-overlay)",
      }}
      onClick={handleCancel}
    >
      <div
        className="xc-dialog"
        style={{
          background: "var(--xc-dialog-bg)",
          color: "var(--xc-text-primary)",
          borderRadius: 12,
          padding: 28,
          minWidth: 400,
          maxWidth: 480,
          border: "1px solid var(--xc-border)",
          boxShadow: "0 12px 48px rgba(0,0,0,0.6)",
        }}
        onClick={(e) => e.stopPropagation()}
      >
        <h3 style={{
          margin: "0 0 20px",
          fontSize: 16,
          fontWeight: 600,
          color: "var(--xc-text-primary)",
        }}>
          {title}
        </h3>

        {/* File picker */}
        <button
          className="xc-btn-primary"
          onClick={() => fileRef.current?.click()}
          style={{ width: "100%", padding: "10px 16px", fontSize: 14 }}
        >
          {previewSrc ? "Farklı Logo Seç" : "Logo Dosyası Seç"}
        </button>
        <input
          ref={fileRef}
          type="file"
          accept="image/*"
          onChange={handleFileChange}
          style={{ display: "none" }}
        />

        {/* Preview */}
        {previewSrc && (
          <div style={{
            marginTop: 16,
            textAlign: "center",
            background: "#fff",
            borderRadius: 8,
            padding: 16,
            border: "1px solid var(--xc-border)",
          }}>
            <img
              src={previewSrc}
              alt="Preview"
              style={{ maxWidth: "100%", maxHeight: 120, objectFit: "contain" }}
            />
          </div>
        )}

        {/* Width / Height */}
        <div style={{ display: "flex", gap: 12, marginTop: 16 }}>
          <label style={{ flex: 1 }}>
            <div style={{
              fontSize: 12,
              fontWeight: 500,
              color: "var(--xc-text-secondary)",
              marginBottom: 6,
            }}>
              Genişlik (px)
            </div>
            <input
              type="number"
              className="xc-input"
              value={width}
              onChange={(e) => setWidth(Number(e.target.value))}
            />
          </label>
          <label style={{ flex: 1 }}>
            <div style={{
              fontSize: 12,
              fontWeight: 500,
              color: "var(--xc-text-secondary)",
              marginBottom: 6,
            }}>
              Yükseklik (px)
            </div>
            <input
              type="number"
              className="xc-input"
              value={height}
              onChange={(e) => setHeight(Number(e.target.value))}
            />
          </label>
        </div>

        {/* Actions */}
        <div style={{ display: "flex", gap: 10, marginTop: 24, justifyContent: "flex-end" }}>
          <button className="xc-btn" onClick={handleCancel}>
            İptal
          </button>
          <button
            className="xc-btn-primary"
            onClick={handleConfirm}
            disabled={!base64}
            style={!base64 ? {
              background: "var(--xc-bg-surface)",
              color: "var(--xc-text-disabled)",
              cursor: "not-allowed",
            } : undefined}
          >
            Uygula
          </button>
        </div>
      </div>
    </div>,
    document.body
  )
}
