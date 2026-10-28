import { forwardRef, type ButtonHTMLAttributes, type ReactNode } from 'react'
import type { LucideIcon } from 'lucide-react'

/*
 * WorkspaceHeader `center` / `actions` slotlarında kullanılacak, bar tokenlarıyla stillenmiş parçalar.
 */

interface HeaderButtonProps extends ButtonHTMLAttributes<HTMLButtonElement> {
  /** primary: accent dolu · secondary: çerçeveli · icon: 34px kare (aria-label ZORUNLU). */
  variant?: 'primary' | 'secondary' | 'icon'
  icon?: LucideIcon
  children?: ReactNode
}

/** Bar üzerindeki buton. `<HeaderButton variant="primary" icon={Download}>XSLT İndir</HeaderButton>` */
export const HeaderButton = forwardRef<HTMLButtonElement, HeaderButtonProps>(function HeaderButton(
  { variant = 'secondary', icon: Icon, children, className = '', type = 'button', ...rest },
  ref,
) {
  const base =
    'xc-focus inline-flex h-[34px] items-center justify-center gap-1.5 rounded-md text-[13px] transition-colors disabled:cursor-not-allowed disabled:opacity-45'
  const look =
    variant === 'primary'
      ? 'px-3.5 bg-ui-accent text-ui-accent-fg font-semibold hover:brightness-110'
      : variant === 'icon'
        ? 'w-[34px] text-ui-bar-muted hover:text-ui-bar-fg hover:bg-ui-bar-line'
        : 'px-3 border border-ui-btn-line text-ui-bar-fg hover:bg-ui-bar-line'
  return (
    <button ref={ref} type={type} className={`${base} ${look} ${className}`} {...rest}>
      {Icon && <Icon size={variant === 'icon' ? 17 : 15} strokeWidth={variant === 'primary' ? 2 : 1.8} aria-hidden="true" />}
      {children}
    </button>
  )
})

/** Bar üzerindeki küçük çip (ör. "XSLT 2.0", belge türü). */
export function HeaderChip({ children, mono = false }: { children: ReactNode; mono?: boolean }) {
  return (
    <span className={`rounded-full bg-ui-chip px-2 py-0.5 text-[11px] text-ui-chip-fg ${mono ? 'font-mono' : ''}`}>
      {children}
    </span>
  )
}

/** Soluk durum metni (ör. "Kaydedilmemiş değişiklik"). aria-live ile duyurulur. */
export function HeaderStatus({ children }: { children: ReactNode }) {
  return (
    <span aria-live="polite" className="text-[12px] text-ui-bar-muted">
      {children}
    </span>
  )
}

/** Eylem grupları arasında dikey ayraç. */
export function HeaderDivider() {
  return <span aria-hidden="true" className="mx-1 h-[22px] w-px bg-ui-bar-line" />
}
