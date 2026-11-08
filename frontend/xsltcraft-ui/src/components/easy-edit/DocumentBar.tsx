import type { ButtonHTMLAttributes, ReactNode } from 'react'
import type { LucideIcon } from 'lucide-react'
import { BookmarkCheck, Download, FileCode2, Printer } from 'lucide-react'

export type DocumentEditMode = 'easyEdit' | 'editor'

const MODES: { value: DocumentEditMode; label: string }[] = [
  { value: 'easyEdit', label: 'Kolay düzenle' },
  { value: 'editor', label: 'XSLT Editör' },
]

export interface DocumentBarProps {
  /** Görüntülenen dosya adı (ör. `fatura.xslt`). */
  fileName: string
  /** Kaydedilmemiş değişiklik var → "Kaydedilmedi" çipi. */
  dirty: boolean
  mode: DocumentEditMode
  /** Aktif olmayan moda geçiş isteği; çağıran gezinmeyi ve çıkış onayını yönetir. */
  onModeChange: (mode: DocumentEditMode) => void
  /** Mod anahtarı devre dışı (ör. kayıt sürerken). */
  modeDisabled?: boolean
  onPrint: () => void
  onSave: () => void
  onDownload: () => void
  printDisabled?: boolean
  saveDisabled?: boolean
  downloadDisabled?: boolean
  /** Kayıt düğmesi metni (ör. "Kaydediliyor…"); varsayılan "Şablonlarıma kaydet". */
  saveLabel?: string
  /** Kayıt düğmesinin açıklaması (ör. kaydın neden kapalı olduğu); varsayılan "Şablonlarıma kaydet". */
  saveTitle?: string
  /** İndirme düğmesi metni (ör. "İndiriliyor…"); varsayılan "İndir". */
  downloadLabel?: string
}

interface BarButtonProps extends ButtonHTMLAttributes<HTMLButtonElement> {
  variant?: 'primary' | 'secondary' | 'icon'
  icon: LucideIcon
  children?: ReactNode
}

function BarButton({ variant = 'secondary', icon: Icon, children, className = '', type = 'button', ...rest }: BarButtonProps) {
  const base =
    'xc-focus inline-flex h-[32px] items-center justify-center gap-1.5 rounded-md text-[13px] transition-colors disabled:cursor-not-allowed disabled:opacity-45'
  const look =
    variant === 'primary'
      ? 'px-3.5 bg-ui-accent text-ui-accent-fg font-semibold hover:brightness-110'
      : variant === 'icon'
        ? 'w-[32px] text-tx-2 hover:bg-ui-hover'
        : 'px-3 border border-field-line bg-ui-card text-tx-2 hover:bg-ui-hover'
  return (
    <button type={type} className={`${base} ${look} ${className}`} {...rest}>
      <Icon size={variant === 'icon' ? 16 : 15} strokeWidth={variant === 'primary' ? 2 : 1.8} aria-hidden="true" />
      {children}
    </button>
  )
}

/**
 * Belge çubuğu: solda dosya adı ve kayıt durumu, ortada düzenleme modu anahtarı, sağda Yazdır / Kaydet / İndir.
 * Açık zeminli; üstteki koyu `WorkspaceHeader`'ın altında durur.
 */
export default function DocumentBar({
  fileName,
  dirty,
  mode,
  onModeChange,
  modeDisabled = false,
  onPrint,
  onSave,
  onDownload,
  printDisabled = false,
  saveDisabled = false,
  downloadDisabled = false,
  saveLabel = 'Şablonlarıma kaydet',
  saveTitle = 'Şablonlarıma kaydet',
  downloadLabel = 'İndir',
}: DocumentBarProps) {
  return (
    <div className="grid grid-cols-[minmax(0,1fr)_auto_minmax(0,1fr)] items-center gap-3 border-b border-ui-line bg-ui-card px-4 py-2 text-[13px] text-tx">
      <div className="flex min-w-0 items-center gap-2">
        <FileCode2 size={15} aria-hidden="true" className="flex-none text-tx-muted" />
        <span className="truncate font-mono text-[13px] text-tx" title={fileName}>{fileName}</span>
        {dirty && (
          <span className="flex-none rounded-full border border-fn-orange-line bg-fn-orange-tint px-2 py-0.5 text-[11px] font-semibold text-fn-orange-ink">
            Kaydedilmedi
          </span>
        )}
      </div>

      <nav aria-label="Düzenleme modu">
        <ul className="m-0 flex list-none rounded-lg border border-ui-line bg-ui-bg p-0.5">
          {MODES.map((m) => {
            const active = m.value === mode
            return (
              <li key={m.value}>
                <button
                  type="button"
                  aria-current={active ? 'page' : undefined}
                  disabled={modeDisabled && !active}
                  onClick={() => { if (!active) onModeChange(m.value) }}
                  className={`xc-focus h-[28px] rounded-md px-3 text-[12px] font-semibold transition-colors disabled:cursor-not-allowed disabled:opacity-45 ${
                    active ? 'bg-ui-card text-ui-ink shadow-sm' : 'text-tx-muted hover:text-tx'
                  }`}
                >
                  {m.label}
                </button>
              </li>
            )
          })}
        </ul>
      </nav>

      <div className="flex items-center justify-end gap-2">
        <BarButton variant="icon" icon={Printer} aria-label="Yazdır" title="Yazdır" onClick={onPrint} disabled={printDisabled} />
        <BarButton icon={BookmarkCheck} onClick={onSave} disabled={saveDisabled} title={saveTitle}>
          {saveLabel}
        </BarButton>
        <BarButton variant="primary" icon={Download} onClick={onDownload} disabled={downloadDisabled} title="XSLT dosyasını indir">
          {downloadLabel}
        </BarButton>
      </div>
    </div>
  )
}
