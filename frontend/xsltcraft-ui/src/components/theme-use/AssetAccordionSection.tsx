import { useId, type ReactNode } from 'react'
import { ChevronDown, ChevronUp, type LucideIcon } from 'lucide-react'

interface Props {
  title: string
  icon: LucideIcon
  /** Sağdaki kısa durum ("Eklendi", "Boş", "2 hesap"). */
  status: string
  /** true → olumlu (yeşil) durum rengi. */
  done: boolean
  open: boolean
  onToggle: () => void
  children: ReactNode
}

/** Tema Kullan sol panelindeki açılır bölüm (aria-expanded + aria-controls). */
export default function AssetAccordionSection({ title, icon: Icon, status, done, open, onToggle, children }: Props) {
  const bodyId = useId()
  const Chevron = open ? ChevronUp : ChevronDown
  return (
    <section className="border-b border-ui-line">
      <button
        type="button"
        aria-expanded={open}
        aria-controls={bodyId}
        onClick={onToggle}
        className="flex h-12 w-full items-center gap-2.5 px-4 text-left text-[13px] font-semibold text-tx transition-colors hover:bg-ui-hover focus-visible:outline-2 focus-visible:-outline-offset-2 focus-visible:outline-ui-accent"
      >
        <Icon size={16} strokeWidth={1.8} aria-hidden="true" className="text-ui-accent" />
        <span className="flex-1">{title}</span>
        <span className={`text-[11px] font-medium ${done ? 'text-fn-green-ink' : 'text-tx-subtle'}`}>{status}</span>
        <Chevron size={14} aria-hidden="true" className="text-tx-subtle" />
      </button>
      <div id={bodyId} hidden={!open} className="px-4 pb-4">
        {children}
      </div>
    </section>
  )
}
