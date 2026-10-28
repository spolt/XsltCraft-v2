import { useEffect, type ReactNode } from 'react'
import { AlertCircle, X } from 'lucide-react'
import { fnStyle, type FnColor } from '../../theme/uiThemes'

/* Admin sayfalarının ortak stil parçaları (yalnız token renkleri). */

export const BTN_PRIMARY =
  'xc-focus inline-flex h-10 items-center justify-center gap-2 rounded-lg bg-ui-accent px-4 text-[13.5px] font-semibold text-ui-accent-fg transition-opacity hover:opacity-90 disabled:cursor-not-allowed disabled:opacity-50'
export const BTN_SECONDARY =
  'xc-focus inline-flex h-9 items-center justify-center gap-1.5 rounded-lg border border-ui-line bg-ui-card px-3 text-[12.5px] font-medium text-tx-2 transition-colors hover:bg-ui-hover disabled:cursor-not-allowed disabled:opacity-50'
export const BTN_DANGER =
  'xc-focus inline-flex h-10 items-center justify-center gap-2 rounded-lg bg-fn-red px-4 text-[13.5px] font-semibold text-on-fn transition-opacity hover:opacity-90 disabled:cursor-not-allowed disabled:opacity-50'
export const BTN_DANGER_SOFT =
  'xc-focus inline-flex h-9 items-center justify-center gap-1.5 rounded-lg border border-fn-red-line bg-fn-red-tint px-3 text-[12.5px] font-semibold text-fn-red-ink transition-opacity hover:opacity-90 disabled:cursor-not-allowed disabled:opacity-50'
export const BTN_ICON =
  'xc-focus inline-flex h-8 w-8 items-center justify-center rounded-md text-tx-muted transition-colors hover:bg-ui-hover hover:text-tx disabled:cursor-not-allowed disabled:opacity-40'
export const BTN_ICON_DANGER =
  'xc-focus inline-flex h-8 w-8 items-center justify-center rounded-md text-fn-red-ink transition-colors hover:bg-fn-red-tint disabled:cursor-not-allowed disabled:opacity-40'

export const INPUT =
  'h-9 w-full rounded-lg border border-field-line bg-ui-card px-3 text-[13px] text-tx outline-none placeholder:text-tx-subtle focus:border-ui-accent focus:ring-2 focus:ring-ui-soft-line disabled:opacity-60'
export const TEXTAREA =
  'w-full rounded-lg border border-field-line bg-ui-card px-3 py-2 text-[13px] text-tx outline-none placeholder:text-tx-subtle focus:border-ui-accent focus:ring-2 focus:ring-ui-soft-line'
export const LABEL = 'flex flex-col gap-1.5 text-[12.5px] font-medium text-tx-2'

export const TABLE = 'w-full border-collapse text-[13px]'
export const TH = 'px-3.5 py-2.5 text-left text-[12.5px] font-medium text-tx-muted whitespace-nowrap'
export const TD = 'px-3.5 py-2.5 align-middle'
export const TR = 'border-t border-ui-line transition-colors hover:bg-ui-hover'

/** Sayfa gövdesi (AdminLayout başlığının altında). */
export function AdminPage({ children }: { children: ReactNode }) {
  return <div className="flex flex-col gap-4 px-8 pt-5 pb-9">{children}</div>
}

/** Kart başlığı satırı (h2 + sağda ek içerik). */
export function SectionHeader({ id, title, aside, description }: {
  id?: string
  title: ReactNode
  aside?: ReactNode
  description?: ReactNode
}) {
  return (
    <div className="flex flex-wrap items-center justify-between gap-3 border-b border-ui-line px-4 py-3.5">
      <div className="min-w-0">
        <h2 id={id} className="m-0 text-[15px] font-semibold text-tx">{title}</h2>
        {description && <p className="mt-0.5 mb-0 text-[12.5px] text-tx-muted">{description}</p>}
      </div>
      {aside && <div className="flex items-center gap-2">{aside}</div>}
    </div>
  )
}

/** İşlev renkli küçük rozet. */
export function Pill({ tone, children, className = '' }: { tone: FnColor; children: ReactNode; className?: string }) {
  return (
    <span
      style={fnStyle(tone)}
      className={`inline-flex items-center gap-1 rounded-full bg-(--fn-tint) px-2.5 py-0.5 text-[11.5px] font-semibold whitespace-nowrap text-(--fn-ink) ${className}`}
    >
      {children}
    </span>
  )
}

/** Hata/uyarı bandı. */
export function Banner({ tone = 'red', children }: { tone?: 'red' | 'orange'; children: ReactNode }) {
  return (
    <div
      role={tone === 'red' ? 'alert' : undefined}
      style={fnStyle(tone)}
      className="flex items-start gap-2 rounded-lg border border-(--fn-line) bg-(--fn-tint) px-3 py-2 text-[12.5px] text-(--fn-ink)"
    >
      <AlertCircle size={14} className="mt-0.5 flex-none" aria-hidden="true" />
      <span className="min-w-0">{children}</span>
    </div>
  )
}

/** aria-pressed segment grubu (ör. rol, plan, kapsam seçimi). */
export function SegmentGroup<T extends string>({ label, options, value, onChange, disabled }: {
  label: string
  options: readonly { value: T; label: ReactNode }[]
  value: T
  onChange: (v: T) => void
  disabled?: boolean
}) {
  return (
    <div
      role="group"
      aria-label={label}
      className="grid gap-0.5 rounded-lg bg-ui-hover p-[3px]"
      style={{ gridTemplateColumns: `repeat(${options.length}, minmax(0, 1fr))` }}
    >
      {options.map((o) => {
        const on = o.value === value
        return (
          <button
            key={o.value}
            type="button"
            aria-pressed={on}
            disabled={disabled}
            onClick={() => onChange(o.value)}
            className={`xc-focus inline-flex h-[30px] items-center justify-center gap-1.5 rounded-md text-[12.5px] transition-colors disabled:opacity-50 ${
              on ? 'bg-ui-card font-semibold text-tx shadow-[var(--ui-shadow-rest)]' : 'font-medium text-tx-muted hover:text-tx'
            }`}
          >
            {o.label}
          </button>
        )
      })}
    </div>
  )
}

/** Diyalog: role=dialog, Esc ile kapanır, arka plana tıklayınca kapanır. */
export function Modal({ titleId, title, description, onClose, children, footer, maxWidth = 'max-w-sm' }: {
  titleId: string
  title: ReactNode
  description?: ReactNode
  onClose: () => void
  children?: ReactNode
  footer?: ReactNode
  maxWidth?: string
}) {
  useEffect(() => {
    const onKey = (e: KeyboardEvent) => { if (e.key === 'Escape') onClose() }
    document.addEventListener('keydown', onKey)
    return () => document.removeEventListener('keydown', onKey)
  }, [onClose])

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/45 p-4" onMouseDown={(e) => { if (e.target === e.currentTarget) onClose() }}>
      <div
        role="dialog"
        aria-modal="true"
        aria-labelledby={titleId}
        className={`flex max-h-[90vh] w-full ${maxWidth} flex-col rounded-xl border border-ui-line bg-ui-card shadow-[var(--ui-shadow-pop)]`}
      >
        <div className="flex items-start justify-between gap-3 px-6 pt-5 pb-3">
          <div className="min-w-0">
            <h3 id={titleId} className="m-0 text-[16px] font-semibold text-tx">{title}</h3>
            {description && <p className="mt-1 mb-0 text-[13px] text-tx-muted">{description}</p>}
          </div>
          <button type="button" onClick={onClose} aria-label="Kapat" className={BTN_ICON}>
            <X size={17} aria-hidden="true" />
          </button>
        </div>
        <div className="flex flex-col gap-3 overflow-y-auto px-6 pb-4">{children}</div>
        {footer && <div className="flex flex-wrap justify-end gap-2 border-t border-ui-line px-6 py-4">{footer}</div>}
      </div>
    </div>
  )
}
