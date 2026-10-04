import { useEffect, useState } from 'react'
import { ImagePlus, Power, Loader2, AlertCircle } from 'lucide-react'
import { getAiVision, setAiVision, type AiVisionStatus } from '../../services/aiAssistantService'
import { useAiStore } from '../../store/aiStore'

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
    <div className="border border-gray-200 rounded-xl bg-white p-5 space-y-4">
      <div className="flex items-center justify-between gap-4">
        <div>
          <div className="text-sm font-medium text-gray-800 flex items-center gap-1.5">
            <ImagePlus size={14} className="text-violet-500" />
            Ekran Görüntüsü (Vision)
          </div>
          <div className="text-xs text-gray-500 mt-0.5">
            Kullanıcılar sohbete PNG/JPG ekran görüntüsü ekleyebilir. Görseller saklanmaz, yalnız o mesajla modele gider.
          </div>
        </div>
        <button
          onClick={() => status && update({ enabled: !status.enabled })}
          disabled={saving || status === null}
          className={`inline-flex items-center gap-2 px-4 py-2 rounded-lg text-sm font-medium transition-colors disabled:opacity-50 flex-shrink-0 ${
            status?.enabled
              ? 'bg-emerald-600 text-white hover:bg-emerald-500'
              : 'bg-gray-200 text-gray-700 hover:bg-gray-300'
          }`}
        >
          {saving ? <Loader2 size={14} className="animate-spin" /> : <Power size={14} />}
          {status?.enabled ? 'Etkin' : 'Devre dışı'}
        </button>
      </div>

      {status === null && !error && (
        <div className="text-xs text-gray-400 flex items-center gap-1.5">
          <Loader2 size={12} className="animate-spin" /> Yükleniyor…
        </div>
      )}

      {status && (
        <>
          <label className="flex items-start gap-2 cursor-pointer select-none">
            <input
              type="checkbox"
              checked={status.geminiFallback}
              onChange={e => update({ geminiFallback: e.target.checked })}
              disabled={saving}
              className="accent-violet-600 mt-0.5"
            />
            <div>
              <span className="text-sm text-gray-800">Görsellerde Gemini'ye izin ver</span>
              <span className="block text-[11px] text-gray-400">
                Tercih "Ollama (yerel)" iken yerel vision modeli yoksa ya da hata verirse görsel Gemini'ye gider.
                Kapalıyken bu tercihte görseller sunucudan dışarı çıkmaz. Otomatik/Gemini tercihinde etkisizdir.
              </span>
            </div>
          </label>

          <div className="text-xs text-gray-500 border-t border-gray-100 pt-3 space-y-1">
            <div>
              Yerel vision modeli:{' '}
              {status.ollamaVisionModel
                ? <span className="font-mono">{status.ollamaVisionModel}</span>
                : <span className="text-amber-600">
                    tanımlı değil — <code className="px-1 bg-gray-100 rounded">appsettings → Ai:Ollama:VisionModel</code>
                  </span>}
            </div>
            {status.enabled && !unavailable && (
              <div>
                Görselli mesaj sırası:{' '}
                <span className="font-mono">{status.providers.map(p => PROVIDER_LABEL[p] ?? p).join(' → ')}</span>
              </div>
            )}
            {unavailable && (
              <div className="flex items-center gap-1.5 text-amber-600">
                <AlertCircle size={12} />
                Etkin ama uygun sağlayıcı yok: yerel vision modeli tanımlayın ya da Gemini istisnasını açın.
              </div>
            )}
          </div>
        </>
      )}

      {error && <div className="text-xs text-red-600">{error}</div>}
    </div>
  )
}
