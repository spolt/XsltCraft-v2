import { AlignLeft, AlignCenter, AlignRight } from 'lucide-react'
import type { Alignment } from '../../services/previewService'

const OPTIONS: { v: Alignment; Icon: typeof AlignLeft; title: string }[] = [
  { v: 'left', Icon: AlignLeft, title: 'Sola yasla' },
  { v: 'center', Icon: AlignCenter, title: 'Ortala' },
  { v: 'right', Icon: AlignRight, title: 'Sağa yasla' },
]

export default function AlignPicker({ value, onChange }: { value: Alignment; onChange: (v: Alignment) => void }) {
  return (
    <div
      className="grid grid-cols-3 overflow-hidden rounded-md border border-field-line"
      role="radiogroup"
      aria-label="Hizalama"
    >
      {OPTIONS.map(({ v, Icon, title }, i) => (
        <button
          key={v}
          type="button"
          role="radio"
          aria-checked={value === v}
          onClick={() => onChange(v)}
          className={`flex h-[30px] focus-visible:outline-2 focus-visible:-outline-offset-2 focus-visible:outline-ui-accent items-center justify-center transition-colors ${i > 0 ? 'border-l border-field-line' : ''} ${
            value === v
              ? 'bg-ui-soft text-ui-ink'
              : 'bg-ui-card text-tx-subtle hover:bg-ui-hover hover:text-tx-2'
          }`}
          title={title}
          aria-label={title}
        >
          <Icon size={14} aria-hidden="true" />
        </button>
      ))}
    </div>
  )
}
