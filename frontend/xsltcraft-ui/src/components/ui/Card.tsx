import type { CSSProperties, HTMLAttributes, ReactNode } from 'react'
import { fnStyle, type FnColor } from '../../theme/uiThemes'

const PADDING = { none: '', sm: 'p-4', md: 'p-5', lg: 'p-6' } as const

interface CardProps extends HTMLAttributes<HTMLElement> {
  as?: 'div' | 'section' | 'article' | 'aside'
  /** Hover'da kalkma (.xc-lift) + `group` (içteki IconTile dolar). */
  lift?: boolean
  /** İşlev rengi: --fn* değişkenlerini verir (IconTile ve hover kenarı bunu kullanır). */
  fn?: FnColor
  padding?: keyof typeof PADDING
  children?: ReactNode
}

/** Yüzey kartı: bg-ui-card + border-ui-line + rounded-xl. */
export default function Card({
  as: Tag = 'div',
  lift = false,
  fn,
  padding = 'lg',
  className = '',
  style,
  children,
  ...rest
}: CardProps) {
  const merged: CSSProperties | undefined = fn ? { ...fnStyle(fn), ...style } : style
  return (
    <Tag
      {...rest}
      style={merged}
      className={`rounded-xl border border-ui-line bg-ui-card ${lift ? 'xc-lift group' : 'shadow-[var(--ui-shadow-rest)]'} ${PADDING[padding]} ${className}`}
    >
      {children}
    </Tag>
  )
}
