import { useState } from 'react'
import { Crown, Check, LayoutGrid, Minus } from 'lucide-react'
import { useEntitlementStore } from '../store/entitlementStore'
import { startCheckout } from '../services/entitlementService'
import { toast } from '../store/toastStore'
import Card from '../components/ui/Card'
import IconTile from '../components/ui/IconTile'

interface Row {
  label: string
  free: string | boolean
  pro: string | boolean
}

const ROWS: Row[] = [
  { label: 'Tasarım alanında şablon tasarla', free: true, pro: true },
  { label: 'Taslak kaydet (Taslaklarım)', free: true, pro: true },
  { label: 'Ücretsiz temaları kullan, düzenle, indir', free: true, pro: true },
  { label: 'XSLT Editör (düzenle & indir)', free: true, pro: true },
  { label: 'Tasarladığın şablonun XSLT’sini indir', free: false, pro: 'günde 3' },
  { label: "Şablonlarım’da ham XSLT sakla", free: false, pro: true },
  { label: 'Ücretli temaların kilidini aç', free: false, pro: true },
  { label: 'AI asistan', free: 'günde 1 soru', pro: '50.000 token/gün' },
]

function Cell({ value, pro = false }: { value: string | boolean; pro?: boolean }) {
  if (value === true) return <Check size={18} strokeWidth={2.4} role="img" aria-label="Var" className="mx-auto text-fn-green" />
  if (value === false) return <Minus size={18} strokeWidth={2.2} role="img" aria-label="Yok" className="mx-auto text-tx-subtle" />
  return <span className={pro ? 'font-semibold text-fn-purple-ink' : 'font-medium text-tx-2'}>{value}</span>
}

function CurrentBadge() {
  return (
    <span className="absolute -top-3 right-[18px] rounded-full bg-fn-purple-ink px-2.5 py-[3px] text-[11.5px] font-bold text-on-fn">
      Mevcut planın
    </span>
  )
}

export default function PricingPage() {
  const entitlements = useEntitlementStore((s) => s.entitlements)
  const refresh = useEntitlementStore((s) => s.refresh)
  const [loading, setLoading] = useState(false)

  const isPro = entitlements?.plan === 'Pro' || entitlements?.isPrivileged
  const isFree = !!entitlements && !isPro

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
    <div className="mx-auto flex w-full max-w-[900px] flex-col gap-[22px] px-8 pt-7 pb-9">
      <div className="text-center">
        <h1 className="m-0 text-[26px] font-semibold text-tx">XsltCraft Pro</h1>
        <p className="mt-1.5 mb-0 text-sm text-tx-muted">
          Üretime hazır XSLT’leri indir, şablonlarını sakla ve AI’dan sınırsız yararlan.
        </p>
      </div>

      <div className="flex flex-wrap gap-[18px]">
        <Card as="section" lift fn="blue" aria-labelledby="plan-std" className="relative flex flex-[1_1_300px] flex-col gap-3">
          {isFree && <CurrentBadge />}
          <IconTile icon={LayoutGrid} size="lg" />
          <h2 id="plan-std" className="m-0 text-lg font-semibold text-tx">Standart</h2>
          <div className="text-[26px] font-bold text-tx">Ücretsiz</div>
          <p className="m-0 leading-normal text-tx-muted">Tasarla, taslak kaydet, ücretsiz temaları kullan.</p>
        </Card>

        <Card
          as="section"
          lift
          fn="purple"
          aria-labelledby="plan-pro"
          className="relative flex flex-[1_1_300px] flex-col gap-3 ring-1 ring-(--fn-line)"
        >
          {isPro && <CurrentBadge />}
          <IconTile icon={Crown} size="lg" />
          <h2 id="plan-pro" className="m-0 text-lg font-semibold text-tx">Pro</h2>
          <p className="m-0 leading-normal text-tx-muted">XSLT indir, ham XSLT sakla, ücretli temaları aç, AI’ı daha çok kullan.</p>
          <div className="mt-auto">
            {isPro ? (
              <span className="inline-flex h-10 items-center gap-2 rounded-lg bg-fn-green-tint px-3.5 font-semibold text-fn-green-ink">
                <Check size={15} strokeWidth={2.4} aria-hidden="true" /> Pro üyeliğin aktif.
              </span>
            ) : (
              <button
                type="button"
                onClick={handleCheckout}
                disabled={loading}
                className="xc-focus inline-flex h-10 items-center gap-2 rounded-lg bg-fn-purple-ink px-5 text-[13.5px] font-semibold text-on-fn transition-colors hover:bg-fn-purple disabled:cursor-not-allowed disabled:opacity-50"
              >
                <Crown size={16} aria-hidden="true" />
                {loading ? 'İşleniyor…' : "Pro'ya Geç"}
              </button>
            )}
          </div>
        </Card>
      </div>

      <Card as="section" padding="none" aria-label="Plan karşılaştırması" className="overflow-hidden">
        <div className="overflow-x-auto">
          <table className="w-full border-collapse text-[13.5px]">
            <thead>
              <tr className="bg-ui-hover">
                <th scope="col" className="px-5 py-3.5 text-left font-semibold text-tx">Özellik</th>
                <th scope="col" className="w-40 px-4 py-3.5 text-center font-semibold text-tx">Standart</th>
                <th scope="col" className="w-[180px] px-4 py-3.5 text-center font-bold text-fn-purple-ink">
                  <span className="inline-flex items-center gap-1.5"><Crown size={15} aria-hidden="true" /> Pro</span>
                </th>
              </tr>
            </thead>
            <tbody>
              {ROWS.map((row) => (
                <tr key={row.label} className="border-t border-ui-line transition-colors hover:bg-ui-hover">
                  <th scope="row" className="px-5 py-3 text-left font-normal text-tx">{row.label}</th>
                  <td className="px-4 py-3 text-center"><Cell value={row.free} /></td>
                  <td className="px-4 py-3 text-center"><Cell value={row.pro} pro /></td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </Card>

      <p className="m-0 text-center text-xs text-tx-subtle">
        Online ödeme yakında. Şimdilik Pro üyelik yönetici tarafından tanımlanır.
      </p>
    </div>
  )
}
