import { useId, useState } from 'react'
import { StickyNote, Loader2 } from 'lucide-react'

import type { FixedNoteMode } from '../../services/userXsltService'
import ModalShell, { MODAL_INPUT, MODAL_PRIMARY, MODAL_SECONDARY } from './ModalShell'
import BulkProgress from './BulkProgress'

const MAX_LEN = 1000

interface Props {
  count: number
  onClose: () => void
  /** `onProgress` her parti bittiğinde işlenen şablon sayısıyla çağrılır. */
  onConfirm: (noteText: string, mode: FixedNoteMode, onProgress: (done: number) => void) => Promise<void>
}

export default function BulkAddNoteModal({ count, onClose, onConfirm }: Props) {
  const [noteText, setNoteText] = useState('')
  const [mode, setMode] = useState<FixedNoteMode>('replace')
  const [submitting, setSubmitting] = useState(false)
  const [done, setDone] = useState(0)
  const modeName = useId()

  const trimmed = noteText.trim()
  const canSubmit = trimmed.length > 0 && trimmed.length <= MAX_LEN && !submitting

  async function handleConfirm() {
    if (!canSubmit) return
    setSubmitting(true)
    setDone(0)
    try {
      await onConfirm(trimmed, mode, setDone)
    } finally {
      setSubmitting(false)
    }
  }

  // İşlem sürerken kapatmak sonucu (BulkNoteResultDialog) kaçırtır; istekler zaten arka planda sürer.
  function guardedClose() {
    if (!submitting) onClose()
  }

  return (
    <ModalShell
      title={<><StickyNote size={18} aria-hidden="true" className="text-ui-accent" /> Sabit Not Ekle</>}
      subtitle={`${count} şablon seçili`}
      onClose={guardedClose}
    >
      <textarea
        value={noteText}
        onChange={(e) => setNoteText(e.target.value.slice(0, MAX_LEN))}
        placeholder="Tüm seçili şablonların not bölümüne gömülecek sabit metin…"
        aria-label="Sabit not metni"
        rows={4}
        autoFocus
        readOnly={submitting}
        className={`${MODAL_INPUT} resize-none`}
      />
      <div className="mt-1 mb-3 flex justify-end text-xs text-tx-subtle">{trimmed.length}/{MAX_LEN}</div>

      {submitting && (
        <BulkProgress
          label="Şablonlara not ekleniyor"
          done={done}
          total={count}
          unit="şablon"
          hint="İşlem bitene kadar bu pencereyi kapatma."
        />
      )}

      <fieldset disabled={submitting} className="m-0 mb-5 flex flex-col gap-2 border-0 p-0">
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
        <button type="button" onClick={onClose} disabled={submitting} className={`${MODAL_SECONDARY} disabled:cursor-not-allowed disabled:opacity-50`}>İptal</button>
        <button type="button" onClick={handleConfirm} disabled={!canSubmit} className={MODAL_PRIMARY}>
          {submitting ? <Loader2 size={15} aria-hidden="true" className="animate-spin motion-reduce:animate-none" /> : <StickyNote size={15} aria-hidden="true" />}
          {submitting ? 'Ekleniyor…' : 'Ekle ve Önizle'}
        </button>
      </div>
    </ModalShell>
  )
}
