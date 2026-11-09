import { useState, type CSSProperties } from 'react'
import { useNavigate } from 'react-router-dom'
import { ArrowRight, FilePlus2 } from 'lucide-react'
import FileDropZone from './FileDropZone'
import SupportInfo from './SupportInfo'
import EditChoiceCards from './EditChoiceCards'
import {
  SAMPLE_XML_LABEL,
  editorPendingState,
  sampleXmlFile,
  sniffDocumentType,
  type EditorFiles,
} from './editorFiles'
import { useUploadAnalysis, type UploadAnalysis } from '../../hooks/useUploadAnalysis'
import { useDocumentHandoffStore } from '../../store/documentHandoffStore'
import { toast } from '../../store/toastStore'
import { readUploadFile, stripXsltExt } from '../../utils/uploadValidation'
import { newId } from '../../utils/newId'
import type { EasyEditDocumentType } from '../../services/easyEditService'

/** Antrasit zeminde odak halkası panel metin rengiyle görünür kalır. */
const FOCUS_ON_DARK = { '--xc-focus-ring': 'var(--ui-workspace-fg)' } as CSSProperties

interface Props {
  className?: string
}

function analysisText(a: UploadAnalysis): string | null {
  return 'xslt' in a ? a.xslt : null
}

/** Örnek XML için belge türü: analizden, yoksa XSLT metnindeki namespace'ten. */
async function documentType(a: UploadAnalysis, file: File): Promise<EasyEditDocumentType> {
  if (a.status === 'ready') return a.analysis.documentType
  if (a.status === 'unsuitable' && a.analysis) return a.analysis.documentType
  const text = analysisText(a) ?? (await file.text().catch(() => ''))
  return sniffDocumentType(text)
}

/**
 * Ana iş akışı: kullanıcının XSLT dosyasıyla çalışma. XSLT seçilince Kolay düzenle uygunluğu analiz edilir ve iki yol
 * sunulur: Kolay düzenle (aktarım → `/easy-edit/:draftId`) ya da XSLT Editör (`location.state` pendingXsltFile /
 * pendingXmlFile; XML yoksa örnek belge). Kayıtlı şablonlar üst arama ve son düzenlenenlerde.
 */
export default function XsltWorkspace({ className = '' }: Props) {
  const navigate = useNavigate()
  const [files, setFiles] = useState<EditorFiles>({ xslt: null, xml: null })
  const [opening, setOpening] = useState(false)
  const { analysis, retry } = useUploadAnalysis(files.xslt)

  async function openEditor(initialLine?: number) {
    let next = files
    let pendingXmlLabel: string | undefined
    if (files.xslt && !files.xml) {
      const type = await documentType(analysis, files.xslt)
      next = { ...files, xml: sampleXmlFile(type) }
      pendingXmlLabel = SAMPLE_XML_LABEL[type]
    }
    const state = editorPendingState(next, { pendingXmlLabel, initialLine })
    navigate('/xslt-editor', state ? { state } : undefined)
  }

  async function openEasyEdit() {
    if (analysis.status !== 'ready' || !files.xslt || opening) return
    const xsltFile = files.xslt
    setOpening(true)
    let xml: string | undefined
    if (files.xml) {
      try {
        xml = await readUploadFile(files.xml, 'xml')
      } catch (err) {
        setOpening(false)
        toast.error(err instanceof Error ? err.message : 'XML dosyası okunamadı.', { title: 'XML eklenemedi' })
        return
      }
    }
    try {
      const draftId = newId()
      useDocumentHandoffStore.getState().stage(draftId, {
        source: 'upload',
        name: stripXsltExt(xsltFile.name),
        fileName: xsltFile.name,
        xslt: analysis.xslt,
        xml,
        dirty: true,
        analysis: analysis.analysis,
        analysisKey: analysis.key,
      })
      navigate(`/easy-edit/${draftId}`)
    } catch {
      // Buton "Açılıyor…"da takılı kalmasın.
      setOpening(false)
      toast.error('Kolay düzenle açılamadı, tekrar deneyin.', { title: 'Kolay düzenle' })
    }
  }

  const actions = files.xslt ? (
    <EditChoiceCards
      analysis={analysis}
      opening={opening}
      onRetry={retry}
      onOpenEasyEdit={() => void openEasyEdit()}
      onOpenEditor={(line) => void openEditor(line)}
    />
  ) : files.xml ? (
    <div className="flex justify-center">
      <button
        type="button"
        onClick={() => void openEditor()}
        className="xc-focus inline-flex h-11 cursor-pointer items-center gap-2 rounded-lg bg-ui-accent px-6 text-[14px] font-semibold text-ui-accent-fg shadow-[var(--ui-shadow-lift)] transition-[filter] hover:brightness-110"
      >
        XML ile editörü aç
        <ArrowRight size={16} strokeWidth={2} aria-hidden="true" />
      </button>
    </div>
  ) : null

  return (
    <section
      aria-labelledby="dash-xslt-title"
      style={FOCUS_ON_DARK}
      className={`flex min-w-0 flex-col gap-4 rounded-xl border border-ui-workspace-line bg-ui-workspace p-4 text-ui-workspace-fg shadow-[var(--ui-shadow-rest)] sm:p-5 ${className}`}
    >
      <div className="flex flex-wrap items-start justify-between gap-x-6 gap-y-3 border-b border-ui-workspace-line pb-4">
        <div className="flex min-w-0 flex-col gap-1">
          <h2 id="dash-xslt-title" className="m-0 text-[18px] font-semibold leading-tight text-ui-workspace-fg">
            XSLT dosyanla çalış
          </h2>
          <p className="m-0 text-[13px] leading-snug text-ui-workspace-muted">
            Şablonunuzu yükleyin, düzenleme yönteminizi seçin.
          </p>
        </div>
        <div className="flex flex-none items-center gap-3">
          <SupportInfo />
          <button
            type="button"
            onClick={() => navigate('/xslt-editor')}
            className="xc-focus inline-flex h-9 flex-none cursor-pointer items-center gap-2 rounded-lg border border-ui-workspace-line px-4 text-sm font-semibold text-ui-workspace-fg transition-colors hover:bg-ui-workspace-raised"
          >
            <FilePlus2 size={15} strokeWidth={2} aria-hidden="true" />
            Boş editörü aç
          </button>
        </div>
      </div>

      <FileDropZone files={files} onChange={setFiles} actions={actions} />
    </section>
  )
}
