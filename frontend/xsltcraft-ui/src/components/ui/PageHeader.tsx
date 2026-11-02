import type { ReactNode } from 'react'
import type { LucideIcon } from 'lucide-react'
import type { FnColor } from '../../theme/uiThemes'
import IconTile from './IconTile'

interface PageHeaderProps {
  title: ReactNode
  description?: ReactNode
  /** Başlığın solunda IconTile (md, dolu değil). */
  icon?: LucideIcon
  iconColor?: FnColor
  /** Sağda hizalanan eylemler (butonlar). */
  actions?: ReactNode
  /** Başlığın altında (ör. SegmentedTabs). */
  children?: ReactNode
  className?: string
}

/** Sayfa başlığı: h1 (22px/600) + açıklama + sağda eylemler. AppShell sayfaları için. */
export default function PageHeader({
  title,
  description,
  icon,
  iconColor = 'accent',
  actions,
  children,
  className = '',
}: PageHeaderProps) {
  return (
    <div className={`flex flex-col gap-4 ${className}`}>
      <div className="flex flex-wrap items-end justify-between gap-3">
        <div className="flex min-w-0 items-center gap-3">
          {icon && <IconTile icon={icon} color={iconColor} size="lg" hoverFill={false} />}
          <div className="min-w-0">
            <h1 className="m-0 text-[22px] font-semibold text-tx">{title}</h1>
            {description && <p className="mt-1 mb-0 text-tx-muted">{description}</p>}
          </div>
        </div>
        {actions && <div className="flex flex-wrap items-center gap-2">{actions}</div>}
      </div>
      {children}
    </div>
  )
}
