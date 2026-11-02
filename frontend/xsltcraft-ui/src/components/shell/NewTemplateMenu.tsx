import { useCallback, useId, useRef, useState } from 'react'
import { FileText, Truck } from 'lucide-react'
import MenuPopover, { type MenuPopoverItem } from './MenuPopover'
import SideNavItem from './SideNavItem'
import { NEW_TEMPLATE_LINKS, type NavItem } from './navItems'

const ICONS = { invoice: FileText, despatch: Truck } as const

const MENU_ITEMS: MenuPopoverItem[] = NEW_TEMPLATE_LINKS.map((l) => ({
  id: l.id,
  label: l.label,
  to: l.to,
  icon: ICONS[l.id as keyof typeof ICONS],
}))

interface NewTemplateMenuProps {
  item: NavItem
  collapsed: boolean
  active: boolean
}

/** "Yeni Şablon" menü öğesi: e-Fatura/e-Arşiv → /editor/new, e-İrsaliye → /editor/new?type=despatch. */
export default function NewTemplateMenu({ item, collapsed, active }: NewTemplateMenuProps) {
  const [open, setOpen] = useState(false)
  const anchorRef = useRef<HTMLButtonElement>(null)
  const menuId = useId()
  const close = useCallback(() => setOpen(false), [])

  return (
    <>
      <SideNavItem
        ref={anchorRef}
        item={item}
        collapsed={collapsed}
        active={active}
        hasMenu
        aria-haspopup="menu"
        aria-expanded={open}
        aria-controls={open ? menuId : undefined}
        onClick={() => setOpen((o) => !o)}
      />
      <MenuPopover
        id={menuId}
        open={open}
        onClose={close}
        anchorRef={anchorRef}
        items={MENU_ITEMS}
        label="Yeni şablon türü"
        placement="right-start"
      />
    </>
  )
}
