import { Loader2, X } from 'lucide-react'
import type { PreparedImage } from '../../utils/imageAttachment'

interface Props {
  attachments: PreparedImage[]
  processing: boolean
  onRemove: (id: string) => void
  disabled?: boolean
}

/** Gönderilmeyi bekleyen ekran görüntüleri — yazma alanının üstünde küçük önizlemeler. */
export default function ChatAttachmentBar({ attachments, processing, onRemove, disabled }: Props) {
  if (attachments.length === 0 && !processing) return null

  return (
    <ul className="flex gap-2 flex-wrap" aria-label="Eklenen ekran görüntüleri">
      {attachments.map((a, i) => (
        <li key={a.id} className="relative group">
          <img
            src={a.previewUrl}
            alt={`Ekran görüntüsü ${i + 1} (${a.width}×${a.height})`}
            className="h-14 w-20 object-cover rounded border border-field-line bg-ui-side"
          />
          <button
            type="button"
            onClick={() => onRemove(a.id)}
            disabled={disabled}
            aria-label={`Ekran görüntüsü ${i + 1}'i kaldır`}
            title="Kaldır"
            className="absolute -top-1.5 -right-1.5 h-5 w-5 flex items-center justify-center rounded-full bg-ui-card border border-field-line text-tx-2 hover:text-white hover:bg-fn-red disabled:opacity-40 transition-colors"
          >
            <X size={11} />
          </button>
        </li>
      ))}
      {processing && (
        <li
          className="h-14 w-20 flex items-center justify-center rounded border border-dashed border-field-line text-tx-subtle"
          aria-label="Görsel hazırlanıyor"
        >
          <Loader2 size={16} className="animate-spin" />
        </li>
      )}
    </ul>
  )
}
