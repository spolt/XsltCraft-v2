import { useId } from 'react'
import { ChevronDown, ChevronUp, Settings } from 'lucide-react'
import ThemeSwatches from '../ui/ThemeSwatches'
import Switch from '../ui/Switch'
import { useUiPrefsStore } from '../../store/uiPrefsStore'

interface SettingsSectionProps {
  collapsed: boolean
  open: boolean
  onToggle: () => void
  /** Daraltılmış menüde Ayarlar'a basınca: menüyü genişlet + ayarları aç. */
  onExpandAndOpen: () => void
}

/** Menü alt bölümündeki Ayarlar: arayüz rengi + "Menü daraltılmış başlasın". */
export default function SettingsSection({ collapsed, open, onToggle, onExpandAndOpen }: SettingsSectionProps) {
  const panelId = useId()
  const navCollapsedOnStart = useUiPrefsStore((s) => s.navCollapsedOnStart)
  const setNavCollapsedOnStart = useUiPrefsStore((s) => s.setNavCollapsedOnStart)

  if (collapsed) {
    return (
      <button
        type="button"
        aria-label="Ayarlar"
        title="Ayarlar"
        onClick={onExpandAndOpen}
        className="xc-focus flex h-11 w-11 items-center justify-center self-center rounded-[10px] text-tx-muted transition-colors hover:bg-ui-hover"
      >
        <Settings size={22} strokeWidth={1.7} aria-hidden="true" />
      </button>
    )
  }

  const Chev = open ? ChevronDown : ChevronUp
  return (
    <div className="flex flex-col">
      <button
        type="button"
        aria-expanded={open}
        aria-controls={panelId}
        onClick={onToggle}
        className={`xc-focus flex h-10 items-center gap-3 rounded-lg px-2.5 text-[13.5px] text-tx transition-colors hover:bg-ui-hover ${open ? 'bg-ui-hover' : ''}`}
      >
        <Settings size={18} strokeWidth={1.7} aria-hidden="true" className="text-tx-muted" />
        <span className="flex-1 text-left">Ayarlar</span>
        <Chev size={13} strokeWidth={2} aria-hidden="true" className="text-tx-subtle" />
      </button>
      {open && (
        <div id={panelId} className="flex flex-col gap-2.5 px-2.5 pt-1.5 pb-2.5">
          <span className="text-[12px] font-semibold text-tx-2">Arayüz rengi</span>
          <ThemeSwatches variant="list" />
          <Switch
            label="Menü daraltılmış başlasın"
            checked={navCollapsedOnStart}
            onChange={setNavCollapsedOnStart}
          />
        </div>
      )}
    </div>
  )
}
