import { ArrowLeft, Download, Save, Settings2 } from 'lucide-react'
import WorkspaceHeader from '../shell/WorkspaceHeader'
import { HeaderButton, HeaderChip, HeaderStatus } from '../shell/headerParts'

interface Props {
  name: string
  version: string | null
  dirty: boolean
  /** Çalışma metni hazır değilken eylemler pasiftir. */
  ready: boolean
  panelOpen: boolean
  onNavigate: (path: string) => void
  confirmLeave: () => boolean
  onBack: () => void
  onTogglePanel: () => void
  onSave: () => void
  onDownload: () => void
}

/** Geliştirici Modu üst barı. */
export default function DevModeHeader({
  name, version, dirty, ready, panelOpen, onNavigate, confirmLeave, onBack, onTogglePanel, onSave, onDownload,
}: Props) {
  return (
    <WorkspaceHeader
      active="themes"
      onNavigate={onNavigate}
      confirmLeave={confirmLeave}
      center={(
        <>
          <span className="max-w-[260px] truncate font-medium">{name}</span>
          {version && <HeaderChip>XSLT {version}</HeaderChip>}
          <HeaderChip>Geliştirici Modu</HeaderChip>
          {dirty && <HeaderStatus>Kaydedilmemiş değişiklik</HeaderStatus>}
        </>
      )}
      actions={(
        <>
          <HeaderButton icon={ArrowLeft} onClick={onBack}>Tema Kullan'a dön</HeaderButton>
          <HeaderButton
            icon={Settings2}
            onClick={onTogglePanel}
            disabled={!ready}
            aria-pressed={panelOpen}
            className={panelOpen ? 'bg-ui-bar-line' : ''}
          >
            Düzenle
          </HeaderButton>
          <HeaderButton icon={Save} onClick={onSave} disabled={!ready}>Şablonlarıma kaydet</HeaderButton>
          <HeaderButton variant="primary" icon={Download} onClick={onDownload} disabled={!ready}>İndir</HeaderButton>
        </>
      )}
    />
  )
}
