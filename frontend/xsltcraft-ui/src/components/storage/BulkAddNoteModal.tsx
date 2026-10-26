import { useId, useState } from 'react'
import { StickyNote, Loader2 } from 'lucide-react'

import type { FixedNoteMode } from '../../services/userXsltService'
import ModalShell, { MODAL_INPUT, MODAL_PRIMARY, MODAL_SECONDARY } from './ModalShell'

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
  const modeName = useId()

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
    <ModalShell
      title={<><StickyNote size={18} aria-hidden="true" className="text-ui-accent" /> Sabit Not Ekle</>}
      subtitle={`${count} şablon seçili`}
      onClose={onClose}
    >
      <textarea
        value={noteText}
        onChange={(e) => setNoteText(e.target.value.slice(0, MAX_LEN))}
        placeholder="Tüm seçili şablonların not bölümüne gömülecek sabit metin…"
        aria-label="Sabit not metni"
        rows={4}
        autoFocus
        className={`${MODAL_INPUT} resize-none`}
      />
      <div className="mt-1 mb-3 flex justify-end text-xs text-tx-subtle">{trimmed.length}/{MAX_LEN}</div>

      <fieldset className="m-0 mb-5 flex flex-col gap-2 border-0 p-0">
        <legend className="sr-only">Ekleme biçimi</legend>
        <label className="flex cursor-pointer items-center gap-2 text-[13px] text-tx-2">
          <input type="radio" name={modeName} checked={mode === 'replace'} onChange={() => setMode('replace')} className="accent-(--ui-accent)" />
          Mevcut sabit notu değiştir <span className="text-tx-subtle">(önerilen)</span>
        </label>
        <label className="flex cursor-pointer items-center gap-2 text-[13px] text-tx-2">
          <input type="radio" name={modeName} checked={mode === 'append'} onChange={() => setMode('append')} className="accent-(--ui-accent)" />
          Yeni not olarak ekle
        </label>
      </fieldset>

      <div className="flex justify-end gap-2">
        <button type="button" onClick={onClose} className={MODAL_SECONDARY}>İptal</button>
        <button type="button" onClick={handleConfirm} disabled={!canSubmit} className={MODAL_PRIMARY}>
          {submitting ? <Loader2 size={15} aria-hidden="true" className="animate-spin motion-reduce:animate-none" /> : <StickyNote size={15} aria-hidden="true" />}
          {submitting ? 'Ekleniyor…' : 'Ekle ve Önizle'}
        </button>
      </div>
    </ModalShell>
  )
}
