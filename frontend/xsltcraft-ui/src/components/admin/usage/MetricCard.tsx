import { ArrowDown, ArrowUp, Minus } from 'lucide-react'
import Sparkline from './Sparkline'
import { fmtNum, fmtPct, metricTint, type MetricDef } from './usageChartMath'

interface MetricCardProps {
  metric: MetricDef
  total: number
  /** Önceki döneme göre % değişim; null → karşılaştırılamaz. */
  delta: number | null
  series: readonly number[]
  selected: boolean
  onSelect: () => void
}

/** Seçilebilir metrik kartı: toplam, önceki döneme göre değişim, sparkline. */
export default function MetricCard({ metric, total, delta, series, selected, onSelect }: MetricCardProps) {
  const Arrow = delta === null ? Minus : delta >= 0 ? ArrowUp : ArrowDown
  return (
    <button
      type="button"
      aria-pressed={selected}
      onClick={onSelect}
      style={{
        borderColor: selected ? metric.color : undefined,
        background: selected ? metricTint(metric.color) : undefined,
      }}
      className="xc-focus xc-lift relative flex flex-col gap-1.5 overflow-hidden rounded-xl border border-ui-line bg-ui-card px-4 pt-4 pb-2.5 text-left text-tx"
    >
      <span
        aria-hidden="true"
        className="absolute inset-x-0 top-0 h-[3px]"
        style={{ background: selected ? metric.color : 'transparent' }}
      />
      <span className="flex items-center gap-2 text-[12.5px] font-semibold text-tx-2">
        <span aria-hidden="true" className="h-2.5 w-2.5 rounded-[3px]" style={{ background: metric.color }} />
        {metric.label}
      </span>
      <span className="text-[24px] leading-[1.1] font-bold tabular-nums">{fmtNum(total)}</span>
      <span className="flex items-center gap-1 text-[12px] text-tx-2">
        <Arrow size={12} strokeWidth={2.6} aria-hidden="true" />
        <b className="tabular-nums">{fmtPct(delta)}</b>
        <span className="text-tx-muted">{delta === null ? 'önceki dönemde veri yok' : 'önceki döneme göre'}</span>
      </span>
      <Sparkline values={series} color={metric.color} />
    </button>
  )
}
