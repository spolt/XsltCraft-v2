import { Link } from 'react-router-dom'
import { FilePlus2, FolderOpen, LayoutTemplate, Truck, type LucideIcon } from 'lucide-react'

interface Action {
  to: string
  label: string
  hint: string
  icon: LucideIcon
}

const ACTIONS: Action[] = [
  { to: '/editor/new', label: 'Yeni e-Fatura / e-Arşiv', hint: 'Boş A4 tasarım alanı', icon: FilePlus2 },
  { to: '/editor/new?type=despatch', label: 'Yeni e-İrsaliye', hint: 'Boş A4 tasarım alanı', icon: Truck },
  { to: '/templates?type=Invoice', label: 'Temadan başla', hint: 'Tema kütüphanesi', icon: LayoutTemplate },
  { to: '/my-xslt-templates', label: 'Şablonlarım', hint: 'Kayıtlı XSLT şablonları', icon: FolderOpen },
]

const ITEM =
  'xc-focus flex h-14 min-w-0 items-center gap-3 rounded-lg border border-ui-line bg-ui-card px-3.5 text-left text-tx no-underline transition-colors hover:border-field-line hover:bg-ui-hover'

/** Üst şerit: eşit ağırlıklı dört aksiyon. */
export default function CreateActions() {
  return (
    <nav aria-label="Oluştur">
      <ul className="m-0 grid list-none grid-cols-1 gap-2.5 p-0 sm:grid-cols-2 xl:grid-cols-4">
        {ACTIONS.map(({ to, label, hint, icon: Icon }) => (
          <li key={to} className="min-w-0">
            <Link to={to} className={ITEM}>
              <Icon size={18} strokeWidth={1.75} aria-hidden="true" className="flex-none text-tx-muted" />
              <span className="flex min-w-0 flex-col">
                <span className="truncate text-[13.5px] font-semibold leading-tight">{label}</span>
                <span className="truncate text-[12px] leading-tight text-tx-muted">{hint}</span>
              </span>
            </Link>
          </li>
        ))}
      </ul>
    </nav>
  )
}
