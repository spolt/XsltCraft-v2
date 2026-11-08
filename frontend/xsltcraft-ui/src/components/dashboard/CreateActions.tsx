import { Link } from 'react-router-dom'
import { ArrowRight, FilePlus2, FolderOpen, LayoutTemplate, Truck, type LucideIcon } from 'lucide-react'

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
  'xc-focus group relative flex h-[60px] min-w-0 items-center gap-3 overflow-hidden rounded-xl border border-ui-line bg-ui-card pl-3 pr-4 text-left text-tx no-underline transition-colors duration-150 hover:border-field-line hover:bg-ui-hover'

/** Üst şerit: eşit ağırlıklı dört gezinme kartı — nötr ikon · başlık/ipucu · sağda soluk büyük ikon ve ok. */
export default function CreateActions() {
  return (
    <nav aria-label="Oluştur">
      <ul className="m-0 grid list-none grid-cols-1 gap-3 p-0 sm:grid-cols-2 xl:grid-cols-4">
        {ACTIONS.map(({ to, label, hint, icon: Icon }) => (
          <li key={to} className="min-w-0">
            <Link to={to} className={ITEM}>
              <span
                aria-hidden="true"
                className="flex h-9 w-9 flex-none items-center justify-center rounded-lg bg-ui-side text-tx-muted transition-colors duration-150 group-hover:text-tx"
              >
                <Icon size={17} strokeWidth={1.75} />
              </span>
              <span className="relative flex min-w-0 flex-1 flex-col gap-0.5">
                <span className="truncate text-[13.5px] font-semibold leading-tight text-tx">{label}</span>
                <span className="truncate text-[12px] leading-tight text-tx-muted">{hint}</span>
              </span>
              <Icon
                aria-hidden="true"
                size={76}
                strokeWidth={1}
                className="pointer-events-none absolute top-1/2 -right-4 -translate-y-1/2 text-tx opacity-[0.045] transition-[opacity,translate] duration-200 group-hover:-translate-x-1 group-hover:opacity-[0.08]"
              />
              <ArrowRight
                aria-hidden="true"
                size={16}
                strokeWidth={2}
                className="relative flex-none text-tx-subtle transition-[color,translate] duration-150 group-hover:translate-x-0.5 group-hover:text-tx"
              />
            </Link>
          </li>
        ))}
      </ul>
    </nav>
  )
}
