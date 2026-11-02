import { useEffect, useState } from 'react'
import { ImagePlus, Loader2, AlertCircle } from 'lucide-react'
import { getAiVision, setAiVision, type AiVisionStatus } from '../../services/aiAssistantService'
import { useAiStore } from '../../store/aiStore'
import Card from '../../components/ui/Card'
import Switch from '../../components/ui/Switch'
import { Banner } from '../../components/admin/adminUi'

const PROVIDER_LABEL: Record<string, string> = { ollama: 'Ollama (yerel)', gemini: 'Gemini (cloud)' }

/**
 * Sohbete ekran görüntüsü desteği: aç/kapat + "ollama" tercihinde Gemini istisnası.
 * Ayarlar DB'ye yazılır (restart gerekmez). Etkin sağlayıcı sırası sağlayıcı tercihine bağlıdır.
 */
export default function AiVisionCard({ providerPreference }: { providerPreference: string | null }) {
  const [status, setStatus] = useState<AiVisionStatus | null>(null)
  const [saving, setSaving] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const refreshAiStore = useAiStore(s => s.refresh)

  useEffect(() => {
    let alive = true
    getAiVision()
      .then(s => { if (alive) setStatus(s) })
      .catch(() => { if (alive) setError('Görsel ayarları okunamadı.') })
    return () => { alive = false }
  }, [providerPreference])

  async function update(patch: { enabled?: boolean; geminiFallback?: boolean }) {
    setSaving(true)
    setError(null)
    try {
      setStatus(await setAiVision(patch))
      refreshAiStore()
    } catch {
      setError('Değiştirilemedi.')
    } finally {
      setSaving(false)
    }
  }

  const unavailable = status?.enabled && status.providers.length === 0

  return (
    <Card as="section" padding="none" aria-labelledby="ai-vision-title" className="flex flex-col gap-3.5 px-5 py-[18px]">
      <div className="flex items-start justify-between gap-4">
        <div className="min-w-0">
          <h2 id="ai-vision-title" className="m-0 flex items-center gap-1.5 text-[15px] font-semibold text-tx">
            <ImagePlus size={15} className="text-fn-violet" aria-hidden="true" />
            Ekran Görüntüsü (Vision)
          </h2>
          <p className="mt-0.5 mb-0 text-[12.5px] text-tx-muted">
            Kullanıcılar sohbete PNG/JPG ekran görüntüsü ekleyebilir. Görseller saklanmaz, yalnız o mesajla modele gider.
          </p>
        </div>
        <div className="flex flex-none items-center gap-2 pt-1">
          {saving && <Loader2 size={14} className="animate-spin text-tx-muted" aria-hidden="true" />}
          <span className="text-[12.5px] font-medium text-tx-2" aria-hidden="true">{status?.enabled ? 'Etkin' : 'Devre dışı'}</span>
          <Switch
            checked={!!status?.enabled}
            onChange={() => status && void update({ enabled: !status.enabled })}
            disabled={saving || status === null}
            label="Ekran görüntüsü desteği"
            hideLabel
          />
        </div>
      </div>

      {status === null && !error && (
        <div className="flex items-center gap-1.5 text-[12px] text-tx-muted">
          <Loader2 size={12} className="animate-spin" aria-hidden="true" /> Yükleniyor…
        </div>
      )}

      {status && (
        <>
          <label className="flex cursor-pointer items-start gap-2.5 select-none">
            <input
              type="checkbox"
              checked={status.geminiFallback}
              onChange={e => update({ geminiFallback: e.target.checked })}
              disabled={saving}
              className="mt-0.5 accent-[var(--ui-accent)]"
            />
            <span>
              <span className="text-[13px] text-tx">Görsellerde Gemini'ye izin ver</span>
              <span className="block text-[11.5px] text-tx-muted">
                Tercih "Ollama (yerel)" iken yerel vision modeli yoksa ya da hata verirse görsel Gemini'ye gider.
                Kapalıyken bu tercihte görseller sunucudan dışarı çıkmaz. Otomatik/Gemini tercihinde etkisizdir.
              </span>
            </span>
          </label>

          <div className="flex flex-col gap-1 border-t border-ui-line pt-3 text-[12px] text-tx-muted">
            <div>
              Yerel vision modeli:{' '}
              {status.ollamaVisionModel
                ? <span className="font-mono text-tx-2">{status.ollamaVisionModel}</span>
                : <span className="text-fn-orange-ink">
                    tanımlı değil — <code className="rounded bg-ui-hover px-1 font-mono">appsettings → Ai:Ollama:VisionModel</code>
                  </span>}
            </div>
            {status.enabled && !unavailable && (
              <div>
                Görselli mesaj sırası:{' '}
                <span className="font-mono text-tx-2">{status.providers.map(p => PROVIDER_LABEL[p] ?? p).join(' → ')}</span>
              </div>
            )}
            {unavailable && (
              <div className="flex items-center gap-1.5 text-fn-orange-ink">
                <AlertCircle size={12} aria-hidden="true" />
                Etkin ama uygun sağlayıcı yok: yerel vision modeli tanımlayın ya da Gemini istisnasını açın.
              </div>
            )}
          </div>
        </>
      )}

      {error && <Banner>{error}</Banner>}
    </Card>
  )
}
