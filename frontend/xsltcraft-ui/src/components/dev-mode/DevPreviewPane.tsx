import { Eye, Sparkles } from 'lucide-react'
import AiAssistantPanel from '../ai/AiAssistantPanel'
import SegmentedTabs from '../ui/SegmentedTabs'
import PreviewDock from '../xslt-editor/PreviewDock'
import type { RawPreview } from '../../hooks/useRawPreview'
import type { useXmlDock } from '../../hooks/useXmlDock'
import type { DevModeAi } from '../../hooks/useDevModeAi'

const PANE_TABS = [
  { id: 'preview', label: 'Önizleme', icon: Eye },
  { id: 'ai', label: 'AI Asistan', icon: Sparkles },
] as const

interface Props {
  ai: DevModeAi
  preview: RawPreview
  xmlDock: ReturnType<typeof useXmlDock>
  /** Sayfanın izin verdiği iframe: tam ekranda ve Alanları göster açıkken önizleme başka yerdedir. */
  showFrame: boolean
  onElementClick: (text: string, opts?: { exact?: boolean }) => void
  onPopOut: () => void
  xslt: string
  xml: string | null
  xsltCursorLine?: number
  xmlDeclarationMissing: boolean
  onApplyXslt: (xslt: string) => void
  confirmLeave: () => boolean
}

/** Önizleme bölmesi: Önizleme | AI Asistan. AI sekmesindeyken önizleme iframe'i mount edilmez. */
export default function DevPreviewPane({
  ai,
  preview,
  xmlDock,
  showFrame,
  onElementClick,
  onPopOut,
  xslt,
  xml,
  xsltCursorLine,
  xmlDeclarationMissing,
  onApplyXslt,
  confirmLeave,
}: Props) {
  const aiPane = ai.pane === 'ai'
  return (
    <div className="flex h-full min-w-0 flex-col bg-ui-card">
      {ai.enabled && (
        <SegmentedTabs
          items={PANE_TABS}
          value={ai.pane}
          onChange={(id) => (id === 'ai' ? ai.open() : ai.showPreview())}
          ariaLabel="Önizleme bölmesi"
          variant="underline"
          idPrefix="dev-pane"
          className="h-[37px] flex-none gap-4 bg-ui-card px-3 [&_[role=tab]]:h-[36px] [&_[role=tab]]:text-[12.5px]"
        />
      )}
      <div
        role={ai.enabled ? 'tabpanel' : undefined}
        id="dev-pane-panel-preview"
        aria-labelledby={ai.enabled ? 'dev-pane-tab-preview' : undefined}
        className={aiPane ? 'hidden' : 'min-h-0 flex-1'}
      >
        <PreviewDock
          tab={xmlDock.tab}
          onTabChange={xmlDock.setTab}
          showFrame={showFrame && !aiPane}
          html={preview.html}
          onElementClick={onElementClick}
          previewLoading={preview.loading}
          lastMs={preview.lastMs}
          engine={preview.engine}
          onRefresh={preview.refresh}
          onPopOut={onPopOut}
          hasXml={!!xml}
          xmlDisplay={xmlDock.display}
          xmlPretty={xmlDock.pretty}
          onToggleXmlPretty={xmlDock.togglePretty}
          xmlDisplayReformatted={xmlDock.reformatted}
          onXmlEditorMount={xmlDock.handleEditorMount}
          hideTitle={ai.enabled}
        />
      </div>
      {ai.enabled && ai.mounted && (
        <aside
          aria-label="AI Asistan"
          role="tabpanel"
          id="dev-pane-panel-ai"
          aria-labelledby="dev-pane-tab-ai"
          className={aiPane ? 'flex min-h-0 flex-1 flex-col' : 'hidden'}
        >
          <AiAssistantPanel
            xslt={xslt}
            xml={xml}
            xmlCursorLine={xmlDock.aiCursorLine}
            xmlSelection={xmlDock.selection}
            xsltCursorLine={xsltCursorLine}
            xsltSelection={ai.xsltSelection}
            prompt={ai.prompt}
            xmlDeclarationMissing={xmlDeclarationMissing}
            onApplyXslt={onApplyXslt}
            confirmLeave={confirmLeave}
            onClose={ai.showPreview}
          />
        </aside>
      )}
    </div>
  )
}
