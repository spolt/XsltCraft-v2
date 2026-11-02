import { LayoutGrid, ListTree } from 'lucide-react'
import SegmentedTabs from '../ui/SegmentedTabs'
import { useXmlStore } from '../../store/xmlStore'
import BlockPalette from './BlockPalette'
import XmlTreeExplorer from './XmlTreeExplorer'

type LeftTab = 'blocks' | 'xml'

const TABS = [
  { id: 'blocks', label: 'Bloklar', icon: LayoutGrid },
  { id: 'xml', label: 'XML Ağacı', icon: ListTree },
] as const

interface EditorLeftPanelProps {
  /** Sayfa kökündeki gizli XML input'unu tetikler. */
  onXmlUploadClick: () => void
  xmlError?: string | null
}

/**
 * Tasarım Alanı sol paneli (296px): Bloklar | XML Ağacı.
 * Aktif sekme `xmlStore.rightPanelTab`'tan türetilir ('xml' → XML Ağacı) — böylece
 * sağ paneldeki XPath düğmesi (`startXPathSelection`) ağacı açar, seçim (`applyXPathSelection`)
 * bittiğinde Bloklar'a döner. Store API'si değişmez.
 */
export default function EditorLeftPanel({ onXmlUploadClick, xmlError }: EditorLeftPanelProps) {
  const rightPanelTab = useXmlStore((s) => s.rightPanelTab)
  const setRightPanelTab = useXmlStore((s) => s.setRightPanelTab)
  const tab: LeftTab = rightPanelTab === 'xml' ? 'xml' : 'blocks'

  return (
    <aside
      aria-label="Bloklar ve XML ağacı"
      className="flex w-[296px] flex-none flex-col overflow-hidden border-r border-ui-line bg-ui-card"
    >
      <SegmentedTabs<LeftTab>
        items={TABS}
        value={tab}
        onChange={(id) => setRightPanelTab(id === 'xml' ? 'xml' : 'properties')}
        ariaLabel="Sol panel"
        variant="underline"
        idPrefix="design-left"
        className="flex-none px-4"
      />
      <div
        role="tabpanel"
        id={`design-left-panel-${tab}`}
        aria-labelledby={`design-left-tab-${tab}`}
        className={`flex min-h-0 flex-1 flex-col ${tab === 'blocks' ? 'overflow-y-auto' : 'overflow-hidden'}`}
      >
        {tab === 'blocks' ? (
          <BlockPalette />
        ) : (
          <XmlTreeExplorer onUploadClick={onXmlUploadClick} uploadError={xmlError} />
        )}
      </div>
    </aside>
  )
}
