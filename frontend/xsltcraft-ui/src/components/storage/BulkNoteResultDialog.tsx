import { useState } from 'react'
import { CheckCircle2, AlertTriangle, Lock, XCircle, Eye, HelpCircle, Loader2 } from 'lucide-react'

import type { BulkAddFixedNoteResult, FixedNoteItemStatus } from '../../services/userXsltService'
import ModalShell, { MODAL_PRIMARY, MODAL_SECONDARY } from './ModalShell'

interface Props {
  result: BulkAddFixedNoteResult
  onClose: () => void
  onPreview: (id: string) => void
  /** Birden fazla not alanı bulunan şablonlarda, kullanıcı onayıyla önce gelen not alanına ekler. */
  onResolveAmbiguous: (ids: string[]) => Promise<void>
}

const STATUS: Record<FixedNoteItemStatus, { label: string; cls: string; icon: typeof CheckCircle2 }> = {
  updated: { label: 'Eklendi', cls: 'text-fn-green-ink', icon: CheckCircle2 },
  no_notes: { label: 'Not bölümü yok', cls: 'text-fn-orange-ink', icon: AlertTriangle },
  ambiguous: { label: 'Onay gerekli', cls: 'text-fn-orange-ink', icon: HelpCircle },
  locked: { label: 'Kilitli', cls: 'text-tx-muted', icon: Lock },
  failed: { label: 'Hata', cls: 'text-fn-red-ink', icon: XCircle },
}

export default function BulkNoteResultDialog({ result, onClose, onPreview, onResolveAmbiguous }: Props) {
  const [resolving, setResolving] = useState(false)
  const updated = result.results.filter((r) => r.status === 'updated').length
  const ambiguousIds = result.results.filter((r) => r.status === 'ambiguous').map((r) => r.id)
  const others = result.results.length - updated - ambiguousIds.length

  async function handleResolve() {
    setResolving(true)
    try {
      await onResolveAmbiguous(ambiguousIds)
    } finally {
      setResolving(false)
    }
  }

  return (
    <ModalShell
      title="Sabit not sonucu"
      subtitle={
        <>
          {updated} şablona eklendi{others > 0 ? ` · ${others} şablon atlandı` : ''}
          {ambiguousIds.length > 0 ? ` · ${ambiguousIds.length} şablon onay bekliyor` : ''}.
          {updated > 0 && ' Önizleme ile kontrol etmeyi unutmayın.'}
        </>
      }
      onClose={resolving ? () => {} : onClose}
    >
      <ul className="m-0 max-h-72 list-none divide-y divide-ui-line overflow-y-auto rounded-lg border border-ui-line p-0">
        {result.results.map((r) => {
          const s = STATUS[r.status]
          const Icon = s.icon
          return (
            <li key={r.id} className="flex items-center gap-2 px-3 py-2 text-[13px]">
              <Icon size={15} aria-hidden="true" className={`${s.cls} shrink-0`} />
              <span className="flex-1 truncate font-mono text-[12.5px] text-tx-2">{r.name}</span>
              <span className={`shrink-0 text-xs ${s.cls}`}>
                {s.label}
                {r.status === 'updated' && (r.copies ?? 0) > 1 ? ` · ${r.copies} kopya` : ''}
              </span>
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

      {ambiguousIds.length > 0 && (
        <div role="note" className="mt-4 rounded-lg border border-ui-line bg-ui-soft px-3 py-2.5 text-[13px] text-tx-2">
          <p className="m-0">
            <strong>Onay gerekli:</strong> bu şablonlarda birden fazla not alanı bulundu. Onaylarsan not, belgede
            <strong> ilk bulunan</strong> not alanına eklenir; onaylamazsan şablonlar değişmez.
          </p>
          <div className="mt-2.5 flex justify-end">
            <button type="button" onClick={handleResolve} disabled={resolving} className={MODAL_PRIMARY}>
              {resolving && <Loader2 size={14} aria-hidden="true" className="animate-spin motion-reduce:animate-none" />}
              {resolving ? 'Ekleniyor…' : 'İlk bulunan not alanına ekle'}
            </button>
          </div>
        </div>
      )}

      <div className="mt-6 flex justify-end">
        <button
          type="button"
          onClick={onClose}
          disabled={resolving}
          className={ambiguousIds.length > 0 ? `${MODAL_SECONDARY} disabled:cursor-not-allowed disabled:opacity-50` : MODAL_PRIMARY}
        >
          {ambiguousIds.length > 0 ? 'Onaylamadan kapat' : 'Kapat'}
        </button>
      </div>
    </ModalShell>
  )
}
