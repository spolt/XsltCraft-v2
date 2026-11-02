import { useEffect, useId, useRef, useState } from 'react'
import { Check, Info } from 'lucide-react'

/** Gündelik dilde yetenek + soluk teknik karşılığı (karar veren için jargonsuz, geliştirici için net). */
const ITEMS: { label: string; tech: string }[] = [
  { label: 'Fatura ve irsaliye şablonlarını düzenleme', tech: 'XSLT 1.0 / 2.0' },
  { label: 'Yazarken anında önizleme', tech: 'Saxon-HE motoru' },
  { label: 'GİB kurallarına uygunluk kontrolü', tech: 'UBL-TR 2.1' },
  { label: 'Yapay zekâ destekli kod yardımı', tech: 'AI asistan' },
]

/** "Neler destekleniyor?" düğmesi + açılır kutu (tıkla/Enter ile açılır; Esc ya da dışarı tıklama kapatır). */
export default function SupportInfo() {
  const [open, setOpen] = useState(false)
  const rootRef = useRef<HTMLDivElement>(null)
  const buttonRef = useRef<HTMLButtonElement>(null)
  const panelId = useId()

  useEffect(() => {
    if (!open) return
    function onPointerDown(e: PointerEvent) {
      if (!rootRef.current?.contains(e.target as Node)) setOpen(false)
    }
    function onKeyDown(e: KeyboardEvent) {
      if (e.key !== 'Escape') return
      setOpen(false)
      buttonRef.current?.focus()
    }
    document.addEventListener('pointerdown', onPointerDown)
    document.addEventListener('keydown', onKeyDown)
    return () => {
      document.removeEventListener('pointerdown', onPointerDown)
      document.removeEventListener('keydown', onKeyDown)
    }
  }, [open])

  return (
    <div ref={rootRef} className="relative flex-none">
      <button
        ref={buttonRef}
        type="button"
        aria-expanded={open}
        aria-controls={panelId}
        onClick={() => setOpen((v) => !v)}
        className="xc-focus inline-flex h-8 cursor-pointer items-center gap-1.5 rounded-md px-2 text-[12.5px] font-medium text-ui-workspace-muted transition-colors hover:bg-ui-workspace-raised hover:text-ui-workspace-fg"
      >
        <Info size={14} strokeWidth={2} aria-hidden="true" />
        Neler destekleniyor?
      </button>
      {open && (
        <div
          id={panelId}
          role="region"
          aria-label="Desteklenen özellikler"
          className="absolute top-full left-0 z-20 mt-2 w-[320px] rounded-lg border border-ui-workspace-line bg-ui-workspace-raised p-3.5 shadow-[var(--ui-shadow-pop)]"
        >
          <ul className="m-0 flex list-none flex-col gap-2.5 p-0">
            {ITEMS.map((item) => (
              <li key={item.label} className="flex items-start gap-2.5">
                <Check size={15} strokeWidth={2.2} aria-hidden="true" className="mt-0.5 flex-none text-ui-workspace-fg" />
                <span className="flex flex-col">
                  <span className="text-[13px] leading-snug text-ui-workspace-fg">{item.label}</span>
                  <span className="text-[11.5px] leading-snug text-ui-workspace-muted">{item.tech}</span>
                </span>
              </li>
            ))}
          </ul>
        </div>
      )}
    </div>
  )
}
