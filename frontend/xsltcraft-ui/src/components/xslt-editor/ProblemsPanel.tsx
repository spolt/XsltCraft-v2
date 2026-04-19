import { useState } from 'react'
import { AlertCircle, AlertTriangle, Info, ChevronDown, ChevronUp, X } from 'lucide-react'

export type ProblemSeverity = 'error' | 'warning' | 'info'
export type ProblemSource = 'xslt' | 'xml' | 'ubl-tr'

export interface ProblemItem {
  source: ProblemSource
  ruleId?: string
  ruleName?: string
  severity: ProblemSeverity
  message: string
  line?: number | null
  column?: number | null
}

type Filter = 'all' | ProblemSeverity

interface Props {
  xsltProblems: ProblemItem[]
  xmlProblems: ProblemItem[]
  ublTrProblems: ProblemItem[] | null  // null => hiç çalıştırılmadı
  ublTrLoading?: boolean
  ublTrError?: string | null
  onLocateXslt?: (line: number, column?: number | null) => void
  onLocateXml?: (line: number, column?: number | null) => void
  onClose: () => void
}

const SEVERITY_CONFIG: Record<ProblemSeverity, { color: string; Icon: typeof AlertCircle }> = {
  error: { color: 'text-red-400', Icon: AlertCircle },
  warning: { color: 'text-amber-400', Icon: AlertTriangle },
  info: { color: 'text-blue-400', Icon: Info },
}

const TAB_LABELS: Record<ProblemSource, string> = {
  xslt: 'XSLT',
  xml: 'XML',
  'ubl-tr': 'UBL-TR',
}

export default function ProblemsPanel({
  xsltProblems, xmlProblems, ublTrProblems, ublTrLoading, ublTrError,
  onLocateXslt, onLocateXml, onClose,
}: Props) {
  const [collapsed, setCollapsed] = useState(false)
  const [activeTab, setActiveTab] = useState<ProblemSource>('xslt')
  const [filter, setFilter] = useState<Filter>('all')

  const problemsBySource: Record<ProblemSource, ProblemItem[]> = {
    xslt: xsltProblems,
    xml: xmlProblems,
    'ubl-tr': ublTrProblems ?? [],
  }

  // Auto-switch: if XSLT has errors, prefer that; else XML; else UBL-TR
  // (Only on first render; keeps user's tab choice after)
  // handled by default useState('xslt')

  const activeList = problemsBySource[activeTab]

  const counts: Record<ProblemSource, { total: number; error: number; warning: number; info: number }> = {
    xslt: { total: 0, error: 0, warning: 0, info: 0 },
    xml: { total: 0, error: 0, warning: 0, info: 0 },
    'ubl-tr': { total: 0, error: 0, warning: 0, info: 0 },
  }
  for (const src of ['xslt', 'xml', 'ubl-tr'] as const) {
    const list = problemsBySource[src]
    counts[src].total = list.length
    list.forEach(p => { counts[src][p.severity] += 1 })
  }

  const filtered = filter === 'all' ? activeList : activeList.filter(p => p.severity === filter)

  const activeCounts = counts[activeTab]

  function handleLocate(item: ProblemItem) {
    if (item.line == null) return
    if (item.source === 'xslt') onLocateXslt?.(item.line, item.column)
    else if (item.source === 'xml' || item.source === 'ubl-tr') onLocateXml?.(item.line, item.column)
  }

  const canLocate = (item: ProblemItem) =>
    item.line != null &&
    ((item.source === 'xslt' && !!onLocateXslt) ||
      ((item.source === 'xml' || item.source === 'ubl-tr') && !!onLocateXml))

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
          Problemler
        </button>

        {/* Source tabs */}
        <div className="flex items-center gap-0.5 text-xs">
          {(['xslt', 'xml', 'ubl-tr'] as const).map(src => {
            const c = counts[src]
            const isActive = activeTab === src
            const hasError = c.error > 0
            return (
              <button
                key={src}
                onClick={() => { setActiveTab(src); setFilter('all') }}
                className={`px-2.5 py-1 rounded flex items-center gap-1.5 transition-colors ${
                  isActive
                    ? 'bg-gray-900 text-white border border-gray-600'
                    : 'text-gray-400 hover:bg-gray-700'
                }`}
              >
                <span>{TAB_LABELS[src]}</span>
                {c.total > 0 && (
                  <span className={`inline-flex items-center justify-center min-w-[16px] h-[16px] px-1 rounded text-[10px] font-semibold ${
                    hasError ? 'bg-red-500 text-white' : 'bg-gray-700 text-gray-200'
                  }`}>
                    {c.total}
                  </span>
                )}
              </button>
            )
          })}
        </div>

        {/* Severity filters (scoped to active tab) */}
        {!collapsed && activeCounts.total > 0 && (
          <div className="flex items-center gap-0.5 text-xs ml-2 border-l border-gray-700 pl-2">
            <FilterPill active={filter === 'all'} onClick={() => setFilter('all')} label={`Tümü ${activeCounts.total}`} />
            {activeCounts.error > 0 && (
              <FilterPill active={filter === 'error'} onClick={() => setFilter('error')}
                color="text-red-400" label={`${activeCounts.error}`} icon={<AlertCircle size={11} />} />
            )}
            {activeCounts.warning > 0 && (
              <FilterPill active={filter === 'warning'} onClick={() => setFilter('warning')}
                color="text-amber-400" label={`${activeCounts.warning}`} icon={<AlertTriangle size={11} />} />
            )}
            {activeCounts.info > 0 && (
              <FilterPill active={filter === 'info'} onClick={() => setFilter('info')}
                color="text-blue-400" label={`${activeCounts.info}`} icon={<Info size={11} />} />
            )}
          </div>
        )}

        <div className="flex-1" />

        {activeTab === 'ubl-tr' && ublTrLoading && (
          <span className="text-xs text-blue-400">Doğrulanıyor…</span>
        )}

        <button onClick={onClose} className="text-gray-400 hover:text-white" title="Paneli kapat">
          <X size={14} />
        </button>
      </div>

      {/* Body */}
      {!collapsed && (
        <div className="max-h-56 overflow-y-auto">
          {activeTab === 'ubl-tr' && ublTrError && (
            <div className="px-4 py-3 text-xs text-red-400 font-mono">{ublTrError}</div>
          )}

          {activeTab === 'ubl-tr' && !ublTrError && ublTrProblems === null && !ublTrLoading && (
            <div className="px-4 py-4 text-xs text-gray-400 flex items-center gap-2">
              <Info size={14} />
              UBL-TR iş kurallarını çalıştırmak için toolbar'daki "UBL-TR" butonuna tıklayın.
            </div>
          )}

          {!ublTrError && activeCounts.total === 0 && !(activeTab === 'ubl-tr' && ublTrProblems === null) && (
            <div className="px-4 py-4 text-xs text-green-400 flex items-center gap-2">
              <Info size={14} />
              {emptyMessage(activeTab)}
            </div>
          )}

          {filtered.length > 0 && (
            <ul className="divide-y divide-gray-800">
              {filtered.map((p, i) => {
                const cfg = SEVERITY_CONFIG[p.severity]
                const clickable = canLocate(p)
                return (
                  <li
                    key={`${p.source}-${p.ruleId ?? 'x'}-${i}`}
                    onClick={() => clickable && handleLocate(p)}
                    className={`px-4 py-2 flex items-start gap-3 text-xs ${
                      clickable ? 'cursor-pointer hover:bg-gray-800' : ''
                    }`}
                  >
                    <cfg.Icon size={14} className={`flex-shrink-0 mt-0.5 ${cfg.color}`} />
                    <div className="flex-1 min-w-0">
                      <div className="flex items-center gap-2 flex-wrap">
                        {p.ruleId && <span className="font-mono text-gray-500">{p.ruleId}</span>}
                        {p.ruleName && <span className="font-medium text-gray-200">{p.ruleName}</span>}
                        {p.line != null && (
                          <span className="text-gray-500">
                            · satır {p.line}{p.column ? `:${p.column}` : ''}
                          </span>
                        )}
                      </div>
                      <div className="text-gray-300 mt-0.5 break-words">{p.message}</div>
                    </div>
                  </li>
                )
              })}
            </ul>
          )}
        </div>
      )}
    </div>
  )
}

function emptyMessage(source: ProblemSource): string {
  switch (source) {
    case 'xslt': return 'XSLT sözdizimi geçerli.'
    case 'xml': return 'XML dosyası well-formed.'
    case 'ubl-tr': return 'Tüm UBL-TR iş kuralları geçti.'
  }
}

function FilterPill({
  active, onClick, label, color, icon,
}: { active: boolean; onClick: () => void; label: string; color?: string; icon?: React.ReactNode }) {
  return (
    <button
      onClick={onClick}
      className={`px-1.5 py-0.5 rounded flex items-center gap-1 transition-colors ${
        active
          ? 'bg-gray-700 text-white'
          : `bg-transparent ${color ?? 'text-gray-400'} hover:bg-gray-700`
      }`}
    >
      {icon}
      {label}
    </button>
  )
}
