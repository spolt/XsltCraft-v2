import type { MouseEvent, ReactNode } from 'react'
import { CheckCircle2, Code2, FileText, FileUp, type LucideIcon } from 'lucide-react'

interface Props {
  hasXslt: boolean
  hasXml: boolean
  xsltName: string | null
  xmlName: string | null
  /** Sayfa kökündeki tek gizli input'ları tetikler (e2e strict mode: ikinci input yok). */
  onUploadXslt: () => void
  onUploadXml: () => void
  /** Örnek belgenin etiketi (ör. "Örnek fatura XML"); tür XSLT'den çıkarılır. */
  sampleXmlLabel: string
  /** XML yokken varsayılan örnek XML ile devam eder. */
  onUseSampleXml: () => void
  /** "Kayıtlı şablonlar" bağlantısı — kaydedilmemiş değişiklik korumasından geçer. */
  onOpenSaved: () => void
}

/** Faz 1 / 1b: iki dosyadan biri ya da ikisi de yokken yükleme kartları. */
export default function XsltEmptyState({
  hasXslt,
  hasXml,
  xsltName,
  xmlName,
  onUploadXslt,
  onUploadXml,
  sampleXmlLabel,
  onUseSampleXml,
  onOpenSaved,
}: Props) {
  const description = !hasXslt && !hasXml
    ? 'XSLT ve XML dosyalarınızı yükleyerek düzenlemeye başlayın.'
    : hasXslt
      ? 'XML dosyası yükleyin ya da örnek belgeyle devam edin.'
      : 'XSLT dosyası yükleyerek devam edin.'

  function openSaved(e: MouseEvent<HTMLAnchorElement>) {
    if (e.button !== 0 || e.metaKey || e.ctrlKey || e.shiftKey || e.altKey) return
    e.preventDefault()
    onOpenSaved()
  }

  return (
    <main className="flex min-w-0 flex-1 flex-col items-center justify-center gap-6 overflow-auto px-4 py-8">
      <div className="text-center">
        <h1 className="mb-1 text-xl font-semibold text-tx">XSLT Editör</h1>
        <p className="text-sm text-tx-muted">{description}</p>
      </div>

      {hasXslt && (
        <div role="status" className="flex items-center gap-2 rounded-lg border border-fn-green-line bg-fn-green-tint px-4 py-3 text-sm text-fn-green-ink">
          <CheckCircle2 size={15} aria-hidden="true" className="flex-none" />
          XSLT dosyası yüklendi — XML seçmeye hazır.
        </div>
      )}
      {hasXml && (
        <div role="status" className="flex items-center gap-2 rounded-lg border border-fn-green-line bg-fn-green-tint px-4 py-3 text-sm text-fn-green-ink">
          <CheckCircle2 size={15} aria-hidden="true" className="flex-none" />
          XML dosyası yüklendi — XSLT seçmeye hazır.
        </div>
      )}

      <div className="grid w-full max-w-[640px] gap-5 sm:grid-cols-2">
        <UploadCard
          icon={Code2}
          title="XSLT Dosyası Yükle"
          hint=".xsl, .xslt"
          loadedName={hasXslt ? xsltName : null}
          primary
          onClick={onUploadXslt}
        />
        <UploadCard
          icon={FileUp}
          title="XML Dosyası Yükle"
          hint=".xml — fatura veya irsaliye"
          loadedName={hasXml ? xmlName : null}
          onClick={onUploadXml}
          footer={hasXslt && !hasXml ? (
            <button
              type="button"
              onClick={onUseSampleXml}
              title={`${sampleXmlLabel} kullanılır`}
              className="xc-focus inline-flex items-center gap-1.5 rounded px-1 text-[13px] font-medium text-ui-accent transition-colors hover:text-ui-ink hover:underline"
            >
              <FileText size={14} aria-hidden="true" className="flex-none" />
              Varsayılan XML ile devam et
            </button>
          ) : undefined}
        />
      </div>

      <a
        href="/my-xslt-templates"
        onClick={openSaved}
        className="xc-focus rounded text-xs text-tx-subtle transition-colors hover:text-ui-accent"
      >
        veya kayıtlı şablonlarınızdan birini açın →
      </a>
    </main>
  )
}

function UploadCard({
  icon: Icon,
  title,
  hint,
  loadedName,
  primary = false,
  onClick,
  footer,
}: {
  icon: LucideIcon
  title: string
  hint: string
  /** Yüklüyse dosya adı (kart "değiştir" olarak çalışır). */
  loadedName: string | null
  primary?: boolean
  onClick: () => void
  /** Kartın altında, ayırıcı çizgiyle gösterilen ikincil eylem. */
  footer?: ReactNode
}) {
  const loaded = loadedName !== null
  return (
    <div
      className={`xc-lift flex flex-col rounded-xl border-2 border-dashed bg-ui-card transition-colors ${
        loaded ? 'border-fn-green-line' : 'border-ui-soft-line'
      }`}
    >
    <button
      type="button"
      onClick={onClick}
      className={`xc-focus group flex flex-1 flex-col items-center gap-3 rounded-xl px-8 py-8 text-center transition-colors ${
        loaded ? '' : 'hover:bg-ui-soft'
      }`}
    >
      <span
        aria-hidden="true"
        className={`flex h-12 w-12 items-center justify-center rounded-full ${
          loaded ? 'bg-fn-green-tint text-fn-green-ink' : primary ? 'bg-ui-accent text-ui-accent-fg' : 'bg-ui-soft text-ui-ink'
        }`}
      >
        {loaded ? <CheckCircle2 size={24} /> : <Icon size={24} />}
      </span>
      <span className={`text-sm font-semibold ${loaded ? 'text-fn-green-ink' : 'text-ui-ink'}`}>
        {loaded ? 'Yüklendi' : title}
      </span>
      <span className="max-w-full truncate font-mono text-xs text-tx-subtle">{loaded ? loadedName : hint}</span>
      {loaded && <span className="text-[11.5px] text-tx-muted">Değiştirmek için tıklayın</span>}
    </button>
    {footer && (
      <div className="mx-6 flex justify-center border-t border-ui-line py-3">{footer}</div>
    )}
    </div>
  )
}
