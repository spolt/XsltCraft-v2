import { useId, useRef, useState } from 'react'
import { FileUp, Loader2 } from 'lucide-react'
import {
  createTheme,
  updateTheme,
  UploadDecisionRequiredError,
  type AdminTheme,
  type UploadDecision,
  type UploadDecisionRequest,
} from '../../../services/adminService'
import type { FreeTheme } from '../../../services/templateService'
import ThemeUploadDecisionDialog from '../ThemeUploadDecisionDialog'
import { Banner, BTN_PRIMARY, BTN_SECONDARY, INPUT, LABEL, SegmentGroup } from '../adminUi'

type DocType = 'Invoice' | 'Despatch'

const DOC_OPTIONS = [
  { value: 'Invoice', label: 'e-Fatura' },
  { value: 'Despatch', label: 'İrsaliye' },
] as const

const FEE_OPTIONS = [
  { value: 'free', label: 'Ücretsiz' },
  { value: 'pro', label: 'Ücretli (Pro)' },
] as const

function errorMessage(err: unknown, fallback: string): string {
  return (err as { response?: { data?: { message?: string } } })?.response?.data?.message ?? fallback
}

/** .xslt seçici: gizli input + etiket görünümünde bırakma alanı. */
function XsltFilePicker({ inputRef, file, onFile, compact = false }: {
  inputRef: React.RefObject<HTMLInputElement | null>
  file: File | null
  onFile: (f: File | null) => void
  compact?: boolean
}) {
  const id = useId()
  return (
    <label
      htmlFor={id}
      className={`group flex cursor-pointer flex-col items-center gap-1.5 rounded-[10px] border-[1.5px] border-dashed border-ui-soft-line bg-ui-soft text-center text-ui-ink transition-colors hover:border-ui-accent has-[:focus-visible]:outline-2 has-[:focus-visible]:outline-offset-2 has-[:focus-visible]:outline-ui-accent ${
        compact ? 'px-3 py-2.5' : 'px-3 py-5'
      }`}
    >
      <input
        ref={inputRef}
        id={id}
        type="file"
        accept=".xslt"
        onChange={(e) => onFile(e.target.files?.[0] ?? null)}
        className="sr-only"
      />
      <FileUp size={compact ? 16 : 22} aria-hidden="true" />
      <span className="text-[13px] font-semibold break-all">{file ? file.name : '.xslt dosyası seç'}</span>
      {!compact && (
        <span className="text-[11.5px] text-tx-muted">Yüklemeden sonra logo / imza alanları otomatik analiz edilir</span>
      )}
    </label>
  )
}

// ---- Yeni tema yükleme formu ----
export function UploadForm({ onCreated }: { onCreated: (t: AdminTheme) => void }) {
  const [name, setName] = useState('')
  const [docType, setDocType] = useState<DocType>('Invoice')
  const [file, setFile] = useState<File | null>(null)
  const [isPremium, setIsPremium] = useState(false)
  const [loading, setLoading] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [pending, setPending] = useState<UploadDecisionRequest | null>(null)
  const fileRef = useRef<HTMLInputElement>(null)

  const upload = async (decision?: UploadDecision) => {
    if (!file) return
    setLoading(true)
    setError(null)
    try {
      const created = await createTheme({ name, documentType: docType, file, isPremium, decision })
      setPending(null)
      onCreated(created)
      setName('')
      setDocType('Invoice')
      setFile(null)
      setIsPremium(false)
      if (fileRef.current) fileRef.current.value = ''
    } catch (err: unknown) {
      if (err instanceof UploadDecisionRequiredError) {
        setPending(err.request)
      } else {
        setPending(null)
        setError(errorMessage(err, 'Yükleme başarısız.'))
      }
    } finally {
      setLoading(false)
    }
  }

  const submit = (e: React.FormEvent) => {
    e.preventDefault()
    void upload()
  }

  return (
    <form
      onSubmit={submit}
      aria-labelledby="theme-upload-title"
      className="flex min-w-0 flex-[1_1_320px] flex-col gap-3.5 rounded-xl border border-ui-line bg-ui-card p-[18px] shadow-[var(--ui-shadow-rest)]"
    >
      <h2 id="theme-upload-title" className="m-0 text-[15px] font-semibold text-tx">Yeni Tema Yükle</h2>

      {error && <Banner>{error}</Banner>}

      <label className={LABEL}>
        Tema Adı
        <input required value={name} onChange={(e) => setName(e.target.value)} placeholder="UBL e-Fatura Teması" className={INPUT} />
      </label>

      <div className={LABEL}>
        <span>Döküman Tipi</span>
        <SegmentGroup label="Döküman tipi" options={DOC_OPTIONS} value={docType} onChange={setDocType} />
      </div>

      <div className={LABEL}>
        <span>Ücret</span>
        <SegmentGroup
          label="Ücret"
          options={FEE_OPTIONS}
          value={isPremium ? 'pro' : 'free'}
          onChange={(v) => setIsPremium(v === 'pro')}
        />
        <span className="text-[11.5px] font-normal text-tx-muted">Ücretli temayı yalnız Pro/Editör/Admin kullanabilir; herkes önizleyebilir.</span>
      </div>

      <XsltFilePicker inputRef={fileRef} file={file} onFile={setFile} />

      <button type="submit" disabled={loading || !file} className={BTN_PRIMARY}>
        {loading && <Loader2 size={14} className="animate-spin" aria-hidden="true" />}
        {loading ? 'Yükleniyor…' : 'Temayı yükle'}
      </button>

      {pending && (
        <ThemeUploadDecisionDialog
          request={pending}
          busy={loading}
          onDecide={(d) => void upload(d)}
          onCancel={() => setPending(null)}
        />
      )}
    </form>
  )
}

// ---- Satır içi güncelleme formu ----
export function UpdateRow({ theme, onUpdated, onCancel }: {
  theme: FreeTheme
  onUpdated: (t: AdminTheme) => void
  onCancel: () => void
}) {
  const [name, setName] = useState(theme.name)
  const [docType, setDocType] = useState<DocType>(theme.documentType)
  const [file, setFile] = useState<File | null>(null)
  const [isPremium, setIsPremium] = useState(theme.isPremium)
  const [loading, setLoading] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [pending, setPending] = useState<UploadDecisionRequest | null>(null)
  const fileRef = useRef<HTMLInputElement>(null)

  const save = async (decision?: UploadDecision) => {
    setLoading(true)
    setError(null)
    try {
      const updated = await updateTheme(theme.id, {
        name,
        documentType: docType,
        file: file ?? undefined,
        isPremium,
        decision,
      })
      setPending(null)
      onUpdated(updated)
      setFile(null)
      if (fileRef.current) fileRef.current.value = ''
    } catch (err: unknown) {
      if (err instanceof UploadDecisionRequiredError) {
        setPending(err.request)
      } else {
        setPending(null)
        setError(errorMessage(err, 'Güncelleme başarısız.'))
      }
    } finally {
      setLoading(false)
    }
  }

  const submit = (e: React.FormEvent) => {
    e.preventDefault()
    void save()
  }

  return (
    <form onSubmit={submit} aria-label={`${theme.name} temasını güncelle`} className="flex flex-col gap-3">
      {error && <Banner>{error}</Banner>}
      <div className="grid grid-cols-[repeat(auto-fit,minmax(180px,1fr))] items-end gap-3">
        <label className={LABEL}>
          Tema Adı
          <input value={name} onChange={(e) => setName(e.target.value)} className={INPUT} />
        </label>
        <div className={LABEL}>
          <span>Döküman Tipi</span>
          <SegmentGroup label="Döküman tipi" options={DOC_OPTIONS} value={docType} onChange={setDocType} />
        </div>
        <div className={LABEL}>
          <span>Ücret</span>
          <SegmentGroup
            label="Ücret"
            options={FEE_OPTIONS}
            value={isPremium ? 'pro' : 'free'}
            onChange={(v) => setIsPremium(v === 'pro')}
          />
        </div>
        <div className={LABEL}>
          <span>Yeni dosya (opsiyonel)</span>
          <XsltFilePicker inputRef={fileRef} file={file} onFile={setFile} compact />
        </div>
      </div>
      <div className="flex justify-end gap-2">
        <button type="button" onClick={onCancel} className={BTN_SECONDARY}>İptal</button>
        <button type="submit" disabled={loading} className={`${BTN_PRIMARY} h-9!`}>
          {loading ? 'Kaydediliyor…' : 'Kaydet'}
        </button>
      </div>

      {pending && (
        <ThemeUploadDecisionDialog
          request={pending}
          busy={loading}
          onDecide={(d) => void save(d)}
          onCancel={() => setPending(null)}
        />
      )}
    </form>
  )
}
