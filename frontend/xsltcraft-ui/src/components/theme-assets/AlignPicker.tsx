import { AlignLeft, AlignCenter, AlignRight } from 'lucide-react'
import type { Alignment } from '../../services/previewService'

const OPTIONS: { v: Alignment; Icon: typeof AlignLeft; title: string }[] = [
  { v: 'left', Icon: AlignLeft, title: 'Sola yasla' },
  { v: 'center', Icon: AlignCenter, title: 'Ortala' },
  { v: 'right', Icon: AlignRight, title: 'Sağa yasla' },
]

export default function AlignPicker({ value, onChange }: { value: Alignment; onChange: (v: Alignment) => void }) {
  return (
    <div className="flex gap-1" role="radiogroup" aria-label="Hizalama">
      {OPTIONS.map(({ v, Icon, title }) => (
        <button
          key={v}
          type="button"
          role="radio"
          aria-checked={value === v}
          onClick={() => onChange(v)}
          className={`flex items-center justify-center w-7 h-7 rounded border transition-colors ${
            value === v
              ? 'bg-blue-600 border-blue-600 text-white'
              : 'border-gray-200 text-gray-400 hover:border-blue-300 hover:text-blue-500'
          }`}
          title={title}
          aria-label={title}
        >
          <Icon size={13} />
        </button>
      ))}
    </div>
  )
}
