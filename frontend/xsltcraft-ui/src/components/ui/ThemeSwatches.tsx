import { UI_THEMES } from '../../theme/uiThemes'
import { useUiPrefsStore, type UiThemeId } from '../../store/uiPrefsStore'

interface ThemeSwatchesProps {
  /** Verilmezse uiPrefsStore.uiTheme kullanılır. */
  value?: UiThemeId
  /** Verilmezse uiPrefsStore.setUiTheme çağrılır. */
  onChange?: (id: UiThemeId) => void
  /** 'list': adlı satırlar (Ayarlar). 'dots': yuvarlak örnekler (auth paneli); koyu zeminde kapsayıcıya style={{'--xc-swatch-offset':'var(--ui-panel)'}} ver. */
  variant?: 'list' | 'dots'
  /** Grubun erişilebilir adı. */
  ariaLabel?: string
  className?: string
}

/** Arayüz rengi seçici. Her seçenek aria-pressed'li bir buton. */
export default function ThemeSwatches({
  value,
  onChange,
  variant = 'list',
  ariaLabel = 'Arayüz rengi',
  className = '',
}: ThemeSwatchesProps) {
  const storeTheme = useUiPrefsStore((s) => s.uiTheme)
  const setStoreTheme = useUiPrefsStore((s) => s.setUiTheme)
  const current = value ?? storeTheme
  const pick = onChange ?? setStoreTheme

  if (variant === 'dots') {
    return (
      <div role="group" aria-label={ariaLabel} className={`flex items-center gap-2 ${className}`}>
        {UI_THEMES.map((t) => {
          const on = t.id === current
          return (
            <button
              key={t.id}
              type="button"
              aria-label={t.name}
              aria-pressed={on}
              title={t.name}
              onClick={() => pick(t.id)}
              style={{ background: t.swatch }}
              className={`xc-focus h-4 w-4 flex-none cursor-pointer rounded-full border-0 p-0 ${on ? 'ring-2 ring-ui-accent ring-offset-2 ring-offset-[color:var(--xc-swatch-offset,var(--ui-card))]' : 'shadow-[inset_0_0_0_1px_rgba(127,127,127,.5)]'}`}
            />
          )
        })}
      </div>
    )
  }

  return (
    <div role="group" aria-label={ariaLabel} className={`flex flex-col gap-1 ${className}`}>
      {UI_THEMES.map((t) => {
        const on = t.id === current
        return (
          <button
            key={t.id}
            type="button"
            aria-pressed={on}
            onClick={() => pick(t.id)}
            className={`xc-focus flex h-[34px] items-center gap-2.5 rounded-[7px] border px-2 text-left text-[12.5px] text-tx transition-colors ${on ? 'border-ui-soft-line bg-ui-soft font-semibold' : 'border-ui-line bg-ui-card font-normal hover:bg-ui-hover'}`}
          >
            <span
              aria-hidden="true"
              style={{ background: t.swatch }}
              className="h-[18px] w-[18px] flex-none rounded-full shadow-[inset_0_0_0_1px_rgba(127,127,127,.4)]"
            />
            {t.name}
          </button>
        )
      })}
    </div>
  )
}
