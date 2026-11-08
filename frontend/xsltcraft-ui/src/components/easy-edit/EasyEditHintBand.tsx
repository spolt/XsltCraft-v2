import { ArrowRight, Sparkles, X } from 'lucide-react'

interface EasyEditHintBandProps {
  onSwitch: () => void
  onDismiss: () => void
  /** Geçiş hazırlanıyor (analiz sürüyor). */
  busy?: boolean
}

/** XSLT Editör'de, Kolay düzenle ile yapılabilecek işler için kapatılabilir öneri bandı. */
export default function EasyEditHintBand({ onSwitch, onDismiss, busy = false }: EasyEditHintBandProps) {
  return (
    <div role="note" className="flex flex-none items-center gap-2.5 border-b border-fn-blue-line bg-fn-blue-tint px-4 py-2 text-[12.5px] text-fn-blue-ink">
      <Sparkles size={14} aria-hidden="true" className="flex-none" />
      <p className="m-0 min-w-0 flex-1">
        Bu dosyada logo, imza, IBAN ve not alanları var. Bunlar için kod yazman gerekmez.
      </p>
      <button
        type="button"
        onClick={onSwitch}
        disabled={busy}
        className="xc-focus inline-flex h-[28px] flex-none items-center gap-1.5 rounded-md bg-ui-accent px-3 text-[12px] font-semibold text-ui-accent-fg hover:brightness-110 disabled:cursor-wait disabled:opacity-60"
      >
        {busy ? 'Hazırlanıyor…' : 'Kolay düzenle\'ye geç'}
        {!busy && <ArrowRight size={13} aria-hidden="true" />}
      </button>
      <button
        type="button"
        onClick={onDismiss}
        aria-label="Öneriyi kapat"
        title="Öneriyi kapat"
        className="xc-focus inline-flex h-[28px] w-[28px] flex-none items-center justify-center rounded-md hover:bg-ui-hover"
      >
        <X size={14} aria-hidden="true" />
      </button>
    </div>
  )
}
