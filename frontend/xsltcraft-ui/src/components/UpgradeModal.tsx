import { useNavigate } from 'react-router-dom'
import { Crown, X, Check } from 'lucide-react'
import { useUpgradeModalStore } from '../store/upgradeModalStore'

const PRO_PERKS = [
  'Sınırsız şablon oluştur ve günde 3 indirme',
  "Taslaklarım ve Şablonlarım'da saklama",
  'XSLT Editör + AI: günde 50.000 token',
  'Ücretli temaların kilidini aç',
]

export default function UpgradeModal() {
  const navigate = useNavigate()
  const isOpen = useUpgradeModalStore((s) => s.isOpen)
  const context = useUpgradeModalStore((s) => s.context)
  const close = useUpgradeModalStore((s) => s.close)

  if (!isOpen) return null

  const goPricing = () => {
    close()
    navigate('/pricing')
  }

  return (
    <div className="fixed inset-0 z-[100] flex items-center justify-center bg-black/60 p-4" onClick={close}>
      <div
        className="relative w-full max-w-md overflow-hidden rounded-2xl bg-white shadow-2xl"
        onClick={(e) => e.stopPropagation()}
      >
        <button
          onClick={close}
          className="absolute right-3 top-3 text-white/80 hover:text-white transition-colors"
          aria-label="Kapat"
        >
          <X size={20} />
        </button>

        <div className="bg-gradient-to-br from-indigo-600 to-violet-600 px-6 py-7 text-white">
          <div className="mb-3 inline-flex h-11 w-11 items-center justify-center rounded-xl bg-white/15">
            <Crown size={22} />
          </div>
          <h2 className="text-xl font-bold">{context?.title ?? "XsltCraft Pro'ya Geçin"}</h2>
          <p className="mt-1 text-sm text-indigo-100">
            {context?.message ?? 'Bu özellik Pro üyeliğe dahildir. Pro ile tam erişim kazanın.'}
          </p>
        </div>

        <div className="px-6 py-5">
          <ul className="space-y-2.5">
            {PRO_PERKS.map((perk) => (
              <li key={perk} className="flex items-start gap-2.5 text-sm text-gray-700">
                <Check size={17} className="mt-0.5 shrink-0 text-emerald-500" />
                <span>{perk}</span>
              </li>
            ))}
          </ul>

          <div className="mt-6 flex flex-col gap-2">
            <button
              onClick={goPricing}
              className="w-full rounded-lg bg-indigo-600 py-2.5 text-sm font-semibold text-white transition-colors hover:bg-indigo-700"
            >
              Pro'ya Geç
            </button>
            <button
              onClick={close}
              className="w-full rounded-lg py-2 text-sm text-gray-500 transition-colors hover:bg-gray-100"
            >
              Belki sonra
            </button>
          </div>
        </div>
      </div>
    </div>
  )
}
