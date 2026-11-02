import { useState } from 'react'
import { BookMarked, Code2, FileCode2, FileText, FolderOpen, Settings, Upload, type LucideIcon } from 'lucide-react'
import type { UserSnippet } from '../../services/snippetService'
import AssetAccordionSection from '../theme-use/AssetAccordionSection'
import SnippetLibraryList from '../xslt-editor/SnippetLibraryList'
import XsltEditorSettings from '../xslt-editor/XsltEditorSettings'

interface DevToolsSectionsProps {
  xsltFile: { name: string; size: string; dirty?: boolean }
  xmlFile: { name: string; size: string }
  onReplaceXml: () => void
  onOpenXml: () => void
  snippets: UserSnippet[]
  onInsertSnippet: (snippet: UserSnippet) => void
  onManageSnippets: () => void
}

const actionBtn =
  'xc-focus flex h-8 min-w-0 flex-1 items-center justify-center gap-1.5 rounded-md border border-field-line bg-ui-card px-2 text-[12px] text-tx-2 hover:bg-ui-hover'

/** Geliştirici Modu sol paneli: varlıkların altındaki dosya, snippet ve ayar bölümleri. */
export default function DevToolsSections({
  xsltFile,
  xmlFile,
  onReplaceXml,
  onOpenXml,
  snippets,
  onInsertSnippet,
  onManageSnippets,
}: DevToolsSectionsProps) {
  const [openSections, setOpenSections] = useState({ files: false, snippets: false, settings: false })
  const [query, setQuery] = useState('')
  const toggleSection = (key: keyof typeof openSections) => setOpenSections((o) => ({ ...o, [key]: !o[key] }))

  return (
    <>
      <AssetAccordionSection
        title="Belge ve dosyalar"
        icon={FolderOpen}
        status={xsltFile.dirty ? 'Değişti' : ''}
        done={false}
        open={openSections.files}
        onToggle={() => toggleSection('files')}
      >
        <div className="flex flex-col gap-2.5">
          <div className="flex flex-col gap-1">
            <FileRow icon={FileCode2} file={xsltFile} />
            <FileRow icon={FileText} file={xmlFile} />
          </div>
          <div className="flex gap-2">
            <button type="button" onClick={onReplaceXml} className={actionBtn}>
              <Upload size={13} aria-hidden="true" className="flex-none" />
              <span className="truncate">XML Değiştir</span>
            </button>
            <button type="button" onClick={onOpenXml} className={actionBtn}>
              <Code2 size={13} aria-hidden="true" className="flex-none" />
              <span className="truncate">XML Kaynağını Aç</span>
            </button>
          </div>
        </div>
      </AssetAccordionSection>
      <AssetAccordionSection
        title="Snippet Kütüphanesi"
        icon={BookMarked}
        status={snippets.length > 0 ? `${snippets.length} snippet` : 'Boş'}
        done={false}
        open={openSections.snippets}
        onToggle={() => toggleSection('snippets')}
      >
        <SnippetLibraryList
          flush
          snippets={snippets}
          query={query}
          onQueryChange={setQuery}
          onInsertSnippet={onInsertSnippet}
          onManageSnippets={onManageSnippets}
        />
      </AssetAccordionSection>
      <AssetAccordionSection
        title="Editör ayarları"
        icon={Settings}
        status=""
        done={false}
        open={openSections.settings}
        onToggle={() => toggleSection('settings')}
      >
        {/* Ayarların kendi dolgusu akordeon gövdesininkiyle toplanmasın. */}
        <div className="-mx-3.5 -mb-3.5">
          <XsltEditorSettings hideRightPanelMode />
        </div>
      </AssetAccordionSection>
    </>
  )
}

function FileRow({
  icon: Icon,
  file,
}: {
  icon: LucideIcon
  file: { name: string; size: string; dirty?: boolean }
}) {
  return (
    <div className="flex h-7 items-center gap-2 text-[12px] text-tx">
      <Icon size={14} aria-hidden="true" className="flex-none text-tx-muted" />
      <span title={file.name} className="min-w-0 truncate font-mono">{file.name}</span>
      <span className="ml-auto flex flex-none items-center gap-1 whitespace-nowrap text-[11px] text-tx-subtle">
        {file.dirty && (
          <>
            <span aria-hidden="true" className="h-1.5 w-1.5 rounded-full bg-ui-accent" />
            <span className="sr-only">kaydedilmemiş,</span>
          </>
        )}
        {file.size}
      </span>
    </div>
  )
}
