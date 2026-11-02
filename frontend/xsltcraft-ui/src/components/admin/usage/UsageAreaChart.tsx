import { useMemo, useState } from 'react'
import type { UsageDailyPoint } from '../../../services/usageReportService'
import ChartTooltip from './ChartTooltip'
import {
  buildChart,
  fmtNum,
  seriesOf,
  shortDate,
  sum,
  VB_H,
  VB_W,
  type MetricDef,
} from './usageChartMath'

interface UsageAreaChartProps {
  /** Seçili aralığın sıfırla doldurulmuş günlük noktaları. */
  points: readonly UsageDailyPoint[]
  /** Önceki dönemin aynı uzunluktaki noktaları. */
  prevPoints: readonly UsageDailyPoint[]
  metrics: readonly MetricDef[]
  selected: MetricDef
  compare: boolean
}

const PREV_COLOR = 'var(--color-fn-gray)'

/** Tek metriğin günlük alan grafiği (tek y ekseni). Hover → o günün tüm metrikleri. */
export default function UsageAreaChart({ points, prevPoints, metrics, selected, compare }: UsageAreaChartProps) {
  const [hov, setHov] = useState(-1)
  const cur = useMemo(() => seriesOf(points, selected.key), [points, selected.key])
  const prev = useMemo(() => seriesOf(prevPoints, selected.key), [prevPoints, selected.key])
  const g = useMemo(() => buildChart(cur, prev, compare), [cur, prev, compare])

  const n = points.length
  const total = sum(cur)
  const hasData = total > 0
  const peakVal = cur[g.peakIdx] ?? 0
  const peakRel = n <= 1 ? 0.5 : g.peakIdx / (n - 1)
  const peakShift =
    peakRel > 0.8
      ? 'translate(calc(-100% - 10px), -50%)'
      : peakRel < 0.2
        ? 'translate(10px, -50%)'
        : 'translate(-50%, calc(-100% - 12px))'
  const colW = 100 / Math.max(n - 1, 1)
  const first = points[0]?.date
  const last = points[n - 1]?.date
  const ariaLabel =
    n && first && last
      ? `Günlük ${selected.title}, ${shortDate(first)} – ${shortDate(last)}. Toplam ${fmtNum(total)}, günlük ortalama ${fmtNum(g.avg)}` +
        (hasData ? `, en yüksek ${fmtNum(peakVal)} (${shortDate(points[g.peakIdx].date)})` : '') +
        '. Ayrıntılar tabloda.'
      : `Günlük ${selected.title}: veri yok.`

  const hp = hov >= 0 && hov < n ? hov : -1
  const hovRel = n <= 1 ? 0.5 : hp / (n - 1)

  return (
    <div className="flex gap-2.5">
      <div aria-hidden="true" className="relative h-[260px] w-[64px] flex-none">
        {g.yTicks.map((t) => (
          <span
            key={t.value}
            className="absolute right-0 -translate-y-1/2 text-[11px] whitespace-nowrap text-tx-subtle tabular-nums"
            style={{ top: t.top }}
          >
            {fmtNum(t.value)}
          </span>
        ))}
      </div>
      <div className="flex min-w-0 flex-1 flex-col gap-1.5">
        <div role="img" aria-label={ariaLabel} className="relative h-[260px]" onMouseLeave={() => setHov(-1)}>
          {g.yTicks.map((t, j) => (
            <span
              key={t.value}
              aria-hidden="true"
              className="absolute inset-x-0 h-px"
              style={{ top: t.top, background: j === 0 ? 'var(--color-fn-gray-line)' : 'var(--ui-line)' }}
            />
          ))}
          <svg
            aria-hidden="true"
            viewBox={`0 0 ${VB_W} ${VB_H}`}
            preserveAspectRatio="none"
            className="absolute inset-0 h-full w-full overflow-visible"
          >
            <path d={g.area} fill={selected.color} fillOpacity={0.13} />
            {compare && g.prev && (
              <path
                d={g.prev}
                fill="none"
                stroke={PREV_COLOR}
                strokeWidth={1.8}
                strokeDasharray="5 5"
                vectorEffect="non-scaling-stroke"
              />
            )}
            {hasData && (
              <path
                d={g.avgPath}
                fill="none"
                stroke={selected.color}
                strokeOpacity={0.7}
                strokeWidth={1.6}
                strokeDasharray="1.5 4"
                strokeLinecap="round"
                vectorEffect="non-scaling-stroke"
              />
            )}
            <path
              d={g.line}
              fill="none"
              stroke={selected.color}
              strokeWidth={2.6}
              strokeLinejoin="round"
              strokeLinecap="round"
              vectorEffect="non-scaling-stroke"
            />
          </svg>

          {hasData ? (
            <>
              <span
                aria-hidden="true"
                className="absolute right-0 rounded bg-ui-card px-1.5 py-px text-[11px] font-semibold text-tx-2"
                style={{ top: g.pctY(g.avg), transform: 'translateY(-120%)' }}
              >
                Ort. {fmtNum(g.avg)}
              </span>
              <span
                aria-hidden="true"
                className="absolute h-2.5 w-2.5 -translate-x-1/2 -translate-y-1/2 rounded-full"
                style={{
                  left: g.pctX(g.peakIdx),
                  top: g.pctY(peakVal),
                  background: selected.color,
                  boxShadow: '0 0 0 2.5px var(--ui-card)',
                }}
              />
              <span
                aria-hidden="true"
                className="absolute rounded-md border border-ui-line bg-ui-card px-2 py-0.5 text-[11px] font-semibold whitespace-nowrap text-tx"
                style={{ left: g.pctX(g.peakIdx), top: g.pctY(peakVal), transform: peakShift }}
              >
                En yüksek · {fmtNum(peakVal)}
              </span>
            </>
          ) : (
            <span aria-hidden="true" className="absolute inset-x-0 top-[40%] text-center text-[12.5px] text-tx-muted">
              Bu aralıkta {selected.title} yok.
            </span>
          )}

          {hp >= 0 && (
            <>
              <span
                aria-hidden="true"
                className="pointer-events-none absolute inset-y-0 w-px"
                style={{ left: g.pctX(hp), background: PREV_COLOR }}
              />
              {compare && prev[hp] !== undefined && (
                <span
                  aria-hidden="true"
                  className="pointer-events-none absolute h-2 w-2 -translate-x-1/2 -translate-y-1/2 rounded-full border-2 bg-ui-card"
                  style={{ left: g.pctX(hp), top: g.pctY(prev[hp]), borderColor: PREV_COLOR }}
                />
              )}
              <span
                aria-hidden="true"
                className="pointer-events-none absolute h-3 w-3 -translate-x-1/2 -translate-y-1/2 rounded-full"
                style={{
                  left: g.pctX(hp),
                  top: g.pctY(cur[hp]),
                  background: selected.color,
                  boxShadow: '0 0 0 3px var(--ui-card), var(--ui-shadow-rest)',
                }}
              />
              <ChartTooltip
                point={points[hp]}
                prevPoint={prevPoints[hp]}
                metrics={metrics}
                selected={selected}
                compare={compare}
                left={g.pctX(hp)}
                flip={hovRel > 0.6}
              />
            </>
          )}

          {points.map((p, i) => (
            <span
              key={p.date}
              aria-hidden="true"
              onMouseEnter={() => setHov(i)}
              className="absolute inset-y-0 z-[3]"
              style={
                n === 1
                  ? { left: 0, width: '100%' }
                  : { left: `${(i * colW - colW / 2).toFixed(3)}%`, width: `${colW.toFixed(3)}%` }
              }
            />
          ))}
        </div>
        <div aria-hidden="true" className="relative h-[18px]">
          {g.xTickIdx.map((i) =>
            points[i] ? (
              <span
                key={i}
                className="absolute -translate-x-1/2 text-[11px] whitespace-nowrap text-tx-subtle"
                style={{ left: g.pctX(i) }}
              >
                {shortDate(points[i].date)}
              </span>
            ) : null,
          )}
        </div>
      </div>
    </div>
  )
}
