import { useId } from 'react'
import { Plus, X } from 'lucide-react'
import type { BankInfoItem } from '../../services/previewService'

interface Props {
  rows: BankInfoItem[]
  onChange: (rows: BankInfoItem[]) => void
  /** Başlığı gizle (başlık dışarıda, ör. akordeon düğmesinde gösteriliyorsa). */
  hideTitle?: boolean
}

const inputClass =
  'h-[30px] w-full rounded-[5px] border border-field-line bg-ui-card px-2 text-[12px] text-tx placeholder:text-tx-subtle focus:border-ui-accent focus:outline-none focus:ring-2 focus:ring-ui-soft-line'

export default function BankInfoPanel({ rows, onChange, hideTitle = false }: Props) {
  const baseId = useId()
  const update = (idx: number, field: keyof BankInfoItem, value: string) =>
    onChange(rows.map((r, i) => (i === idx ? { ...r, [field]: value } : r)))

  return (
    <section className="flex flex-col gap-2">
      {!hideTitle && (
        <h3 className="text-[12px] font-semibold uppercase tracking-wide text-tx-muted">Banka / IBAN</h3>
      )}
      {rows.map((row, idx) => (
        <div key={idx} className="flex flex-col gap-1.5 rounded-md border border-field-line bg-ui-card p-2.5">
          <div className="flex items-center justify-between">
            <span className="text-[12px] font-semibold text-tx">Hesap {idx + 1}</span>
            {rows.length > 1 && (
              <button
                type="button"
                onClick={() => onChange(rows.filter((_, i) => i !== idx))}
                className="xc-focus flex h-6 w-6 items-center justify-center rounded text-tx-subtle hover:bg-ui-hover hover:text-fn-red-ink"
                title="Kaldır"
                aria-label="Banka hesabını kaldır"
              >
                <X size={13} aria-hidden="true" />
              </button>
            )}
          </div>
          <label htmlFor={`${baseId}-${idx}-name`} className="sr-only">Banka adı</label>
          <input
            id={`${baseId}-${idx}-name`}
            type="text" placeholder="Banka adı" maxLength={100} value={row.bankName}
            onChange={(e) => update(idx, 'bankName', e.target.value)}
            className={inputClass}
          />
          <label htmlFor={`${baseId}-${idx}-iban`} className="sr-only">IBAN</label>
          <input
            id={`${baseId}-${idx}-iban`}
            type="text" placeholder="TR00 0000 0000 0000 0000 0000 00" maxLength={50} value={row.iban}
            onChange={(e) => update(idx, 'iban', e.target.value)}
            className={`${inputClass} font-mono tracking-wide`}
          />
        </div>
      ))}
      {rows.length < 10 && (
        <button
          type="button"
          onClick={() => onChange([...rows, { bankName: '', iban: '' }])}
          className="xc-focus flex h-[30px] items-center gap-1 self-start rounded px-1 text-[12px] font-semibold text-ui-ink hover:underline"
        >
          <Plus size={13} aria-hidden="true" />
          Banka hesabı ekle
        </button>
      )}
    </section>
  )
}
