import { useEffect, useRef, useState } from 'react'
import { ImagePlus, X } from 'lucide-react'
import type { PreparedImage } from '../../utils/imageAttachment'

interface Props {
  images: PreparedImage[]
  /** Görselleri yazma alanına tekrar ekler (görseller sonraki turlarda modele gitmez). */
  onReattach?: () => void
}

/** Kullanıcı mesaj balonundaki ekran görüntüleri + tıklayınca büyütme. */
export default function ChatImageGrid({ images, onReattach }: Props) {
  const [open, setOpen] = useState<PreparedImage | null>(null)

  return (
    <div className="mb-1.5">
      <div className="flex gap-1.5 flex-wrap justify-end">
        {images.map((img, i) => (
          <button
            key={img.id}
            type="button"
            onClick={() => setOpen(img)}
            aria-label={`Ekran görüntüsü ${i + 1}'i büyüt`}
            className="rounded overflow-hidden border border-violet-400/40 hover:border-violet-200 transition-colors"
          >
            <img
              src={img.previewUrl}
              alt={`Ekran görüntüsü ${i + 1}`}
              className="h-20 max-w-[10rem] object-cover bg-gray-800"
            />
          </button>
        ))}
      </div>
      {onReattach && (
        <button
          type="button"
          onClick={onReattach}
          className="mt-1 ml-auto flex items-center gap-1 text-[10px] text-violet-200/80 hover:text-white"
          title="Görseller yalnız bu mesajla modele gönderildi; yeniden sormak için tekrar ekleyin"
        >
          <ImagePlus size={10} /> Tekrar ekle
        </button>
      )}
      {open && <Lightbox image={open} onClose={() => setOpen(null)} />}
    </div>
  )
}

function Lightbox({ image, onClose }: { image: PreparedImage; onClose: () => void }) {
  const closeRef = useRef<HTMLButtonElement>(null)

  useEffect(() => {
    const previous = document.activeElement as HTMLElement | null
    closeRef.current?.focus()
    const onKey = (e: KeyboardEvent) => { if (e.key === 'Escape') onClose() }
    window.addEventListener('keydown', onKey)
    return () => {
      window.removeEventListener('keydown', onKey)
      previous?.focus()
    }
  }, [onClose])

  return (
    <div
      role="dialog"
      aria-modal="true"
      aria-label="Ekran görüntüsü"
      onClick={onClose}
      className="fixed inset-0 z-50 bg-black/80 flex items-center justify-center p-6"
    >
      <button
        ref={closeRef}
        type="button"
        onClick={onClose}
        aria-label="Kapat"
        className="absolute top-4 right-4 h-8 w-8 flex items-center justify-center rounded-full bg-gray-800 text-gray-200 hover:bg-gray-700"
      >
        <X size={16} />
      </button>
      <img
        src={image.previewUrl}
        alt={`Ekran görüntüsü (${image.width}×${image.height})`}
        onClick={e => e.stopPropagation()}
        className="max-h-full max-w-full rounded shadow-2xl"
      />
    </div>
  )
}
