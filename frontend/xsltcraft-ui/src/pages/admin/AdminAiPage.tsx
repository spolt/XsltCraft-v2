import { useEffect, useState } from 'react'
import { Sparkles, Power, Loader2, RefreshCw, CheckCircle2, AlertCircle, MinusCircle, Radio } from 'lucide-react'
import {
  getAdminAiFlag, setAdminAiFlag,
  getAiProviderHealth, type AiProviderHealth,
  getAiProvider, setAiProvider,
  getAiUsage, type AiUsageEntry,
} from '../../services/aiAssistantService'
import { useAiStore } from '../../store/aiStore'

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
    // eslint-disable-next-line react-hooks/exhaustive-deps
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

  return (
    <div className="max-w-3xl mx-auto p-6 space-y-6">
      <header className="flex items-center gap-3">
        <div className="w-10 h-10 rounded-lg bg-violet-100 flex items-center justify-center">
          <Sparkles size={20} className="text-violet-600" />
        </div>
        <div>
          <h1 className="text-lg font-semibold text-gray-800">AI Asistan</h1>
          <p className="text-sm text-gray-500">Tüm kullanıcılar için AI özelliklerini açın veya kapatın.</p>
        </div>
      </header>

      {/* Feature flag toggle */}
      {loading ? (
        <div className="flex items-center gap-2 text-sm text-gray-500">
          <Loader2 size={16} className="animate-spin" /> Yükleniyor…
        </div>
      ) : (
        <div className="border border-gray-200 rounded-xl bg-white p-5 space-y-4">
          <div className="flex items-center justify-between gap-4">
            <div>
              <div className="text-sm font-medium text-gray-800">Genel kullanıma açık</div>
              <div className="text-xs text-gray-500 mt-0.5">
                Kapalıyken kullanıcılar editörde AI butonlarını göremez ve uçnoktalar 403 döner.
              </div>
            </div>
            <button
              onClick={toggle}
              disabled={saving}
              className={`inline-flex items-center gap-2 px-4 py-2 rounded-lg text-sm font-medium transition-colors disabled:opacity-50 ${
                enabled
                  ? 'bg-emerald-600 text-white hover:bg-emerald-500'
                  : 'bg-gray-200 text-gray-700 hover:bg-gray-300'
              }`}
            >
              {saving ? <Loader2 size={14} className="animate-spin" /> : <Power size={14} />}
              {enabled ? 'Etkin' : 'Devre dışı'}
            </button>
          </div>

          {error && <div className="text-xs text-red-600">{error}</div>}

          <div className="text-xs text-gray-500 border-t border-gray-100 pt-3">
            Birincil sağlayıcı: <span className="font-mono">Ollama (qwen2.5-coder:7b)</span>.
            Sunucuda kurulu olmalı: <code className="px-1 py-0.5 bg-gray-100 rounded">ollama serve</code>.
          </div>
        </div>
      )}

      {/* Provider preference */}
      <div className="border border-gray-200 rounded-xl bg-white p-5 space-y-3">
        <div className="flex items-center justify-between">
          <div>
            <div className="text-sm font-medium text-gray-800 flex items-center gap-1.5">
              <Radio size={14} className="text-violet-500" />
              Sağlayıcı Tercihi
            </div>
            <div className="text-xs text-gray-500 mt-0.5">
              Hangi AI sağlayıcısının önce deneneceğini seçin.
            </div>
          </div>
          {providerSaving && <Loader2 size={14} className="animate-spin text-gray-400" />}
        </div>

        {providerLoading ? (
          <div className="text-xs text-gray-400 flex items-center gap-1.5">
            <Loader2 size={12} className="animate-spin" /> Yükleniyor…
          </div>
        ) : (
          <div className="flex gap-5 flex-wrap">
            {PROVIDER_OPTIONS.map(opt => (
              <label key={opt.value} className="flex items-center gap-2 cursor-pointer select-none">
                <input
                  type="radio"
                  name="ai-provider"
                  value={opt.value}
                  checked={provider === opt.value}
                  onChange={() => handleProviderChange(opt.value)}
                  disabled={providerSaving}
                  className="accent-violet-600"
                />
                <div>
                  <span className="text-sm text-gray-800">{opt.label}</span>
                  <span className="block text-[11px] text-gray-400">{opt.hint}</span>
                </div>
              </label>
            ))}
          </div>
        )}
        <div className="text-xs text-gray-400 border-t border-gray-100 pt-2">
          Otomatik: Ollama → Gemini sırasıyla denenir. Gemini seçildiğinde cloud önce denenir, Ollama yedek kalır. DB'ye yazıldığından restart gerekmez.
        </div>
      </div>

      {/* Provider health */}
      <div className="border border-gray-200 rounded-xl bg-white p-5 space-y-3">
        <div className="flex items-center justify-between">
          <div>
            <div className="text-sm font-medium text-gray-800">Sağlayıcı Durumu</div>
            <div className="text-xs text-gray-500 mt-0.5">
              Ollama'ya canlı bağlantı atılır; Gemini için yalnızca konfigürasyon kontrol edilir.
            </div>
          </div>
          <button
            onClick={refreshHealth}
            disabled={healthLoading}
            className="inline-flex items-center gap-1.5 px-3 py-1.5 text-xs rounded-md border border-gray-200 hover:bg-gray-50 text-gray-700 disabled:opacity-40"
          >
            {healthLoading ? <Loader2 size={12} className="animate-spin" /> : <RefreshCw size={12} />}
            Yenile
          </button>
        </div>

        {providers === null && healthLoading && (
          <div className="text-xs text-gray-500 flex items-center gap-2">
            <Loader2 size={12} className="animate-spin" /> Sağlayıcılar kontrol ediliyor…
          </div>
        )}
        {providers && providers.length === 0 && (
          <div className="text-xs text-gray-500">Sağlayıcı bilgisi alınamadı.</div>
        )}
        {providers && providers.length > 0 && (
          <ul className="divide-y divide-gray-100">
            {providers.map(p => <ProviderRow key={p.name} p={p} />)}
          </ul>
        )}
      </div>

      {/* Daily token usage */}
      <div className="border border-gray-200 rounded-xl bg-white p-5 space-y-3">
        <div className="flex items-center justify-between">
          <div>
            <div className="text-sm font-medium text-gray-800">Günlük Token Kullanımı</div>
            <div className="text-xs text-gray-500 mt-0.5">
              Bugün ({new Date().toLocaleDateString('tr-TR')}) — limit:{' '}
              {usageLimit > 0
                ? <span className="font-mono">{usageLimit.toLocaleString('tr-TR')}</span>
                : 'sınırsız'}{' '}token/kullanıcı
            </div>
          </div>
          <button
            onClick={refreshUsage}
            disabled={usageLoading}
            className="inline-flex items-center gap-1.5 px-3 py-1.5 text-xs rounded-md border border-gray-200 hover:bg-gray-50 text-gray-700 disabled:opacity-40"
          >
            {usageLoading ? <Loader2 size={12} className="animate-spin" /> : <RefreshCw size={12} />}
            Yenile
          </button>
        </div>

        {usageLoading && usage === null && (
          <div className="text-xs text-gray-400 flex items-center gap-1.5">
            <Loader2 size={12} className="animate-spin" /> Yükleniyor…
          </div>
        )}
        {usage !== null && usage.length === 0 && (
          <div className="text-xs text-gray-500">Bugün henüz AI kullanımı yok.</div>
        )}
        {usage !== null && usage.length > 0 && (
          <table className="w-full text-xs">
            <thead>
              <tr className="border-b border-gray-100 text-left text-gray-500">
                <th className="py-1.5 font-medium">Kullanıcı</th>
                <th className="py-1.5 font-medium text-right">Token (~)</th>
                {usageLimit > 0 && <th className="py-1.5 font-medium text-right">Bütçe</th>}
              </tr>
            </thead>
            <tbody className="divide-y divide-gray-50">
              {usage.map(u => {
                const pct = usageLimit > 0 ? Math.min(100, (u.tokensUsed / usageLimit) * 100) : 0
                return (
                  <tr key={u.userId}>
                    <td className="py-1.5">
                      <span className="font-medium text-gray-800">{u.username ?? u.email}</span>
                      {u.username && (
                        <span className="text-gray-400 ml-1.5">{u.email}</span>
                      )}
                    </td>
                    <td className="py-1.5 text-right font-mono text-gray-700">
                      {u.tokensUsed.toLocaleString('tr-TR')}
                    </td>
                    {usageLimit > 0 && (
                      <td className="py-1.5 text-right">
                        <span className={
                          pct >= 90 ? 'text-red-600 font-medium' :
                          pct >= 70 ? 'text-amber-600' :
                          'text-gray-500'
                        }>
                          {pct.toFixed(0)}%
                        </span>
                      </td>
                    )}
                  </tr>
                )
              })}
            </tbody>
          </table>
        )}
        <div className="text-xs text-gray-400 border-t border-gray-100 pt-2">
          Token sayısı yaklaşıktır: üretilen karakter sayısı ÷ 4. Limit değiştirmek için <code className="px-1 bg-gray-100 rounded">appsettings.json → Ai:DailyTokenBudgetPerUser</code>.
        </div>
      </div>
    </div>
  )
}

const PROVIDER_OPTIONS = [
  { value: 'auto', label: 'Otomatik', hint: 'Ollama → Gemini' },
  { value: 'ollama', label: 'Ollama (yerel)', hint: 'Her zaman önce Ollama' },
  { value: 'gemini', label: 'Gemini (cloud)', hint: 'Her zaman önce Gemini' },
]

function ProviderRow({ p }: { p: AiProviderHealth }) {
  const status = !p.configured
    ? { Icon: MinusCircle, color: 'text-gray-400', bg: 'bg-gray-100', label: 'Devre dışı' }
    : p.available
      ? { Icon: CheckCircle2, color: 'text-emerald-600', bg: 'bg-emerald-50', label: 'Çalışıyor' }
      : { Icon: AlertCircle, color: 'text-red-600', bg: 'bg-red-50', label: 'Erişilemez' }
  const role = p.name === 'ollama' ? 'Birincil' : 'Yedek (cloud)'
  return (
    <li className="flex items-start gap-3 py-2.5">
      <div className={`w-7 h-7 rounded-full flex items-center justify-center flex-shrink-0 ${status.bg}`}>
        <status.Icon size={14} className={status.color} />
      </div>
      <div className="flex-1 min-w-0">
        <div className="flex items-baseline gap-2 flex-wrap">
          <span className="text-sm font-medium text-gray-800 capitalize">{p.name}</span>
          <span className="text-[10px] uppercase tracking-wide text-gray-400">{role}</span>
          <span className={`text-xs ${status.color}`}>{status.label}</span>
          {p.latencyMs != null && (
            <span className="text-[11px] text-gray-400 font-mono">{p.latencyMs}ms</span>
          )}
        </div>
        <div className="text-xs text-gray-500 mt-0.5">
          {p.model && <span className="font-mono">{p.model}</span>}
          {p.error && <span className="block text-amber-600 mt-0.5 break-words">{p.error}</span>}
        </div>
      </div>
    </li>
  )
}
