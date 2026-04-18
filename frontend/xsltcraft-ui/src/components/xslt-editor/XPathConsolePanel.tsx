import { useState, useRef, type KeyboardEvent } from 'react'
import { Terminal, Play, X, ChevronDown, ChevronUp, AlertCircle, Info } from 'lucide-react'
import { evaluateXPath, type XPathResultItem, type XPathEvaluateResponse } from '../../services/xpathService'

interface Props {
  xmlContent: string | null
  onLocateLine?: (line: number, column?: number | null) => void
  onClose: () => void
  initialExpression?: string
}

const KIND_BADGE: Record<string, string> = {
  element:   'bg-blue-900 text-blue-300',
  attribute: 'bg-purple-900 text-purple-300',
  text:      'bg-gray-700 text-gray-300',
  atomic:    'bg-green-900 text-green-300',
  comment:   'bg-yellow-900 text-yellow-300',
  pi:        'bg-orange-900 text-orange-300',
  node:      'bg-gray-700 text-gray-300',
}

export default function XPathConsolePanel({ xmlContent, onLocateLine, onClose, initialExpression }: Props) {
  const [expression, setExpression] = useState(initialExpression ?? '')
  const [result, setResult] = useState<XPathEvaluateResponse | null>(null)
  const [loading, setLoading] = useState(false)
  const [collapsed, setCollapsed] = useState(false)
  const inputRef = useRef<HTMLInputElement>(null)

  async function handleEvaluate() {
    if (!expression.trim() || !xmlContent || loading) return
    setLoading(true)
    try {
      const res = await evaluateXPath(expression.trim(), xmlContent)
      setResult(res)
    } catch {
      setResult({ kind: 'error', items: [], executionMs: 0, error: 'İstek başarısız oldu.' })
    } finally {
      setLoading(false)
    }
  }

  function handleKeyDown(e: KeyboardEvent<HTMLInputElement>) {
    if (e.key === 'Enter') handleEvaluate()
  }

  function handleLocate(item: XPathResultItem) {
    if (item.line != null && onLocateLine) onLocateLine(item.line, item.column)
  }

  return (
    <div className="border-t border-gray-700 bg-gray-900 flex flex-col flex-shrink-0">
      {/* Header */}
      <div className="h-9 px-3 flex items-center gap-2 border-b border-gray-700 bg-gray-800">
        <button
          onClick={() => setCollapsed(c => !c)}
          className="flex items-center gap-1 text-xs font-medium text-gray-300 hover:text-white uppercase tracking-wide mr-1"
          title={collapsed ? 'Genişlet' : 'Daralt'}
        >
          {collapsed ? <ChevronUp size={14} /> : <ChevronDown size={14} />}
          <Terminal size={13} className="mr-0.5" />
          XPath Konsolu
        </button>

        {result && !loading && (
          <span className="text-xs text-gray-500">
            {result.kind === 'error'
              ? <span className="text-red-400">Hata</span>
              : <span>{result.items.length} sonuç · {result.executionMs} ms</span>
            }
          </span>
        )}

        <div className="flex-1" />

        <button onClick={onClose} className="text-gray-400 hover:text-white" title="Kapat">
          <X size={14} />
        </button>
      </div>

      {/* Body */}
      {!collapsed && (
        <div className="flex flex-col" style={{ maxHeight: '16rem' }}>
          {/* Input */}
          <div className="flex items-center gap-2 px-3 py-2 border-b border-gray-800 flex-shrink-0">
            <span className="text-gray-500 font-mono text-xs select-none">ƒ(x)</span>
            <input
              ref={inputRef}
              value={expression}
              onChange={e => setExpression(e.target.value)}
              onKeyDown={handleKeyDown}
              placeholder="//cbc:InvoiceTypeCode   veya   count(//cac:InvoiceLine)"
              className="flex-1 bg-gray-800 border border-gray-700 rounded px-2 py-1 text-xs text-gray-200 font-mono placeholder-gray-600 focus:outline-none focus:border-blue-500"
              autoFocus
            />
            <button
              onClick={handleEvaluate}
              disabled={!expression.trim() || !xmlContent || loading}
              className="h-7 px-2.5 flex items-center gap-1 rounded bg-blue-600 hover:bg-blue-700 text-white text-xs font-medium disabled:opacity-40 transition-colors flex-shrink-0"
            >
              {loading
                ? <span className="inline-block w-3 h-3 border-2 border-white border-t-transparent rounded-full animate-spin" />
                : <Play size={11} />
              }
              Çalıştır
            </button>
          </div>

          {/* Results */}
          <div className="overflow-y-auto flex-1">
            {!xmlContent && (
              <div className="px-4 py-4 text-xs text-gray-400 flex items-center gap-2">
                <Info size={14} />
                XPath değerlendirmek için önce XML dosyası yükleyin.
              </div>
            )}

            {xmlContent && !result && !loading && (
              <div className="px-4 py-4 text-xs text-gray-500 flex items-center gap-2">
                <Info size={14} />
                XPath ifadesini yazıp Enter'a basın veya Çalıştır'a tıklayın.
              </div>
            )}

            {result?.kind === 'error' && (
              <div className="px-4 py-3 text-xs text-red-400 flex gap-2">
                <AlertCircle size={14} className="flex-shrink-0 mt-0.5" />
                <span className="font-mono break-all">{result.error}</span>
              </div>
            )}

            {result?.kind === 'empty' && (
              <div className="px-4 py-4 text-xs text-gray-400 flex items-center gap-2">
                <Info size={14} />
                Sonuç yok — hiçbir node eşleşmedi.
              </div>
            )}

            {result && result.items.length > 0 && (
              <table className="w-full text-xs">
                <thead className="bg-gray-800 border-b border-gray-700 sticky top-0">
                  <tr>
                    <th className="px-3 py-1 text-left text-gray-400 font-medium w-8">#</th>
                    <th className="px-3 py-1 text-left text-gray-400 font-medium w-20">Tür</th>
                    <th className="px-3 py-1 text-left text-gray-400 font-medium w-48">Ad</th>
                    <th className="px-3 py-1 text-left text-gray-400 font-medium">Değer</th>
                    <th className="px-3 py-1 text-right text-gray-400 font-medium w-20">Satır</th>
                  </tr>
                </thead>
                <tbody className="divide-y divide-gray-800">
                  {result.items.map((item, i) => {
                    const canLocate = item.line != null && !!onLocateLine
                    return (
                      <tr
                        key={i}
                        onClick={() => canLocate && handleLocate(item)}
                        className={canLocate ? 'cursor-pointer hover:bg-gray-800' : ''}
                      >
                        <td className="px-3 py-1.5 text-gray-500">{i + 1}</td>
                        <td className="px-3 py-1.5">
                          <span className={`inline-block px-1.5 py-0.5 rounded text-[10px] font-medium ${KIND_BADGE[item.kind] ?? 'bg-gray-700 text-gray-300'}`}>
                            {item.kind}
                          </span>
                        </td>
                        <td className="px-3 py-1.5 font-mono text-blue-300 max-w-[12rem] overflow-hidden text-ellipsis whitespace-nowrap">
                          {item.name || '—'}
                        </td>
                        <td className="px-3 py-1.5 text-gray-200 font-mono break-all">
                          {item.value || <span className="text-gray-600 italic">boş</span>}
                        </td>
                        <td className="px-3 py-1.5 text-right text-gray-500">
                          {item.line != null
                            ? `${item.line}${item.column ? `:${item.column}` : ''}`
                            : '—'}
                        </td>
                      </tr>
                    )
                  })}
                </tbody>
              </table>
            )}
          </div>
        </div>
      )}
    </div>
  )
}
