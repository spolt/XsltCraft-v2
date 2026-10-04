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
    <ul className="flex gap-2 mb-2 flex-wrap" aria-label="Eklenen ekran görüntüleri">
      {attachments.map((a, i) => (
        <li key={a.id} className="relative group">
          <img
            src={a.previewUrl}
            alt={`Ekran görüntüsü ${i + 1} (${a.width}×${a.height})`}
            className="h-14 w-20 object-cover rounded border border-gray-600 bg-gray-800"
          />
          <button
            type="button"
            onClick={() => onRemove(a.id)}
            disabled={disabled}
            aria-label={`Ekran görüntüsü ${i + 1}'i kaldır`}
            title="Kaldır"
            className="absolute -top-1.5 -right-1.5 h-5 w-5 flex items-center justify-center rounded-full bg-gray-900 border border-gray-600 text-gray-300 hover:text-white hover:bg-rose-700 disabled:opacity-40 transition-colors"
          >
            <X size={11} />
          </button>
        </li>
      ))}
      {processing && (
        <li
          className="h-14 w-20 flex items-center justify-center rounded border border-dashed border-gray-600 text-gray-500"
          aria-label="Görsel hazırlanıyor"
        >
          <Loader2 size={16} className="animate-spin" />
        </li>
      )}
    </ul>
  )
}
