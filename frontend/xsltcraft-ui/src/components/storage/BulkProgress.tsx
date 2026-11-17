import { Loader2 } from 'lucide-react'

interface Props {
  /** Kısa durum metni (ör. "Dosyalar taranıyor"). */
  label: string
  done: number
  total: number
  /** Sayacın birimi (ör. "dosya", "şablon"). */
  unit: string
  /** İsteğe bağlı ikinci satır (ör. "Pencereyi kapatma"). */
  hint?: string
  /** Kenarlık/zemin olmadan; zaten vurgulu bir şeridin (ör. BulkBar) içinde kullanmak için. */
  bare?: boolean
}

/** Toplu işlemlerin (tarama, yükleme, not, taşıma, silme) ilerleme paneli; ekran okuyuculara canlı bölge olarak duyurulur. */
export default function BulkProgress({ label, done, total, unit, hint, bare }: Props) {
  const pct = total > 0 ? Math.min(100, Math.round((done / total) * 100)) : 0
  return (
    <div
      role="status"
      aria-live="polite"
      className={bare ? 'w-full' : 'mb-4 rounded-lg border border-ui-soft-line bg-ui-soft px-3 py-2.5'}
    >
      <div className="mb-2 flex items-center gap-2 text-[13px]">
        <Loader2 size={15} aria-hidden="true" className="shrink-0 animate-spin text-ui-accent motion-reduce:animate-none" />
        <span className="flex-1 font-medium text-tx">{label}…</span>
        <span className="shrink-0 tabular-nums text-tx-muted">
          {done} / {total} {unit}
        </span>
      </div>
      <div
        role="progressbar"
        aria-label={label}
        aria-valuemin={0}
        aria-valuemax={total}
        aria-valuenow={done}
        className="h-1.5 overflow-hidden rounded-full bg-ui-line"
      >
        <div
          className="h-full rounded-full bg-ui-accent transition-[width] duration-300 motion-reduce:transition-none"
          style={{ width: `${pct}%` }}
        />
      </div>
      {hint && <p className="mt-1.5 mb-0 text-xs text-tx-muted">{hint}</p>}
    </div>
  )
}
