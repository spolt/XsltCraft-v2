import { useId } from 'react'
import { Plus, X } from 'lucide-react'
import type { BankInfoItem } from '../../services/previewService'

export interface BankRowLock {
  /** Banka adı salt okunur. */
  bankName?: boolean
  /** `false` ise satır kaldırılamaz. */
  deletable?: boolean
}

interface Props {
  rows: BankInfoItem[]
  onChange: (rows: BankInfoItem[]) => void
  /** Başlığı gizle (başlık dışarıda, ör. akordeon düğmesinde gösteriliyorsa). */
  hideTitle?: boolean
  /** Satır başına kilitler (`rows` sırasıyla). */
  locks?: BankRowLock[]
  /** `false` ise yeni satır eklenemez ve bilgi notu gösterilir. Varsayılan: `true`. */
  canAdd?: boolean
  /** Son satır da kaldırılabilir; liste boşken boş durum gösterilir. */
  allowEmpty?: boolean
  /** Satır başına IBAN hata metni (`rows` sırasıyla). */
  ibanError?: (string | null | undefined)[]
}

const inputClass =
  'h-[30px] w-full rounded-[5px] border border-field-line bg-ui-card px-2 text-[12px] text-tx placeholder:text-tx-subtle focus:border-ui-accent focus:outline-none focus:ring-2 focus:ring-ui-soft-line'

export default function BankInfoPanel({
  rows,
  onChange,
  hideTitle = false,
  locks,
  canAdd = true,
  allowEmpty = false,
  ibanError,
}: Props) {
  const baseId = useId()
  const update = (idx: number, field: keyof BankInfoItem, value: string) =>
    onChange(rows.map((r, i) => (i === idx ? { ...r, [field]: value } : r)))

  return (
    <section className="flex flex-col gap-2">
      {!hideTitle && (
        <h3 className="text-[12px] font-semibold uppercase tracking-wide text-tx-muted">Banka / IBAN</h3>
      )}
      {allowEmpty && rows.length === 0 && (
        <p className="text-[12px] text-tx-subtle">Banka hesabı yok.</p>
      )}
      {rows.map((row, idx) => {
        const lock = locks?.[idx]
        const nameLocked = lock?.bankName === true
        const removable = (rows.length > 1 || allowEmpty) && lock?.deletable !== false
        const error = ibanError?.[idx] || undefined
        const errorId = `${baseId}-${idx}-iban-error`
        return (
          <div key={idx} className="flex flex-col gap-1.5 rounded-md border border-field-line bg-ui-card p-2.5">
            <div className="flex items-center justify-between">
              <span className="text-[12px] font-semibold text-tx">Hesap {idx + 1}</span>
              {removable && (
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
              readOnly={nameLocked}
              title={nameLocked ? 'Banka adı belgede düzenlenemiyor' : undefined}
              onChange={(e) => update(idx, 'bankName', e.target.value)}
              className={`${inputClass}${nameLocked ? ' cursor-default bg-ui-hover text-tx-muted' : ''}`}
            />
            <label htmlFor={`${baseId}-${idx}-iban`} className="sr-only">IBAN</label>
            <input
              id={`${baseId}-${idx}-iban`}
              type="text" placeholder="TR00 0000 0000 0000 0000 0000 00" maxLength={50} value={row.iban}
              aria-invalid={error ? true : undefined}
              aria-describedby={error ? errorId : undefined}
              onChange={(e) => update(idx, 'iban', e.target.value)}
              className={`${inputClass} font-mono tracking-wide${error ? ' border-fn-red' : ''}`}
            />
            {error && (
              <p id={errorId} className="text-[11px] text-fn-red-ink">{error}</p>
            )}
          </div>
        )
      })}
      {canAdd && rows.length < 10 && (
        <button
          type="button"
          onClick={() => onChange([...rows, { bankName: '', iban: '' }])}
          className="xc-focus flex h-[30px] items-center gap-1 self-start rounded px-1 text-[12px] font-semibold text-ui-ink hover:underline"
        >
          <Plus size={13} aria-hidden="true" />
          Banka hesabı ekle
        </button>
      )}
      {!canAdd && (
        <p className="text-[11px] text-tx-subtle">
          Belgedeki banka tablosuna yeni satır eklenemiyor; mevcut satırları düzenleyebilir ya da kaldırabilirsiniz.
        </p>
      )}
    </section>
  )
}
