import { Loader2 } from 'lucide-react'
import type { DownloadProgressState } from './useTemplateDownload'

function formatBytes(n: number): string {
  if (n < 1024 * 1024) return `${Math.max(1, Math.round(n / 1024))} KB`
  return `${(n / (1024 * 1024)).toFixed(1).replace('.', ',')} MB`
}

/**
 * İndirme göstergesi (BulkProgress görünümüyle tutarlı). Sunucu arşivi kurarken süre bilinmediği için belirsiz çubuk;
 * veri akmaya başlayınca (Content-Length varsa) gerçek yüzde. Ekran okuyuculara canlı bölge olarak duyurulur.
 */
export default function DownloadProgress({ state }: { state: DownloadProgressState }) {
  const preparing = state.phase === 'preparing'
  const pct = !preparing && state.total ? Math.min(100, Math.round((state.loaded / state.total) * 100)) : null
  const what = state.count === 1 ? 'Şablon' : 'ZIP'
  const label = preparing
    ? `${what} hazırlanıyor${state.count > 1 ? ` (${state.count} şablon)` : ''}`
    : 'İndiriliyor'
  const right = preparing ? '' : pct !== null ? `%${pct}` : formatBytes(state.loaded)

  return (
    <div className="border-b border-ui-line px-3.5 py-3">
      <div role="status" aria-live="polite" className="rounded-lg border border-ui-soft-line bg-ui-soft px-3 py-2.5">
        <div className="mb-2 flex items-center gap-2 text-[13px]">
          <Loader2 size={15} aria-hidden="true" className="shrink-0 animate-spin text-ui-accent motion-reduce:animate-none" />
          <span className="flex-1 font-medium text-tx">{label}…</span>
          {right && <span className="shrink-0 tabular-nums text-tx-muted">{right}</span>}
        </div>
        <div
          role="progressbar"
          aria-label={label}
          aria-valuemin={0}
          aria-valuemax={100}
          aria-valuenow={pct ?? undefined}
          className="h-1.5 overflow-hidden rounded-full bg-ui-line"
        >
          {pct === null ? (
            <div className="h-full w-full animate-pulse rounded-full bg-ui-accent/60 motion-reduce:animate-none" />
          ) : (
            <div
              className="h-full rounded-full bg-ui-accent transition-[width] duration-300 motion-reduce:transition-none"
              style={{ width: `${pct}%` }}
            />
          )}
        </div>
        {preparing && state.count > 1 && (
          <p className="mt-1.5 mb-0 text-xs text-tx-muted">Büyük seçimlerde birkaç saniye sürebilir; sayfadan ayrılmayın.</p>
        )}
      </div>
    </div>
  )
}
