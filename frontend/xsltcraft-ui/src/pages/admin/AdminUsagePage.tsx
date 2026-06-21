import { useCallback, useEffect, useState } from 'react'
import { BarChart3, Download, Loader2, RefreshCw } from 'lucide-react'
import {
  getUsageReport,
  getUsageDaily,
  type UsageReport,
  type UsageDaily,
} from '../../services/usageReportService'

// UTC tabanlı yyyy-mm-dd (backend DateOnly UTC ile hizalı).
function utcDate(offsetDays = 0): string {
  const d = new Date()
  d.setUTCDate(d.getUTCDate() + offsetDays)
  return d.toISOString().slice(0, 10)
}

const PRESETS: { label: string; from: () => string; to: () => string }[] = [
  { label: 'Bugün', from: () => utcDate(0), to: () => utcDate(0) },
  { label: 'Son 7 gün', from: () => utcDate(-6), to: () => utcDate(0) },
  { label: 'Son 30 gün', from: () => utcDate(-29), to: () => utcDate(0) },
  { label: 'Son 90 gün', from: () => utcDate(-89), to: () => utcDate(0) },
]

const num = (n: number) => n.toLocaleString('tr-TR')

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

function SummaryCard({ label, value, accent }: { label: string; value: number; accent: string }) {
  return (
    <div className="bg-white rounded-lg border border-gray-200 p-4">
      <div className="text-xs text-gray-500 uppercase tracking-wide">{label}</div>
      <div className={`text-2xl font-semibold mt-1 ${accent}`}>{num(value)}</div>
    </div>
  )
}

export default function AdminUsagePage() {
  const [from, setFrom] = useState(utcDate(-29))
  const [to, setTo] = useState(utcDate(0))
  const [report, setReport] = useState<UsageReport | null>(null)
  const [daily, setDaily] = useState<UsageDaily | null>(null)
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)

  const load = useCallback(async (f: string, t: string) => {
    setLoading(true)
    setError(null)
    try {
      const [rep, day] = await Promise.all([getUsageReport(f, t), getUsageDaily(f, t)])
      setReport(rep)
      setDaily(day)
    } catch {
      setError('Rapor yüklenemedi.')
    } finally {
      setLoading(false)
    }
  }, [])

  useEffect(() => { load(from, to) }, [from, to, load])

  const activePreset = PRESETS.find(p => p.from() === from && p.to() === to)?.label

  return (
    <div className="max-w-6xl mx-auto px-6 py-8 space-y-6">
      {/* Başlık */}
      <div className="flex items-center justify-between">
        <div className="flex items-center gap-3">
          <div className="w-10 h-10 rounded-lg bg-blue-100 flex items-center justify-center">
            <BarChart3 size={20} className="text-blue-600" />
          </div>
          <div>
            <h1 className="text-lg font-semibold text-gray-900">Kullanım Raporu</h1>
            <p className="text-sm text-gray-500">Kullanıcı başına token, AI soru, kaydetme ve indirme — geçmişe dönük.</p>
          </div>
        </div>
        <button
          onClick={() => report && downloadCsv(report)}
          disabled={!report || report.rows.length === 0}
          className="inline-flex items-center gap-2 px-4 py-2 bg-blue-600 hover:bg-blue-700 disabled:opacity-50 text-white text-sm font-medium rounded-lg transition"
        >
          <Download size={15} /> CSV İndir
        </button>
      </div>

      {/* Tarih aralığı */}
      <div className="bg-white border border-gray-200 rounded-lg p-4 flex flex-wrap items-center gap-3">
        <div className="flex flex-wrap gap-1.5">
          {PRESETS.map(p => (
            <button
              key={p.label}
              onClick={() => { setFrom(p.from()); setTo(p.to()) }}
              className={`px-3 py-1.5 text-sm rounded-md border transition ${
                activePreset === p.label
                  ? 'border-blue-500 bg-blue-50 text-blue-700 font-medium'
                  : 'border-gray-300 bg-white text-gray-600 hover:bg-gray-50'
              }`}
            >
              {p.label}
            </button>
          ))}
        </div>
        <div className="flex items-center gap-2 ml-auto text-sm">
          <input type="date" value={from} max={to} onChange={e => setFrom(e.target.value)}
            className="px-2 py-1.5 border border-gray-300 rounded-md outline-none focus:ring-2 focus:ring-blue-500" />
          <span className="text-gray-400">—</span>
          <input type="date" value={to} min={from} max={utcDate(0)} onChange={e => setTo(e.target.value)}
            className="px-2 py-1.5 border border-gray-300 rounded-md outline-none focus:ring-2 focus:ring-blue-500" />
          <button
            onClick={() => load(from, to)}
            disabled={loading}
            className="inline-flex items-center gap-1.5 px-3 py-1.5 border border-gray-200 rounded-md hover:bg-gray-50 text-gray-700 disabled:opacity-40"
          >
            {loading ? <Loader2 size={13} className="animate-spin" /> : <RefreshCw size={13} />} Yenile
          </button>
        </div>
      </div>

      {error && <div className="text-sm text-red-600 bg-red-50 border border-red-200 rounded-lg px-4 py-3">{error}</div>}

      {/* Özet kartları */}
      {report && (
        <div className="grid grid-cols-2 sm:grid-cols-5 gap-4">
          <SummaryCard label="Token" value={report.totals.tokensUsed} accent="text-violet-600" />
          <SummaryCard label="AI Sorusu" value={report.totals.aiRequests} accent="text-blue-600" />
          <SummaryCard label="Kaydetme" value={report.totals.saveCount} accent="text-emerald-600" />
          <SummaryCard label="İndirme" value={report.totals.downloadCount} accent="text-amber-600" />
          <SummaryCard label="Üretim İnd." value={report.totals.templateExports} accent="text-gray-700" />
        </div>
      )}

      {/* Kullanıcı bazlı tablo */}
      <div className="bg-white border border-gray-200 rounded-lg overflow-hidden">
        <div className="px-4 py-3 border-b border-gray-100 text-sm font-medium text-gray-700">
          Kullanıcı Bazlı ({report?.totals.userCount ?? 0} kullanıcı)
        </div>
        {loading && !report ? (
          <div className="px-4 py-8 text-center text-sm text-gray-400 flex items-center justify-center gap-2">
            <Loader2 size={14} className="animate-spin" /> Yükleniyor…
          </div>
        ) : (
          <table className="w-full text-sm">
            <thead className="bg-gray-50 border-b border-gray-200 text-left text-xs uppercase text-gray-500 tracking-wide">
              <tr>
                <th className="px-4 py-3 font-medium">Kullanıcı</th>
                <th className="px-4 py-3 font-medium text-right">Token</th>
                <th className="px-4 py-3 font-medium text-right">AI Sorusu</th>
                <th className="px-4 py-3 font-medium text-right">Kaydetme</th>
                <th className="px-4 py-3 font-medium text-right">İndirme</th>
                <th className="px-4 py-3 font-medium text-right">Üretim İnd.</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-gray-100">
              {report && report.rows.length === 0 && (
                <tr><td colSpan={6} className="px-4 py-8 text-center text-gray-400">Bu aralıkta kullanım yok.</td></tr>
              )}
              {report?.rows.map(r => (
                <tr key={r.userId} className="hover:bg-gray-50">
                  <td className="px-4 py-3">
                    <div className="font-medium text-gray-900">{r.username ?? r.email}</div>
                    {r.username && <div className="text-xs text-gray-500">{r.email}</div>}
                  </td>
                  <td className="px-4 py-3 text-right tabular-nums font-mono text-violet-700">{num(r.tokensUsed)}</td>
                  <td className="px-4 py-3 text-right tabular-nums text-gray-700">{num(r.aiRequests)}</td>
                  <td className="px-4 py-3 text-right tabular-nums text-gray-700">{num(r.saveCount)}</td>
                  <td className="px-4 py-3 text-right tabular-nums text-gray-700">{num(r.downloadCount)}</td>
                  <td className="px-4 py-3 text-right tabular-nums text-gray-500">{num(r.templateExports)}</td>
                </tr>
              ))}
            </tbody>
            {report && report.rows.length > 0 && (
              <tfoot className="bg-gray-50 border-t border-gray-200 font-medium text-gray-800">
                <tr>
                  <td className="px-4 py-3">Toplam</td>
                  <td className="px-4 py-3 text-right tabular-nums font-mono">{num(report.totals.tokensUsed)}</td>
                  <td className="px-4 py-3 text-right tabular-nums">{num(report.totals.aiRequests)}</td>
                  <td className="px-4 py-3 text-right tabular-nums">{num(report.totals.saveCount)}</td>
                  <td className="px-4 py-3 text-right tabular-nums">{num(report.totals.downloadCount)}</td>
                  <td className="px-4 py-3 text-right tabular-nums">{num(report.totals.templateExports)}</td>
                </tr>
              </tfoot>
            )}
          </table>
        )}
      </div>

      {/* Günlük toplam (trend) */}
      <div className="bg-white border border-gray-200 rounded-lg overflow-hidden">
        <div className="px-4 py-3 border-b border-gray-100 text-sm font-medium text-gray-700">
          Günlük Toplam
        </div>
        <div className="max-h-80 overflow-y-auto">
          <table className="w-full text-sm">
            <thead className="bg-gray-50 border-b border-gray-200 text-left text-xs uppercase text-gray-500 tracking-wide sticky top-0">
              <tr>
                <th className="px-4 py-2.5 font-medium">Tarih</th>
                <th className="px-4 py-2.5 font-medium text-right">Token</th>
                <th className="px-4 py-2.5 font-medium text-right">AI Sorusu</th>
                <th className="px-4 py-2.5 font-medium text-right">Kaydetme</th>
                <th className="px-4 py-2.5 font-medium text-right">İndirme</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-gray-50">
              {daily && daily.points.length === 0 && (
                <tr><td colSpan={5} className="px-4 py-6 text-center text-gray-400">Veri yok.</td></tr>
              )}
              {daily?.points.slice().reverse().map(p => (
                <tr key={p.date} className="hover:bg-gray-50">
                  <td className="px-4 py-2 text-gray-700">{new Date(p.date).toLocaleDateString('tr-TR')}</td>
                  <td className="px-4 py-2 text-right tabular-nums font-mono text-violet-700">{num(p.tokensUsed)}</td>
                  <td className="px-4 py-2 text-right tabular-nums text-gray-600">{num(p.aiRequests)}</td>
                  <td className="px-4 py-2 text-right tabular-nums text-gray-600">{num(p.saveCount)}</td>
                  <td className="px-4 py-2 text-right tabular-nums text-gray-600">{num(p.downloadCount)}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </div>

      <p className="text-xs text-gray-400">
        Token yaklaşıktır (üretilen karakter ÷ 4). "İndirme" sunucu indirme uçlarına (UserActivities), "Üretim İnd."
        ise Pro günlük kotasına sayılan grid-canvas üretim indirmelerine karşılık gelir. Tarihler UTC.
      </p>
    </div>
  )
}
