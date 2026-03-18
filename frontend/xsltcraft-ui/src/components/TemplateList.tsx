import { useState } from "react"
import { getTemplateFiles } from "../api/templateApi"

interface Props {
  onFileSelect: (templateId: string, fileName: string) => void
}

const TEMPLATES = ["invoice", "despatch", "order"]

export default function TemplateList({ onFileSelect }: Props) {
  const [expanded, setExpanded] = useState<string | null>(null)
  const [filesMap, setFilesMap] = useState<Record<string, string[]>>({})
  const [loading, setLoading] = useState<string | null>(null)
  const [activeFile, setActiveFile] = useState<{ templateId: string; fileName: string } | null>(null)

  async function handleTemplateClick(templateId: string) {
    if (expanded === templateId) {
      setExpanded(null)
      return
    }

    setExpanded(templateId)

    if (!filesMap[templateId]) {
      setLoading(templateId)
      try {
        const data = await getTemplateFiles(templateId)
        setFilesMap(prev => ({ ...prev, [templateId]: data.files ?? [] }))
      } finally {
        setLoading(null)
      }
    }
  }

  function handleFileClick(templateId: string, fileName: string) {
    setActiveFile({ templateId, fileName })
    onFileSelect(templateId, fileName)
  }

  return (
    <div style={{
      padding: 12,
      fontSize: 13,
      color: "var(--xc-text-primary)",
      height: "100%",
      boxSizing: "border-box",
      overflowY: "auto",
    }}>
      <div style={{
        fontSize: 11,
        fontWeight: 600,
        textTransform: "uppercase",
        letterSpacing: 1,
        color: "var(--xc-text-secondary)",
        margin: "0 0 12px 4px",
        paddingBottom: 8,
        borderBottom: "1px solid var(--xc-border)",
      }}>
        Templates
      </div>

      {TEMPLATES.map(t => (
        <div key={t} style={{ marginBottom: 2 }}>
          {/* Folder row */}
          <div
            className="xc-tree-folder"
            onClick={() => handleTemplateClick(t)}
            style={{
              background: expanded === t ? "var(--xc-bg-surface)" : undefined,
            }}
          >
            <span className={`xc-chevron ${expanded === t ? "xc-chevron--expanded" : ""}`}>
              ▶
            </span>
            {t}
            {loading === t && (
              <span className="xc-loading">loading...</span>
            )}
          </div>

          {/* File list */}
          {expanded === t && (
            <div style={{ paddingLeft: 20, marginTop: 2 }}>
              {(filesMap[t] ?? []).length === 0 && loading !== t && (
                <div style={{
                  padding: "8px 12px",
                  color: "var(--xc-text-disabled)",
                  fontSize: 12,
                  fontStyle: "italic",
                }}>
                  No .xslt files found
                </div>
              )}
              {(filesMap[t] ?? []).map(file => {
                const isActive =
                  activeFile?.templateId === t && activeFile?.fileName === file
                return (
                  <div
                    key={file}
                    className={`xc-tree-file ${isActive ? "xc-tree-file--active" : ""}`}
                    onClick={() => handleFileClick(t, file)}
                  >
                    {file}
                  </div>
                )
              })}
            </div>
          )}
        </div>
      ))}
    </div>
  )
}
