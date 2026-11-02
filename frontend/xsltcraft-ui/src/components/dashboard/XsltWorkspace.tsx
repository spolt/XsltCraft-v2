import { useState, type CSSProperties } from 'react'
import { useNavigate } from 'react-router-dom'
import { ArrowRight } from 'lucide-react'
import FileDropZone from './FileDropZone'
import SupportInfo from './SupportInfo'
import { editorPendingState, type EditorFiles } from './editorFiles'

/** Antrasit zeminde odak halkası panel metin rengiyle görünür kalır. */
const FOCUS_ON_DARK = { '--xc-focus-ring': 'var(--ui-workspace-fg)' } as CSSProperties

interface Props {
  className?: string
}

/**
 * Ana iş akışı: XSLT Editör çalışma alanı. Dosyalar sürükle-bırak ya da yuvalardan seçilir
 * (→ `location.state` pendingXsltFile / pendingXmlFile); kayıtlı şablonlar üst arama ve son düzenlenenlerde.
 */
export default function XsltWorkspace({ className = '' }: Props) {
  const navigate = useNavigate()
  const [files, setFiles] = useState<EditorFiles>({ xslt: null, xml: null })
  const hasFiles = !!(files.xslt || files.xml)

  function openEditor() {
    const state = editorPendingState(files)
    navigate('/xslt-editor', state ? { state } : undefined)
  }

  return (
    <section
      aria-labelledby="dash-xslt-title"
      style={FOCUS_ON_DARK}
      className={`flex min-w-0 flex-col gap-4 rounded-xl border border-ui-workspace-line bg-ui-workspace p-4 text-ui-workspace-fg shadow-[var(--ui-shadow-rest)] sm:p-5 ${className}`}
    >
      <div className="flex flex-wrap items-center gap-x-4 gap-y-2">
        <div className="flex min-w-0 flex-1 flex-wrap items-center gap-x-3 gap-y-1">
          <h2 id="dash-xslt-title" className="m-0 text-[18px] font-semibold leading-tight text-ui-workspace-fg">
            XSLT Editör
          </h2>
          <p className="m-0 min-w-0 truncate text-[13px] text-ui-workspace-muted">
            Fatura ve irsaliye şablonlarını düzenle, önizle ve indir.
          </p>
          <SupportInfo />
        </div>
        <button
          type="button"
          onClick={openEditor}
          className="xc-focus inline-flex h-9 flex-none cursor-pointer items-center gap-2 rounded-lg bg-ui-accent px-4 text-sm font-semibold text-ui-accent-fg transition-[filter] hover:brightness-110"
        >
          {hasFiles ? 'Dosyalarla editörü aç' : 'Editörü aç'}
          <ArrowRight size={15} strokeWidth={2} aria-hidden="true" />
        </button>
      </div>

      <FileDropZone files={files} onChange={setFiles} onOpen={openEditor} />
    </section>
  )
}
