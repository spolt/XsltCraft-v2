import { X } from 'lucide-react'
import type { BankInfoItem } from '../../services/previewService'

interface Props {
  rows: BankInfoItem[]
  onChange: (rows: BankInfoItem[]) => void
}

export default function BankInfoPanel({ rows, onChange }: Props) {
  const update = (idx: number, field: keyof BankInfoItem, value: string) =>
    onChange(rows.map((r, i) => (i === idx ? { ...r, [field]: value } : r)))

  return (
    <section>
      <h3 className="text-xs font-semibold text-gray-500 uppercase tracking-wide mb-2">Banka / IBAN</h3>
      <div className="flex flex-col gap-3">
        {rows.map((row, idx) => (
          <div key={idx} className="relative bg-gray-50 rounded-lg p-3 border border-gray-100">
            {rows.length > 1 && (
              <button
                type="button"
                onClick={() => onChange(rows.filter((_, i) => i !== idx))}
                className="absolute top-2 right-2 text-gray-300 hover:text-red-400"
                title="Kaldır"
                aria-label="Banka hesabını kaldır"
              >
                <X size={12} />
              </button>
            )}
            <div className="flex flex-col gap-1.5">
              <input
                type="text" placeholder="Banka adı" maxLength={100} value={row.bankName}
                onChange={(e) => update(idx, 'bankName', e.target.value)}
                className="text-xs border border-gray-200 rounded px-2 py-1.5 focus:outline-none focus:ring-1 focus:ring-blue-400 bg-white w-full"
              />
              <input
                type="text" placeholder="TR00 0000 0000 0000 0000 0000 00" maxLength={50} value={row.iban}
                onChange={(e) => update(idx, 'iban', e.target.value)}
                className="text-xs border border-gray-200 rounded px-2 py-1.5 focus:outline-none focus:ring-1 focus:ring-blue-400 bg-white w-full font-mono tracking-wide"
              />
            </div>
          </div>
        ))}
        {rows.length < 10 && (
          <button
            type="button"
            onClick={() => onChange([...rows, { bankName: '', iban: '' }])}
            className="text-xs text-blue-600 hover:text-blue-700 text-left flex items-center gap-1"
          >
            + Banka hesabı ekle
          </button>
        )}
      </div>
    </section>
  )
}
