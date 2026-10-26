import { useId, useRef, type ClipboardEvent, type ReactNode, type RefObject } from 'react'
import { ArrowRight, Paperclip } from 'lucide-react'
import ChatAttachmentBar from './ChatAttachmentBar'
import { ACCEPT_ATTR, filesFrom, type PreparedImage } from '../../utils/imageAttachment'

interface Props {
  value: string
  onChange: (v: string) => void
  onSend: () => void
  streaming: boolean
  inputRef: RefObject<HTMLTextAreaElement | null>
  /** Ekran görüntüsü gönderilebilir mi (admin vision açık + uygun sağlayıcı). */
  visionEnabled: boolean
  maxImages: number
  attachments: PreparedImage[]
  processing: boolean
  onAddFiles: (files: File[]) => void
  onRemoveAttachment: (id: string) => void
  /** Yazma kutusunun üstünde (ör. hazır komut çipleri). */
  above?: ReactNode
}

/** Sohbet yazma alanı: metin, ataç (ekran görüntüsü), gönder. Ctrl+V ile görsel yapıştırılabilir. */
export default function ChatComposer({
  value, onChange, onSend, streaming, inputRef,
  visionEnabled, maxImages, attachments, processing, onAddFiles, onRemoveAttachment, above,
}: Props) {
  const fileRef = useRef<HTMLInputElement>(null)
  const hintId = useId()
  const canSend = !streaming && !processing && (value.trim() !== '' || attachments.length > 0)
  const full = attachments.length >= maxImages

  function handlePaste(e: ClipboardEvent<HTMLTextAreaElement>) {
    if (!visionEnabled) return
    const files = filesFrom(e.clipboardData.files).filter(f => f.type.startsWith('image/'))
    // Excel/Word kopyası görsel + metin taşır: o durumda metin yapıştırılsın, görsel yakalanmasın.
    if (files.length === 0 || e.clipboardData.types.includes('text/plain')) return
    e.preventDefault()
    onAddFiles(files)
  }

  const attachTitle = !visionEnabled
    ? 'Ekran görüntüsü analizi şu an kapalı'
    : full
      ? `Mesaj başına en fazla ${maxImages} ekran görüntüsü`
      : 'Ekran görüntüsü ekle (PNG, JPG) — Ctrl+V ile de yapıştırabilirsiniz'

  return (
    <div className="flex flex-shrink-0 flex-col gap-2 border-t border-ui-line px-3 pb-3 pt-2.5">
      {above}
      <ChatAttachmentBar
        attachments={attachments}
        processing={processing}
        onRemove={onRemoveAttachment}
        disabled={streaming}
      />
      <div className="flex items-end gap-1 rounded-lg border border-field-line bg-ui-card p-1.5 pl-2.5 transition-colors focus-within:border-ui-accent">
        <textarea
          ref={inputRef}
          value={value}
          onChange={e => onChange(e.target.value)}
          onPaste={handlePaste}
          onKeyDown={e => {
            // Enter → gönder · Shift+Enter → yeni satır (IME kompozisyonu sürerken gönderme)
            if (e.key === 'Enter' && !e.shiftKey && !e.nativeEvent.isComposing) {
              e.preventDefault()
              if (canSend) onSend()
            }
          }}
          rows={2}
          aria-label="AI Asistana sor"
          aria-describedby={hintId}
          className="min-w-0 flex-1 resize-none self-center bg-transparent py-0.5 text-[12.5px] leading-[1.5] text-tx outline-none placeholder:text-tx-subtle"
          placeholder={attachments.length > 0
            ? 'Ekran görüntüsü hakkında ne yapılsın? (boş bırakabilirsiniz)'
            : 'Soru sor veya seçili kodu açıklat…'}
          disabled={streaming}
        />
        <button
          type="button"
          onClick={() => fileRef.current?.click()}
          disabled={!visionEnabled || streaming || full}
          aria-label="Ekran görüntüsü ekle"
          title={attachTitle}
          className="xc-focus flex h-7 w-7 flex-none items-center justify-center rounded-md text-tx-muted transition-colors hover:bg-ui-hover hover:text-ui-accent disabled:cursor-not-allowed disabled:opacity-30 disabled:hover:bg-transparent disabled:hover:text-tx-muted"
        >
          <Paperclip size={15} aria-hidden="true" />
        </button>
        <input
          ref={fileRef}
          type="file"
          accept={ACCEPT_ATTR}
          multiple={maxImages > 1}
          hidden
          onChange={e => {
            onAddFiles(filesFrom(e.target.files))
            e.target.value = '' // aynı dosya tekrar seçilebilsin
          }}
        />
        <button
          type="button"
          onClick={onSend}
          disabled={!canSend}
          aria-label="Gönder"
          title="Gönder (Enter)"
          className="xc-focus flex h-7 w-7 flex-none items-center justify-center rounded-md bg-ui-accent text-white transition-[filter] hover:brightness-110 disabled:cursor-not-allowed disabled:opacity-30"
        >
          <ArrowRight size={15} strokeWidth={2} aria-hidden="true" />
        </button>
      </div>
      <p id={hintId} className="sr-only">
        Enter ile gönder, Shift+Enter ile yeni satır.
        {visionEnabled && ' Ctrl+V ile ekran görüntüsü yapıştırılabilir.'}
      </p>
      {attachments.length > 0 && (
        <p className="text-[10.5px] text-tx-subtle">Ekran görüntüleri yalnız bu mesajla gönderilir, saklanmaz.</p>
      )}
    </div>
  )
}
