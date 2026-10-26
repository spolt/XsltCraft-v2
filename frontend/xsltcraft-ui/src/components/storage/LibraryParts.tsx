import { useState, type ButtonHTMLAttributes, type ReactNode } from 'react'
import { Check, Star, X, type LucideIcon } from 'lucide-react'
import { folderDot } from './folderColors'

/*
 * Taslaklarım / Şablonlarım tablolarının ortak, yalnız görünüm parçaları.
 * Davranış (servis çağrıları, durum) sayfalarda kalır.
 */

/** Liste durum mesajı (yükleniyor / hata). */
export function LibraryStatus({ tone = 'muted', children }: { tone?: 'muted' | 'error'; children: ReactNode }) {
  return (
    <div className="flex items-center justify-center py-24">
      <p role={tone === 'error' ? 'alert' : 'status'} className={`text-sm ${tone === 'error' ? 'text-fn-red-ink' : 'text-tx-subtle'}`}>
        {children}
      </p>
    </div>
  )
}

interface IconActionProps extends ButtonHTMLAttributes<HTMLButtonElement> {
  icon: LucideIcon
  label: string
  tone?: 'default' | 'danger'
}

/** Satır işlemi: 28px ikon buton (aria-label + title). */
export function IconAction({ icon: Icon, label, tone = 'default', className = '', ...rest }: IconActionProps) {
  const look = tone === 'danger' ? 'hover:bg-fn-red-tint hover:text-fn-red-ink' : 'hover:bg-ui-hover hover:text-tx'
  return (
    <button
      type="button"
      aria-label={label}
      title={label}
      className={`xc-focus inline-flex h-7 w-7 flex-none items-center justify-center rounded-md text-tx-muted transition-colors ${look} ${className}`}
      {...rest}
    >
      <Icon size={15} strokeWidth={1.7} aria-hidden="true" />
    </button>
  )
}

/** Favori yıldızı (aria-pressed). */
export function FavoriteToggle({ on, onToggle }: { on: boolean; onToggle: () => void }) {
  return (
    <button
      type="button"
      onClick={onToggle}
      aria-pressed={on}
      aria-label={on ? 'Favorilerden çıkar' : 'Favorilere ekle'}
      title={on ? 'Favorilerden çıkar' : 'Favorilere ekle'}
      className={`xc-focus inline-flex h-7 w-7 flex-none items-center justify-center rounded-md transition-colors hover:bg-fn-orange-tint ${on ? 'text-fn-orange' : 'text-tx-subtle hover:text-fn-orange'}`}
    >
      <Star size={15} strokeWidth={1.7} fill={on ? 'currentColor' : 'none'} aria-hidden="true" />
    </button>
  )
}

/** Satır içi yeniden adlandırma alanı (Enter kaydet, Esc vazgeç, odaktan çıkınca kaydet). */
export function RenameField({ initial, onCommit, onCancel, mono = false }: {
  initial: string
  onCommit: (value: string) => void
  onCancel: () => void
  mono?: boolean
}) {
  const [value, setValue] = useState(initial)
  return (
    <div className="flex min-w-0 flex-1 items-center gap-1">
      <input
        autoFocus
        aria-label="Yeni ad"
        value={value}
        onChange={(e) => setValue(e.target.value)}
        onBlur={() => onCommit(value)}
        onKeyDown={(e) => {
          if (e.key === 'Enter') onCommit(value)
          if (e.key === 'Escape') onCancel()
        }}
        className={`min-w-0 flex-1 rounded-md border border-ui-soft-line bg-ui-card px-2 py-0.5 text-[13px] text-tx outline-none focus:ring-2 focus:ring-ui-soft-line ${mono ? 'font-mono' : ''}`}
      />
      <button type="button" onMouseDown={(e) => e.preventDefault()} onClick={() => onCommit(value)} aria-label="Adı kaydet" className="xc-focus inline-flex h-6 w-6 items-center justify-center rounded text-fn-green-ink hover:bg-fn-green-tint">
        <Check size={14} aria-hidden="true" />
      </button>
      <button type="button" onMouseDown={(e) => e.preventDefault()} onClick={onCancel} aria-label="Vazgeç" className="xc-focus inline-flex h-6 w-6 items-center justify-center rounded text-tx-subtle hover:bg-ui-hover">
        <X size={14} aria-hidden="true" />
      </button>
    </div>
  )
}

/** Satır içi silme onayı: "Emin misin?" Evet / Hayır. */
export function DeleteConfirm({ onYes, onNo }: { onYes: () => void; onNo: () => void }) {
  return (
    <div role="group" aria-label="Silme onayı" className="flex items-center justify-end gap-1.5">
      <span className="text-xs font-medium text-fn-red-ink">Emin misin?</span>
      <button type="button" onClick={onYes} className="xc-focus h-7 rounded-md bg-fn-red px-2.5 text-xs font-semibold text-white hover:bg-fn-red-ink">Evet</button>
      <button type="button" onClick={onNo} className="xc-focus h-7 rounded-md border border-ui-line bg-ui-card px-2.5 text-xs text-tx-2 hover:bg-ui-hover">Hayır</button>
    </div>
  )
}

/** Toplu işlem şeridi (seçim varken). */
export function BulkBar({ count, children }: { count: number; children: ReactNode }) {
  return (
    <div role="region" aria-label="Toplu işlemler" className="flex flex-wrap items-center gap-2 border-b border-ui-soft-line bg-ui-soft px-3.5 py-2.5">
      <span className="flex-1 text-[13px] font-semibold text-ui-ink" aria-live="polite">{count} şablon seçildi</span>
      {children}
    </div>
  )
}

/** Toplu şeritte ikincil buton. */
export function BulkButton({ icon: Icon, children, ...rest }: ButtonHTMLAttributes<HTMLButtonElement> & { icon: LucideIcon }) {
  return (
    <button
      type="button"
      className="xc-focus inline-flex h-8 items-center gap-1.5 rounded-md px-3 text-[13px] font-medium text-ui-ink transition-colors hover:bg-ui-card"
      {...rest}
    >
      <Icon size={14} aria-hidden="true" /> {children}
    </button>
  )
}

/** Toplu silme (onaylı). */
export function BulkDelete({ confirming, onAsk, onYes, onNo }: { confirming: boolean; onAsk: () => void; onYes: () => void; onNo: () => void }) {
  if (confirming) return <DeleteConfirm onYes={onYes} onNo={onNo} />
  return (
    <button
      type="button"
      onClick={onAsk}
      className="xc-focus inline-flex h-8 items-center gap-1.5 rounded-md bg-fn-red px-3 text-[13px] font-semibold text-white transition-colors hover:bg-fn-red-ink"
    >
      Seçilenleri Sil
    </button>
  )
}

/** Klasör hücresi: renk noktası + ad (klasörsüzse tire). */
export function FolderCell({ folder }: { folder: { name: string; color: string | null } | undefined }) {
  if (!folder) return <span className="text-tx-subtle">—</span>
  return (
    <span className="inline-flex max-w-[180px] items-center gap-1.5 text-tx-2">
      <span aria-hidden="true" className={`h-2 w-2 flex-none rounded-[2px] ${folderDot(folder.color)}`} />
      <span className="truncate">{folder.name}</span>
    </span>
  )
}

/** Satır seçim kutusu. */
export function SelectBox({ checked, onChange, label }: { checked: boolean; onChange: () => void; label: string }) {
  return (
    <input
      type="checkbox"
      checked={checked}
      onChange={onChange}
      aria-label={label}
      className="xc-focus h-4 w-4 cursor-pointer accent-(--ui-accent)"
    />
  )
}

/** Boş liste kartı. */
export function LibraryEmpty({ icon: Icon, title, hint, children }: { icon: LucideIcon; title: string; hint: string; children?: ReactNode }) {
  return (
    <div className="flex flex-col items-center px-6 py-20 text-center">
      <span aria-hidden="true" className="mb-4 inline-flex h-14 w-14 items-center justify-center rounded-[14px] bg-ui-hover text-tx-subtle">
        <Icon size={28} strokeWidth={1.6} />
      </span>
      <p className="m-0 font-medium text-tx-2">{title}</p>
      <p className="mt-1 mb-0 text-[13px] text-tx-muted">{hint}</p>
      {children && <div className="mt-5 flex flex-wrap justify-center gap-3">{children}</div>}
    </div>
  )
}

/** Birincil / ikincil sayfa bağlantı-buton sınıfları. */
export const PRIMARY_LINK =
  'xc-focus inline-flex h-9 items-center gap-1.5 rounded-lg bg-ui-accent px-4 text-[13px] font-semibold text-white no-underline transition-[filter] hover:brightness-110'
export const SECONDARY_LINK =
  'xc-focus inline-flex h-9 items-center gap-1.5 rounded-lg border border-ui-line bg-ui-card px-3.5 text-[13px] font-medium text-tx-2 no-underline transition-colors hover:bg-ui-hover'

/** Tablo başlık hücresi sınıfı. */
export const TH = 'px-2 py-2.5 text-left font-medium text-tx-muted'
