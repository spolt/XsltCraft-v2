import { useEffect, useState } from 'react'
import ProgressHairline from './ProgressHairline'

interface Props {
  title: string
  /** Sırayla gösterilen adımlar; son adımda durur. */
  steps: string[]
  stepMs?: number
}

const LINES = ['72%', '88%', '56%', '80%', '64%']

/** Taranan sayfa: A4 oranlı küçük belge, satırlar ve üzerinden geçen yumuşak ışık. */
function PageGlyph() {
  return (
    <div className="relative h-[68px] w-[52px] overflow-hidden rounded-[6px] border border-ui-line bg-ui-card shadow-[var(--ui-shadow-rest)]">
      <div className="flex flex-col gap-[5px] px-[9px] pt-[11px]">
        <div className="mb-[3px] h-[5px] w-[45%] rounded-full bg-ui-soft-line" />
        {LINES.map((w, i) => (
          <div key={i} className="h-[3px] rounded-full bg-ui-line" style={{ width: w }} />
        ))}
      </div>
      <div
        className="xc-scan pointer-events-none absolute inset-x-0 top-0 h-[26px]"
        style={{
          background:
            'linear-gradient(to bottom, transparent, color-mix(in srgb, var(--ui-accent) 16%, transparent) 70%, color-mix(in srgb, var(--ui-accent) 55%, transparent) 96%, transparent)',
        }}
      />
    </div>
  )
}

/** Tam ekran, sakin yükleme ekranı: belge taranıyor görseli, başlık, ilerleyen adım ve ince ilerleme çizgisi. */
export default function DocumentLoader({ title, steps, stepMs = 1100 }: Props) {
  const [step, setStep] = useState(0)

  useEffect(() => {
    if (step >= steps.length - 1) return
    const t = setTimeout(() => setStep((s) => s + 1), stepMs)
    return () => clearTimeout(t)
  }, [step, steps.length, stepMs])

  const current = steps[Math.min(step, steps.length - 1)] ?? ''

  return (
    <div className="flex h-screen items-center justify-center bg-ui-bg px-4" role="status" aria-live="polite" aria-busy="true">
      <div className="xc-fade-in flex flex-col items-center text-center">
        <PageGlyph />
        <p className="m-0 mt-6 text-[14px] font-medium tracking-[0.01em] text-tx">{title}</p>
        <p key={current} className="xc-fade-in-now m-0 mt-1 min-h-[19px] text-[12.5px] text-tx-muted">
          {current}
        </p>
        <ProgressHairline className="mt-5 w-40" />
      </div>
    </div>
  )
}
