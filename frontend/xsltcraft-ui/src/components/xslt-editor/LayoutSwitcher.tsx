import type { EditorLayoutId } from '../../store/uiPrefsStore'
import { EDITOR_LAYOUTS } from './editorLayouts'

interface Props {
  value: EditorLayoutId
  onChange: (id: EditorLayoutId) => void
}

/** Editör araç çubuğundaki düzen seçici (4 mod, aria-pressed). */
export default function LayoutSwitcher({ value, onChange }: Props) {
  return (
    <div role="group" aria-label="Düzen" className="flex flex-none gap-0.5 rounded-[6px] bg-ed-seg p-0.5">
      {EDITOR_LAYOUTS.map(l => {
        const on = l.id === value
        const Icon = l.icon
        return (
          <button
            key={l.id}
            type="button"
            aria-label={l.name}
            title={l.name}
            aria-pressed={on}
            onClick={() => onChange(l.id)}
            className={`xc-focus flex h-[22px] w-[26px] items-center justify-center rounded-[4px] transition-colors ${
              on ? 'bg-ed-active text-ed-text' : 'text-ed-muted hover:text-ed-text'
            }`}
          >
            <Icon size={14} strokeWidth={1.8} aria-hidden="true" />
          </button>
        )
      })}
    </div>
  )
}
