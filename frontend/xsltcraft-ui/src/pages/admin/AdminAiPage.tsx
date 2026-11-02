import { useEffect, useState } from 'react'
import { Loader2, RefreshCw, Radio, Sparkles, CheckCircle2, AlertCircle, MinusCircle } from 'lucide-react'
import {
  getAdminAiFlag, setAdminAiFlag,
  getAiProviderHealth, type AiProviderHealth,
  getAiProvider, setAiProvider,
  getAiUsage, type AiUsageEntry,
} from '../../services/aiAssistantService'
import { useAiStore } from '../../store/aiStore'
import PageHeader from '../../components/ui/PageHeader'
import Card from '../../components/ui/Card'
import IconTile from '../../components/ui/IconTile'
import Switch from '../../components/ui/Switch'
import { fnStyle } from '../../theme/uiThemes'
import { AdminPage, Banner, BTN_SECONDARY, Pill, SectionHeader } from '../../components/admin/adminUi'
import AiVisionCard from './AiVisionCard'

export default function AdminAiPage() {
  const [enabled, setEnabled] = useState<boolean | null>(null)
  const [loading, setLoading] = useState(true)
  const [saving, setSaving] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [providers, setProviders] = useState<AiProviderHealth[] | null>(null)
  const [healthLoading, setHealthLoading] = useState(false)

  // Provider preference
  const [provider, setProvider] = useState<string | null>(null)
  const [providerLoading, setProviderLoading] = useState(false)
  const [providerSaving, setProviderSaving] = useState(false)

  // Daily usage
  const [usage, setUsage] = useState<AiUsageEntry[] | null>(null)
  const [usageLimit, setUsageLimit] = useState(0)
  const [usageLoading, setUsageLoading] = useState(false)

  const refreshAiStore = useAiStore(s => s.refresh)
  const setAiStoreEnabled = useAiStore(s => s.setEnabled)

  useEffect(() => {
    let alive = true
    getAdminAiFlag()
      .then(({ enabled }) => { if (alive) setEnabled(enabled) })
      .catch(() => { if (alive) setError('AI durumu okunamadı.') })
      .finally(() => { if (alive) setLoading(false) })

    setProviderLoading(true)
    getAiProvider()
      .then(({ provider }) => { if (alive) setProvider(provider) })
      .catch(() => { if (alive) setProvider('auto') })
      .finally(() => { if (alive) setProviderLoading(false) })

    refreshHealth()
    refreshUsage()
    return () => { alive = false }
  }, [])

  async function refreshHealth() {
    setHealthLoading(true)
    try {
      const { providers } = await getAiProviderHealth()
      setProviders(providers)
    } catch {
      setProviders([])
    } finally {
      setHealthLoading(false)
    }
  }

  async function refreshUsage() {
    setUsageLoading(true)
    try {
      const { users, limit } = await getAiUsage()
      setUsage(users)
      setUsageLimit(limit)
    } catch {
      setUsage([])
    } finally {
      setUsageLoading(false)
    }
  }

  async function toggle() {
    if (enabled === null) return
    setSaving(true)
    setError(null)
    const next = !enabled
    try {
      await setAdminAiFlag(next)
      setEnabled(next)
      setAiStoreEnabled(next)
      refreshAiStore()
    } catch {
      setError('Değiştirilemedi.')
    } finally {
      setSaving(false)
    }
  }

  async function handleProviderChange(p: string) {
    setProvider(p)
    setProviderSaving(true)
    try {
      await setAiProvider(p)
    } catch {
      // revert on error
      const { provider: prev } = await getAiProvider().catch(() => ({ provider: 'auto' }))
      setProvider(prev)
    } finally {
      setProviderSaving(false)
    }
  }

  const ollamaModel = providers?.find(p => p.name === 'ollama')?.model

  return (
    <AdminPage>
      <PageHeader title="AI Asistan" description="Tüm kullanıcılar için AI özelliklerini açın veya kapatın." />

      {/* Feature flag */}
      <Card as="section" padding="none" aria-labelledby="ai-flag-title" className="flex flex-col gap-3 px-5 py-[18px]">
        {loading ? (
          <div className="flex items-center gap-2 text-[13px] text-tx-muted">
            <Loader2 size={15} className="animate-spin" aria-hidden="true" /> Yükleniyor…
          </div>
        ) : (
          <>
            <div className="flex flex-wrap items-center gap-3.5">
              <IconTile icon={Sparkles} color="orange" size="lg" hoverFill={false} />
              <div className="min-w-0 flex-[1_1_260px]">
                <h2 id="ai-flag-title" className="m-0 text-[15px] font-semibold text-tx">Genel kullanıma açık</h2>
                <span className="text-[12.5px] text-tx-muted">
                  {enabled
                    ? 'Açık — tüm kullanıcılar AI asistanını kullanabilir.'
                    : 'Kapalı — kullanıcılar editörde AI butonlarını göremez ve uçnoktalar 403 döner.'}
                </span>
              </div>
              {saving && <Loader2 size={15} className="animate-spin text-tx-muted" aria-hidden="true" />}
              <Switch
                checked={!!enabled}
                onChange={() => void toggle()}
                disabled={saving || enabled === null}
                label="AI genel kullanım"
                hideLabel
              />
            </div>
            {error && <Banner>{error}</Banner>}
            <div className="border-t border-ui-line pt-3 text-[12px] text-tx-muted">
              Birincil sağlayıcı: <span className="font-mono">ollama{ollamaModel ? ` (${ollamaModel})` : ''}</span>. Sunucuda kurulu olmalı:{' '}
              <code className="rounded bg-ui-hover px-1 py-0.5 font-mono text-tx-2">ollama serve</code>.
            </div>
          </>
        )}
      </Card>

      {/* Provider preference */}
      <Card as="section" padding="none" aria-labelledby="ai-provider-title" className="flex flex-col gap-3 px-5 py-[18px]">
        <div className="flex items-start justify-between gap-3">
          <div>
            <h2 id="ai-provider-title" className="m-0 flex items-center gap-1.5 text-[15px] font-semibold text-tx">
              <Radio size={15} className="text-fn-violet" aria-hidden="true" />
              Sağlayıcı Tercihi
            </h2>
            <p className="mt-0.5 mb-0 text-[12.5px] text-tx-muted">Hangi AI sağlayıcısının önce deneneceğini seçin.</p>
          </div>
          {providerSaving && <Loader2 size={15} className="animate-spin text-tx-muted" aria-hidden="true" />}
        </div>

        {providerLoading ? (
          <div className="flex items-center gap-1.5 text-[12px] text-tx-muted">
            <Loader2 size={12} className="animate-spin" aria-hidden="true" /> Yükleniyor…
          </div>
        ) : (
          <div role="radiogroup" aria-labelledby="ai-provider-title" className="grid grid-cols-[repeat(auto-fit,minmax(180px,1fr))] gap-2">
            {PROVIDER_OPTIONS.map(opt => {
              const on = provider === opt.value
              return (
                <label
                  key={opt.value}
                  className={`flex cursor-pointer items-start gap-2.5 rounded-[10px] border px-3 py-2.5 transition-colors has-[:focus-visible]:outline-2 has-[:focus-visible]:outline-offset-2 has-[:focus-visible]:outline-ui-accent ${
                    on ? 'border-ui-soft-line bg-ui-soft' : 'border-ui-line bg-ui-card hover:bg-ui-hover'
                  }`}
                >
                  <input
                    type="radio"
                    name="ai-provider"
                    value={opt.value}
                    checked={on}
                    onChange={() => handleProviderChange(opt.value)}
                    disabled={providerSaving}
                    className="mt-0.5 accent-[var(--ui-accent)]"
                  />
                  <span>
                    <span className={`block text-[13px] ${on ? 'font-semibold text-ui-ink' : 'font-medium text-tx'}`}>{opt.label}</span>
                    <span className="block text-[11.5px] text-tx-muted">{opt.hint}</span>
                  </span>
                </label>
              )
            })}
          </div>
        )}
        <div className="border-t border-ui-line pt-2.5 text-[12px] text-tx-muted">
          Otomatik: Ollama → Gemini sırasıyla denenir. Gemini seçildiğinde cloud önce denenir, Ollama yedek kalır. DB'ye yazıldığından restart gerekmez.
        </div>
      </Card>

      {/* Ekran görüntüsü (vision) — sıra sağlayıcı tercihine bağlı olduğu için tercihle yenilenir */}
      <AiVisionCard providerPreference={provider} />

      {/* Provider health */}
      <Card as="section" padding="none" aria-labelledby="ai-health-title">
        <SectionHeader
          id="ai-health-title"
          title="Sağlayıcı Durumu"
          description="Ollama'ya canlı bağlantı atılır; Gemini için yalnızca konfigürasyon kontrol edilir."
          aside={
            <button type="button" onClick={refreshHealth} disabled={healthLoading} className={BTN_SECONDARY}>
              {healthLoading ? <Loader2 size={13} className="animate-spin" aria-hidden="true" /> : <RefreshCw size={13} aria-hidden="true" />}
              Yenile
            </button>
          }
        />
        <div className="flex flex-col gap-2.5 p-4">
          {providers === null && healthLoading && (
            <div className="flex items-center gap-2 text-[12.5px] text-tx-muted">
              <Loader2 size={12} className="animate-spin" aria-hidden="true" /> Sağlayıcılar kontrol ediliyor…
            </div>
          )}
          {providers && providers.length === 0 && (
            <div className="text-[12.5px] text-tx-muted">Sağlayıcı bilgisi alınamadı.</div>
          )}
          {providers && providers.length > 0 && (
            <ul className="m-0 flex list-none flex-col gap-2.5 p-0">
              {providers.map(p => <ProviderRow key={p.name} p={p} />)}
            </ul>
          )}
        </div>
      </Card>

      {/* Daily token usage */}
      <Card as="section" padding="none" aria-labelledby="ai-usage-title">
        <SectionHeader
          id="ai-usage-title"
          title="Günlük Token Kullanımı"
          description={
            <>
              Bugün ({new Date().toLocaleDateString('tr-TR')}) — limit:{' '}
              {usageLimit > 0 ? <span className="font-mono">{usageLimit.toLocaleString('tr-TR')}</span> : 'sınırsız'} token/kullanıcı
            </>
          }
          aside={
            <button type="button" onClick={refreshUsage} disabled={usageLoading} className={BTN_SECONDARY}>
              {usageLoading ? <Loader2 size={13} className="animate-spin" aria-hidden="true" /> : <RefreshCw size={13} aria-hidden="true" />}
              Yenile
            </button>
          }
        />
        {usageLoading && usage === null && (
          <div className="flex items-center gap-1.5 px-4 py-4 text-[12.5px] text-tx-muted">
            <Loader2 size={12} className="animate-spin" aria-hidden="true" /> Yükleniyor…
          </div>
        )}
        {usage !== null && usage.length === 0 && (
          <div className="px-4 py-4 text-[12.5px] text-tx-muted">Bugün henüz AI kullanımı yok.</div>
        )}
        {usage !== null && usage.length > 0 && (
          <ul className="m-0 flex list-none flex-col p-0">
            {usage.map((u, i) => {
              const pct = usageLimit > 0 ? Math.min(100, (u.tokensUsed / usageLimit) * 100) : 0
              const tone = pct >= 90 ? 'red' : pct >= 70 ? 'orange' : 'blue'
              const name = u.username ?? u.email
              return (
                <li
                  key={u.userId}
                  className={`flex flex-wrap items-center gap-3.5 px-4 py-2.5 transition-colors hover:bg-ui-hover ${i ? 'border-t border-ui-line' : ''}`}
                >
                  <span className="flex w-[200px] min-w-0 flex-col">
                    <span className="truncate text-[13px] font-medium text-tx">{name}</span>
                    {u.username && <span className="truncate text-[11.5px] text-tx-muted">{u.email}</span>}
                  </span>
                  {usageLimit > 0 && (
                    <div
                      role="img"
                      aria-label={`${name}: günlük ${usageLimit.toLocaleString('tr-TR')} tokenin ${u.tokensUsed.toLocaleString('tr-TR')} kadarı kullanıldı (%${pct.toFixed(0)})`}
                      className="h-2 min-w-[120px] flex-[1_1_200px] overflow-hidden rounded bg-ui-hover"
                    >
                      <div style={{ ...fnStyle(tone), width: `${pct}%` }} className="h-full rounded bg-(--fn)" />
                    </div>
                  )}
                  <span className="ml-auto text-right font-mono text-[12.5px] text-tx-2 tabular-nums">
                    {u.tokensUsed.toLocaleString('tr-TR')}
                    {usageLimit > 0 && <span className="text-tx-muted"> / {usageLimit.toLocaleString('tr-TR')} · %{pct.toFixed(0)}</span>}
                  </span>
                </li>
              )
            })}
          </ul>
        )}
        <div className="border-t border-ui-line px-4 py-2.5 text-[12px] text-tx-muted">
          Token sayısı yaklaşıktır: üretilen karakter sayısı ÷ 4. Limit değiştirmek için{' '}
          <code className="rounded bg-ui-hover px-1 font-mono text-tx-2">appsettings.json → Ai:DailyTokenBudgetPerUser</code>.
        </div>
      </Card>
    </AdminPage>
  )
}

const PROVIDER_OPTIONS = [
  { value: 'auto', label: 'Otomatik', hint: 'Ollama → Gemini' },
  { value: 'ollama', label: 'Ollama (yerel)', hint: 'Her zaman önce Ollama' },
  { value: 'gemini', label: 'Gemini (cloud)', hint: 'Her zaman önce Gemini' },
]

function ProviderRow({ p }: { p: AiProviderHealth }) {
  const status = !p.configured
    ? { Icon: MinusCircle, tone: 'gray' as const, label: 'Devre dışı' }
    : p.available
      ? { Icon: CheckCircle2, tone: 'green' as const, label: 'Çalışıyor' }
      : { Icon: AlertCircle, tone: 'red' as const, label: 'Erişilemez' }
  const role = p.name === 'ollama' ? 'Birincil' : 'Yedek (cloud)'
  return (
    <li className="flex flex-wrap items-center gap-3 rounded-[10px] border border-ui-line bg-ui-side px-3.5 py-3">
      <span
        aria-hidden="true"
        style={fnStyle(p.name === 'ollama' ? 'blue' : 'orange')}
        className="flex h-[38px] w-[38px] flex-none items-center justify-center rounded-[10px] bg-(--fn-tint) text-[13px] font-bold text-(--fn-ink) uppercase"
      >
        {p.name.slice(0, 2)}
      </span>
      <div className="flex min-w-0 flex-[1_1_200px] flex-col">
        <span className="text-[13px] font-semibold text-tx">
          <span className="capitalize">{p.name}</span> <span className="font-normal text-tx-muted">· {role}</span>
          {p.latencyMs != null && <span className="ml-1.5 font-mono text-[11.5px] font-normal text-tx-muted">{p.latencyMs}ms</span>}
        </span>
        {p.model && <span className="font-mono text-[12px] text-tx-muted">{p.model}</span>}
        {p.error && <span className="mt-0.5 text-[12px] break-words text-fn-orange-ink">{p.error}</span>}
      </div>
      <Pill tone={status.tone}>
        <status.Icon size={12} aria-hidden="true" />
        {status.label}
      </Pill>
    </li>
  )
}
