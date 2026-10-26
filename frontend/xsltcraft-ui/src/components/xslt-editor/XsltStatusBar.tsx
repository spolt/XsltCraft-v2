import { useEffect, useRef, useState } from 'react'
import { Timer } from 'lucide-react'
import type { PreviewTimings, XsltEngineName } from '../../services/previewService'

interface Props {
  previewLoading: boolean
  lastMs: number | null
  lastTimings: PreviewTimings | null
  engine: XsltEngineName | null
  errorCount: number
  warningCount: number
  onToggleProblems: () => void
  cursorLine?: number
  cursorColumn?: number
  /** Stylesheet kök öğesindeki version (ör. "2.0"); bilinmiyorsa gösterilmez. */
  xsltVersion?: string | null
  /** Belgenin satır sonu: LF | CRLF. */
  eol: 'LF' | 'CRLF'
  layoutLabel: string
}

const ENGINE_LABEL: Record<XsltEngineName, string> = {
  Saxon: 'Saxon-HE',
  DotNet: '.NET XslCompiledTransform',
}

function TimingPopover({ timings, onClose }: { timings: PreviewTimings; onClose: () => void }) {
  const ref = useRef<HTMLDivElement>(null)

  useEffect(() => {
    function handleClick(e: MouseEvent) {
      if (ref.current && !ref.current.contains(e.target as Node)) onClose()
    }
    function handleKey(e: KeyboardEvent) {
      if (e.key === 'Escape') onClose()
    }
    document.addEventListener('mousedown', handleClick)
    document.addEventListener('keydown', handleKey)
    return () => {
      document.removeEventListener('mousedown', handleClick)
      document.removeEventListener('keydown', handleKey)
    }
  }, [onClose])

  const rows: [string, number][] = [
    ['XSLT Derleme', timings.compileMs],
    ['XML Ayrıştırma', timings.parseMs],
    ['Dönüşüm', timings.transformMs],
    ['Serileştirme', timings.serializeMs],
  ]
  const total = rows.reduce((s, [, v]) => s + v, 0)

  return (
    <div
      ref={ref}
      role="dialog"
      aria-label="Önizleme süresi kırılımı"
      className="absolute bottom-full left-0 z-50 mb-1.5 min-w-[210px] rounded-lg border border-ui-line bg-ui-card p-3 shadow-[var(--ui-shadow-pop)]"
    >
      <p className="mb-2 text-[10px] font-semibold uppercase tracking-wide text-tx-subtle">Önizleme Süresi</p>
      <table className="w-full text-xs">
        <tbody>
          {rows.map(([label, ms]) => (
            <tr key={label}>
              <td className="py-0.5 pr-4 text-tx-2">{label}</td>
              <td className="py-0.5 text-right font-mono text-tx">{ms} ms</td>
            </tr>
          ))}
          <tr className="border-t border-ui-line">
            <td className="pt-1.5 font-medium text-tx-muted">Toplam</td>
            <td className="pt-1.5 text-right font-mono font-semibold text-tx">{total} ms</td>
          </tr>
        </tbody>
      </table>
    </div>
  )
}

/** Alt durum çubuğu: motor · XSLT sürümü · süre, hata/uyarı sayacı, imleç konumu, kodlama · satır sonu, düzen. */
export default function XsltStatusBar({
  previewLoading,
  lastMs,
  lastTimings,
  engine,
  errorCount,
  warningCount,
  onToggleProblems,
  cursorLine,
  cursorColumn,
  xsltVersion,
  eol,
  layoutLabel,
}: Props) {
  const [showTiming, setShowTiming] = useState(false)

  return (
    <footer className="flex h-[24px] flex-none items-center gap-x-4 whitespace-nowrap border-t border-ui-line bg-ui-side px-3 text-[11.5px] text-tx-muted">
      <div className="relative flex items-center gap-1.5">
        {previewLoading ? (
          <span role="status" className="flex items-center gap-1.5">
            <span aria-hidden="true" className="inline-block h-3 w-3 animate-spin rounded-full border-2 border-ui-accent border-t-transparent motion-reduce:animate-none" />
            Derleniyor
          </span>
        ) : lastMs !== null ? (
          <button
            type="button"
            onClick={() => lastTimings && setShowTiming(s => !s)}
            disabled={!lastTimings}
            aria-expanded={lastTimings ? showTiming : undefined}
            className="xc-focus flex h-5 items-center gap-1.5 rounded px-1 enabled:hover:bg-ui-hover enabled:hover:text-tx disabled:cursor-default"
            title={lastTimings ? 'Ayrıntılı süre kırılımı' : undefined}
          >
            <span aria-hidden="true" className="h-[7px] w-[7px] rounded-full bg-fn-green" />
            {engine && <span>{ENGINE_LABEL[engine]} ·</span>}
            {xsltVersion && <span>XSLT {xsltVersion} ·</span>}
            {lastTimings && <Timer size={12} aria-hidden="true" />}
            {lastMs} ms
          </button>
        ) : (
          <span>Önizleme bekleniyor</span>
        )}
        {showTiming && lastTimings && (
          <TimingPopover timings={lastTimings} onClose={() => setShowTiming(false)} />
        )}
      </div>

      <button
        type="button"
        onClick={onToggleProblems}
        className="xc-focus h-5 rounded px-1 hover:bg-ui-hover"
        title="Problemler panelini aç/kapat"
      >
        <b className={errorCount > 0 ? 'text-fn-red-ink' : 'text-fn-green-ink'}>{errorCount} hata</b>
        {' · '}
        <b className={warningCount > 0 ? 'text-fn-orange-ink' : 'text-tx-muted'}>{warningCount} uyarı</b>
      </button>

      {cursorLine != null && <span>Satır {cursorLine}{cursorColumn != null && `, Sütun ${cursorColumn}`}</span>}
      <span>UTF-8 · {eol}</span>
      <span className="ml-auto">Düzen: {layoutLabel}</span>
    </footer>
  )
}
