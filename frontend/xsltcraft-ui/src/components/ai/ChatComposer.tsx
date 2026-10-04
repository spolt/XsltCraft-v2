import { useRef, type ClipboardEvent, type RefObject } from 'react'
import { Paperclip } from 'lucide-react'
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
}

/** Sohbet yazma alanı: ataç (ekran görüntüsü), metin, gönder. Ctrl+V ile görsel yapıştırılabilir. */
export default function ChatComposer({
  value, onChange, onSend, streaming, inputRef,
  visionEnabled, maxImages, attachments, processing, onAddFiles, onRemoveAttachment,
}: Props) {
  const fileRef = useRef<HTMLInputElement>(null)
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
    <div className="px-3 py-2 border-t border-gray-700 flex-shrink-0">
      <ChatAttachmentBar
        attachments={attachments}
        processing={processing}
        onRemove={onRemoveAttachment}
        disabled={streaming}
      />
      <div className="flex gap-2">
        <button
          type="button"
          onClick={() => fileRef.current?.click()}
          disabled={!visionEnabled || streaming || full}
          aria-label="Ekran görüntüsü ekle"
          title={attachTitle}
          className="self-stretch px-2 rounded border border-gray-700 bg-gray-800 text-gray-400 hover:text-violet-300 hover:border-violet-500 disabled:opacity-30 disabled:cursor-not-allowed disabled:hover:text-gray-400 disabled:hover:border-gray-700 transition-colors"
        >
          <Paperclip size={16} />
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
          className="flex-1 bg-gray-800 border border-gray-700 rounded px-2 py-1.5 text-sm text-gray-100 placeholder-gray-500 focus:outline-none focus:border-violet-500 resize-none font-mono"
          placeholder={attachments.length > 0
            ? 'Ekran görüntüsü hakkında ne yapılsın? (boş bırakabilirsiniz)'
            : 'Buraya yaz… (Enter ile gönder)'}
          disabled={streaming}
        />
        <button
          type="button"
          onClick={onSend}
          disabled={!canSend}
          className="self-stretch px-3 rounded bg-violet-600 hover:bg-violet-500 disabled:opacity-30 disabled:cursor-not-allowed text-sm text-white font-medium"
        >
          Gönder
        </button>
      </div>
      <div className="mt-1 text-[10px] text-gray-500 select-none">
        <kbd className="font-mono text-gray-400">Enter</kbd> ile gönder ·{' '}
        <kbd className="font-mono text-gray-400">Shift+Enter</kbd> ile yeni satır
        {visionEnabled && (
          <> · <kbd className="font-mono text-gray-400">Ctrl+V</kbd> ile ekran görüntüsü — yalnız bu mesajla gönderilir, saklanmaz</>
        )}
      </div>
    </div>
  )
}
