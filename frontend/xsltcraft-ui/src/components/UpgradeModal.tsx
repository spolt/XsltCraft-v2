import { useEffect, useId, useRef } from 'react'
import { useNavigate } from 'react-router-dom'
import { Crown, X, Check } from 'lucide-react'
import { useUpgradeModalStore } from '../store/upgradeModalStore'
import IconTile from './ui/IconTile'
import { fnStyle } from '../theme/uiThemes'

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
  const titleId = useId()
  const descId = useId()
  const primaryRef = useRef<HTMLButtonElement>(null)

  useEffect(() => {
    if (!isOpen) return
    const previous = document.activeElement as HTMLElement | null
    primaryRef.current?.focus()
    function onKey(e: KeyboardEvent) {
      if (e.key === 'Escape') close()
    }
    document.addEventListener('keydown', onKey)
    return () => {
      document.removeEventListener('keydown', onKey)
      previous?.focus?.()
    }
  }, [isOpen, close])

  if (!isOpen) return null

  const goPricing = () => {
    close()
    navigate('/pricing')
  }

  return (
    <div className="fixed inset-0 z-[100] flex items-center justify-center bg-black/60 p-4" onClick={close}>
      <div
        role="dialog"
        aria-modal="true"
        aria-labelledby={titleId}
        aria-describedby={descId}
        style={fnStyle('purple')}
        className="relative w-full max-w-md overflow-hidden rounded-2xl border border-ui-line bg-ui-card text-tx shadow-[var(--ui-shadow-pop)]"
        onClick={(e) => e.stopPropagation()}
      >
        <button
          type="button"
          onClick={close}
          className="xc-focus absolute top-3 right-3 inline-flex h-8 w-8 items-center justify-center rounded-md text-tx-subtle transition-colors hover:bg-ui-card hover:text-tx"
          aria-label="Kapat"
        >
          <X size={18} aria-hidden="true" />
        </button>

        <div className="border-b border-(--fn-line) bg-(--fn-tint) px-6 pt-7 pb-6">
          <IconTile icon={Crown} size="lg" filled className="mb-3" />
          <h2 id={titleId} className="m-0 pr-8 text-xl font-bold text-tx">{context?.title ?? "XsltCraft Pro'ya Geçin"}</h2>
          <p id={descId} className="mt-1 mb-0 text-sm text-tx-2">
            {context?.message ?? 'Bu özellik Pro üyeliğe dahildir. Pro ile tam erişim kazanın.'}
          </p>
        </div>

        <div className="px-6 py-5">
          <ul className="m-0 flex list-none flex-col gap-2.5 p-0">
            {PRO_PERKS.map((perk) => (
              <li key={perk} className="flex items-start gap-2.5 text-sm text-tx-2">
                <Check size={17} strokeWidth={2.2} aria-hidden="true" className="mt-0.5 shrink-0 text-fn-green" />
                <span>{perk}</span>
              </li>
            ))}
          </ul>

          <div className="mt-6 flex flex-col gap-2">
            <button
              ref={primaryRef}
              type="button"
              onClick={goPricing}
              className="xc-focus w-full rounded-lg bg-fn-purple-ink py-2.5 text-sm font-semibold text-on-fn transition-colors hover:bg-fn-purple"
            >
              Pro'ya Geç
            </button>
            <button
              type="button"
              onClick={close}
              className="xc-focus w-full rounded-lg py-2 text-sm text-tx-muted transition-colors hover:bg-ui-hover"
            >
              Belki sonra
            </button>
          </div>
        </div>
      </div>
    </div>
  )
}
