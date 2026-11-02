import { CheckCircle2, AlertTriangle, Lock, XCircle, Eye } from 'lucide-react'

import type { BulkAddFixedNoteResult, FixedNoteItemStatus } from '../../services/userXsltService'
import ModalShell, { MODAL_PRIMARY } from './ModalShell'

interface Props {
  result: BulkAddFixedNoteResult
  onClose: () => void
  onPreview: (id: string) => void
}

const STATUS: Record<FixedNoteItemStatus, { label: string; cls: string; icon: typeof CheckCircle2 }> = {
  updated: { label: 'Eklendi', cls: 'text-fn-green-ink', icon: CheckCircle2 },
  no_notes: { label: 'Not bölümü yok', cls: 'text-fn-orange-ink', icon: AlertTriangle },
  locked: { label: 'Kilitli', cls: 'text-tx-muted', icon: Lock },
  failed: { label: 'Hata', cls: 'text-fn-red-ink', icon: XCircle },
}

export default function BulkNoteResultDialog({ result, onClose, onPreview }: Props) {
  const updated = result.results.filter((r) => r.status === 'updated').length
  const others = result.results.length - updated

  return (
    <ModalShell
      title="Sabit not sonucu"
      subtitle={
        <>
          {updated} şablona eklendi{others > 0 ? ` · ${others} şablon atlandı` : ''}.
          {updated > 0 && ' Önizleme ile kontrol etmeyi unutmayın.'}
        </>
      }
      onClose={onClose}
    >
      <ul className="m-0 max-h-72 list-none divide-y divide-ui-line overflow-y-auto rounded-lg border border-ui-line p-0">
        {result.results.map((r) => {
          const s = STATUS[r.status]
          const Icon = s.icon
          return (
            <li key={r.id} className="flex items-center gap-2 px-3 py-2 text-[13px]">
              <Icon size={15} aria-hidden="true" className={`${s.cls} shrink-0`} />
              <span className="flex-1 truncate font-mono text-[12.5px] text-tx-2">{r.name}</span>
              <span className={`shrink-0 text-xs ${s.cls}`}>{s.label}</span>
              {r.status === 'updated' && (
                <button
                  type="button"
                  onClick={() => onPreview(r.id)}
                  aria-label={`${r.name} önizle`}
                  className="xc-focus flex shrink-0 items-center gap-1 rounded text-xs font-semibold text-ui-ink hover:underline"
                >
                  <Eye size={13} aria-hidden="true" /> Önizle
                </button>
              )}
            </li>
          )
        })}
      </ul>

      <div className="mt-6 flex justify-end">
        <button type="button" onClick={onClose} className={MODAL_PRIMARY}>Kapat</button>
      </div>
    </ModalShell>
  )
}
