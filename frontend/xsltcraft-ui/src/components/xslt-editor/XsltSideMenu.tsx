import { useState } from 'react'
import {
  ChevronsLeft,
  ChevronsRight,
  ChevronDown,
  ChevronUp,
  Code2,
  FileUp,
  FileCode2,
  FileText,
  Folder,
  BookMarked,
  Settings,
} from 'lucide-react'
import SegmentedTabs from '../ui/SegmentedTabs'
import SnippetLibraryList from './SnippetLibraryList'
import XsltEditorSettings from './XsltEditorSettings'
import { useUiPrefsStore } from '../../store/uiPrefsStore'
import type { UserSnippet } from '../../services/snippetService'

type LeftTab = 'files' | 'lib'

export interface SideMenuFile {
  name: string
  /** Görünen boyut, ör. "18 KB". */
  size: string
  dirty?: boolean
}

interface Props {
  xsltFile: SideMenuFile | null
  xmlFile: SideMenuFile | null
  /** Sayfa kökündeki tek gizli XSLT input'unu tetikler. */
  onUploadXslt: () => void
  /** Sayfa kökündeki tek gizli XML input'unu tetikler. */
  onUploadXml: () => void
  onOpenXslt?: () => void
  onOpenXml?: () => void
  snippets: UserSnippet[]
  /** Verilmezse snippet satırları devre dışı (editör henüz yok). */
  onInsertSnippet?: (snippet: UserSnippet) => void
  onManageSnippets: () => void
}

const LEFT_TABS = [
  { id: 'files', label: 'Dosyalar' },
  { id: 'lib', label: 'Snippet Kütüphanesi' },
] as const

const railBtn =
  'xc-focus flex h-9 w-9 items-center justify-center rounded-md text-tx-muted transition-colors hover:bg-ui-hover hover:text-tx'

/** XSLT editör sol menüsü: dosya yükleme, Dosyalar | Snippet Kütüphanesi, Ayarlar. 56/220 px. */
export default function XsltSideMenu({
  xsltFile,
  xmlFile,
  onUploadXslt,
  onUploadXml,
  onOpenXslt,
  onOpenXml,
  snippets,
  onInsertSnippet,
  onManageSnippets,
}: Props) {
  const collapsed = useUiPrefsStore(s => s.xsltMenuCollapsed)
  const setCollapsed = useUiPrefsStore(s => s.setXsltMenuCollapsed)
  const [tab, setTab] = useState<LeftTab>('files')
  const [settingsOpen, setSettingsOpen] = useState(false)
  const [query, setQuery] = useState('')

  function expandTo(next: LeftTab | 'settings') {
    setCollapsed(false)
    if (next === 'settings') setSettingsOpen(true)
    else setTab(next)
  }

  if (collapsed) {
    return (
      <nav
        aria-label="Sol menü (daraltılmış)"
        className="flex w-14 flex-none flex-col items-center gap-1.5 border-r border-ui-line bg-ui-card py-2.5"
      >
        <button type="button" aria-label="Menüyü genişlet" title="Menüyü genişlet" onClick={() => setCollapsed(false)} className={railBtn}>
          <ChevronsRight size={17} aria-hidden="true" />
        </button>
        <button
          type="button"
          aria-label="XSLT yükle"
          title="XSLT YÜKLE"
          onClick={onUploadXslt}
          className="xc-focus flex h-[38px] w-[38px] items-center justify-center rounded-lg bg-ui-accent text-ui-accent-fg hover:brightness-110"
        >
          <Code2 size={18} strokeWidth={2} aria-hidden="true" />
        </button>
        <button
          type="button"
          aria-label="Belge XML yükle"
          title="BELGE XML YÜKLE"
          onClick={onUploadXml}
          className="xc-focus flex h-[38px] w-[38px] items-center justify-center rounded-lg border-[1.5px] border-ui-accent bg-ui-card text-ui-ink hover:bg-ui-soft"
        >
          <FileUp size={18} aria-hidden="true" />
        </button>
        <span aria-hidden="true" className="my-1 h-px w-7 bg-ui-line" />
        <button type="button" aria-label="Dosyalar" title="Dosyalar" onClick={() => expandTo('files')} className={railBtn}>
          <Folder size={17} aria-hidden="true" />
        </button>
        <button type="button" aria-label="Snippet Kütüphanesi" title="Snippet Kütüphanesi" onClick={() => expandTo('lib')} className={railBtn}>
          <BookMarked size={17} aria-hidden="true" />
        </button>
        <div className="flex-1" />
        <button type="button" aria-label="Ayarlar" title="Ayarlar" onClick={() => expandTo('settings')} className={railBtn}>
          <Settings size={18} aria-hidden="true" />
        </button>
      </nav>
    )
  }

  return (
    <aside aria-label="Sol menü" className="flex w-[220px] min-h-0 flex-none flex-col border-r border-ui-line bg-ui-card">
      <div className="flex flex-col gap-1.5 border-b border-ui-line px-2.5 pb-2.5 pt-2">
        <div className="flex items-center justify-between">
          <span className="text-[10.5px] font-semibold uppercase tracking-[.07em] text-tx-muted">Dosya yükle</span>
          <button
            type="button"
            aria-label="Menüyü daralt"
            title="Menüyü daralt"
            onClick={() => setCollapsed(true)}
            className="xc-focus flex h-6 w-6 items-center justify-center rounded-md text-tx-muted hover:bg-ui-hover hover:text-tx"
          >
            <ChevronsLeft size={15} aria-hidden="true" />
          </button>
        </div>
        <button
          type="button"
          onClick={onUploadXslt}
          className="xc-focus flex items-center gap-2.5 rounded-lg bg-ui-accent px-2.5 py-2 text-left text-ui-accent-fg transition-[filter] hover:brightness-110"
        >
          <span aria-hidden="true" className="flex h-[30px] w-[30px] flex-none items-center justify-center rounded-[6px] bg-ui-accent-fg/15">
            <Code2 size={16} strokeWidth={2} />
          </span>
          <span className="flex flex-col">
            <span className="text-[12px] font-bold leading-tight tracking-[.04em]">XSLT YÜKLE</span>
            <span className="text-[11px] leading-tight opacity-85">.xsl, .xslt</span>
          </span>
        </button>
        <button
          type="button"
          onClick={onUploadXml}
          className="xc-focus flex items-center gap-2.5 rounded-lg border-[1.5px] border-ui-accent bg-ui-card px-2.5 py-[7px] text-left text-ui-ink transition-colors hover:bg-ui-soft"
        >
          <span aria-hidden="true" className="flex h-[30px] w-[30px] flex-none items-center justify-center rounded-[6px] bg-ui-soft">
            <FileUp size={16} />
          </span>
          <span className="flex flex-col">
            <span className="text-[12px] font-bold leading-tight tracking-[.04em]">BELGE XML YÜKLE</span>
            <span className="text-[11px] leading-tight text-tx-muted">e-Fatura / e-İrsaliye .xml</span>
          </span>
        </button>
      </div>

      <SegmentedTabs
        items={LEFT_TABS}
        value={tab}
        onChange={setTab}
        ariaLabel="Sol panel"
        variant="underline"
        idPrefix="xslt-side"
        className="flex-none gap-4 px-2.5 [&_[role=tab]]:h-[34px] [&_[role=tab]]:text-[12px]"
      />

      <div
        role="tabpanel"
        id={`xslt-side-panel-${tab}`}
        aria-labelledby={`xslt-side-tab-${tab}`}
        className="min-h-0 flex-1 overflow-auto"
      >
        {tab === 'files' ? (
          <div className="flex flex-col gap-2.5 px-2 py-2.5">
            <FileGroup title="XSLT">
              {xsltFile ? (
                <FileRow icon={FileCode2} file={xsltFile} active onClick={onOpenXslt} />
              ) : (
                <EmptyRow>Henüz XSLT yüklenmedi</EmptyRow>
              )}
            </FileGroup>
            <FileGroup title="Belge XML">
              {xmlFile ? (
                <FileRow icon={FileText} file={xmlFile} onClick={onOpenXml} />
              ) : (
                <EmptyRow>Henüz XML yüklenmedi</EmptyRow>
              )}
            </FileGroup>
          </div>
        ) : (
          <SnippetLibraryList
            snippets={snippets}
            query={query}
            onQueryChange={setQuery}
            onInsertSnippet={onInsertSnippet}
            onManageSnippets={onManageSnippets}
          />
        )}
      </div>

      <section className="flex max-h-[520px] flex-none flex-col border-t border-ui-line">
        <button
          type="button"
          aria-expanded={settingsOpen}
          aria-controls="xslt-side-settings"
          onClick={() => setSettingsOpen(o => !o)}
          className={`xc-focus flex h-[38px] flex-none items-center gap-2 px-3 text-[12.5px] font-semibold text-tx ${settingsOpen ? 'bg-ui-side' : 'hover:bg-ui-hover'}`}
        >
          <Settings size={16} aria-hidden="true" />
          <span className="flex-1 text-left">Ayarlar</span>
          {settingsOpen ? <ChevronDown size={13} aria-hidden="true" className="text-tx-subtle" /> : <ChevronUp size={13} aria-hidden="true" className="text-tx-subtle" />}
        </button>
        {settingsOpen && (
          <div id="xslt-side-settings" className="min-h-0 overflow-auto">
            <XsltEditorSettings />
          </div>
        )}
      </section>
    </aside>
  )
}

function FileGroup({ title, children }: { title: string; children: React.ReactNode }) {
  return (
    <section className="flex flex-col gap-1">
      <h2 className="mx-1 mb-0.5 text-[10.5px] font-semibold uppercase tracking-[.07em] text-tx-muted">{title}</h2>
      {children}
    </section>
  )
}

function EmptyRow({ children }: { children: React.ReactNode }) {
  return <p className="mx-1 text-[12px] text-tx-subtle">{children}</p>
}

function FileRow({
  icon: Icon,
  file,
  active = false,
  onClick,
}: {
  icon: typeof FileText
  file: SideMenuFile
  active?: boolean
  onClick?: () => void
}) {
  return (
    <button
      type="button"
      onClick={onClick}
      disabled={!onClick}
      className={`xc-focus flex h-[28px] items-center gap-2 rounded-md px-2 text-left text-[12px] transition-colors ${
        active ? 'bg-ui-soft text-ui-ink' : 'text-tx enabled:hover:bg-ui-hover'
      }`}
    >
      <Icon size={14} aria-hidden="true" className="flex-none" />
      <span className="truncate font-mono">{file.name}</span>
      <span className="ml-auto flex flex-none items-center gap-1 whitespace-nowrap text-[11px] text-tx-subtle">
        {file.dirty && (
          <>
            <span aria-hidden="true" className="h-1.5 w-1.5 rounded-full bg-ui-accent" />
            <span className="sr-only">kaydedilmemiş,</span>
          </>
        )}
        {file.size}
      </span>
    </button>
  )
}
