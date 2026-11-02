import { X } from 'lucide-react'

interface Props {
  name: string
  html: string
  loading: boolean
  actionLabel: string
  onAction: () => void
  onClose: () => void
}

/** Taslak / XSLT önizleme paneli (eski TemplatePreviewPanel ile aynı davranış; tokenlarla). Önizleme zemini beyaz. */
export default function LibraryPreviewPanel({ name, html, loading, actionLabel, onAction, onClose }: Props) {
  return (
    <section aria-label={`${name} önizlemesi`} className="flex min-w-0 flex-[1.4] flex-col overflow-hidden border-l border-ui-line">
      <div className="flex h-12 flex-none items-center gap-3 border-b border-ui-line bg-ui-card px-4">
        <span className="min-w-0 flex-1 truncate text-sm font-semibold text-tx">{name}</span>
        {loading && (
          <span role="status" className="flex flex-none items-center gap-1.5 text-xs text-ui-ink">
            <span aria-hidden="true" className="inline-block h-3 w-3 animate-spin rounded-full border-2 border-ui-accent border-t-transparent motion-reduce:animate-none" />
            Yükleniyor
          </span>
        )}
        <button
          type="button"
          onClick={onAction}
          disabled={loading}
          className="xc-focus inline-flex h-[34px] flex-none items-center rounded-md bg-ui-accent px-4 text-[13px] font-semibold text-ui-accent-fg transition-[filter] hover:brightness-110 disabled:cursor-not-allowed disabled:opacity-50"
        >
          {actionLabel}
        </button>
        <button
          type="button"
          onClick={onClose}
          aria-label="Önizlemeyi kapat"
          title="Kapat"
          className="xc-focus inline-flex h-8 w-8 flex-none items-center justify-center rounded-md text-tx-subtle transition-colors hover:bg-ui-hover hover:text-tx"
        >
          <X size={16} aria-hidden="true" />
        </button>
      </div>
      <div className="flex flex-1 justify-center overflow-auto bg-ui-hover">
        <iframe
          srcDoc={html || '<html><body style="background:#ffffff;margin:0"></body></html>'}
          sandbox="allow-same-origin allow-scripts allow-modals"
          className="border-none bg-white shadow-[var(--ui-shadow-pop)]"
          style={{ width: 860, minHeight: '100%', height: 'max(100%, 1200px)' }}
          title="Şablon Önizleme"
        />
      </div>
    </section>
  )
}
