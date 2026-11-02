import { useRef, type KeyboardEvent } from 'react'
import type { LucideIcon } from 'lucide-react'

export interface SegmentedTabItem<T extends string> {
  id: T
  label: string
  icon?: LucideIcon
  /** Etiketin yanında küçük sayı rozeti. */
  count?: number
}

interface SegmentedTabsProps<T extends string> {
  items: readonly SegmentedTabItem<T>[]
  value: T
  onChange: (id: T) => void
  /** tablist'in erişilebilir adı. */
  ariaLabel: string
  /** 'pill' (varsayılan): gri zemin + beyaz seçili segment. 'underline': alt çizgili sekmeler. */
  variant?: 'pill' | 'underline'
  /** Verilirse her sekmeye aria-controls=`${idPrefix}-panel-${id}` ve id=`${idPrefix}-tab-${id}` eklenir. */
  idPrefix?: string
  className?: string
}

/**
 * Erişilebilir sekme grubu (role=tablist/tab, aria-selected, ←/→/Home/End ile gezinme).
 * Panel içeriğini çağıran çizer; idPrefix verilirse panelde `role="tabpanel"`,
 * `id={`${idPrefix}-panel-${id}`}`, `aria-labelledby={`${idPrefix}-tab-${id}`}` kullan.
 */
export default function SegmentedTabs<T extends string>({
  items,
  value,
  onChange,
  ariaLabel,
  variant = 'pill',
  idPrefix,
  className = '',
}: SegmentedTabsProps<T>) {
  const refs = useRef<(HTMLButtonElement | null)[]>([])

  function onKeyDown(e: KeyboardEvent<HTMLDivElement>) {
    const idx = items.findIndex((i) => i.id === value)
    let next = -1
    if (e.key === 'ArrowRight') next = (idx + 1) % items.length
    else if (e.key === 'ArrowLeft') next = (idx - 1 + items.length) % items.length
    else if (e.key === 'Home') next = 0
    else if (e.key === 'End') next = items.length - 1
    if (next < 0) return
    e.preventDefault()
    onChange(items[next].id)
    refs.current[next]?.focus()
  }

  const pill = variant === 'pill'
  return (
    <div
      role="tablist"
      aria-label={ariaLabel}
      onKeyDown={onKeyDown}
      className={
        pill
          ? `inline-flex items-center gap-1 rounded-lg bg-ui-hover p-1 ${className}`
          : `flex items-stretch gap-5 border-b border-ui-line ${className}`
      }
    >
      {items.map((item, i) => {
        const on = item.id === value
        const Icon = item.icon
        const base = pill
          ? `h-8 px-3 rounded-md text-[13px] ${on ? 'bg-ui-card text-tx font-semibold shadow-[var(--ui-shadow-rest)]' : 'text-tx-muted font-medium hover:text-tx'}`
          : `h-11 -mb-px text-[13px] border-b-2 ${on ? 'border-ui-accent text-tx font-semibold' : 'border-transparent text-tx-subtle font-normal hover:text-tx'}`
        return (
          <button
            key={item.id}
            ref={(el) => {
              refs.current[i] = el
            }}
            type="button"
            role="tab"
            id={idPrefix ? `${idPrefix}-tab-${item.id}` : undefined}
            aria-controls={idPrefix ? `${idPrefix}-panel-${item.id}` : undefined}
            aria-selected={on}
            tabIndex={on ? 0 : -1}
            onClick={() => onChange(item.id)}
            className={`xc-focus inline-flex items-center gap-1.5 whitespace-nowrap transition-colors ${base}`}
          >
            {Icon && <Icon size={15} strokeWidth={1.8} aria-hidden="true" />}
            {item.label}
            {item.count !== undefined && (
              <span className="rounded-full bg-ui-soft px-1.5 text-[11px] font-semibold text-ui-ink">{item.count}</span>
            )}
          </button>
        )
      })}
    </div>
  )
}
