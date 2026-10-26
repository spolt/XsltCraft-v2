import { fmtNum, fmtPct, longDate, pctChange, shortDate, type MetricDef, type MetricKey } from './usageChartMath'
import type { UsageDailyPoint } from '../../../services/usageReportService'

interface ChartTooltipProps {
  point: UsageDailyPoint
  prevPoint: UsageDailyPoint | undefined
  metrics: readonly MetricDef[]
  selected: MetricDef
  compare: boolean
  left: string
  /** Sağa yakın günlerde ipucu solda açılır. */
  flip: boolean
}

/** Gün sütunu üzerine gelince: o günün 5 metriği + (karşılaştırma açıksa) önceki dönem ve fark. */
export default function ChartTooltip({ point, prevPoint, metrics, selected, compare, left, flip }: ChartTooltipProps) {
  const cur = point[selected.key as MetricKey] ?? 0
  const prev = prevPoint ? prevPoint[selected.key] ?? 0 : 0
  return (
    <div
      aria-hidden="true"
      style={{ left, transform: flip ? 'translateX(calc(-100% - 16px))' : 'translateX(16px)' }}
      className="pointer-events-none absolute top-1.5 z-[2] flex w-[236px] flex-col gap-1.5 rounded-[10px] bg-tx px-3.5 py-3 text-white shadow-[var(--ui-shadow-pop)]"
    >
      <span className="text-[12px] font-semibold text-white/75">{longDate(point.date)}</span>
      {metrics.map((m) => {
        const on = m.key === selected.key
        return (
          <span key={m.key} className={`flex items-center gap-2 text-[12.5px] ${on ? 'font-bold' : 'font-normal'}`}>
            <span className="h-[9px] w-[9px] flex-none rounded-[3px]" style={{ background: m.color }} />
            <span className={`flex-1 ${on ? 'text-white' : 'text-white/75'}`}>{m.label}</span>
            <span className="tabular-nums">{fmtNum(point[m.key] ?? 0)}</span>
          </span>
        )
      })}
      {compare && prevPoint && (
        <span className="mt-0.5 border-t border-white/20 pt-2 text-[11.5px] text-white/75">
          Önceki dönem ({shortDate(prevPoint.date)}): <b className="text-white">{fmtNum(prev)}</b> ·{' '}
          {fmtPct(pctChange(cur, prev))} {selected.label.toLocaleLowerCase('tr-TR')}
        </span>
      )}
    </div>
  )
}
