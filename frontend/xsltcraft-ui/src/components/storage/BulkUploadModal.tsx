import { useEffect, useId, useRef, useState } from 'react'
import { Upload, FolderUp, FileCode2, Trash2, AlertCircle, Loader2 } from 'lucide-react'

import { createFolder, type Folder } from '../../services/folderService'
import { bulkUploadUserXslt, type BulkUploadItem } from '../../services/userXsltService'
import { FOLDER_COLORS, FOLDER_COLOR_NAME, folderDot } from './folderColors'
import ModalShell, { MODAL_INPUT, MODAL_PRIMARY, MODAL_SECONDARY } from './ModalShell'
import { toast } from '../../store/toastStore'
import { validateUploadContent } from '../../utils/uploadValidation'

const MAX_FILES = 100
const MAX_BYTES = 2 * 1024 * 1024
// İstekleri Kestrel'in 30 MB gövde sınırının güvenle altında tutmak için boyut-bazlı parti.
// ~10 MB ham içerik → JSON kaçışı sonrası dahi sınırın altında kalır.
const MAX_BATCH_BYTES = 10 * 1024 * 1024
const MAX_BATCH_COUNT = 50

function buildBatches(items: BulkUploadItem[]): BulkUploadItem[][] {
  const batches: BulkUploadItem[][] = []
  let current: BulkUploadItem[] = []
  let bytes = 0
  for (const it of items) {
    const size = it.xsltContent.length
    if (current.length > 0 && (bytes + size > MAX_BATCH_BYTES || current.length >= MAX_BATCH_COUNT)) {
      batches.push(current)
      current = []
      bytes = 0
    }
    current.push(it)
    bytes += size
  }
  if (current.length > 0) batches.push(current)
  return batches
}

interface PickedFile {
  name: string
  content: string
  valid: boolean
  error?: string
}

interface Props {
  folders: Folder[]
  /** Modal açıldığında ön-seçili hedef klasör (aktif klasör). */
  defaultFolderId?: string | null
  onClose: () => void
  /** Başarılı yükleme sonrası; hedef klasöre yönlendirmek için folderId verir. */
  onUploaded: (folderId: string | null) => void
}

function readFileText(file: File): Promise<string> {
  return new Promise((resolve, reject) => {
    const reader = new FileReader()
    reader.onload = () => resolve(reader.result as string)
    reader.onerror = () => reject(reader.error)
    reader.readAsText(file, 'utf-8')
  })
}

function baseName(fileName: string): string {
  return fileName.replace(/\.(xsl|xslt)$/i, '')
}

export default function BulkUploadModal({ folders, defaultFolderId, onClose, onUploaded }: Props) {
  const [files, setFiles] = useState<PickedFile[]>([])
  const [reading, setReading] = useState(false)
  const [uploading, setUploading] = useState(false)
  const [progress, setProgress] = useState('')
  const [target, setTarget] = useState<'new' | 'existing'>(defaultFolderId ? 'existing' : 'new')
  const [newName, setNewName] = useState('')
  const [newColor, setNewColor] = useState<string>('blue')
  const [existingId, setExistingId] = useState<string>(defaultFolderId ?? folders[0]?.id ?? '')

  const dirRef = useRef<HTMLInputElement>(null)
  const targetName = useId()


  // webkitdirectory React tiplerinde yok; ref ile attribute ekle.
  useEffect(() => {
    dirRef.current?.setAttribute('webkitdirectory', '')
    dirRef.current?.setAttribute('directory', '')
  }, [])

  async function ingest(fileList: FileList | null) {
    if (!fileList || fileList.length === 0) return
    setReading(true)
    try {
      const picked: PickedFile[] = []
      for (const file of Array.from(fileList)) {
        if (!/\.(xsl|xslt)$/i.test(file.name)) continue // yalnız XSLT
        if (file.size > MAX_BYTES) {
          picked.push({ name: baseName(file.name), content: '', valid: false, error: '2 MB sınırı aşıldı' })
          continue
        }
        try {
          const content = await readFileText(file)
          const contentError = validateUploadContent(content, 'xslt')
          picked.push(contentError
            ? { name: baseName(file.name), content: '', valid: false, error: 'XSLT şablonu değil' }
            : { name: baseName(file.name), content, valid: true })
        } catch {
          picked.push({ name: baseName(file.name), content: '', valid: false, error: 'Okunamadı' })
        }
      }
      setFiles((prev) => {
        // Aynı ada sahip tekrarları (ör. klasör+dosya seçimi) ele.
        const seen = new Set(prev.map((f) => f.name))
        const merged = [...prev]
        for (const p of picked) if (!seen.has(p.name)) { merged.push(p); seen.add(p.name) }
        return merged.slice(0, MAX_FILES)
      })
    } finally {
      setReading(false)
    }
  }

  function removeFile(name: string) {
    setFiles((prev) => prev.filter((f) => f.name !== name))
  }

  const validFiles = files.filter((f) => f.valid)
  const canUpload =
    !uploading && !reading && validFiles.length > 0 &&
    (target === 'new' ? newName.trim().length > 0 : existingId.length > 0)

  async function handleUpload() {
    if (!canUpload) return
    setUploading(true)
    try {
      let folderId: string | null
      if (target === 'new') {
        const folder = await createFolder({ name: newName.trim(), kind: 'XsltTemplate', color: newColor })
        folderId = folder.id
      } else {
        folderId = existingId
      }

      const items: BulkUploadItem[] = validFiles.map((f) => ({ name: f.name, xsltContent: f.content }))
      // Büyük yüklemeleri partilere böl (tek dev JSON gövdesi 30 MB sınırını aşıyordu).
      const batches = buildBatches(items)
      let createdCount = 0
      let skippedCount = 0
      for (let i = 0; i < batches.length; i++) {
        if (batches.length > 1) setProgress(`${i + 1}/${batches.length}`)
        const res = await bulkUploadUserXslt(folderId, batches[i])
        createdCount += res.created.length
        skippedCount += res.skipped.length
      }

      if (createdCount > 0) {
        toast.success(
          skippedCount > 0
            ? `${createdCount} şablon yüklendi, ${skippedCount} dosya atlandı.`
            : `${createdCount} şablon yüklendi.`,
          { durationMs: 5000 },
        )
      } else {
        toast.error('Hiçbir şablon yüklenemedi.', { durationMs: 5000 })
      }
      onUploaded(folderId)
      onClose()
    } catch (err: unknown) {
      const status = (err as { response?: { status?: number } })?.response?.status
      toast.error(
        status === 402
          ? "Toplu yükleme XsltCraft Pro üyeliği gerektirir."
          : 'Yükleme başarısız.',
        { durationMs: 5000 },
      )
    } finally {
      setUploading(false)
      setProgress('')
    }
  }

  const pickerCls =
    'flex flex-1 cursor-pointer items-center justify-center gap-2 rounded-lg border border-dashed border-field-line py-3 text-[13px] text-tx-2 transition-colors hover:border-ui-accent hover:bg-ui-hover focus-within:outline-2 focus-within:outline-offset-2 focus-within:outline-ui-accent'

  return (
    <ModalShell
      title="Klasör olarak toplu yükle"
      subtitle={<>Birden çok <code className="font-mono">.xslt</code> dosyasını tek seferde bir klasöre aktar.</>}
      onClose={onClose}
      width="max-w-lg"
    >
      {/* Dosya / klasör seçiciler */}
      <div className="mb-4 flex gap-2">
        <label className={pickerCls}>
          <FileCode2 size={16} aria-hidden="true" className="text-ui-accent" /> Dosya seç
          <input type="file" multiple accept=".xsl,.xslt" className="sr-only" onChange={(e) => { ingest(e.target.files); e.target.value = '' }} />
        </label>
        <label className={pickerCls}>
          <FolderUp size={16} aria-hidden="true" className="text-ui-accent" /> Klasör seç
          <input ref={dirRef} type="file" className="sr-only" onChange={(e) => { ingest(e.target.files); e.target.value = '' }} />
        </label>
      </div>

      {/* Hedef klasör */}
      <fieldset className="m-0 mb-4 flex flex-col gap-2 border-0 p-0">
        <legend className="sr-only">Hedef klasör</legend>
        <label className="flex cursor-pointer items-center gap-2 text-[13px] text-tx-2">
          <input type="radio" name={targetName} checked={target === 'new'} onChange={() => setTarget('new')} className="accent-(--ui-accent)" />
          Yeni klasör
        </label>
        {target === 'new' && (
          <div className="flex items-center gap-2 pl-6">
            <input
              type="text"
              value={newName}
              onChange={(e) => setNewName(e.target.value)}
              placeholder="Klasör adı"
              aria-label="Yeni klasör adı"
              maxLength={60}
              className={`${MODAL_INPUT} flex-1 py-1.5`}
            />
            <div role="group" aria-label="Klasör rengi" className="flex items-center gap-1.5">
              {FOLDER_COLORS.map((c) => (
                <button
                  key={c}
                  type="button"
                  onClick={() => setNewColor(c)}
                  aria-label={FOLDER_COLOR_NAME[c]}
                  aria-pressed={newColor === c}
                  title={FOLDER_COLOR_NAME[c]}
                  className={`xc-focus h-5 w-5 rounded-full ${folderDot(c)} ${newColor === c ? 'ring-2 ring-tx-muted ring-offset-1 ring-offset-ui-card' : ''}`}
                />
              ))}
            </div>
          </div>
        )}
        <label className={`flex items-center gap-2 text-[13px] ${folders.length === 0 ? 'cursor-not-allowed text-tx-subtle opacity-60' : 'cursor-pointer text-tx-2'}`}>
          <input type="radio" name={targetName} checked={target === 'existing'} disabled={folders.length === 0} onChange={() => setTarget('existing')} className="accent-(--ui-accent)" />
          Mevcut klasör
        </label>
        {target === 'existing' && (
          <div className="pl-6">
            <select
              value={existingId}
              onChange={(e) => setExistingId(e.target.value)}
              aria-label="Mevcut klasör"
              className={`${MODAL_INPUT} py-1.5`}
            >
              {folders.map((f) => (
                <option key={f.id} value={f.id}>{f.name}</option>
              ))}
            </select>
          </div>
        )}
      </fieldset>

      {/* Dosya listesi */}
      {files.length > 0 && (
        <div className="mb-4">
          <div className="mb-1.5 flex items-center justify-between text-xs text-tx-muted">
            <span className="font-semibold uppercase tracking-[.07em]">Yüklenecekler</span>
            <span>{validFiles.length} geçerli{files.length !== validFiles.length ? ` · ${files.length - validFiles.length} hatalı` : ''}</span>
          </div>
          <ul className="m-0 max-h-44 list-none divide-y divide-ui-line overflow-y-auto rounded-lg border border-ui-line p-0">
            {files.map((f) => (
              <li key={f.name} className="flex items-center gap-2 px-3 py-1.5 text-[13px]">
                {f.valid
                  ? <FileCode2 size={14} aria-hidden="true" className="shrink-0 text-tx-subtle" />
                  : <AlertCircle size={14} aria-hidden="true" className="shrink-0 text-fn-orange" />}
                <span className={`flex-1 truncate font-mono text-[12.5px] ${f.valid ? 'text-tx-2' : 'text-tx-subtle'}`}>{f.name}</span>
                {f.error && <span className="shrink-0 text-xs text-fn-orange-ink">{f.error}</span>}
                <button
                  type="button"
                  onClick={() => removeFile(f.name)}
                  aria-label={`${f.name} listeden çıkar`}
                  className="xc-focus rounded p-0.5 text-tx-subtle transition-colors hover:text-fn-red-ink"
                >
                  <Trash2 size={13} aria-hidden="true" />
                </button>
              </li>
            ))}
          </ul>
        </div>
      )}

      <div className="mt-6 flex justify-end gap-2">
        <button type="button" onClick={onClose} className={MODAL_SECONDARY}>İptal</button>
        <button type="button" onClick={handleUpload} disabled={!canUpload} className={MODAL_PRIMARY}>
          {uploading ? <Loader2 size={15} aria-hidden="true" className="animate-spin motion-reduce:animate-none" /> : <Upload size={15} aria-hidden="true" />}
          {uploading ? `Yükleniyor…${progress ? ` ${progress}` : ''}` : `Yükle${validFiles.length ? ` (${validFiles.length})` : ''}`}
        </button>
      </div>
    </ModalShell>
  )
}
