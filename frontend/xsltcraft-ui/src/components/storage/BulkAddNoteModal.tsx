import { useState } from 'react'
import { X, StickyNote, Loader2 } from 'lucide-react'

import type { FixedNoteMode } from '../../services/userXsltService'

const MAX_LEN = 1000

interface Props {
  count: number
  onClose: () => void
  onConfirm: (noteText: string, mode: FixedNoteMode) => Promise<void>
}

export default function BulkAddNoteModal({ count, onClose, onConfirm }: Props) {
  const [noteText, setNoteText] = useState('')
  const [mode, setMode] = useState<FixedNoteMode>('replace')
  const [submitting, setSubmitting] = useState(false)

  const trimmed = noteText.trim()
  const canSubmit = trimmed.length > 0 && trimmed.length <= MAX_LEN && !submitting

  async function handleConfirm() {
    if (!canSubmit) return
    setSubmitting(true)
    try {
      await onConfirm(trimmed, mode)
    } finally {
      setSubmitting(false)
    }
  }

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/60" onClick={onClose}>
      <div className="bg-white rounded-xl shadow-2xl w-full max-w-md mx-4 p-6" onClick={(e) => e.stopPropagation()}>
        <div className="flex items-center justify-between mb-1">
          <h2 className="flex items-center gap-2 text-lg font-semibold text-gray-800">
            <StickyNote size={18} className="text-blue-600" /> Sabit Not Ekle
          </h2>
          <button onClick={onClose} className="text-gray-400 hover:text-gray-600 transition-colors">
            <X size={18} />
          </button>
        </div>
        <p className="text-sm text-gray-500 mb-4">{count} şablon seçili</p>

        <textarea
          value={noteText}
          onChange={(e) => setNoteText(e.target.value.slice(0, MAX_LEN))}
          placeholder="Tüm seçili şablonların not bölümüne gömülecek sabit metin…"
          rows={4}
          autoFocus
          className="w-full border border-gray-300 rounded-lg px-3 py-2 text-sm resize-none focus:outline-none focus:ring-2 focus:ring-blue-500 focus:border-blue-500"
        />
        <div className="flex justify-end text-xs text-gray-400 mt-1 mb-3">{trimmed.length}/{MAX_LEN}</div>

        <div className="space-y-2 mb-5">
          <label className="flex items-center gap-2 text-sm text-gray-700 cursor-pointer">
            <input type="radio" checked={mode === 'replace'} onChange={() => setMode('replace')} className="accent-blue-600" />
            Mevcut sabit notu değiştir <span className="text-gray-400">(önerilen)</span>
          </label>
          <label className="flex items-center gap-2 text-sm text-gray-700 cursor-pointer">
            <input type="radio" checked={mode === 'append'} onChange={() => setMode('append')} className="accent-blue-600" />
            Yeni not olarak ekle
          </label>
        </div>

        <div className="flex justify-end gap-2">
          <button onClick={onClose} className="px-4 py-2 text-sm font-medium text-gray-600 hover:bg-gray-100 rounded-lg transition-colors">İptal</button>
          <button
            onClick={handleConfirm}
            disabled={!canSubmit}
            className="flex items-center gap-1.5 px-4 py-2 text-sm font-medium text-white bg-blue-600 hover:bg-blue-700 rounded-lg transition-colors disabled:opacity-50 disabled:cursor-not-allowed"
          >
            {submitting ? <Loader2 size={15} className="animate-spin" /> : <StickyNote size={15} />}
            {submitting ? 'Ekleniyor…' : 'Ekle ve Önizle'}
          </button>
        </div>
      </div>
    </div>
  )
}
