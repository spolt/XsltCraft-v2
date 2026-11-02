import { CircleCheck, CircleDashed, CircleX, LayoutTemplate, TriangleAlert, type LucideIcon } from 'lucide-react'
import IconTile from '../ui/IconTile'
import type { CheckItem, CheckTone } from './themeChecklist'

interface Props {
  templateName: string
  docTypeLabel: string
  /** Temada sürüklenebilir yerleşim alanı sayısı (0 → temanın varsayılan alanları). */
  slotCount: number
  /** Kullanıcının planı kaydetmeye izin vermiyor (entitlements). */
  saveNeedsPro: boolean
  checks: CheckItem[]
}

const TONE: Record<CheckTone, { icon: LucideIcon; className: string }> = {
  ok: { icon: CircleCheck, className: 'text-fn-green-ink' },
  info: { icon: CircleDashed, className: 'text-tx-subtle' },
  warn: { icon: TriangleAlert, className: 'text-fn-orange-ink' },
  error: { icon: CircleX, className: 'text-fn-red-ink' },
}

/** Tema Kullan sağ paneli: tema bilgisi, plan notu ve kontrol listesi. */
export default function ThemeInfoAside({ templateName, docTypeLabel, slotCount, saveNeedsPro, checks }: Props) {
  return (
    <aside
      aria-label="Tema bilgisi"
      className="flex flex-col gap-4 overflow-y-auto border-l border-ui-line bg-ui-card p-4 lg:w-[272px] lg:flex-none"
    >
      <div className="flex items-center gap-2.5">
        <IconTile icon={LayoutTemplate} color="green" size="md" hoverFill={false} />
        <span className="min-w-0 truncate text-[13px] font-semibold text-tx">{templateName}</span>
      </div>

      <dl className="m-0 flex flex-col gap-2 text-[12px]">
        <div className="flex justify-between gap-3">
          <dt className="text-tx-muted">Tür</dt>
          <dd className="m-0 text-right text-tx">{docTypeLabel}</dd>
        </div>
        <div className="flex justify-between gap-3">
          <dt className="text-tx-muted">Yerleşim alanı</dt>
          <dd className="m-0 text-right text-tx">{slotCount > 0 ? `${slotCount} alan` : 'Temanın varsayılanı'}</dd>
        </div>
      </dl>

      {saveNeedsPro && (
        <p className="m-0 rounded-md bg-ui-soft p-3 text-[12px] leading-normal text-tx-2">
          <b className="text-ui-ink">Kaydetme Pro üyelik gerektirir.</b> İndirme ve yazdırma her planda açık.
        </p>
      )}

      <div className="flex flex-col gap-2 text-[12px]">
        <h2 className="m-0 text-[12px] font-semibold text-tx">Kontrol listesi</h2>
        <ul className="m-0 flex list-none flex-col gap-2 p-0" aria-live="polite">
          {checks.map((c) => {
            const { icon: Icon, className } = TONE[c.tone]
            return (
              <li key={c.id} className={`flex items-center gap-2 ${className}`}>
                <Icon size={14} strokeWidth={2.2} aria-hidden="true" className="flex-none" />
                <span>{c.label}</span>
              </li>
            )
          })}
        </ul>
      </div>
    </aside>
  )
}
