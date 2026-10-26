import { useRef, type ChangeEvent } from 'react'
import { Check, X, type LucideIcon } from 'lucide-react'

interface Props {
  /** "XSLT şablonu" / "Belge XML" */
  label: string
  /** Boşken gösterilen uzantı ipucu, ör. ".xsl · .xslt" */
  hint: string
  accept: string
  icon: LucideIcon
  file: File | null
  onSelect: (file: File) => void
  onClear: () => void
}

/** Çalışma alanındaki dosya yuvası: tıklayınca / Enter / Space ile dosya seçici açılır. */
export default function FileSlot({ label, hint, accept, icon: Icon, file, onSelect, onClear }: Props) {
  const inputRef = useRef<HTMLInputElement>(null)

  function handleChange(e: ChangeEvent<HTMLInputElement>) {
    const f = e.target.files?.[0]
    e.target.value = ''
    if (f) onSelect(f)
  }

  return (
    <div className="relative min-w-0">
      <button
        type="button"
        onClick={() => inputRef.current?.click()}
        aria-label={file ? `${label}: ${file.name} — değiştir` : `${label} seç`}
        className={`xc-focus flex h-20 w-full min-w-0 cursor-pointer items-center gap-3.5 rounded-lg border px-4 pr-11 text-left transition-colors ${
          file
            ? 'border-ui-workspace-muted/50 bg-ui-workspace-raised'
            : 'border-ui-workspace-line bg-ui-workspace-raised/60 hover:border-ui-workspace-muted/60 hover:bg-ui-workspace-raised'
        }`}
      >
        <span
          aria-hidden="true"
          className="flex h-11 w-11 flex-none items-center justify-center rounded-lg border border-ui-workspace-line bg-ui-workspace"
        >
          {file ? (
            <Check size={20} strokeWidth={2} className="text-ui-workspace-fg" />
          ) : (
            <Icon size={20} strokeWidth={1.75} className="text-ui-workspace-muted" />
          )}
        </span>
        <span className="flex min-w-0 flex-col gap-0.5">
          <span className="text-[12px] font-medium leading-tight text-ui-workspace-muted">{label}</span>
          <span
            className={`truncate leading-snug text-ui-workspace-fg ${file ? 'font-mono text-[13px]' : 'text-[13.5px] font-medium'}`}
          >
            {file ? file.name : `Dosya seç · ${hint}`}
          </span>
        </span>
      </button>
      {file && (
        <button
          type="button"
          onClick={onClear}
          aria-label={`${label} dosyasını kaldır`}
          className="xc-focus absolute top-1/2 right-2 flex h-7 w-7 -translate-y-1/2 cursor-pointer items-center justify-center rounded-md text-ui-workspace-muted transition-colors hover:bg-ui-workspace hover:text-ui-workspace-fg"
        >
          <X size={15} strokeWidth={2} aria-hidden="true" />
        </button>
      )}
      <input
        ref={inputRef}
        type="file"
        accept={accept}
        className="hidden"
        tabIndex={-1}
        aria-label={`${label} dosyası`}
        onChange={handleChange}
      />
    </div>
  )
}
