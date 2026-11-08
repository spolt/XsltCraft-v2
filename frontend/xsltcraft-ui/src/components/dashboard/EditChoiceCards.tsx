import { useEffect, useId, useState, type ReactNode } from 'react'
import { AlertTriangle, ArrowRight, Check, Lock, RotateCw, ShieldAlert, Sparkles } from 'lucide-react'
import type { EasyEditAnalyzeResponse } from '../../services/easyEditService'
import type { UploadAnalysis } from '../../hooks/useUploadAnalysis'
import { ASSET_LABEL } from '../theme-assets/imageState'
import { openUpgradeModal } from '../../store/upgradeModalStore'

const RECOMMENDED = '#5B8DEF'
const EDITOR_FEATURES = ['Sözdizimi ve UBL-TR doğrulama', 'AI asistanı', 'Canlı önizleme']
/** Banka / IBAN alanına ayrılmış konum grubu. */
const BANK_GROUP = /banka|iban/i

interface Props {
  analysis: UploadAnalysis
  /** Aktarım hazırlanırken (XML okunuyor) iki CTA da beklemede. */
  opening: boolean
  onRetry: () => void
  onOpenEasyEdit: () => void
  /** `initialLine` yalnız "Editörde düzelt"te. */
  onOpenEditor: (initialLine?: number) => void
}

interface Chip {
  label: string
  /** Desteklenmiyorsa üstü çizili gösterilir; neden kart altında yazar. */
  unsupported?: string
}

/** Kolay düzenle kartındaki çipler: görsel rolleri, konum grupları (`features`), banka ve sabit not. */
function featureChips(a: EasyEditAnalyzeResponse): Chip[] {
  const chips: Chip[] = []
  for (const kind of ['logo', 'signature'] as const) {
    const count = a.images.filter((i) => i.role === kind).length
    if (count > 0) chips.push({ label: `${ASSET_LABEL[kind]} · dosyada ${count}` })
  }
  for (const f of a.features) {
    if (f.count > 0) chips.push({ label: `${f.group} · ${f.count} konum` })
  }
  const banks = a.previous?.bank.length ?? 0
  if (banks > 0) chips.push({ label: `Banka / IBAN · ${banks} hesap` })
  else if (a.features.some((f) => f.count > 0 && BANK_GROUP.test(f.group))) chips.push({ label: 'Banka / IBAN' })
  chips.push(
    a.notesSupported
      ? { label: 'Sabit not' }
      : { label: 'Sabit not', unsupported: 'Sabit not eklenemez: dosyada notların yeri bulunamadı.' },
  )
  return chips
}

type Tone = 'warn' | 'danger' | 'info'

interface View {
  easyRecommended: boolean
  easyEnabled: boolean
  /** Kolay düzenle CTA'sı neden kapalı / kart altındaki bilgi. */
  easyNote: { tone: Tone; text: string } | null
  chips: Chip[] | 'skeleton' | null
  locked: boolean
  retry: { message: string; retryAfterSeconds?: number } | null
  editorCta: string
  editorLine?: number
  editorNote: string | null
  announce: string
}

function buildView(a: UploadAnalysis): View {
  const base: View = {
    easyRecommended: false,
    easyEnabled: false,
    easyNote: null,
    chips: null,
    locked: false,
    retry: null,
    editorCta: 'Editörde aç',
    editorNote: null,
    announce: '',
  }
  switch (a.status) {
    case 'idle':
      return base
    case 'analyzing':
      return { ...base, easyRecommended: true, chips: 'skeleton', easyNote: { tone: 'info', text: 'Dosya analiz ediliyor…' }, announce: 'Dosya analiz ediliyor.' }
    case 'ready': {
      const chips = featureChips(a.analysis)
      const partial = a.analysis.verdict === 'partial'
      const warn = partial
        ? a.analysis.warnings.map((w) => w.message).filter(Boolean).slice(0, 2).join(' ')
        : ''
      const unsupported = chips.map((c) => c.unsupported).filter(Boolean).join(' ')
      const text = [partial ? 'Bazı alanlar desteklenmiyor.' : '', unsupported, warn].filter(Boolean).join(' ')
      return {
        ...base,
        easyRecommended: true,
        easyEnabled: true,
        chips,
        easyNote: text ? { tone: 'warn', text } : null,
        announce: partial ? 'Dosya Kolay düzenle için kısmen uygun.' : 'Dosya Kolay düzenle için uygun.',
      }
    }
    case 'unsuitable':
      return { ...base, easyNote: { tone: 'warn', text: a.message }, announce: `Kolay düzenle kullanılamıyor: ${a.message}` }
    case 'invalid': {
      const where = a.line ? `satır ${a.line}: ` : ''
      const text = `XML hatası var (${where}${a.message})`
      return { ...base, easyNote: { tone: 'danger', text }, editorCta: 'Editörde düzelt', editorLine: a.line ?? undefined, announce: text }
    }
    case 'rejected':
      return {
        ...base,
        easyNote: { tone: 'danger', text: a.message },
        editorNote: 'Editörde açabilirsin; ancak önizleme de aynı denetim nedeniyle reddedilecek.',
        announce: a.message,
      }
    case 'fileError':
      return { ...base, easyNote: { tone: 'danger', text: a.message }, announce: a.message }
    case 'retry':
      return { ...base, retry: { message: a.message, retryAfterSeconds: a.retryAfterSeconds }, easyNote: { tone: 'info', text: a.message }, announce: a.message }
    case 'locked':
      return { ...base, locked: true, announce: 'Kolay düzenle XsltCraft Pro ile kullanılabilir.' }
  }
}

const NOTE_CLASS: Record<Tone, string> = {
  warn: 'border-ui-workspace-warn/50 bg-ui-workspace-warn/10 text-ui-workspace-warn',
  danger: 'border-fn-red-line bg-fn-red-tint text-fn-red-ink',
  info: 'border-ui-workspace-line bg-ui-workspace text-ui-workspace-muted',
}

/** 429'da `Retry-After` dolana dek "Tekrar dene" kapalı. */
function RetryButton({ seconds, onRetry }: { seconds?: number; onRetry: () => void }) {
  const [left, setLeft] = useState(seconds && seconds > 0 ? Math.ceil(seconds) : 0)
  useEffect(() => {
    if (left <= 0) return
    const t = setTimeout(() => setLeft((n) => n - 1), 1000)
    return () => clearTimeout(t)
  }, [left])
  return (
    <button
      type="button"
      onClick={onRetry}
      disabled={left > 0}
      className="xc-focus inline-flex h-8 cursor-pointer items-center gap-1.5 rounded-md border border-ui-workspace-line px-3 text-[12.5px] font-medium text-ui-workspace-fg transition-colors hover:bg-ui-workspace disabled:cursor-not-allowed disabled:opacity-60"
    >
      <RotateCw size={13} strokeWidth={2} aria-hidden="true" />
      {left > 0 ? `Tekrar dene (${left} sn)` : 'Tekrar dene'}
    </button>
  )
}

function Badge() {
  return (
    <span
      className="inline-flex h-5 items-center rounded-full px-2 text-[11px] font-semibold text-white"
      style={{ backgroundColor: RECOMMENDED }}
    >
      Önerilen
    </span>
  )
}

function Card({ recommended, titleId, children }: { recommended: boolean; titleId: string; children: ReactNode }) {
  return (
    <section
      aria-labelledby={titleId}
      className={`flex min-w-0 flex-col gap-3 rounded-lg bg-ui-workspace-raised p-4 ${recommended ? 'border-2' : 'border border-ui-workspace-line'}`}
      style={recommended ? { borderColor: RECOMMENDED } : undefined}
    >
      {children}
    </section>
  )
}

function Cta({
  label,
  primary,
  enabled,
  describedBy,
  onClick,
}: {
  label: string
  primary: boolean
  enabled: boolean
  describedBy?: string
  onClick: () => void
}) {
  return (
    <button
      type="button"
      aria-disabled={!enabled}
      aria-describedby={describedBy}
      onClick={() => enabled && onClick()}
      className={`xc-focus mt-auto inline-flex h-10 w-full items-center justify-center gap-2 rounded-lg px-4 text-[13.5px] font-semibold transition-[filter,background-color] ${
        !enabled
          ? 'cursor-not-allowed border border-ui-workspace-line bg-transparent text-ui-workspace-muted opacity-70'
          : primary
            ? 'cursor-pointer bg-ui-accent text-ui-accent-fg hover:brightness-110'
            : 'cursor-pointer border border-ui-workspace-line text-ui-workspace-fg hover:bg-ui-workspace'
      }`}
    >
      {label}
      <ArrowRight size={15} strokeWidth={2} aria-hidden="true" />
    </button>
  )
}

/** XSLT seçildikten sonra "Nasıl devam etmek istersin?": Kolay düzenle | XSLT Editör. */
export default function EditChoiceCards({ analysis, opening, onRetry, onOpenEasyEdit, onOpenEditor }: Props) {
  const uid = useId()
  const easyTitle = `${uid}-easy`
  const editorTitle = `${uid}-editor`
  const easyNoteId = `${uid}-easy-note`
  const editorNoteId = `${uid}-editor-note`
  const v = buildView(analysis)
  const editorRecommended = !v.easyRecommended
  const easyCtaEnabled = v.easyEnabled && !opening

  function upsell() {
    openUpgradeModal({
      title: "Kolay düzenle Pro'ya dahil",
      message: "Dosyandaki logo, imza, banka ve not alanlarını kod yazmadan düzenlemek için XsltCraft Pro'ya geç.",
    })
  }

  return (
    <div className="flex flex-col gap-3">
      <h3 className="m-0 text-[14px] font-semibold text-ui-workspace-fg">Nasıl devam etmek istersin?</h3>
      <p role="status" aria-live="polite" className="sr-only">{v.announce}</p>

      <div className="grid grid-cols-1 gap-3 @xl:grid-cols-2">
        <Card recommended={v.easyRecommended} titleId={easyTitle}>
          <div className="flex items-center gap-2">
            {v.locked ? (
              <Lock size={16} strokeWidth={2} aria-hidden="true" className="text-ui-workspace-muted" />
            ) : (
              <Sparkles size={16} strokeWidth={2} aria-hidden="true" className="text-ui-workspace-fg" />
            )}
            <h4 id={easyTitle} className="m-0 text-[15px] font-semibold text-ui-workspace-fg">Kolay düzenle</h4>
            {v.easyRecommended && <Badge />}
            {v.locked && (
              <span className="ml-auto rounded-full border border-ui-workspace-line px-2 text-[11px] font-semibold text-ui-workspace-muted">
                Pro
              </span>
            )}
          </div>
          <p className="m-0 text-[12.5px] leading-snug text-ui-workspace-muted">
            Logo, imza, banka ve not alanlarını kod yazmadan değiştir.
          </p>

          {v.chips === 'skeleton' && (
            <ul aria-hidden="true" className="m-0 flex list-none flex-wrap gap-1.5 p-0">
              {[88, 112, 72].map((w) => (
                <li key={w} className="h-6 animate-pulse rounded-full bg-ui-workspace-line" style={{ width: w }} />
              ))}
            </ul>
          )}
          {Array.isArray(v.chips) && (
            <ul aria-label="Bulunan alanlar" className="m-0 flex list-none flex-wrap gap-1.5 p-0">
              {v.chips.map((c) => (
                <li
                  key={c.label}
                  className={`inline-flex h-6 items-center rounded-full border border-ui-workspace-line px-2.5 text-[12px] ${
                    c.unsupported ? 'text-ui-workspace-muted line-through' : 'text-ui-workspace-fg'
                  }`}
                >
                  {c.unsupported && <span className="sr-only">Desteklenmiyor: </span>}
                  {c.label}
                </li>
              ))}
            </ul>
          )}

          {v.easyNote && (
            <p id={easyNoteId} className={`m-0 flex gap-2 rounded-md border px-2.5 py-2 text-[12.5px] leading-snug ${NOTE_CLASS[v.easyNote.tone]}`}>
              {v.easyNote.tone === 'danger' ? (
                <ShieldAlert size={14} strokeWidth={2} aria-hidden="true" className="mt-0.5 flex-none" />
              ) : v.easyNote.tone === 'warn' ? (
                <AlertTriangle size={14} strokeWidth={2} aria-hidden="true" className="mt-0.5 flex-none" />
              ) : null}
              <span className="min-w-0 break-words">{v.easyNote.text}</span>
            </p>
          )}
          {v.retry && <RetryButton key={analysis.status + (v.retry.retryAfterSeconds ?? 0)} seconds={v.retry.retryAfterSeconds} onRetry={onRetry} />}

          {v.locked ? (
            <>
              <p id={easyNoteId} className="m-0 text-[12.5px] leading-snug text-ui-workspace-muted">
                Kolay düzenle XsltCraft Pro ile kullanılabilir.
              </p>
              <button
                type="button"
                onClick={upsell}
                className="xc-focus mt-auto inline-flex h-10 w-full cursor-pointer items-center justify-center gap-2 rounded-lg border border-ui-workspace-line px-4 text-[13.5px] font-semibold text-ui-workspace-fg transition-colors hover:bg-ui-workspace"
              >
                <Lock size={14} strokeWidth={2} aria-hidden="true" />
                Pro'ya geç
              </button>
            </>
          ) : (
            <Cta
              label={opening ? 'Açılıyor…' : 'Kolay düzenle ile aç'}
              primary={v.easyRecommended}
              enabled={easyCtaEnabled}
              describedBy={v.easyNote ? easyNoteId : undefined}
              onClick={onOpenEasyEdit}
            />
          )}
        </Card>

        <Card recommended={editorRecommended} titleId={editorTitle}>
          <div className="flex items-center gap-2">
            <h4 id={editorTitle} className="m-0 text-[15px] font-semibold text-ui-workspace-fg">XSLT Editör</h4>
            {editorRecommended && <Badge />}
          </div>
          <p className="m-0 text-[12.5px] leading-snug text-ui-workspace-muted">Kodu doğrudan düzenle; tam kontrol sende.</p>
          <ul className="m-0 flex list-none flex-col gap-1.5 p-0">
            {EDITOR_FEATURES.map((f) => (
              <li key={f} className="flex items-center gap-2 text-[12.5px] text-ui-workspace-fg">
                <Check size={14} strokeWidth={2} aria-hidden="true" className="flex-none text-ui-workspace-muted" />
                {f}
              </li>
            ))}
          </ul>
          {v.editorNote && (
            <p id={editorNoteId} className={`m-0 rounded-md border px-2.5 py-2 text-[12.5px] leading-snug ${NOTE_CLASS.warn}`}>
              {v.editorNote}
            </p>
          )}
          <Cta
            label={v.editorCta}
            primary={editorRecommended}
            enabled={!opening}
            describedBy={v.editorNote ? editorNoteId : undefined}
            onClick={() => onOpenEditor(v.editorLine)}
          />
        </Card>
      </div>
    </div>
  )
}
