import { useId } from 'react'

interface SwitchProps {
  checked: boolean
  onChange: (next: boolean) => void
  /** Erişilebilir ad; hideLabel değilse anahtarın solunda görünür metin olarak da çizilir. */
  label: string
  hideLabel?: boolean
  disabled?: boolean
  /** 'light' (varsayılan) açık zemin, 'dark' editör (--ed-*) zemini için. */
  tone?: 'light' | 'dark'
  className?: string
}

/** Aç/kapa anahtarı (role=switch, aria-checked). Satır düzeni: etiket solda, anahtar sağda. */
export default function Switch({
  checked,
  onChange,
  label,
  hideLabel = false,
  disabled = false,
  tone = 'light',
  className = '',
}: SwitchProps) {
  const labelId = useId()
  const offTrack = tone === 'dark' ? 'bg-ed-border' : 'bg-field-line'
  const button = (
    <button
      type="button"
      role="switch"
      aria-checked={checked}
      aria-label={hideLabel ? label : undefined}
      aria-labelledby={hideLabel ? undefined : labelId}
      disabled={disabled}
      onClick={() => onChange(!checked)}
      className={`xc-focus relative h-[17px] w-[30px] flex-none rounded-full transition-colors disabled:cursor-not-allowed disabled:opacity-50 ${checked ? 'bg-ui-accent' : offTrack}`}
    >
      <span
        aria-hidden="true"
        className={`absolute top-[2px] h-[13px] w-[13px] rounded-full bg-white transition-[left] duration-150 motion-reduce:transition-none ${checked ? 'left-[15px]' : 'left-[2px]'}`}
      />
    </button>
  )
  if (hideLabel) return button
  return (
    <div className={`flex items-center justify-between gap-3 text-[12.5px] ${className}`}>
      <span id={labelId} className={tone === 'dark' ? 'text-ed-text' : 'text-tx-2'}>
        {label}
      </span>
      {button}
    </div>
  )
}
