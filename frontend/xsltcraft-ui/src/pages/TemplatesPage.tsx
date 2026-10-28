import { useEffect, useRef, useState } from 'react'
import { useNavigate, useSearchParams } from 'react-router-dom'
import { BookOpen, Crown, Eye, Lock, X } from 'lucide-react'
import { getFreeThemes, type FreeTheme } from '../services/templateService'
import { previewFromStoredXslt } from '../services/previewService'
import { useEntitlementStore } from '../store/entitlementStore'
import { openUpgradeModal } from '../store/upgradeModalStore'
import PageHeader from '../components/ui/PageHeader'
import SegmentedTabs from '../components/ui/SegmentedTabs'
import { fnStyle } from '../theme/uiThemes'
import { DOC_TYPE_LABEL } from '../utils/workRoutes'
import defaultInvoiceXml from '../assets/default-invoice.xml?raw'

type DocType = 'Invoice' | 'Despatch'

const DOC_TABS = [
  { id: 'Invoice', label: 'e-Fatura / e-Arşiv' },
  { id: 'Despatch', label: 'e-İrsaliye' },
] as const satisfies readonly { id: DocType; label: string }[]

function isDocType(v: string | null): v is DocType {
  return v === 'Invoice' || v === 'Despatch'
}

export default function TemplatesPage() {
  const [themes, setThemes] = useState<FreeTheme[]>([])
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)
  const [searchParams, setSearchParams] = useSearchParams()
  const rawType = searchParams.get('type')
  const typeFilter: DocType = isDocType(rawType) ? rawType : 'Invoice'

  const [selectedTheme, setSelectedTheme] = useState<FreeTheme | null>(null)
  const [previewHtml, setPreviewHtml] = useState('')
  const [previewLoading, setPreviewLoading] = useState(false)
  const iframeRef = useRef<HTMLIFrameElement>(null)
  const navigate = useNavigate()
  const entitlements = useEntitlementStore((s) => s.entitlements)
  const canUsePremium = !!(entitlements?.canUsePremiumThemes || entitlements?.isPrivileged)

  // ?type yoksa (ya da geçersizse) Invoice'a sabitle — geçmişe yeni kayıt eklemeden.
  useEffect(() => {
    if (!isDocType(rawType)) setSearchParams({ type: 'Invoice' }, { replace: true })
  }, [rawType, setSearchParams])

  // Ücretli tema: önizleme serbest, "kullan" Pro gerektirir.
  function gatedUse(theme: FreeTheme) {
    if (theme.isPremium && !canUsePremium) {
      openUpgradeModal({
        title: 'Ücretli tema',
        message: 'Bu temayı kullanmak için XsltCraft Pro üyeliği gerekir. Önizleme serbesttir.',
      })
      return
    }
    navigate(`/theme-use/${theme.id}`)
  }

  useEffect(() => {
    getFreeThemes()
      .then(setThemes)
      .catch(() => setError('Temalar yüklenirken bir hata oluştu.'))
      .finally(() => setLoading(false))
  }, [])

  useEffect(() => {
    if (!selectedTheme) return
    let cancelled = false
    Promise.resolve()
      .then(() => { if (!cancelled) { setPreviewHtml(''); setPreviewLoading(true) } })
      .then(() => previewFromStoredXslt(selectedTheme.id, defaultInvoiceXml))
      .then((res) => { if (!cancelled) setPreviewHtml(res.html) })
      .catch(() => { if (!cancelled) setPreviewHtml('<html><body style="display:flex;align-items:center;justify-content:center;height:100%;color:#ef4444;font-family:sans-serif;font-size:13px">Önizleme alınamadı.</body></html>') })
      .finally(() => { if (!cancelled) setPreviewLoading(false) })
    return () => { cancelled = true }
  }, [selectedTheme])

  function handleUse() {
    if (!selectedTheme) return
    gatedUse(selectedTheme)
  }

  function changeType(t: DocType) {
    setSearchParams({ type: t }, { replace: true })
  }

  const filtered = themes.filter((t) => t.documentType === typeFilter)

  if (loading) {
    return (
      <div className="flex items-center justify-center py-24">
        <p role="status" className="text-sm text-tx-subtle">Yükleniyor...</p>
      </div>
    )
  }

  if (error) {
    return (
      <div className="flex items-center justify-center py-24">
        <p role="alert" className="text-sm text-fn-red-ink">{error}</p>
      </div>
    )
  }

  const compact = !!selectedTheme

  return (
    <div className="flex h-full overflow-hidden">
      {/* ── Liste ── */}
      <div
        className={`overflow-y-auto ${compact ? 'w-[360px] flex-none border-r border-ui-line px-5 py-6' : 'flex-1 px-8 pt-7 pb-9'}`}
      >
        <div className="flex flex-col gap-[18px]">
          {compact ? (
            <div className="flex flex-col gap-3">
              <h1 className="m-0 text-lg font-semibold text-tx">Tema Kütüphanesi</h1>
              <DocTypeTabs value={typeFilter} onChange={changeType} />
            </div>
          ) : (
            <PageHeader
              title="Tema Kütüphanesi"
              description="Hazır temayı seç, logo ve banka bilgilerini ekle, XSLT olarak indir."
              actions={<DocTypeTabs value={typeFilter} onChange={changeType} />}
            />
          )}

          <div className="flex items-center justify-end">
            <span className="text-[12.5px] text-tx-muted" aria-live="polite">{filtered.length} tema</span>
          </div>

          {filtered.length === 0 ? (
            <div className="flex flex-col items-center gap-2 rounded-xl border border-dashed border-ui-line bg-ui-card px-6 py-16 text-center">
              <BookOpen size={28} strokeWidth={1.6} aria-hidden="true" className="text-tx-subtle" />
              <p className="m-0 text-sm text-tx-muted">Bu kategoride henüz şablon yok.</p>
            </div>
          ) : compact ? (
            <ul className="m-0 flex list-none flex-col gap-2 p-0">
              {filtered.map((theme) => (
                <li key={theme.id}>
                  <CompactThemeRow
                    theme={theme}
                    selected={selectedTheme?.id === theme.id}
                    locked={theme.isPremium && !canUsePremium}
                    onSelect={() => setSelectedTheme(theme)}
                    onUse={() => gatedUse(theme)}
                  />
                </li>
              ))}
            </ul>
          ) : (
            <ul className="m-0 grid list-none grid-cols-[repeat(auto-fill,minmax(220px,1fr))] gap-[18px] p-0">
              {filtered.map((theme) => (
                <li key={theme.id} className="flex">
                  <ThemeCard
                    theme={theme}
                    locked={theme.isPremium && !canUsePremium}
                    onSelect={() => setSelectedTheme(theme)}
                    onUse={() => gatedUse(theme)}
                  />
                </li>
              ))}
            </ul>
          )}
        </div>
      </div>

      {/* ── Önizleme ── */}
      {selectedTheme && (
        <section aria-label={`${selectedTheme.name} önizlemesi`} className="flex min-w-0 flex-1 flex-col overflow-hidden">
          <div className="flex h-12 flex-none items-center gap-3 border-b border-ui-line bg-ui-card px-4">
            <span className="min-w-0 flex-1 truncate text-sm font-semibold text-tx">{selectedTheme.name}</span>
            {previewLoading && (
              <span role="status" className="flex flex-none items-center gap-1.5 text-xs text-ui-ink">
                <span aria-hidden="true" className="inline-block h-3 w-3 animate-spin rounded-full border-2 border-ui-accent border-t-transparent motion-reduce:animate-none" />
                Yükleniyor
              </span>
            )}
            <button
              type="button"
              onClick={handleUse}
              disabled={previewLoading}
              className="xc-focus inline-flex h-[34px] flex-none items-center gap-1.5 rounded-md bg-ui-accent px-4 text-[13px] font-semibold text-ui-accent-fg transition-[filter] hover:brightness-110 disabled:cursor-not-allowed disabled:opacity-50"
            >
              {selectedTheme.isPremium && !canUsePremium ? (<><Lock size={13} aria-hidden="true" /> Pro ile aç</>) : 'Bu temayı kullan'}
            </button>
            <button
              type="button"
              onClick={() => setSelectedTheme(null)}
              aria-label="Önizlemeyi kapat"
              title="Kapat"
              className="xc-focus inline-flex h-8 w-8 flex-none items-center justify-center rounded-md text-tx-subtle transition-colors hover:bg-ui-hover hover:text-tx"
            >
              <X size={16} aria-hidden="true" />
            </button>
          </div>

          <div className="flex flex-1 justify-center overflow-auto bg-ui-hover">
            <iframe
              ref={iframeRef}
              srcDoc={previewHtml || '<html><body style="background:#ffffff;margin:0"></body></html>'}
              sandbox="allow-same-origin allow-scripts allow-modals"
              className="border-none bg-white shadow-[var(--ui-shadow-pop)]"
              style={{ width: 860, minHeight: '100%', height: 'max(100%, 1200px)' }}
              title="Tema Önizleme"
            />
          </div>
        </section>
      )}
    </div>
  )
}

// ── Parçalar ────────────────────────────────────────────────────────────────

function DocTypeTabs({ value, onChange }: { value: DocType; onChange: (t: DocType) => void }) {
  return <SegmentedTabs items={DOC_TABS} value={value} onChange={onChange} ariaLabel="Belge türü" />
}

function PlanBadge({ premium }: { premium: boolean }) {
  return premium ? (
    <span className="inline-flex items-center gap-1 rounded-full bg-fn-purple-ink px-2 py-0.5 text-[11px] font-bold text-on-fn">
      <Crown size={11} aria-hidden="true" /> Pro
    </span>
  ) : (
    <span className="rounded-full bg-fn-green-tint px-2 py-0.5 text-[11px] font-bold text-fn-green-ink">Ücretsiz</span>
  )
}

/** Küçük resim yer tutucusu: temanın görseli sunulmadığı için stilize A4 sayfa (zemin her zaman beyaz). */
function ThemeThumb({ docType }: { docType: string }) {
  return (
    <div
      aria-hidden="true"
      style={fnStyle(docType === 'Despatch' ? 'orange' : 'blue')}
      className="xc-paper flex aspect-[210/230] w-full flex-col overflow-hidden bg-white shadow-[var(--ui-shadow-rest)]"
    >
      <div className="h-[18%] flex-none bg-(--fn)" />
      <div className="flex flex-1 flex-col gap-[5px] p-2.5">
        <div className="flex justify-between">
          <span className="h-1.5 w-[34%] rounded-[2px] bg-tx" />
          <span className="h-4 w-4 border-[1.5px] border-tx-muted" />
        </div>
        <span className="h-1 w-[70%] rounded-[2px] bg-field-line" />
        <span className="h-1 w-[55%] rounded-[2px] bg-field-line" />
        <span className="mt-1 h-[7px] bg-(--fn-tint)" />
        <span className="h-1 bg-ui-hover" />
        <span className="h-1 bg-ui-side" />
        <span className="h-1 bg-ui-hover" />
        <span className="mt-1 h-[7px] w-[40%] self-end bg-(--fn)" />
      </div>
    </div>
  )
}

function ThemeCard({
  theme,
  locked,
  onSelect,
  onUse,
}: {
  theme: FreeTheme
  locked: boolean
  onSelect: () => void
  onUse: () => void
}) {
  const titleId = `theme-${theme.id}-title`
  return (
    <article
      aria-labelledby={titleId}
      style={fnStyle('accent')}
      className="xc-lift flex flex-1 flex-col overflow-hidden rounded-xl border border-ui-line bg-ui-card"
    >
      <div className="relative flex justify-center bg-ui-hover px-[26px] pt-[18px]">
        <ThemeThumb docType={theme.documentType} />
        <span className="absolute top-2.5 right-2.5"><PlanBadge premium={theme.isPremium} /></span>
      </div>
      <div className="flex flex-col gap-2 border-t border-ui-line px-3.5 pt-3 pb-3.5">
        <h2 id={titleId} className="m-0 text-sm font-semibold text-tx">{theme.name}</h2>
        <span className="text-xs text-tx-muted">{DOC_TYPE_LABEL[theme.documentType] ?? theme.documentType}</span>
        <div className="flex gap-1.5">
          <button
            type="button"
            onClick={onSelect}
            className="xc-focus inline-flex h-[34px] flex-1 items-center justify-center gap-1.5 rounded-[7px] border border-ui-line bg-ui-card text-[12.5px] text-tx-2 transition-colors hover:bg-ui-hover"
          >
            <Eye size={14} aria-hidden="true" /> Önizle
          </button>
          <button
            type="button"
            onClick={onUse}
            className={`xc-focus inline-flex h-[34px] flex-1 items-center justify-center gap-1.5 rounded-[7px] text-[12.5px] font-semibold transition-colors ${
              locked
                ? 'bg-fn-purple-tint text-fn-purple-ink hover:bg-fn-purple-ink hover:text-on-fn'
                : 'bg-ui-accent text-ui-accent-fg hover:brightness-110'
            }`}
          >
            {locked && <Lock size={12} aria-hidden="true" />}
            {locked ? 'Pro ile aç' : 'Kullan'}
          </button>
        </div>
      </div>
    </article>
  )
}

function CompactThemeRow({
  theme,
  selected,
  locked,
  onSelect,
  onUse,
}: {
  theme: FreeTheme
  selected: boolean
  locked: boolean
  onSelect: () => void
  onUse: () => void
}) {
  return (
    <div
      className={`flex items-center gap-2 rounded-xl border px-3 py-2.5 transition-colors ${
        selected ? 'border-ui-soft-line bg-ui-soft' : 'border-ui-line bg-ui-card hover:bg-ui-hover'
      }`}
    >
      <button
        type="button"
        onClick={onSelect}
        aria-current={selected ? 'true' : undefined}
        className="xc-focus min-w-0 flex-1 rounded text-left"
      >
        <span className="mb-1 flex items-center gap-1.5">
          <span className="rounded-full bg-ui-hover px-2 py-0.5 text-[11px] font-medium text-tx-2">
            {DOC_TYPE_LABEL[theme.documentType] ?? theme.documentType}
          </span>
          <PlanBadge premium={theme.isPremium} />
        </span>
        <span className={`block truncate text-sm ${selected ? 'font-semibold text-ui-ink' : 'font-medium text-tx'}`}>{theme.name}</span>
      </button>
      <button
        type="button"
        onClick={onUse}
        className="xc-focus inline-flex h-[30px] flex-none items-center gap-1 rounded-md border border-ui-soft-line px-3 text-xs font-semibold text-ui-ink transition-colors hover:bg-ui-accent hover:text-ui-accent-fg"
      >
        {locked && <Lock size={11} aria-hidden="true" />} Seç
      </button>
    </div>
  )
}
