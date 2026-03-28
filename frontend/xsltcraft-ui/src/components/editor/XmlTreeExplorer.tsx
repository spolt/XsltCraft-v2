import { useMemo, useState } from 'react'
import { useXmlStore } from '../../store/xmlStore'
import { parseXmlToTree, type XmlNode } from '../../utils/xmlUtils'
import { ChevronRight, ChevronDown, Search, X, MousePointerClick } from 'lucide-react'

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

  // For parent nodes, we still render if any potential child might match (we can't know ahead of time without traversal)
  // Simpler: just always render when filter is empty; when filter set, only show leaf matches + their ancestors
  const shouldShow = !filter || nameMatch || valueMatch

  if (!shouldShow && node.isLeaf) return null

  // Auto-expand when filtering
  const effectiveExpanded = filter ? true : expanded

  const indent = node.depth * 16

  return (
    <div>
      <div
        className={`flex items-center gap-1.5 py-1 pr-2 rounded-md group transition-colors ${
          isSelecting
            ? 'cursor-pointer hover:bg-blue-50'
            : node.isLeaf
            ? 'cursor-default hover:bg-gray-50'
            : 'cursor-pointer hover:bg-gray-50'
        }`}
        style={{ paddingLeft: `${indent + 6}px` }}
        onClick={() => {
          if (isSelecting) {
            onSelect(node.xpath)
          } else if (!node.isLeaf) {
            setExpanded((v) => !v)
          }
        }}
      >
        {/* Expand toggle / leaf indicator */}
        <span
          className="flex-shrink-0 w-4 h-4 flex items-center justify-center text-gray-400"
          onClick={(e) => {
            if (!node.isLeaf) {
              e.stopPropagation()
              setExpanded((v) => !v)
            }
          }}
        >
          {!node.isLeaf ? (
            effectiveExpanded
              ? <ChevronDown size={13} />
              : <ChevronRight size={13} />
          ) : (
            <span className="w-1.5 h-1.5 rounded-full bg-gray-300 inline-block" />
          )}
        </span>

        {/* Tag name */}
        <span
          className={`font-mono text-xs font-medium flex-shrink-0 ${
            isSelecting
              ? 'text-blue-700 group-hover:text-blue-800'
              : node.isLeaf
              ? 'text-indigo-600'
              : 'text-gray-700'
          }`}
        >
          {node.name}
        </span>

        {/* Leaf value pill */}
        {node.isLeaf && node.value && (
          <span
            className="text-xs text-gray-500 bg-gray-100 rounded px-1.5 py-0.5 truncate max-w-[200px] flex-shrink min-w-0"
            title={node.value}
          >
            {node.value}
          </span>
        )}

        {/* Child count badge for collapsed parents */}
        {!node.isLeaf && !effectiveExpanded && node.children.length > 0 && (
          <span className="text-[10px] text-gray-400 flex-shrink-0">
            ({node.children.length})
          </span>
        )}

        {/* Select hint */}
        {isSelecting && (
          <span className="ml-auto text-blue-500 opacity-0 group-hover:opacity-100 text-[10px] font-medium flex-shrink-0 flex items-center gap-0.5">
            <MousePointerClick size={10} />
            seç
          </span>
        )}
      </div>

      {/* Children */}
      {!node.isLeaf && effectiveExpanded && (
        <div className="relative">
          {/* Indent guide line */}
          <div
            className="absolute top-0 bottom-0 w-px bg-gray-100"
            style={{ left: `${indent + 13}px` }}
          />
          {node.children.map((child) => (
            <TreeNode
              key={child.id}
              node={child}
              onSelect={onSelect}
              isSelecting={isSelecting}
              filter={filter}
            />
          ))}
        </div>
      )}
    </div>
  )
}

// ── Root XmlTreeExplorer ──────────────────────────────────────────────────────

export default function XmlTreeExplorer() {
  const xmlFiles = useXmlStore((s) => s.xmlFiles)
  const activeXmlId = useXmlStore((s) => s.activeXmlId)
  const isSelecting = useXmlStore((s) => s.isSelecting)
  const applyXPathSelection = useXmlStore((s) => s.applyXPathSelection)
  const cancelXPathSelection = useXmlStore((s) => s.cancelXPathSelection)

  const [filter, setFilter] = useState('')

  const activeXml = xmlFiles.find((f) => f.id === activeXmlId)

  const tree = useMemo(
    () => (activeXml ? parseXmlToTree(activeXml.content) : null),
    [activeXml]
  )

  if (!activeXml) {
    return (
      <div className="flex-1 flex items-center justify-center p-6">
        <p className="text-xs text-gray-400 text-center leading-relaxed">
          Önizleme için toolbar'daki<br />
          <strong className="text-gray-500">+ XML</strong> butonunu kullanın.
        </p>
      </div>
    )
  }

  if (!tree) {
    return (
      <div className="flex-1 flex items-center justify-center p-4">
        <p className="text-xs text-red-400 text-center">XML ayrıştırılamadı.</p>
      </div>
    )
  }

  return (
    <div className="flex-1 flex flex-col overflow-hidden">

      {/* Selection mode banner */}
      {isSelecting && (
        <div className="flex items-center justify-between px-3 py-2 bg-blue-600 flex-shrink-0">
          <div className="flex items-center gap-2">
            <MousePointerClick size={13} className="text-blue-200" />
            <span className="text-xs text-white font-medium">
              Bir node'a tıklayarak XPath'i seçin
            </span>
          </div>
          <button
            onClick={cancelXPathSelection}
            className="flex items-center gap-1 text-xs text-blue-200 hover:text-white transition-colors"
          >
            <X size={12} />
            İptal
          </button>
        </div>
      )}

      {/* Search */}
      <div className="px-2 py-2 border-b border-gray-100 flex-shrink-0">
        <div className="flex items-center gap-1.5 bg-gray-50 border border-gray-200 rounded-md px-2 py-1.5">
          <Search size={12} className="text-gray-400 flex-shrink-0" />
          <input
            type="text"
            value={filter}
            onChange={(e) => setFilter(e.target.value)}
            placeholder="Node veya değer ara..."
            className="flex-1 text-xs bg-transparent outline-none text-gray-700 placeholder-gray-400"
          />
          {filter && (
            <button onClick={() => setFilter('')} className="text-gray-400 hover:text-gray-600">
              <X size={11} />
            </button>
          )}
        </div>
      </div>

      {/* File name */}
      <div className="px-3 py-1 flex-shrink-0">
        <span className="text-[10px] font-medium text-gray-400 uppercase tracking-wide">
          {activeXml.name}
        </span>
      </div>

      {/* Tree */}
      <div className="flex-1 overflow-y-auto pb-4">
        <TreeNode
          node={tree}
          onSelect={applyXPathSelection}
          isSelecting={isSelecting}
          filter={filter}
        />
      </div>
    </div>
  )
}
