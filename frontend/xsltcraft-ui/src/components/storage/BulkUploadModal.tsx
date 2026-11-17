import { useEffect, useId, useRef, useState } from 'react'
import { Upload, FolderUp, FileCode2, Trash2, AlertCircle, Loader2 } from 'lucide-react'

import { createFolder, type Folder } from '../../services/folderService'
import { bulkUploadUserXslt, type BulkUploadItem } from '../../services/userXsltService'
import { FOLDER_COLORS, FOLDER_COLOR_NAME, folderDot } from './folderColors'
import ModalShell, { MODAL_INPUT, MODAL_PRIMARY, MODAL_SECONDARY } from './ModalShell'
import BulkProgress from './BulkProgress'
import { toast } from '../../store/toastStore'
import { validateUploadContent } from '../../utils/uploadValidation'

const MAX_FILES = 500
const MAX_BYTES = 2 * 1024 * 1024
// İstekleri Kestrel'in 30 MB gövde sınırının güvenle altında tutmak için boyut-bazlı parti.
// ~10 MB ham içerik → JSON kaçışı sonrası dahi sınırın altında kalır.
const MAX_BATCH_BYTES = 10 * 1024 * 1024
const MAX_BATCH_COUNT = 50
// Tarama bu süreden kısa sürerse ilerleme paneli hiç gösterilmez (titremeyi önler).
const SCAN_PANEL_DELAY_MS = 300

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

interface Progress {
  done: number
  total: number
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
  const [scan, setScan] = useState<Progress | null>(null)
  const [upload, setUpload] = useState<Progress | null>(null)
  const busy = reading || uploading
  // Görsel meşguliyet: tarama göstergeleri yalnız gecikmeden sonra (scan dolunca) görünür; kilitler `busy` ile anında.
  const showBusy = uploading || scan !== null
  const [target, setTarget] = useState<'new' | 'existing'>(defaultFolderId ? 'existing' : 'new')
  const [newName, setNewName] = useState('')
  const [newColor, setNewColor] = useState<string>('blue')
  const [existingId, setExistingId] = useState<string>(defaultFolderId ?? folders[0]?.id ?? '')

  const dirRef = useRef<HTMLInputElement>(null)
  const targetName = useId()
  // ingest dosya okumayı beklerken güncel listeyi görebilsin (kapanıştaki `files` bayat kalabilir).
  const filesRef = useRef<PickedFile[]>(files)
  useEffect(() => { filesRef.current = files }, [files])

  // webkitdirectory React tiplerinde yok; ref ile attribute ekle.
  useEffect(() => {
    dirRef.current?.setAttribute('webkitdirectory', '')
    dirRef.current?.setAttribute('directory', '')
  }, [])

  async function ingest(fileList: FileList | null) {
    if (!fileList || fileList.length === 0) return
    // Klasör seçiminde XSLT dışı dosyalar da gelir; sayacı yalnız XSLT'ler üzerinden tut.
    const all = Array.from(fileList).filter((f) => /\.(xsl|xslt)$/i.test(f.name))
    if (all.length === 0) {
      toast.warning('Seçimde .xsl / .xslt dosyası bulunamadı.')
      return
    }
    setReading(true)
    // Kısa seçimlerde panel bir an görünüp kaybolmasın: yalnız tarama gecikmeyi aşarsa göster.
    let done = 0
    let panelShown = false
    const panelTimer = setTimeout(() => {
      panelShown = true
      setScan({ done, total: all.length })
    }, SCAN_PANEL_DELAY_MS)
    try {
      const picked: PickedFile[] = []
      for (const [i, file] of all.entries()) {
        done = i
        if (panelShown && i > 0) setScan({ done: i, total: all.length })
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
      // Aynı ada sahip tekrarları (ör. klasör+dosya seçimi) ele.
      const prev = filesRef.current
      const seen = new Set(prev.map((f) => f.name))
      const merged = [...prev]
      for (const p of picked) if (!seen.has(p.name)) { merged.push(p); seen.add(p.name) }
      const dropped = merged.length - MAX_FILES
      const next = dropped > 0 ? merged.slice(0, MAX_FILES) : merged
      filesRef.current = next
      setFiles(next)
      if (dropped > 0) {
        toast.warning(
          `Tek seferde en fazla ${MAX_FILES} dosya yüklenebilir. ${dropped} dosya listeye eklenmedi; bunları ayrı bir yüklemede aktarabilirsin.`,
          { durationMs: 8000 },
        )
      }
    } finally {
      clearTimeout(panelTimer)
      setReading(false)
      setScan(null)
    }
  }

  function removeFile(name: string) {
    setFiles((prev) => prev.filter((f) => f.name !== name))
  }

  const validFiles = files.filter((f) => f.valid)
  const canUpload =
    !busy && validFiles.length > 0 &&
    (target === 'new' ? newName.trim().length > 0 : existingId.length > 0)

  async function handleUpload() {
    if (!canUpload) return
    setUploading(true)
    setUpload({ done: 0, total: validFiles.length })
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
      let sent = 0
      for (const batch of batches) {
        const res = await bulkUploadUserXslt(folderId, batch)
        createdCount += res.created.length
        skippedCount += res.skipped.length
        sent += batch.length
        setUpload({ done: sent, total: items.length })
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
      setUpload(null)
    }
  }

  // Yükleme sürerken kapatmak isteklerin arka planda yarım kalmasına ve sonucun görülmemesine yol açar.
  function guardedClose() {
    if (!uploading) onClose()
  }

  const pickerCls =
    'flex flex-1 items-center justify-center gap-2 rounded-lg border border-dashed border-field-line py-3 text-[13px] text-tx-2 transition-colors focus-within:outline-2 focus-within:outline-offset-2 focus-within:outline-ui-accent ' +
    (showBusy ? 'cursor-not-allowed opacity-50' : 'cursor-pointer hover:border-ui-accent hover:bg-ui-hover')

  return (
    <ModalShell
      title="Klasör olarak toplu yükle"
      subtitle={<>Birden çok <code className="font-mono">.xslt</code> dosyasını tek seferde bir klasöre aktar (en fazla {MAX_FILES}).</>}
      onClose={guardedClose}
      width="max-w-lg"
    >
      {/* Dosya / klasör seçiciler — tarama/yükleme sürerken kilitli */}
      <div className="mb-4 flex gap-2">
        <label className={pickerCls}>
          <FileCode2 size={16} aria-hidden="true" className="text-ui-accent" /> Dosya seç
          <input type="file" multiple accept=".xsl,.xslt" disabled={busy} className="sr-only" onChange={(e) => { ingest(e.target.files); e.target.value = '' }} />
        </label>
        <label className={pickerCls}>
          <FolderUp size={16} aria-hidden="true" className="text-ui-accent" /> Klasör seç
          <input ref={dirRef} type="file" disabled={busy} className="sr-only" onChange={(e) => { ingest(e.target.files); e.target.value = '' }} />
        </label>
      </div>

      {scan && <BulkProgress label="Dosyalar taranıyor" done={scan.done} total={scan.total} unit="dosya" />}

      {/* Hedef klasör */}
      <fieldset disabled={uploading} className="m-0 mb-4 flex flex-col gap-2 border-0 p-0">
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
                  disabled={busy}
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

      {upload && (
        <BulkProgress
          label="Şablonlar yükleniyor"
          done={upload.done}
          total={upload.total}
          unit="şablon"
          hint="Yükleme bitene kadar bu pencereyi kapatma."
        />
      )}

      <div className="mt-6 flex justify-end gap-2">
        <button type="button" onClick={onClose} disabled={uploading} className={`${MODAL_SECONDARY} disabled:cursor-not-allowed disabled:opacity-50`}>İptal</button>
        <button type="button" onClick={handleUpload} disabled={!canUpload} className={MODAL_PRIMARY}>
          {showBusy ? <Loader2 size={15} aria-hidden="true" className="animate-spin motion-reduce:animate-none" /> : <Upload size={15} aria-hidden="true" />}
          {uploading ? 'Yükleniyor…' : scan ? 'Taranıyor…' : `Yükle${validFiles.length ? ` (${validFiles.length})` : ''}`}
        </button>
      </div>
    </ModalShell>
  )
}
