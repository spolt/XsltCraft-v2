import { useCallback, useEffect, useMemo, useRef, useState } from 'react'
import { ChevronDown, Download, Loader2, RefreshCw } from 'lucide-react'
import {
  getUsageReport,
  getUsageDaily,
  type UsageReport,
  type UsageDailyPoint,
} from '../../services/usageReportService'
import Card from '../../components/ui/Card'
import PageHeader from '../../components/ui/PageHeader'
import {
  AdminPage, Banner, BTN_SECONDARY, INPUT, SectionHeader, TABLE, TD, TH, TR,
} from '../../components/admin/adminUi'
import MetricCard from '../../components/admin/usage/MetricCard'
import UsageAreaChart from '../../components/admin/usage/UsageAreaChart'
import {
  METRICS, dayCount, fillDaily, fmtNum, metricTint, pctChange, previousRange, seriesOf, shortDate, sum, utcDate,
  type MetricKey,
} from '../../components/admin/usage/usageChartMath'

const PRESETS = [7, 14, 30] as const

function downloadCsv(report: UsageReport) {
  const headers = ['Kullanıcı', 'E-posta', 'Token', 'AI Sorusu', 'Kaydetme', 'İndirme', 'Üretim İndirme']
  const escape = (v: string | number) => {
    const s = String(v)
    return /[",\n;]/.test(s) ? `"${s.replace(/"/g, '""')}"` : s
  }
  const lines = [
    headers.join(';'),
    ...report.rows.map(r =>
      [r.username ?? '', r.email, r.tokensUsed, r.aiRequests, r.saveCount, r.downloadCount, r.templateExports]
        .map(escape).join(';')
    ),
  ]
  // BOM → Excel Türkçe karakterleri doğru gösterir.
  const blob = new Blob(['﻿' + lines.join('\n')], { type: 'text/csv;charset=utf-8' })
  const a = document.createElement('a')
  a.href = URL.createObjectURL(blob)
  a.download = `kullanim-raporu_${report.from}_${report.to}.csv`
  a.click()
  URL.revokeObjectURL(a.href)
}

export default function AdminUsagePage() {
  const [from, setFrom] = useState(utcDate(-29))
  const [to, setTo] = useState(utcDate(0))
  const [customOpen, setCustomOpen] = useState(false)
  const [metricKey, setMetricKey] = useState<MetricKey>('tokensUsed')
  const [compare, setCompare] = useState(true)
  const [showDaily, setShowDaily] = useState(false)

  const [report, setReport] = useState<UsageReport | null>(null)
  const [daily, setDaily] = useState<UsageDailyPoint[]>([])
  const [prevDaily, setPrevDaily] = useState<UsageDailyPoint[]>([])
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)
  const reqId = useRef(0)

  const load = useCallback(async (f: string, t: string) => {
    const id = ++reqId.current
    const [a, b] = f <= t ? [f, t] : [t, f]
    const prev = previousRange(a, b)
    setLoading(true)
    setError(null)
    try {
      const [rep, day, prevDay] = await Promise.all([
        getUsageReport(a, b),
        getUsageDaily(a, b),
        getUsageDaily(prev.from, prev.to),
      ])
      if (id !== reqId.current) return
      setReport(rep)
      setDaily(fillDaily(day.points, a, b))
      setPrevDaily(fillDaily(prevDay.points, prev.from, prev.to))
    } catch {
      if (id === reqId.current) setError('Rapor yüklenemedi.')
    } finally {
      if (id === reqId.current) setLoading(false)
    }
  }, [])

  useEffect(() => { load(from, to) }, [from, to, load])

  const today = utcDate(0)
  const n = dayCount(from, to)
  const activePreset = to === today ? PRESETS.find(p => from === utcDate(-(p - 1))) : undefined
  const isCustom = customOpen || activePreset === undefined
  const [rangeFrom, rangeTo] = from <= to ? [from, to] : [to, from]
  const rangeLabel = activePreset && !customOpen
    ? `son ${n} gün (${shortDate(rangeFrom)} – ${shortDate(rangeTo)})`
    : `${shortDate(rangeFrom)} – ${shortDate(rangeTo)} (${n} gün)`

  const selected = METRICS.find(m => m.key === metricKey) ?? METRICS[0]

  const cards = useMemo(() => METRICS.map(m => {
    const series = seriesOf(daily, m.key)
    return {
      metric: m,
      series,
      delta: pctChange(sum(series), sum(seriesOf(prevDaily, m.key))),
    }
  }), [daily, prevDaily])

  const chartSeries = useMemo(() => seriesOf(daily, selected.key), [daily, selected.key])
  const chartTotal = sum(chartSeries)
  const chartAvg = chartSeries.length ? chartTotal / chartSeries.length : 0
  const peakVal = chartSeries.length ? Math.max(...chartSeries) : 0
  const peakIdx = chartSeries.indexOf(peakVal)

  const pickPreset = (p: number) => {
    setCustomOpen(false)
    setFrom(utcDate(-(p - 1)))
    setTo(utcDate(0))
  }

  const colStyle = (k: MetricKey) => (k === metricKey ? { background: metricTint(selected.color) } : undefined)

  return (
    <AdminPage>
      <PageHeader
        title="Kullanım Raporu"
        description={`Token, AI soru, kaydetme ve indirme kullanımı — ${rangeLabel}.`}
        actions={
          <>
            <button
              type="button"
              aria-pressed={compare}
              onClick={() => setCompare(c => !c)}
              className={`xc-focus inline-flex h-[38px] items-center gap-2 rounded-[9px] border px-3 text-[12.5px] font-semibold transition-colors ${
                compare ? 'border-ui-soft-line bg-ui-soft text-ui-ink' : 'border-ui-line bg-ui-card text-tx-2 hover:bg-ui-hover'
              }`}
            >
              <svg aria-hidden="true" width="22" height="10" viewBox="0 0 22 10">
                <path d="M1 5h20" stroke="currentColor" strokeWidth="2" strokeDasharray="4 3" />
              </svg>
              Önceki dönemle karşılaştır
            </button>
            <div role="group" aria-label="Tarih aralığı" className="flex rounded-[9px] border border-ui-line bg-ui-card p-[3px]">
              {PRESETS.map(p => {
                const on = !isCustom && activePreset === p
                return (
                  <button
                    key={p}
                    type="button"
                    aria-pressed={on}
                    onClick={() => pickPreset(p)}
                    className={`xc-focus h-8 rounded-[7px] px-3.5 text-[12.5px] font-semibold transition-colors ${
                      on ? 'bg-ui-accent text-ui-accent-fg' : 'text-tx-2 hover:bg-ui-hover'
                    }`}
                  >
                    {p} gün
                  </button>
                )
              })}
              <button
                type="button"
                aria-pressed={isCustom}
                aria-expanded={isCustom}
                aria-controls="usage-custom-range"
                onClick={() => setCustomOpen(true)}
                className={`xc-focus h-8 rounded-[7px] px-3.5 text-[12.5px] font-semibold transition-colors ${
                  isCustom ? 'bg-ui-accent text-ui-accent-fg' : 'text-tx-2 hover:bg-ui-hover'
                }`}
              >
                Özel
              </button>
            </div>
            <button type="button" onClick={() => load(from, to)} disabled={loading} className={BTN_SECONDARY}>
              {loading ? <Loader2 size={14} className="animate-spin" aria-hidden="true" /> : <RefreshCw size={14} aria-hidden="true" />}
              Yenile
            </button>
            <button
              type="button"
              onClick={() => report && downloadCsv(report)}
              disabled={!report || report.rows.length === 0}
              className={BTN_SECONDARY}
            >
              <Download size={14} aria-hidden="true" /> CSV İndir
            </button>
          </>
        }
      />

      {isCustom && (
        <div id="usage-custom-range" className="flex flex-wrap items-end gap-3 self-end">
          <label className="flex flex-col gap-1 text-[12px] font-medium text-tx-muted">
            Başlangıç
            <input
              type="date"
              value={from}
              max={to}
              onChange={e => e.target.value && setFrom(e.target.value)}
              className={`${INPUT} w-[160px]!`}
            />
          </label>
          <label className="flex flex-col gap-1 text-[12px] font-medium text-tx-muted">
            Bitiş
            <input
              type="date"
              value={to}
              min={from}
              max={today}
              onChange={e => e.target.value && setTo(e.target.value)}
              className={`${INPUT} w-[160px]!`}
            />
          </label>
        </div>
      )}

      {error && <Banner>{error}</Banner>}

      {loading && !report ? (
        <div className="flex items-center justify-center gap-2 py-16 text-[13px] text-tx-muted">
          <Loader2 size={16} className="animate-spin" aria-hidden="true" /> Yükleniyor…
        </div>
      ) : report && (
        <>
          <div role="group" aria-label="Grafikte gösterilecek metrik" className="grid grid-cols-[repeat(auto-fit,minmax(190px,1fr))] gap-3.5">
            {cards.map(c => (
              <MetricCard
                key={c.metric.key}
                metric={c.metric}
                total={report.totals[c.metric.key]}
                delta={c.delta}
                series={c.series}
                selected={c.metric.key === metricKey}
                onSelect={() => setMetricKey(c.metric.key)}
              />
            ))}
          </div>

          <Card as="section" padding="none" aria-labelledby="usage-chart-title" className="flex flex-col gap-3.5 px-5 pt-[18px] pb-3.5">
            <div className="flex flex-wrap items-start justify-between gap-4">
              <div className="flex flex-col gap-1">
                <h2 id="usage-chart-title" className="m-0 flex items-center gap-2 text-[15px] font-semibold text-tx">
                  <span aria-hidden="true" className="h-3 w-3 rounded-[3px]" style={{ background: selected.color }} />
                  Günlük {selected.title}
                </h2>
                <span className="text-[12.5px] text-tx-muted">{rangeLabel} · tüm kullanıcılar</span>
              </div>
              <dl className="m-0 flex flex-wrap gap-[22px]">
                <div className="flex flex-col">
                  <dt className="text-[11.5px] text-tx-muted">Dönem toplamı</dt>
                  <dd className="m-0 text-[20px] font-bold text-tx tabular-nums">{fmtNum(chartTotal)}</dd>
                </div>
                <div className="flex flex-col">
                  <dt className="text-[11.5px] text-tx-muted">Günlük ortalama</dt>
                  <dd className="m-0 text-[20px] font-bold text-tx tabular-nums">{fmtNum(chartAvg)}</dd>
                </div>
                <div className="flex flex-col">
                  <dt className="text-[11.5px] text-tx-muted">En yüksek gün</dt>
                  <dd className="m-0 text-[20px] font-bold text-tx tabular-nums">
                    {fmtNum(peakVal)}{' '}
                    {peakVal > 0 && daily[peakIdx] && (
                      <span className="text-[12.5px] font-medium text-tx-muted">{shortDate(daily[peakIdx].date)}</span>
                    )}
                  </dd>
                </div>
              </dl>
            </div>

            <div aria-hidden="true" className="flex flex-wrap gap-[18px] text-[12px] text-tx-2">
              <span className="flex items-center gap-1.5">
                <svg width="22" height="10" viewBox="0 0 22 10"><path d="M1 5h20" stroke={selected.color} strokeWidth="3" strokeLinecap="round" /></svg>
                Bu dönem
              </span>
              {compare && (
                <span className="flex items-center gap-1.5">
                  <svg width="22" height="10" viewBox="0 0 22 10"><path d="M1 5h20" stroke="var(--color-fn-gray)" strokeWidth="2" strokeDasharray="4 3" /></svg>
                  Önceki dönem
                </span>
              )}
              <span className="flex items-center gap-1.5">
                <svg width="22" height="10" viewBox="0 0 22 10"><path d="M1 5h20" stroke={selected.color} strokeWidth="2" strokeDasharray="1.5 3" strokeLinecap="round" /></svg>
                Ortalama
              </span>
            </div>

            <UsageAreaChart points={daily} prevPoints={prevDaily} metrics={METRICS} selected={selected} compare={compare} />

            <span className="text-[11.5px] text-tx-muted">
              Grafik bir metriği gösterir; yukarıdaki kartlardan değiştirin. Bir günün üzerine gelince o günün tüm metrikleri görünür.
            </span>
          </Card>

          <Card as="section" padding="none" aria-labelledby="usage-users-title">
            <SectionHeader id="usage-users-title" title="Kullanıcı bazlı" aside={<span className="text-[12.5px] text-tx-muted">{report.totals.userCount} kullanıcı</span>} />
            <div className="overflow-x-auto">
              <table className={TABLE}>
                <thead>
                  <tr>
                    <th scope="col" className={`${TH} px-4!`}>Kullanıcı</th>
                    {METRICS.map(m => (
                      <th
                        key={m.key}
                        scope="col"
                        style={colStyle(m.key)}
                        className={`${TH} text-right! ${m.key === metricKey ? 'font-bold! text-tx!' : ''}`}
                      >
                        <span className="inline-flex items-center gap-1.5">
                          <span aria-hidden="true" className="h-2 w-2 rounded-[2px]" style={{ background: m.color }} />
                          {m.label}
                        </span>
                      </th>
                    ))}
                  </tr>
                </thead>
                <tbody>
                  {report.rows.length === 0 && (
                    <tr className="border-t border-ui-line">
                      <td colSpan={6} className="px-4 py-8 text-center text-[13px] text-tx-muted">Bu aralıkta kullanım yok.</td>
                    </tr>
                  )}
                  {report.rows.map(r => (
                    <tr key={r.userId} className={TR}>
                      <td className={`${TD} px-4!`}>
                        <div className="font-medium text-tx">{r.username ?? r.email}</div>
                        {r.username && <div className="text-[12px] text-tx-muted">{r.email}</div>}
                      </td>
                      {METRICS.map(m => (
                        <td key={m.key} style={colStyle(m.key)} className={`${TD} text-right text-tx-2 tabular-nums`}>
                          {fmtNum(r[m.key])}
                        </td>
                      ))}
                    </tr>
                  ))}
                </tbody>
                {report.rows.length > 0 && (
                  <tfoot>
                    <tr className="border-t border-ui-line bg-ui-hover font-bold text-tx">
                      <th scope="row" className={`${TD} px-4! text-left`}>Toplam</th>
                      {METRICS.map(m => (
                        <td key={m.key} style={colStyle(m.key)} className={`${TD} text-right tabular-nums`}>
                          {fmtNum(report.totals[m.key])}
                        </td>
                      ))}
                    </tr>
                  </tfoot>
                )}
              </table>
            </div>
          </Card>

          <Card as="section" padding="none">
            <button
              type="button"
              aria-expanded={showDaily}
              aria-controls="usage-daily-table"
              onClick={() => setShowDaily(v => !v)}
              className="xc-focus flex w-full items-center justify-between gap-3 rounded-xl px-4 py-3.5 text-left"
            >
              <span className="text-[15px] font-semibold text-tx">Günlük toplam (tablo)</span>
              <ChevronDown size={16} aria-hidden="true" className={`text-tx-muted transition-transform ${showDaily ? 'rotate-180' : ''}`} />
            </button>
            {showDaily && (
              <div id="usage-daily-table" className="max-h-80 overflow-y-auto border-t border-ui-line">
                <table className={TABLE}>
                  <thead className="sticky top-0 bg-ui-card">
                    <tr>
                      <th scope="col" className={`${TH} px-4!`}>Tarih</th>
                      {METRICS.map(m => (
                        <th key={m.key} scope="col" className={`${TH} text-right!`}>{m.label}</th>
                      ))}
                    </tr>
                  </thead>
                  <tbody>
                    {daily.slice().reverse().map(p => (
                      <tr key={p.date} className={TR}>
                        <td className={`${TD} px-4! text-tx-2`}>
                          {new Date(`${p.date.slice(0, 10)}T00:00:00Z`).toLocaleDateString('tr-TR', { timeZone: 'UTC' })}
                        </td>
                        {METRICS.map(m => (
                          <td key={m.key} className={`${TD} text-right text-tx-2 tabular-nums`}>{fmtNum(p[m.key])}</td>
                        ))}
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            )}
          </Card>
        </>
      )}

      <p className="m-0 text-[12px] text-tx-muted">
        Token yaklaşıktır (üretilen karakter ÷ 4). "İndirme" sunucu indirme uçlarına (UserActivities), "Üretim İnd."
        ise Pro günlük kotasına sayılan Tasarım Alanı üretim indirmelerine karşılık gelir. Tarihler UTC.
      </p>
    </AdminPage>
  )
}
