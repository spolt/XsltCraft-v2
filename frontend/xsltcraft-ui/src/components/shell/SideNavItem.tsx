import { forwardRef, type ButtonHTMLAttributes, type ReactNode } from 'react'
import { Link } from 'react-router-dom'
import { ChevronRight } from 'lucide-react'
import { fnStyle } from '../../theme/uiThemes'
import { NAV_ACTIVE_CLASS, type NavItem } from './navItems'

interface SideNavItemProps extends Omit<ButtonHTMLAttributes<HTMLButtonElement>, 'children'> {
  item: NavItem
  collapsed: boolean
  active: boolean
  /** Menü açan öğelerde sağda ok gösterilir (yalnız açık menüde). */
  hasMenu?: boolean
}

/** İkon kutusu: aktifte vurgu rengi; inaktifte nötr (soluk), hover/odakta öğenin kendi işlev rengine döner. */
const TILE_ACTIVE = 'bg-ui-card text-ui-accent shadow-[var(--ui-shadow-rest)]'
const TILE_IDLE =
  'bg-ui-hover/60 text-tx-muted group-hover:bg-(--fn-tint) group-hover:text-(--fn) group-focus-visible:bg-(--fn-tint) group-focus-visible:text-(--fn)'

/** Ana menü öğesi: `item.to` varsa Link, yoksa (menü açan) buton. Daraltılmış: 40px ikon kutusu + kısa etiket. */
const SideNavItem = forwardRef<HTMLButtonElement, SideNavItemProps>(function SideNavItem(
  { item, collapsed, active, hasMenu = false, className = '', ...buttonProps },
  ref,
) {
  const Icon = item.icon
  const tone = active ? NAV_ACTIVE_CLASS : 'text-tx-2 font-medium hover:bg-ui-hover hover:text-tx'
  const layout = collapsed
    ? 'flex-col gap-1 px-0.5 py-2 rounded-[10px] text-[10.5px] text-center leading-tight'
    : 'gap-3 h-[42px] px-2.5 rounded-lg text-[13.5px]'
  const cls = `group xc-focus relative flex w-full items-center no-underline transition-colors ${layout} ${tone} ${className}`

  const content: ReactNode = (
    <>
      <span
        aria-hidden="true"
        className={`inline-flex flex-none items-center justify-center transition-colors duration-150 ${
          collapsed ? 'h-10 w-10 rounded-[10px]' : 'h-[30px] w-[30px] rounded-[7px]'
        } ${active ? TILE_ACTIVE : TILE_IDLE}`}
      >
        <Icon size={collapsed ? 21 : 17} strokeWidth={1.8} />
      </span>
      <span className={collapsed ? '' : 'flex-1 text-left'}>{collapsed ? item.short : item.label}</span>
      {hasMenu && !collapsed && <ChevronRight size={14} aria-hidden="true" className="text-tx-subtle" />}
    </>
  )

  if (item.to) {
    return (
      <Link
        to={item.to}
        style={fnStyle(item.color)}
        className={cls}
        title={collapsed ? item.label : undefined}
        aria-label={collapsed ? item.label : undefined}
        aria-current={active ? 'page' : undefined}
      >
        {content}
      </Link>
    )
  }
  return (
    <button
      ref={ref}
      type="button"
      style={fnStyle(item.color)}
      className={cls}
      title={collapsed ? item.label : undefined}
      aria-label={collapsed ? item.label : undefined}
      {...buttonProps}
    >
      {content}
    </button>
  )
})

export default SideNavItem
