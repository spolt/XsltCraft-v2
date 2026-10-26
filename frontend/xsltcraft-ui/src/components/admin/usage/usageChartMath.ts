import type { UsageDailyPoint } from '../../../services/usageReportService'

/* Kullanım raporu grafiği için saf hesaplar (SVG viewBox 1000×260). */

export type MetricKey = 'tokensUsed' | 'aiRequests' | 'saveCount' | 'downloadCount' | 'templateExports'

export interface MetricDef {
  key: MetricKey
  label: string
  /** "Günlük …" başlığında kullanılır. */
  title: string
  /** CSS rengi (theme.css --color-metric-N). */
  color: string
}

export const METRICS: readonly MetricDef[] = [
  { key: 'tokensUsed', label: 'Token', title: 'token kullanımı', color: 'var(--color-metric-1)' },
  { key: 'aiRequests', label: 'AI Sorusu', title: 'AI soruları', color: 'var(--color-metric-2)' },
  { key: 'saveCount', label: 'Kaydetme', title: 'kaydetmeler', color: 'var(--color-metric-3)' },
  { key: 'downloadCount', label: 'İndirme', title: 'indirmeler', color: 'var(--color-metric-4)' },
  { key: 'templateExports', label: 'Üretim İnd.', title: 'üretim indirmeleri', color: 'var(--color-metric-5)' },
]

/** Metrik renginin yarı saydam zemini (seçili kart / tablo sütunu). */
export const metricTint = (color: string, pct = 8) => `color-mix(in srgb, ${color} ${pct}%, transparent)`

export const fmtNum = (v: number) => Math.round(v).toLocaleString('tr-TR')

/** Yüzde değişim; önceki değer 0 ise karşılaştırma anlamsız → null. */
export function pctChange(cur: number, prev: number): number | null {
  if (!prev) return null
  return ((cur - prev) / prev) * 100
}

export function fmtPct(d: number | null): string {
  if (d === null) return '—'
  const abs = Math.abs(d).toLocaleString('tr-TR', { maximumFractionDigits: 1 })
  return (d >= 0 ? '+%' : '−%') + abs
}

/* ---------- Tarihler (UTC, yyyy-mm-dd) ---------- */

export function utcDate(offsetDays = 0): string {
  const d = new Date()
  d.setUTCDate(d.getUTCDate() + offsetDays)
  return d.toISOString().slice(0, 10)
}

const parseIso = (iso: string) => new Date(`${iso.slice(0, 10)}T00:00:00Z`)

export function addDays(iso: string, days: number): string {
  const d = parseIso(iso)
  d.setUTCDate(d.getUTCDate() + days)
  return d.toISOString().slice(0, 10)
}

/** from..to dahil gün sayısı (en az 1). */
export function dayCount(from: string, to: string): number {
  const diff = Math.round((parseIso(to).getTime() - parseIso(from).getTime()) / 86_400_000)
  return Math.max(1, Math.abs(diff) + 1)
}

/** Aynı uzunlukta, hemen önceki aralık. */
export function previousRange(from: string, to: string): { from: string; to: string } {
  const n = dayCount(from, to)
  const prevTo = addDays(from, -1)
  return { from: addDays(prevTo, -(n - 1)), to: prevTo }
}

export const shortDate = (iso: string) =>
  parseIso(iso).toLocaleDateString('tr-TR', { day: 'numeric', month: 'short', timeZone: 'UTC' })
export const longDate = (iso: string) =>
  parseIso(iso).toLocaleDateString('tr-TR', { day: 'numeric', month: 'long', weekday: 'long', timeZone: 'UTC' })

/** Seyrek günlük noktaları from..to aralığında sıfırla doldurur (backend yalnız veri olan günleri döner). */
export function fillDaily(points: readonly UsageDailyPoint[], from: string, to: string): UsageDailyPoint[] {
  const byDate = new Map(points.map((p) => [p.date.slice(0, 10), p]))
  const n = dayCount(from, to)
  const out: UsageDailyPoint[] = []
  for (let i = 0; i < n; i++) {
    const date = addDays(from, i)
    out.push(
      byDate.get(date) ?? { date, tokensUsed: 0, aiRequests: 0, templateExports: 0, saveCount: 0, downloadCount: 0 },
    )
  }
  return out
}

export const seriesOf = (points: readonly UsageDailyPoint[], key: MetricKey) => points.map((p) => p[key] ?? 0)
export const sum = (a: readonly number[]) => a.reduce((x, y) => x + y, 0)

/* ---------- Grafik geometrisi ---------- */

export const VB_W = 1000
export const VB_H = 260
const PLOT = 248

/** 4 aralıklı "güzel" üst sınır (1/2/2.5/5/10 × 10^k). */
export function niceMax(v: number): number {
  const raw = Math.max(v, 1) / 4
  const mag = Math.pow(10, Math.floor(Math.log10(raw)))
  const r = raw / mag
  const s = r <= 1 ? 1 : r <= 2 ? 2 : r <= 2.5 ? 2.5 : r <= 5 ? 5 : 10
  return Math.max(1, s * mag) * 4
}

export interface ChartGeometry {
  n: number
  yMax: number
  xOf: (i: number) => number
  yOf: (v: number) => number
  /** Yatay konum, kapsayıcının yüzdesi olarak. */
  pctX: (i: number) => string
  pctY: (v: number) => string
  line: string
  area: string
  prev: string
  avg: number
  avgPath: string
  peakIdx: number
  yTicks: { value: number; top: string }[]
  xTickIdx: number[]
}

export function buildChart(cur: readonly number[], prev: readonly number[], includePrev: boolean): ChartGeometry {
  const n = Math.max(cur.length, 1)
  const values = cur.length ? cur : [0]
  const yMax = niceMax(Math.max(0, ...values, ...(includePrev ? prev : [])))
  const xOf = (i: number) => (n === 1 ? VB_W / 2 : (i / (n - 1)) * VB_W)
  const yOf = (v: number) => VB_H - (v / yMax) * PLOT
  const pathOf = (arr: readonly number[]) =>
    arr.map((v, i) => `${i ? 'L' : 'M'}${xOf(i).toFixed(1)},${yOf(v).toFixed(1)}`).join(' ')
  const line = pathOf(values)
  const total = sum(values)
  const avg = total / n
  const peakIdx = values.indexOf(Math.max(...values))
  const tickStep = n <= 7 ? 1 : n <= 14 ? 2 : Math.ceil((n - 1) / 6)
  const xTickIdx: number[] = []
  for (let i = 0; i < n; i++) if ((n - 1 - i) % tickStep === 0) xTickIdx.push(i)
  return {
    n,
    yMax,
    xOf,
    yOf,
    pctX: (i) => `${(xOf(i) / 10).toFixed(2)}%`,
    pctY: (v) => `${((yOf(v) / VB_H) * 100).toFixed(2)}%`,
    line,
    area: `${line} L${xOf(n - 1).toFixed(1)},${VB_H} L${xOf(0).toFixed(1)},${VB_H} Z`,
    prev: prev.length ? pathOf(prev.slice(0, n)) : '',
    avg,
    avgPath: `M0,${yOf(avg).toFixed(1)} L${VB_W},${yOf(avg).toFixed(1)}`,
    peakIdx,
    yTicks: [0, 1, 2, 3, 4].map((j) => {
      const value = (yMax / 4) * j
      return { value, top: `${((yOf(value) / VB_H) * 100).toFixed(2)}%` }
    }),
    xTickIdx,
  }
}

/** Kart sparkline'ı (viewBox 100×32). */
export function sparkPaths(values: readonly number[]): { line: string; area: string } {
  const n = values.length
  if (n === 0) return { line: '', area: '' }
  const mx = Math.max(...values) || 1
  const sx = (i: number) => (n === 1 ? 50 : (i / (n - 1)) * 100).toFixed(2)
  const sy = (v: number) => (30 - (v / mx) * 26).toFixed(2)
  const line = values.map((v, i) => `${i ? 'L' : 'M'}${sx(i)},${sy(v)}`).join(' ')
  return { line, area: `${line} L${sx(n - 1)},32 L${sx(0)},32 Z` }
}
