import { useState } from 'react'
import { Crown, Check, Minus } from 'lucide-react'
import { useEntitlementStore } from '../store/entitlementStore'
import { startCheckout } from '../services/entitlementService'
import { toast } from '../store/toastStore'

interface Row {
  label: string
  free: string | boolean
  pro: string | boolean
}

const ROWS: Row[] = [
  { label: 'Grid canvas ile şablon tasarla', free: true, pro: true },
  { label: 'Taslak kaydet (Taslaklarım)', free: true, pro: true },
  { label: 'Ücretsiz temaları kullan, düzenle, indir', free: true, pro: true },
  { label: 'XSLT Editör (düzenle & indir)', free: true, pro: true },
  { label: 'Tasarladığın şablonun XSLT’sini indir', free: false, pro: 'günde 3' },
  { label: "Şablonlarım’da ham XSLT sakla", free: false, pro: true },
  { label: 'Ücretli temaların kilidini aç', free: false, pro: true },
  { label: 'AI asistan', free: 'günde 1 soru', pro: '50.000 token/gün' },
]

function Cell({ value }: { value: string | boolean }) {
  if (value === true) return <Check size={18} className="mx-auto text-emerald-500" />
  if (value === false) return <Minus size={18} className="mx-auto text-gray-300" />
  return <span className="text-sm font-medium text-gray-700">{value}</span>
}

export default function PricingPage() {
  const entitlements = useEntitlementStore((s) => s.entitlements)
  const refresh = useEntitlementStore((s) => s.refresh)
  const [loading, setLoading] = useState(false)

  const isPro = entitlements?.plan === 'Pro' || entitlements?.isPrivileged

  async function handleCheckout() {
    setLoading(true)
    try {
      const res = await startCheckout('Pro')
      // Faz 1: ödeme stub'ı — "yakında" mesajı. (Admin grant ile Pro tanımlandıysa tazele.)
      toast.info(res.message, { title: 'XsltCraft Pro', durationMs: 4000 })
      await refresh()
    } catch {
      toast.error('İşlem başlatılamadı. Lütfen tekrar deneyin.')
    } finally {
      setLoading(false)
    }
  }

  return (
    <div className="mx-auto max-w-3xl px-4 py-10">
      <div className="text-center">
        <h1 className="text-3xl font-bold text-gray-900">XsltCraft Pro</h1>
        <p className="mt-2 text-gray-500">
          Üretime hazır XSLT’leri indir, şablonlarını sakla ve AI’dan sınırsız yararlan.
        </p>
      </div>

      <div className="mt-8 overflow-hidden rounded-2xl border border-gray-200 shadow-sm">
        <table className="w-full">
          <thead>
            <tr className="border-b border-gray-200 bg-gray-50">
              <th className="px-5 py-4 text-left text-sm font-semibold text-gray-700">Özellik</th>
              <th className="px-4 py-4 text-center text-sm font-semibold text-gray-700">Standart</th>
              <th className="px-4 py-4 text-center">
                <span className="inline-flex items-center gap-1.5 text-sm font-bold text-indigo-600">
                  <Crown size={15} /> Pro
                </span>
              </th>
            </tr>
          </thead>
          <tbody>
            {ROWS.map((row, i) => (
              <tr key={row.label} className={i % 2 ? 'bg-gray-50/40' : ''}>
                <td className="px-5 py-3 text-sm text-gray-700">{row.label}</td>
                <td className="px-4 py-3 text-center"><Cell value={row.free} /></td>
                <td className="px-4 py-3 text-center"><Cell value={row.pro} /></td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>

      <div className="mt-8 flex flex-col items-center">
        {isPro ? (
          <div className="inline-flex items-center gap-2 rounded-lg bg-emerald-50 px-4 py-2.5 text-sm font-medium text-emerald-700">
            <Check size={16} /> Pro üyeliğin aktif.
          </div>
        ) : (
          <button
            onClick={handleCheckout}
            disabled={loading}
            className="inline-flex items-center gap-2 rounded-lg bg-indigo-600 px-6 py-3 text-sm font-semibold text-white transition-colors hover:bg-indigo-700 disabled:opacity-50"
          >
            <Crown size={16} />
            {loading ? 'İşleniyor…' : "Pro'ya Geç"}
          </button>
        )}
        <p className="mt-3 text-xs text-gray-400">
          Online ödeme yakında. Şimdilik Pro üyelik yönetici tarafından tanımlanır.
        </p>
      </div>
    </div>
  )
}
