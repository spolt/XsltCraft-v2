import { useRef, useState, type DragEvent } from 'react'
import { ArrowRight, FileCode2, FileText, Upload } from 'lucide-react'
import { UPLOAD_ACCEPT } from '../../utils/uploadValidation'
import FileSlot from './FileSlot'
import { splitDroppedFiles, type EditorFiles } from './editorFiles'

const STEPS = ['XSLT şablonunu ekle', "Belge XML'ini ekle", 'Editörde önizle, doğrula ve indir']

interface Props {
  files: EditorFiles
  onChange: (update: (prev: EditorFiles) => EditorFiles) => void
  /** İki dosya da seçiliyken yuvaların altında görünen "Editörü aç" aksiyonu. */
  onOpen: () => void
}

/** Sürükle-bırak alanı + iki dosya yuvası (XSLT / belge XML). Dosyalar uzantıya göre yuvalara yerleşir. */
export default function FileDropZone({ files, onChange, onOpen }: Props) {
  const ready = !!(files.xslt && files.xml)
  const [dragging, setDragging] = useState(false)
  const [notice, setNotice] = useState<string | null>(null)
  const dragDepth = useRef(0)

  function onDragEnter(e: DragEvent) {
    if (!e.dataTransfer.types.includes('Files')) return
    e.preventDefault()
    dragDepth.current += 1
    setDragging(true)
  }

  function onDragOver(e: DragEvent) {
    if (!e.dataTransfer.types.includes('Files')) return
    e.preventDefault()
    e.dataTransfer.dropEffect = 'copy'
  }

  function onDragLeave() {
    dragDepth.current = Math.max(0, dragDepth.current - 1)
    if (dragDepth.current === 0) setDragging(false)
  }

  function onDrop(e: DragEvent) {
    e.preventDefault()
    dragDepth.current = 0
    setDragging(false)
    const { xslt, xml, rejected } = splitDroppedFiles(Array.from(e.dataTransfer.files))
    if (xslt || xml) onChange((prev) => ({ xslt: xslt ?? prev.xslt, xml: xml ?? prev.xml }))
    setNotice(
      rejected.length > 0
        ? `Desteklenmeyen dosya: ${rejected.map((f) => f.name).join(', ')}. Yalnız .xsl, .xslt ve .xml bırakılabilir.`
        : null,
    )
  }

  return (
    <div
      onDragEnter={onDragEnter}
      onDragOver={onDragOver}
      onDragLeave={onDragLeave}
      onDrop={onDrop}
      data-testid="dashboard-dropzone"
      className={`@container flex min-h-[300px] flex-1 flex-col rounded-lg border-[1.5px] border-dashed p-4 transition-colors sm:p-5 ${
        dragging ? 'border-ui-accent bg-ui-workspace-raised' : 'border-ui-workspace-line'
      }`}
    >
      {/* Üç bant (bırakma çağrısı · dosya yuvaları · akış adımları) panel yüksekliğine orantılı yayılır. */}
      <div className="mx-auto flex w-full max-w-[820px] flex-1 flex-col items-stretch justify-evenly gap-8 py-2">
        <div className="flex flex-col items-center gap-3.5 text-center">
          <span
            aria-hidden="true"
            className={`flex h-16 w-16 items-center justify-center rounded-full border transition-colors ${
              dragging ? 'border-ui-accent bg-ui-accent text-white' : 'border-ui-workspace-line bg-ui-workspace-raised text-ui-workspace-fg'
            }`}
          >
            <Upload size={28} strokeWidth={1.6} />
          </span>
          <p className="m-0 text-[19px] font-semibold text-ui-workspace-fg">
            {dragging ? 'Bırak — dosyalar yuvalara yerleşsin' : 'Dosyaları buraya sürükleyip bırak'}
          </p>
          <p className="m-0 max-w-[480px] text-[13.5px] leading-relaxed text-ui-workspace-muted">
            XSLT şablonunu ve belge XML'ini birlikte bırakabilirsin; uzantıya göre yerleşir. Ya da aşağıdan tek tek seç.
          </p>
        </div>

        <div className="flex flex-col gap-4">
          <div className="grid grid-cols-1 gap-4 @xl:grid-cols-2">
            <FileSlot
              label="XSLT şablonu"
              hint=".xsl · .xslt"
              accept={UPLOAD_ACCEPT.xslt}
              icon={FileCode2}
              file={files.xslt}
              onSelect={(f) => onChange((p) => ({ ...p, xslt: f }))}
              onClear={() => onChange((p) => ({ ...p, xslt: null }))}
            />
            <FileSlot
              label="Belge XML"
              hint=".xml"
              accept={UPLOAD_ACCEPT.xml}
              icon={FileText}
              file={files.xml}
              onSelect={(f) => onChange((p) => ({ ...p, xml: f }))}
              onClear={() => onChange((p) => ({ ...p, xml: null }))}
            />
          </div>

          {/* İki dosya hazır olunca birincil aksiyon dosyaların hemen altında; yer önceden ayrılır, düzen zıplamaz. */}
          <div className="flex min-h-11 justify-center">
            {ready && (
              <button
                type="button"
                onClick={onOpen}
                className="xc-focus inline-flex h-11 cursor-pointer items-center gap-2 rounded-lg bg-ui-accent px-6 text-[14px] font-semibold text-white shadow-[var(--ui-shadow-lift)] transition-[filter] hover:brightness-110"
              >
                Editörü aç
                <ArrowRight size={16} strokeWidth={2} aria-hidden="true" />
              </button>
            )}
          </div>
        </div>

        <ol className="m-0 grid list-none grid-cols-1 gap-3 border-t border-ui-workspace-line p-0 pt-5 @xl:grid-cols-3">
          {STEPS.map((step, i) => (
            <li key={step} className="flex items-center gap-2.5 text-[12.5px] leading-snug text-ui-workspace-muted">
              <span
                aria-hidden="true"
                className="flex h-6 w-6 flex-none items-center justify-center rounded-full border border-ui-workspace-line font-mono text-[11px] text-ui-workspace-fg"
              >
                {i + 1}
              </span>
              {step}
            </li>
          ))}
        </ol>
      </div>

      <p role="status" aria-live="polite" className="m-0 mt-2 min-h-[18px] text-center text-[12.5px] text-ui-workspace-warn">
        {notice}
      </p>
    </div>
  )
}
