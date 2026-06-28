import { useEffect, useRef, useState } from 'react'
import { X, Upload, FolderUp, FileCode2, Trash2, AlertCircle, Loader2 } from 'lucide-react'

import { createFolder, type Folder } from '../../services/folderService'
import { bulkUploadUserXslt, type BulkUploadItem } from '../../services/userXsltService'
import { FOLDER_COLORS, folderDot } from './folderColors'
import { toast } from '../../store/toastStore'

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
          picked.push({ name: baseName(file.name), content, valid: true })
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

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/60" onClick={onClose}>
      <div className="bg-white rounded-xl shadow-2xl w-full max-w-lg mx-4 p-6" onClick={(e) => e.stopPropagation()}>
        <div className="flex items-center justify-between mb-1">
          <h2 className="text-lg font-semibold text-gray-800">Klasör olarak toplu yükle</h2>
          <button onClick={onClose} className="text-gray-400 hover:text-gray-600 transition-colors">
            <X size={18} />
          </button>
        </div>
        <p className="text-sm text-gray-500 mb-4">Birden çok <code>.xslt</code> dosyasını tek seferde bir klasöre aktar.</p>

        {/* Dosya / klasör seçiciler */}
        <div className="flex gap-2 mb-4">
          <label className="flex-1 flex items-center justify-center gap-2 border border-dashed border-gray-300 rounded-lg py-3 text-sm text-gray-600 hover:bg-gray-50 hover:border-blue-400 cursor-pointer transition-colors">
            <FileCode2 size={16} className="text-blue-600" /> Dosya seç
            <input type="file" multiple accept=".xsl,.xslt" className="hidden" onChange={(e) => { ingest(e.target.files); e.target.value = '' }} />
          </label>
          <label className="flex-1 flex items-center justify-center gap-2 border border-dashed border-gray-300 rounded-lg py-3 text-sm text-gray-600 hover:bg-gray-50 hover:border-blue-400 cursor-pointer transition-colors">
            <FolderUp size={16} className="text-blue-600" /> Klasör seç
            <input ref={dirRef} type="file" className="hidden" onChange={(e) => { ingest(e.target.files); e.target.value = '' }} />
          </label>
        </div>

        {/* Hedef klasör */}
        <div className="mb-4 space-y-2">
          <label className="flex items-center gap-2 text-sm text-gray-700 cursor-pointer">
            <input type="radio" checked={target === 'new'} onChange={() => setTarget('new')} className="accent-blue-600" />
            Yeni klasör
          </label>
          {target === 'new' && (
            <div className="flex items-center gap-2 pl-6">
              <input
                type="text"
                value={newName}
                onChange={(e) => setNewName(e.target.value)}
                placeholder="Klasör adı"
                maxLength={60}
                className="flex-1 border border-gray-300 rounded-lg px-3 py-1.5 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500 focus:border-blue-500"
              />
              <div className="flex items-center gap-1.5">
                {FOLDER_COLORS.map((c) => (
                  <button
                    key={c}
                    type="button"
                    onClick={() => setNewColor(c)}
                    className={`w-5 h-5 rounded-full ${folderDot(c)} ${newColor === c ? 'ring-2 ring-offset-1 ring-gray-400' : ''}`}
                    title={c}
                  />
                ))}
              </div>
            </div>
          )}
          <label className={`flex items-center gap-2 text-sm cursor-pointer ${folders.length === 0 ? 'text-gray-300 cursor-not-allowed' : 'text-gray-700'}`}>
            <input type="radio" checked={target === 'existing'} disabled={folders.length === 0} onChange={() => setTarget('existing')} className="accent-blue-600" />
            Mevcut klasör
          </label>
          {target === 'existing' && (
            <div className="pl-6">
              <select
                value={existingId}
                onChange={(e) => setExistingId(e.target.value)}
                className="w-full border border-gray-300 rounded-lg px-3 py-1.5 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500 focus:border-blue-500"
              >
                {folders.map((f) => (
                  <option key={f.id} value={f.id}>{f.name}</option>
                ))}
              </select>
            </div>
          )}
        </div>

        {/* Dosya listesi */}
        {files.length > 0 && (
          <div className="mb-4">
            <div className="flex items-center justify-between text-xs text-gray-500 mb-1.5">
              <span className="font-medium uppercase tracking-wide">Yüklenecekler</span>
              <span>{validFiles.length} geçerli{files.length !== validFiles.length ? ` · ${files.length - validFiles.length} hatalı` : ''}</span>
            </div>
            <div className="border border-gray-100 rounded-lg max-h-44 overflow-y-auto divide-y divide-gray-50">
              {files.map((f) => (
                <div key={f.name} className="flex items-center gap-2 px-3 py-1.5 text-sm">
                  {f.valid
                    ? <FileCode2 size={14} className="text-gray-400 shrink-0" />
                    : <AlertCircle size={14} className="text-amber-500 shrink-0" />}
                  <span className={`flex-1 truncate ${f.valid ? 'text-gray-700' : 'text-gray-400'}`}>{f.name}</span>
                  {f.error && <span className="text-xs text-amber-600 shrink-0">{f.error}</span>}
                  <button onClick={() => removeFile(f.name)} className="text-gray-300 hover:text-red-500 transition-colors p-0.5">
                    <Trash2 size={13} />
                  </button>
                </div>
              ))}
            </div>
          </div>
        )}

        <div className="flex justify-end gap-2 mt-6">
          <button onClick={onClose} className="px-4 py-2 text-sm font-medium text-gray-600 hover:bg-gray-100 rounded-lg transition-colors">İptal</button>
          <button
            onClick={handleUpload}
            disabled={!canUpload}
            className="flex items-center gap-1.5 px-4 py-2 text-sm font-medium text-white bg-blue-600 hover:bg-blue-700 rounded-lg transition-colors disabled:opacity-50 disabled:cursor-not-allowed"
          >
            {uploading ? <Loader2 size={15} className="animate-spin" /> : <Upload size={15} />}
            {uploading ? `Yükleniyor…${progress ? ` ${progress}` : ''}` : `Yükle${validFiles.length ? ` (${validFiles.length})` : ''}`}
          </button>
        </div>
      </div>
    </div>
  )
}
