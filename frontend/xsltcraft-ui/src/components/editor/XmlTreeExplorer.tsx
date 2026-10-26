import { useId, useMemo, useState } from 'react'
import { useXmlStore } from '../../store/xmlStore'
import { parseXmlToTree, type XmlNode } from '../../utils/xmlUtils'
import { ChevronRight, ChevronDown, Search, X, MousePointerClick, FileCode, Upload, TriangleAlert } from 'lucide-react'

// ── Single tree node ──────────────────────────────────────────────────────────

function TreeNode({
  node,
  onSelect,
  isSelecting,
  filter,
}: {
  node: XmlNode
  onSelect: (xpath: string) => void
  isSelecting: boolean
  filter: string
}) {
  const [expanded, setExpanded] = useState(node.depth < 2)

  // Filter matching: show node if name or value contains filter
  const lowerFilter = filter.toLowerCase()
  const nameMatch = !filter || node.name.toLowerCase().includes(lowerFilter)
  const valueMatch = !filter || (node.value ?? '').toLowerCase().includes(lowerFilter)
  const shouldShow = !filter || nameMatch || valueMatch

  if (!shouldShow && node.isLeaf) return null

  // Auto-expand when filtering
  const effectiveExpanded = filter ? true : expanded
  const indent = node.depth * 14
  const interactive = isSelecting || !node.isLeaf

  const rowContent = (
    <>
      {/* Expand toggle / leaf indicator */}
      <span
        aria-hidden="true"
        className="flex h-4 w-4 flex-none items-center justify-center text-tx-subtle"
        onClick={(e) => {
          if (!node.isLeaf) {
            e.stopPropagation()
            setExpanded((v) => !v)
          }
        }}
      >
        {!node.isLeaf ? (
          effectiveExpanded ? <ChevronDown size={12} strokeWidth={2.2} /> : <ChevronRight size={12} strokeWidth={2.2} />
        ) : (
          <span className="inline-block h-1 w-1 rounded-full bg-tx-subtle" />
        )}
      </span>

      {/* Tag name */}
      <span className={`flex-none whitespace-nowrap font-mono text-[11.5px] ${node.isLeaf ? 'text-ui-ink' : 'font-medium text-tx'}`}>
        {node.name}
      </span>

      {/* Leaf value */}
      {node.isLeaf && node.value && (
        <span className="ml-auto min-w-0 truncate pl-2 text-[11.5px] text-tx-subtle" title={node.value}>
          {node.value}
        </span>
      )}

      {/* Child count badge for collapsed parents */}
      {!node.isLeaf && !effectiveExpanded && node.children.length > 0 && (
        <span className="flex-none text-[10px] text-tx-subtle">({node.children.length})</span>
      )}

      {/* Select hint */}
      {isSelecting && (
        <span className="ml-1 flex flex-none items-center gap-0.5 text-[10px] font-medium text-ui-accent opacity-0 group-hover:opacity-100 group-focus-visible:opacity-100">
          <MousePointerClick size={10} aria-hidden="true" />
          seç
        </span>
      )}
    </>
  )

  const rowClass = `group flex w-full min-h-[25px] items-center gap-1.5 rounded pr-1.5 text-left transition-colors ${
    isSelecting ? 'cursor-pointer hover:bg-ui-soft' : interactive ? 'cursor-pointer hover:bg-ui-hover' : 'cursor-default'
  }`

  return (
    <div>
      {interactive ? (
        <button
          type="button"
          className={`xc-focus ${rowClass}`}
          style={{ paddingLeft: indent + 6 }}
          aria-expanded={!node.isLeaf && !isSelecting ? effectiveExpanded : undefined}
          aria-label={isSelecting ? `${node.name} öğesini seç` : undefined}
          onClick={() => {
            if (isSelecting) onSelect(node.xpath)
            else if (!node.isLeaf) setExpanded((v) => !v)
          }}
        >
          {rowContent}
        </button>
      ) : (
        <div className={rowClass} style={{ paddingLeft: indent + 6 }}>
          {rowContent}
        </div>
      )}

      {/* Children */}
      {!node.isLeaf && effectiveExpanded && (
        <div className="relative">
          {/* Indent guide line */}
          <div aria-hidden="true" className="absolute bottom-0 top-0 w-px bg-ui-line" style={{ left: indent + 13 }} />
          {node.children.map((child) => (
            <TreeNode key={child.id} node={child} onSelect={onSelect} isSelecting={isSelecting} filter={filter} />
          ))}
        </div>
      )}
    </div>
  )
}

// ── Root XmlTreeExplorer ──────────────────────────────────────────────────────

interface XmlTreeExplorerProps {
  /** Sayfa kökündeki gizli XML input'unu tetikler (EditorPage.xmlInputRef). */
  onUploadClick: () => void
  /** Son yükleme hatası (geçersiz XML / doğrulama). */
  uploadError?: string | null
}

/** Sol paneldeki "XML Ağacı" sekmesi: dosya çipi, XML yükleme/seçimi, arama ve XPath seçim ağacı. */
export default function XmlTreeExplorer({ onUploadClick, uploadError }: XmlTreeExplorerProps) {
  const xmlFiles = useXmlStore((s) => s.xmlFiles)
  const activeXmlId = useXmlStore((s) => s.activeXmlId)
  const setActiveXml = useXmlStore((s) => s.setActiveXml)
  const isSelecting = useXmlStore((s) => s.isSelecting)
  const applyXPathSelection = useXmlStore((s) => s.applyXPathSelection)
  const cancelXPathSelection = useXmlStore((s) => s.cancelXPathSelection)

  const [filter, setFilter] = useState('')
  const baseId = useId()

  const activeXml = xmlFiles.find((f) => f.id === activeXmlId)

  const tree = useMemo(() => (activeXml ? parseXmlToTree(activeXml.content) : null), [activeXml])

  return (
    <div className="flex min-h-0 flex-1 flex-col">
      {/* Selection mode banner */}
      {isSelecting && (
        <div role="status" className="flex flex-none items-center justify-between gap-2 bg-ui-accent px-4 py-2 text-white">
          <span className="flex items-center gap-2 text-[12px] font-medium">
            <MousePointerClick size={13} aria-hidden="true" />
            Bir öğeye tıklayarak XPath'i seçin
          </span>
          <button
            type="button"
            onClick={cancelXPathSelection}
            style={{ '--xc-focus-ring': 'white' } as React.CSSProperties}
            className="xc-focus flex items-center gap-1 rounded px-1.5 py-0.5 text-[12px] text-white/85 hover:bg-white/15 hover:text-white"
          >
            <X size={12} aria-hidden="true" />
            İptal
          </button>
        </div>
      )}

      <div className="flex flex-none flex-col gap-2.5 px-4 pb-2 pt-3.5">
        {/* Dosya çipi + yükleme */}
        <div className="flex items-center gap-2 rounded-md bg-ui-soft px-2.5 py-2 text-[12px] text-tx-2">
          <FileCode size={14} strokeWidth={1.8} aria-hidden="true" className="flex-none text-ui-ink" />
          {xmlFiles.length > 1 ? (
            <>
              <label htmlFor={`${baseId}-file`} className="sr-only">Etkin XML dosyası</label>
              <select
                id={`${baseId}-file`}
                value={activeXmlId ?? ''}
                onChange={(e) => setActiveXml(e.target.value)}
                className="xc-focus min-w-0 flex-1 cursor-pointer truncate rounded bg-transparent font-medium text-tx outline-none"
              >
                {xmlFiles.map((f) => (
                  <option key={f.id} value={f.id}>{f.name}</option>
                ))}
              </select>
            </>
          ) : (
            <span className="min-w-0 flex-1 truncate font-medium" title={activeXml?.name}>
              {activeXml?.name ?? 'XML yüklenmedi'}
            </span>
          )}
          <button
            type="button"
            onClick={onUploadClick}
            className="xc-focus flex flex-none items-center gap-1 rounded px-1.5 py-0.5 text-[11.5px] font-medium text-ui-ink hover:bg-ui-card"
            title="XML dosyası yükle"
          >
            <Upload size={12} strokeWidth={2} aria-hidden="true" />
            XML yükle
          </button>
        </div>

        {uploadError && (
          <p role="alert" className="m-0 flex items-start gap-1.5 rounded-md border border-fn-red-line bg-fn-red-tint px-2.5 py-1.5 text-[12px] text-fn-red-ink">
            <TriangleAlert size={13} aria-hidden="true" className="mt-0.5 flex-none" />
            {uploadError}
          </p>
        )}

        {/* Search */}
        {tree && (
          <label
            htmlFor={`${baseId}-search`}
            className="flex h-[34px] items-center gap-2 rounded-md border border-field-line bg-ui-side px-2.5 text-tx-subtle focus-within:border-ui-accent"
          >
            <Search size={15} strokeWidth={1.8} aria-hidden="true" />
            <span className="sr-only">Alan ara</span>
            <input
              id={`${baseId}-search`}
              type="text"
              value={filter}
              onChange={(e) => setFilter(e.target.value)}
              placeholder="Alan ara, ör. IssueDate"
              className="min-w-0 flex-1 bg-transparent text-[13px] text-tx outline-none placeholder:text-tx-subtle"
            />
            {filter && (
              <button
                type="button"
                onClick={() => setFilter('')}
                aria-label="Aramayı temizle"
                className="xc-focus rounded text-tx-subtle hover:text-tx"
              >
                <X size={12} aria-hidden="true" />
              </button>
            )}
          </label>
        )}
      </div>

      {/* Tree / boş durumlar */}
      {!activeXml ? (
        <div className="flex flex-1 items-center justify-center p-6">
          <p className="text-center text-[12px] leading-relaxed text-tx-subtle">
            Bağlama ve önizleme için yukarıdaki <strong className="text-tx-muted">XML yükle</strong> ile bir belge XML'i seçin.
          </p>
        </div>
      ) : !tree ? (
        <div className="flex flex-1 items-center justify-center p-4">
          <p className="text-center text-[12px] text-fn-red-ink">XML ayrıştırılamadı.</p>
        </div>
      ) : (
        <div className="min-h-0 flex-1 overflow-y-auto px-3 pb-4">
          <TreeNode node={tree} onSelect={applyXPathSelection} isSelecting={isSelecting} filter={filter} />
          {!isSelecting && (
            <p className="mx-1 mt-3 text-[12px] leading-normal text-tx-muted">
              Sağ paneldeki bir XPath alanının yanındaki bağlantı düğmesine bastıktan sonra buradan bir öğe seçin.
            </p>
          )}
        </div>
      )}
    </div>
  )
}
