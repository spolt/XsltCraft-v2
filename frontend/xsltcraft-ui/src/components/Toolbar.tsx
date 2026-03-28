import { useState } from "react"
import type React from "react"
import LogoDialog from "./LogoDialog"

type XmlStatus = "empty" | "loaded" | "invalid"
type XsltStatus = "valid" | "invalid" | "checking"

type Props = {
  onXmlUpload: (e: React.ChangeEvent<HTMLInputElement>) => void
  onLogoUpload: (base64: string, mimeType: string, width: number, height: number) => void
  onSignatureUpload: (base64: string, mimeType: string, width: number, height: number) => void
  onSave: () => void
  xmlStatus: XmlStatus
  xsltStatus: XsltStatus
  xsltErrorTooltip?: string
}

const xmlStatusConfig: Record<XmlStatus, { className: string; label: string; tooltip?: string }> = {
  empty:   { className: "xc-status-empty",  label: "XML yüklenmedi" },
  loaded:  { className: "xc-status-loaded", label: "XML yüklendi" },
  invalid: { className: "xc-status-error",  label: "XML Yüklenemedi", tooltip: "Yüklemek istediğiniz XML geçersiz! Kontrol ediniz." },
}

const xsltStatusConfig: Record<XsltStatus, { className: string; label: string }> = {
  valid:    { className: "xc-status-loaded",   label: "XSLT Geçerli" },
  invalid:  { className: "xc-status-error",    label: "XSLT Hatalı" },
  checking: { className: "xc-status-checking", label: "Kontrol ediliyor..." },
}

export default function Toolbar({ onXmlUpload, onLogoUpload, onSignatureUpload, onSave, xmlStatus, xsltStatus, xsltErrorTooltip }: Props) {
  const [logoDialogOpen, setLogoDialogOpen] = useState(false)
  const [signatureDialogOpen, setSignatureDialogOpen] = useState(false)

  const xmlPill = xmlStatusConfig[xmlStatus]
  const xsltPill = xsltStatusConfig[xsltStatus]

  return (
    <div
      style={{
        height: 48,
        background: "var(--xc-bg-toolbar)",
        borderBottom: "1px solid var(--xc-border)",
        boxShadow: "var(--xc-shadow-sm)",
        display: "flex",
        alignItems: "center",
        padding: "0 16px",
        gap: 8,
        position: "relative",
        zIndex: 100,
      }}
    >
      {/* Button group */}
      <div style={{ display: "flex", gap: 6 }}>
        <label className="xc-btn" style={{ cursor: "pointer" }}>
          XML Yükle
          <input
            type="file"
            accept=".xml"
            onChange={onXmlUpload}
            style={{ display: "none" }}
          />
        </label>

        <button className="xc-btn" onClick={() => setLogoDialogOpen(true)}>
          Logo Seç
        </button>

        <button className="xc-btn" onClick={() => setSignatureDialogOpen(true)}>
          İmza Yükle
        </button>
      </div>

      {/* Divider */}
      <div style={{ width: 1, height: 24, background: "var(--xc-border)", margin: "0 4px" }} />

      {/* XML Status pill */}
      <span
        className={xmlPill.className}
        data-tooltip={xmlPill.tooltip}
      >
        {xmlPill.label}
      </span>

      {/* XSLT Status pill */}
      <span
        className={xsltPill.className}
        data-tooltip={xsltStatus === "invalid" ? xsltErrorTooltip : undefined}
      >
        {xsltPill.label}
      </span>

      {/* Spacer */}
      <div style={{ flex: 1 }} />

      {/* Save */}
      <button className="xc-btn-primary" onClick={onSave}>
        Save
      </button>

      {/* Dialogs */}
      <LogoDialog
        open={logoDialogOpen}
        title="Logo Ayarları"
        onClose={() => setLogoDialogOpen(false)}
        onConfirm={onLogoUpload}
      />
      <LogoDialog
        open={signatureDialogOpen}
        title="İmza Ayarları"
        onClose={() => setSignatureDialogOpen(false)}
        onConfirm={onSignatureUpload}
      />
    </div>
  )
}
