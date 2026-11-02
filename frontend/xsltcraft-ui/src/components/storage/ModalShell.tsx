import { useEffect, useId, type ReactNode } from 'react'
import { X } from 'lucide-react'

interface Props {
  title: ReactNode
  subtitle?: ReactNode
  onClose: () => void
  /** Tailwind max-w sınıfı (varsayılan max-w-md). */
  width?: string
  children: ReactNode
}

/** Depolama diyaloglarının ortak kabuğu: perde + kart, role=dialog, Esc ile kapanır. */
export default function ModalShell({ title, subtitle, onClose, width = 'max-w-md', children }: Props) {
  const titleId = useId()
  useEffect(() => {
    function onKey(e: KeyboardEvent) {
      if (e.key === 'Escape') onClose()
    }
    document.addEventListener('keydown', onKey)
    return () => document.removeEventListener('keydown', onKey)
  }, [onClose])

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/60" onClick={onClose}>
      <div
        role="dialog"
        aria-modal="true"
        aria-labelledby={titleId}
        className={`mx-4 w-full ${width} rounded-xl border border-ui-line bg-ui-card p-6 text-tx shadow-[var(--ui-shadow-pop)]`}
        onClick={(e) => e.stopPropagation()}
      >
        <div className="mb-1 flex items-center justify-between gap-3">
          <h2 id={titleId} className="m-0 flex items-center gap-2 text-lg font-semibold text-tx">{title}</h2>
          <button
            type="button"
            onClick={onClose}
            aria-label="Kapat"
            className="xc-focus inline-flex h-8 w-8 items-center justify-center rounded-md text-tx-subtle transition-colors hover:bg-ui-hover hover:text-tx"
          >
            <X size={18} aria-hidden="true" />
          </button>
        </div>
        {subtitle && <p className="mt-0 mb-4 text-[13px] text-tx-muted">{subtitle}</p>}
        {children}
      </div>
    </div>
  )
}

/** Diyalog butonları. */
export const MODAL_PRIMARY =
  'xc-focus inline-flex items-center gap-1.5 rounded-lg bg-ui-accent px-4 py-2 text-[13px] font-semibold text-ui-accent-fg transition-[filter] hover:brightness-110 disabled:cursor-not-allowed disabled:opacity-50'
export const MODAL_SECONDARY =
  'xc-focus rounded-lg px-4 py-2 text-[13px] font-medium text-tx-2 transition-colors hover:bg-ui-hover'
export const MODAL_INPUT =
  'w-full rounded-lg border border-field-line bg-ui-card px-3 py-2 text-[13px] text-tx outline-none focus:border-ui-accent focus:ring-2 focus:ring-ui-soft-line'
