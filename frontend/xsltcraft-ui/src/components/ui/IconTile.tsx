import type { LucideIcon } from 'lucide-react'
import { fnStyle, type FnColor } from '../../theme/uiThemes'

const SIZES = {
  xs: { box: 'w-[22px] h-[22px] rounded-md', icon: 13 },
  sm: { box: 'w-[26px] h-[26px] rounded-md', icon: 15 },
  md: { box: 'w-[30px] h-[30px] rounded-[7px]', icon: 17 },
  lg: { box: 'w-11 h-11 rounded-[11px]', icon: 24 },
  xl: { box: 'w-14 h-14 rounded-[14px]', icon: 28 },
} as const

export type IconTileSize = keyof typeof SIZES

interface IconTileProps {
  icon: LucideIcon
  /** İşlev rengi. Verilmezse üst öğedeki `--fn*` değişkenleri (ör. Card fn=) kullanılır. */
  color?: FnColor
  /** xs 22 · sm 26 · md 30 · lg 44 · xl 56 px. Varsayılan md. */
  size?: IconTileSize
  /** true → her zaman dolu (renk zemin + beyaz ikon), ör. aktif menü öğesi. */
  filled?: boolean
  /** true (varsayılan) → en yakın `group` üzerine gelinince dolar. */
  hoverFill?: boolean
  className?: string
}

/**
 * Renkli ikon kutusu: dinlenmede tint zemin + renkli ikon, hover/aktifte dolu.
 * Kart/satır kapsayıcısına `group` sınıfı ver ki hover dolumu çalışsın.
 */
export default function IconTile({
  icon: Icon,
  color,
  size = 'md',
  filled = false,
  hoverFill = true,
  className = '',
}: IconTileProps) {
  const s = SIZES[size]
  const tone = filled
    ? 'bg-(--fn) text-white'
    : `bg-(--fn-tint) text-(--fn) ${hoverFill ? 'group-hover:bg-(--fn) group-hover:text-white group-focus-visible:bg-(--fn) group-focus-visible:text-white' : ''}`
  return (
    <span
      aria-hidden="true"
      style={color ? fnStyle(color) : undefined}
      className={`inline-flex flex-none items-center justify-center transition-colors duration-150 ${s.box} ${tone} ${className}`}
    >
      <Icon size={s.icon} strokeWidth={1.8} />
    </span>
  )
}
