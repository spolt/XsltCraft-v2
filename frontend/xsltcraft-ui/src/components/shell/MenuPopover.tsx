import { useEffect, useLayoutEffect, useRef, type KeyboardEvent, type RefObject } from 'react'
import { createPortal } from 'react-dom'
import { Link } from 'react-router-dom'
import type { LucideIcon } from 'lucide-react'

export interface MenuPopoverItem {
  id: string
  label: string
  icon?: LucideIcon
  /** Verilirse öğe bir router Link'idir; seçilince menü kapanır. */
  to?: string
  /** Buton öğesi için eylem (to yoksa). Menü önce kapanır, sonra çağrılır. */
  onSelect?: () => void
  tone?: 'default' | 'danger'
}

export type MenuPlacement = 'bottom-start' | 'bottom-end' | 'right-start' | 'right-end' | 'top-start'

interface MenuPopoverProps {
  open: boolean
  onClose: () => void
  /** Menüyü açan buton; konum buna göre hesaplanır, kapanınca odak buna döner. */
  anchorRef: RefObject<HTMLElement | null>
  items: readonly MenuPopoverItem[]
  /** role=menu erişilebilir adı. */
  label: string
  placement?: MenuPlacement
  /** Anchor'ın aria-controls'u için. */
  id?: string
  /** Link öğelerine tıklanınca çağrılır (ör. kaydedilmemiş değişiklik koruması). Verilirse varsayılan gezinme engellenir. */
  onNavigate?: (path: string) => void
}

const GAP = 6

/**
 * Anchor'a bağlı, body'ye portal'lanan açılır menü (role=menu/menuitem).
 * ↑/↓/Home/End gezinme, Esc ve dış tıklamada kapanma, açılınca ilk öğeye odak.
 */
export default function MenuPopover({
  open,
  onClose,
  anchorRef,
  items,
  label,
  placement = 'bottom-end',
  id,
  onNavigate,
}: MenuPopoverProps) {
  const menuRef = useRef<HTMLDivElement>(null)

  // Konumlandırma: DOM'a doğrudan yazılır (render sırasında ref okumadan).
  useLayoutEffect(() => {
    if (!open) return
    const menu = menuRef.current
    const anchor = anchorRef.current
    if (!menu || !anchor) return
    const a = anchor.getBoundingClientRect()
    const m = menu.getBoundingClientRect()
    let top = 0
    let left = 0
    switch (placement) {
      case 'bottom-start': top = a.bottom + GAP; left = a.left; break
      case 'bottom-end': top = a.bottom + GAP; left = a.right - m.width; break
      case 'right-start': top = a.top; left = a.right + GAP; break
      case 'right-end': top = a.bottom - m.height; left = a.right + GAP; break
      case 'top-start': top = a.top - m.height - GAP; left = a.left; break
    }
    const vw = window.innerWidth
    const vh = window.innerHeight
    left = Math.max(8, Math.min(left, vw - m.width - 8))
    top = Math.max(8, Math.min(top, vh - m.height - 8))
    menu.style.top = `${top}px`
    menu.style.left = `${left}px`
    menu.style.visibility = 'visible'
    menu.querySelector<HTMLElement>('[role="menuitem"]')?.focus()
  }, [open, placement, anchorRef])

  useEffect(() => {
    if (!open) return
    const onDown = (e: MouseEvent) => {
      const t = e.target as Node
      if (menuRef.current?.contains(t) || anchorRef.current?.contains(t)) return
      onClose()
    }
    const onResize = () => onClose()
    document.addEventListener('mousedown', onDown)
    window.addEventListener('resize', onResize)
    return () => {
      document.removeEventListener('mousedown', onDown)
      window.removeEventListener('resize', onResize)
    }
  }, [open, onClose, anchorRef])

  if (!open) return null

  function closeAndRestore() {
    onClose()
    anchorRef.current?.focus()
  }

  function onKeyDown(e: KeyboardEvent<HTMLDivElement>) {
    const els = Array.from(menuRef.current?.querySelectorAll<HTMLElement>('[role="menuitem"]') ?? [])
    const idx = els.indexOf(document.activeElement as HTMLElement)
    if (e.key === 'Escape') {
      e.preventDefault()
      closeAndRestore()
    } else if (e.key === 'Tab') {
      onClose()
    } else if (e.key === 'ArrowDown' || e.key === 'ArrowUp' || e.key === 'Home' || e.key === 'End') {
      e.preventDefault()
      let next = 0
      if (e.key === 'ArrowDown') next = (idx + 1) % els.length
      else if (e.key === 'ArrowUp') next = (idx - 1 + els.length) % els.length
      else if (e.key === 'End') next = els.length - 1
      els[next]?.focus()
    }
  }

  const itemCls = (tone: MenuPopoverItem['tone']) =>
    `xc-focus flex h-9 w-full items-center gap-2.5 rounded-md px-2.5 text-left text-[13px] no-underline transition-colors ${
      tone === 'danger' ? 'text-fn-red-ink hover:bg-fn-red-tint focus:bg-fn-red-tint' : 'text-tx hover:bg-ui-hover focus:bg-ui-hover'
    }`

  return createPortal(
    <div
      ref={menuRef}
      id={id}
      role="menu"
      aria-label={label}
      onKeyDown={onKeyDown}
      style={{ position: 'fixed', top: 0, left: 0, visibility: 'hidden' }}
      className="z-[90] flex min-w-[200px] flex-col gap-0.5 rounded-[10px] border border-field-line bg-ui-card p-1.5 shadow-[var(--ui-shadow-pop)] outline-none"
    >
      {items.map((item) => {
        const Icon = item.icon
        const content = (
          <>
            {Icon && <Icon size={16} strokeWidth={1.8} aria-hidden="true" className="flex-none" />}
            <span className="flex-1">{item.label}</span>
          </>
        )
        if (item.to) {
          const to = item.to
          return (
            <Link
              key={item.id}
              to={to}
              role="menuitem"
              tabIndex={-1}
              className={itemCls(item.tone)}
              onClick={(e) => {
                if (onNavigate) {
                  e.preventDefault()
                  onClose()
                  onNavigate(to)
                } else {
                  onClose()
                }
              }}
            >
              {content}
            </Link>
          )
        }
        return (
          <button
            key={item.id}
            type="button"
            role="menuitem"
            tabIndex={-1}
            className={itemCls(item.tone)}
            onClick={() => {
              onClose()
              item.onSelect?.()
            }}
          >
            {content}
          </button>
        )
      })}
    </div>,
    document.body,
  )
}
