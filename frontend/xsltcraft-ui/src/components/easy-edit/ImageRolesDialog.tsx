import { useEffect, useId, useRef, useState } from 'react'
import { ImageOff, Info, LoaderCircle, TriangleAlert } from 'lucide-react'
import type { EasyEditAnalyzeResponse, EasyEditImageBlocker, EasyEditImageRole } from '../../services/easyEditService'
import { safeImageSrc } from '../../services/easyEditGuards'
import { ROLE_OPTIONS, userRoleLabel } from '../theme-assets/roleOptions'
import { MODAL_PRIMARY, MODAL_SECONDARY } from '../storage/ModalShell'

interface Props {
  analysis: EasyEditAnalyzeResponse
  busy: boolean
  error: string | null
  onSubmit: (roles: EasyEditImageRole[]) => void
  onClose: () => void
}

/** Taşınamayan görselin kısa gerekçesi; bilinmeyen kodda genel metin. */
const BLOCKER_TEXT: Record<EasyEditImageBlocker, string> = {
  'no-data-url': 'görsel dosyaya gömülü değil (dış adres ya da okunamayan veri)',
  'format-unsupported': 'görselin biçimi desteklenmiyor (ör. SVG)',
  'dynamic-attribute': 'görselin bir özelliği XML verisinden hesaplanıyor',
}

const blockerText = (blocker: EasyEditImageBlocker | null): string => {
  const reason = blocker ? BLOCKER_TEXT[blocker] : undefined
  return reason ? `Bu görsel yerinde kalır, taşınamaz: ${reason}.` : 'Bu görsel yerinde kalır, taşınamaz.'
}

const FOCUSABLE = 'button:not(:disabled), input:not(:disabled), [href], [tabindex]:not([tabindex="-1"])'
/** Analiz sürerken denetimler `disabled` yerine `aria-disabled` olur: odak kaybolmaz, tıklama yok sayılır. */
const BUSY_CLS = 'aria-disabled:cursor-not-allowed aria-disabled:opacity-50'

function previousSummary(analysis: EasyEditAnalyzeResponse): string | null {
  const previous = analysis.previous
  if (!previous) return null
  const images = previous.images.filter((i) => i.valid)
  const parts = [
    [images.filter((i) => i.kind === 'logo').length, 'logo'],
    [images.filter((i) => i.kind === 'signature').length, 'imza / kaşe'],
    [previous.bank.length, 'banka hesabı'],
  ] as const
  const found = parts.filter(([n]) => n > 0).map(([n, label]) => `${n} ${label}`)
  if (found.length === 0) return 'Daha önce eklediklerin de bulundu; Kolay düzenle ekranında hazır gelecek.'
  const list = found.length > 1 ? `${found.slice(0, -1).join(', ')} ve ${found[found.length - 1]}` : found[0]
  return `Daha önce eklediklerin de bulundu: ${list}. Kolay düzenle ekranında hazır gelecek.`
}

/**
 * Tek soru modalı: dosyadaki görsellerin rolü. Önerilen roller seçili gelir; değişirse çağıran yeniden analiz eder.
 * Odak modal içinde kalır; Esc ve "Geri" seçimi değiştirmeden kapatır.
 */
export default function ImageRolesDialog({ analysis, busy, error, onSubmit, onClose }: Props) {
  const images = analysis.images ?? []
  const [roles, setRoles] = useState<EasyEditImageRole[]>(() => images.map((i) => i.role))
  const ref = useRef<HTMLDivElement>(null)
  const titleId = useId()
  const descId = useId()
  const name = useId()
  const summary = previousSummary(analysis)

  useEffect(() => {
    const opener = document.activeElement as HTMLElement | null
    const first = ref.current?.querySelector<HTMLElement>('input:checked') ?? ref.current?.querySelector<HTMLElement>(FOCUSABLE)
    first?.focus()
    return () => { if (opener?.isConnected) opener.focus() }
  }, [])

  function onKeyDown(e: React.KeyboardEvent) {
    if (e.key === 'Escape') {
      e.preventDefault()
      if (!busy) onClose()
      return
    }
    if (e.key !== 'Tab' || !ref.current) return
    // Odak tuzağı. Aynı radyo grubunda yalnız seçili girdi Tab sırasındadır.
    const items = Array.from(ref.current.querySelectorAll<HTMLElement>(FOCUSABLE))
      .filter((el) => !(el instanceof HTMLInputElement && el.type === 'radio' && !el.checked))
    if (items.length === 0) return
    const first = items[0]
    const last = items[items.length - 1]
    const active = document.activeElement
    const inside = active instanceof Node && ref.current.contains(active)
    if (e.shiftKey && (active === first || !inside)) {
      e.preventDefault()
      last.focus()
    } else if (!e.shiftKey && (active === last || !inside)) {
      e.preventDefault()
      first.focus()
    }
  }

  const duplicates = (['logo', 'signature'] as const).filter((r) => roles.filter((x) => x === r).length > 1)

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/50 p-4">
      <div
        ref={ref}
        role="dialog"
        aria-modal="true"
        aria-labelledby={titleId}
        aria-describedby={descId}
        data-testid="image-roles-dialog"
        onKeyDown={onKeyDown}
        className="flex max-h-[90vh] w-full max-w-xl flex-col rounded-xl border border-ui-line bg-ui-card text-tx shadow-[var(--ui-shadow-pop)]"
      >
        <div className="px-6 pt-6 pb-3">
          <h2 id={titleId} className="m-0 text-[17px] font-semibold text-tx">Başlamadan önce kısa bir soru</h2>
          <p id={descId} className="mt-1 mb-0 text-[13px] leading-normal text-tx-muted">
            Dosyandaki görselleri doğru yere koyabilmemiz için. Önerilerimizi seçtik — yanlışsa değiştir.
          </p>
        </div>

        <div className="flex flex-col gap-3 overflow-y-auto px-6 pb-3">
          {images.length > 0 && (
            <section aria-labelledby={`${titleId}-q`}>
              <h3 id={`${titleId}-q`} className="mt-0 mb-2 text-[13px] font-semibold text-tx">Bu görseller ne?</h3>
              <ul className="m-0 flex list-none flex-col gap-2 p-0">
                {images.map((img, i) => {
                  const src = safeImageSrc(img.previewDataUrl)
                  return (
                    <li key={img.index} className="flex flex-wrap items-center gap-3 rounded-lg border border-ui-line bg-ui-side p-2.5">
                      {src
                        ? <img src={src} alt="" className="h-12 w-20 flex-none rounded border border-ui-line bg-white object-contain" />
                        : (
                          <span className="flex h-12 w-20 flex-none items-center justify-center text-tx-subtle">
                            <ImageOff size={18} aria-hidden="true" />
                          </span>
                        )}
                      <div className="min-w-0 flex-1 text-[12px]">
                        <p className="m-0 font-semibold text-tx">
                          {`${i + 1}. görsel`}<span className="font-normal text-tx-subtle"> · satır {img.line}</span>
                        </p>
                        <p className="m-0 text-tx-muted">{img.reason}</p>
                        {!img.movable && roles[i] !== 'design' && (
                          <p className="m-0 text-tx-subtle">{blockerText(img.blocker)}</p>
                        )}
                      </div>
                      <div
                        role="radiogroup"
                        aria-label={`${i + 1}. görselin rolü`}
                        data-testid={`image-role-${img.index}`}
                        className="flex flex-none overflow-hidden rounded-md border border-field-line"
                      >
                        {ROLE_OPTIONS.map((o) => (
                          <label
                            key={o.value}
                            className={`cursor-pointer px-2.5 py-1.5 text-[11.5px] font-medium transition-colors focus-within:outline-2 focus-within:-outline-offset-2 focus-within:outline-ui-accent ${
                              roles[i] === o.value ? 'bg-ui-accent text-ui-accent-fg' : 'bg-ui-card text-tx-2 hover:bg-ui-hover'
                            }`}
                          >
                            <input
                              type="radio"
                              className="sr-only"
                              name={`${name}-${img.index}`}
                              value={o.value}
                              checked={roles[i] === o.value}
                              aria-disabled={busy || undefined}
                              onChange={() => { if (!busy) setRoles((r) => r.map((v, k) => (k === i ? o.value : v))) }}
                            />
                            {o.userLabel}
                          </label>
                        ))}
                      </div>
                    </li>
                  )
                })}
              </ul>
            </section>
          )}

          {duplicates.length > 0 && (
            <p role="status" className="m-0 flex gap-1.5 rounded-md border border-fn-orange-line bg-fn-orange-tint px-3 py-2 text-[12px] text-fn-orange-ink">
              <TriangleAlert size={14} aria-hidden="true" className="mt-px flex-none" />
              <span>
                {duplicates.map((r) => `“${userRoleLabel(r)}”`).join(' ve ')} için birden fazla görsel seçtin. Yalnız ilki
                Kolay düzenle'de taşınıp değiştirilebilir; diğerleri yerinde kalır.
              </span>
            </p>
          )}

          {summary && (
            <p className="m-0 flex gap-1.5 rounded-md border border-ui-soft-line bg-ui-soft px-3 py-2 text-[12px] text-tx-2">
              <Info size={14} aria-hidden="true" className="mt-px flex-none text-ui-accent" />
              <span>{summary}</span>
            </p>
          )}

          {error && (
            <p role="alert" className="m-0 rounded-md border border-fn-red-line bg-fn-red-tint px-3 py-2 text-[12px] text-fn-red-ink">
              {error}
            </p>
          )}
        </div>

        <div className="flex flex-wrap items-center gap-3 border-t border-ui-line px-6 py-4">
          <p className="m-0 min-w-0 flex-1 text-[11.5px] text-tx-subtle">
            Bu seçimleri sonra Kolay düzenle ekranındaki “Analiz” bölümünden değiştirebilirsin.
          </p>
          <button
            type="button"
            className={`${MODAL_SECONDARY} ${BUSY_CLS}`}
            onClick={() => { if (!busy) onClose() }}
            aria-disabled={busy || undefined}
          >
            Geri
          </button>
          <button
            type="button"
            className={`${MODAL_PRIMARY} ${BUSY_CLS}`}
            onClick={() => { if (!busy) onSubmit(roles) }}
            aria-disabled={busy || undefined}
            aria-busy={busy || undefined}
            data-testid="image-roles-submit"
          >
            {busy && <LoaderCircle size={14} aria-hidden="true" className="animate-spin motion-reduce:animate-none" />}
            {busy ? 'Analiz ediliyor…' : 'Devam et'}
          </button>
        </div>
      </div>
    </div>
  )
}
