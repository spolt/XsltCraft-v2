import { X, CheckCircle2, AlertTriangle, Lock, XCircle, Eye } from 'lucide-react'

import type { BulkAddFixedNoteResult, FixedNoteItemStatus } from '../../services/userXsltService'

interface Props {
  result: BulkAddFixedNoteResult
  onClose: () => void
  onPreview: (id: string) => void
}

const STATUS: Record<FixedNoteItemStatus, { label: string; cls: string; icon: typeof CheckCircle2 }> = {
  updated: { label: 'Eklendi', cls: 'text-emerald-600', icon: CheckCircle2 },
  no_notes: { label: 'Not bölümü yok', cls: 'text-amber-600', icon: AlertTriangle },
  locked: { label: 'Kilitli', cls: 'text-gray-500', icon: Lock },
  failed: { label: 'Hata', cls: 'text-red-600', icon: XCircle },
}

export default function BulkNoteResultDialog({ result, onClose, onPreview }: Props) {
  const updated = result.results.filter((r) => r.status === 'updated').length
  const others = result.results.length - updated

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/60" onClick={onClose}>
      <div className="bg-white rounded-xl shadow-2xl w-full max-w-md mx-4 p-6" onClick={(e) => e.stopPropagation()}>
        <div className="flex items-center justify-between mb-1">
          <h2 className="text-lg font-semibold text-gray-800">Sabit not sonucu</h2>
          <button onClick={onClose} className="text-gray-400 hover:text-gray-600 transition-colors">
            <X size={18} />
          </button>
        </div>
        <p className="text-sm text-gray-500 mb-4">
          {updated} şablona eklendi{others > 0 ? ` · ${others} şablon atlandı` : ''}.
          {updated > 0 && ' Önizleme ile kontrol etmeyi unutmayın.'}
        </p>

        <div className="border border-gray-100 rounded-lg max-h-72 overflow-y-auto divide-y divide-gray-50">
          {result.results.map((r) => {
            const s = STATUS[r.status]
            const Icon = s.icon
            return (
              <div key={r.id} className="flex items-center gap-2 px-3 py-2 text-sm">
                <Icon size={15} className={`${s.cls} shrink-0`} />
                <span className="flex-1 truncate text-gray-700">{r.name}</span>
                <span className={`text-xs ${s.cls} shrink-0`}>{s.label}</span>
                {r.status === 'updated' && (
                  <button
                    onClick={() => onPreview(r.id)}
                    className="flex items-center gap-1 text-xs font-medium text-blue-600 hover:text-blue-700 shrink-0"
                  >
                    <Eye size={13} /> Önizle
                  </button>
                )}
              </div>
            )
          })}
        </div>

        <div className="flex justify-end mt-6">
          <button onClick={onClose} className="px-4 py-2 text-sm font-medium text-white bg-blue-600 hover:bg-blue-700 rounded-lg transition-colors">
            Kapat
          </button>
        </div>
      </div>
    </div>
  )
}
