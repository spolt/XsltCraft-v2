import { ImageIcon, TriangleAlert } from 'lucide-react'
import type { EasyEditAnalyzeResponse } from '../../services/easyEditService'

interface Props {
  analysis: EasyEditAnalyzeResponse | null
  /** Verilmezse "Görsel seçimlerini değiştir" gösterilmez (dosyada görsel ya da önceki kayıt yok). */
  onChangeRoles?: () => void
}

const VERDICT_LABEL: Partial<Record<EasyEditAnalyzeResponse['verdict'], string>> = {
  suitable: 'Kolay düzenle için uygun',
  partial: 'Kısmen uygun',
}

/** Sağ panelin doc bölümü: analiz kararı, bulunan özellikler, uyarılar ve görsel rollerini değiştirme bağlantısı. */
export default function DocAnalysisSummary({ analysis, onChangeRoles }: Props) {
  if (!analysis) return null
  const features = (analysis.features ?? []).filter((f) => f.count > 0)
  const warnings = analysis.warnings ?? []

  return (
    <section aria-labelledby="doc-analysis-title" className="flex flex-col gap-2 text-[12px]" data-testid="doc-analysis-summary">
      <h2 id="doc-analysis-title" className="m-0 text-[12px] font-semibold text-tx">Analiz</h2>
      <p className="m-0 text-tx-2">
        <b className={analysis.verdict === 'partial' ? 'text-fn-orange-ink' : 'text-fn-green-ink'}>
          {VERDICT_LABEL[analysis.verdict] ?? 'Analiz edildi'}
        </b>
        {analysis.message && <span className="text-tx-muted"> · {analysis.message}</span>}
      </p>
      {features.length > 0 && (
        <ul className="m-0 flex list-none flex-wrap gap-1.5 p-0" aria-label="Bulunan alanlar">
          {features.map((f) => (
            <li key={f.group} className="rounded bg-ui-soft px-1.5 py-0.5 text-[11px] font-medium text-ui-ink">
              {f.group} · {f.count} konum
            </li>
          ))}
        </ul>
      )}
      {warnings.length > 0 && (
        <ul className="m-0 flex list-none flex-col gap-1 p-0">
          {warnings.map((w, i) => (
            <li key={`${w.code}-${i}`} className="flex gap-1.5 text-fn-orange-ink">
              <TriangleAlert size={12} aria-hidden="true" className="mt-0.5 flex-none" />
              <span>{w.message}</span>
            </li>
          ))}
        </ul>
      )}
      {onChangeRoles && (
        <button
          type="button"
          onClick={onChangeRoles}
          data-testid="change-image-roles"
          className="xc-focus inline-flex items-center gap-1.5 self-start rounded font-semibold text-ui-ink underline hover:no-underline"
        >
          <ImageIcon size={13} aria-hidden="true" />
          Görsel seçimlerini değiştir
        </button>
      )}
    </section>
  )
}
