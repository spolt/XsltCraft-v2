import { useId } from 'react'
import { Bold, Italic, Underline, Eraser, CircleX, Info, type LucideIcon } from 'lucide-react'
import type { FixedNotePosition, FixedNoteFontFamily } from '../../services/themeComposeService'
import AlignPicker from './AlignPicker'
import {
  NOTE_COLOR_PRESETS,
  NOTE_FONTS,
  NOTE_FONT_SIZES,
  NOTE_MAX_CHARS,
  NOTE_MAX_LINES,
  NOTE_POSITIONS,
  clampNoteText,
  isHexColor,
  lineCount,
  noteFontCss,
  type FixedNoteState,
  type FixedNotesState,
} from './fixedNoteState'

interface Props {
  notes: FixedNotesState
  /** `immediate` → stil değişimi (önizleme beklemeden yenilenir); metin değişiminde false (debounce). */
  onChange: (position: FixedNotePosition, patch: Partial<FixedNoteState>, immediate: boolean) => void
  /** false → temada Notlar alanı yok; içerik pasif. null → henüz bilinmiyor. */
  supported: boolean | null
  /** Sunucunun `fixedNotes` alanı için döndürdüğü doğrulama hatası. */
  error: string | null
}

const selectClass =
  'xc-focus h-[30px] rounded-[5px] border border-field-line bg-ui-card px-1.5 text-[12px] text-tx disabled:opacity-60'

/** Tema Kullan: temanın Notlar kutusuna eklenen iki sabit not (üst / alt) ve yazı biçimi. */
export default function FixedNotesPanel({ notes, onChange, supported, error }: Props) {
  const disabled = supported === false
  return (
    <div className="flex flex-col gap-2.5">
      {disabled ? (
        <p className="m-0 flex items-start gap-1.5 rounded-md border border-ui-line bg-ui-bg px-3 py-2 text-[12px] leading-normal text-tx-muted">
          <Info size={13} aria-hidden="true" className="mt-0.5 flex-none" />
          Bu temada Notlar alanı bulunamadı. Sabit not eklenemez.
        </p>
      ) : (
        <p className="m-0 text-[12px] leading-normal text-tx-muted">
          Yazdığınız metin her faturada temanın Notlar kutusunda görünür.
        </p>
      )}
      {error && (
        <p role="alert" className="m-0 flex items-start gap-1.5 rounded-md border border-fn-red-line bg-fn-red-tint px-3 py-2 text-[12px] text-fn-red-ink">
          <CircleX size={13} aria-hidden="true" className="mt-0.5 flex-none" />
          {error}
        </p>
      )}
      <fieldset disabled={disabled} className="m-0 flex min-w-0 flex-col gap-2.5 border-0 p-0 disabled:opacity-60">
        {NOTE_POSITIONS.map((p) => (
          <NoteCard
            key={p.value}
            label={p.label}
            hint={p.hint}
            note={notes[p.value]}
            onChange={(patch, immediate) => onChange(p.value, patch, immediate)}
          />
        ))}
      </fieldset>
    </div>
  )
}

function NoteCard({
  label,
  hint,
  note,
  onChange,
}: {
  label: string
  hint: string
  note: FixedNoteState
  onChange: (patch: Partial<FixedNoteState>, immediate: boolean) => void
}) {
  const id = useId()
  const style = (patch: Partial<FixedNoteState>) => onChange(patch, true)
  const lines = lineCount(note.text)
  const hasText = note.text.length > 0

  return (
    <section aria-labelledby={`${id}-title`} className="flex flex-col gap-2 rounded-md border border-field-line bg-ui-card p-2.5">
      <div className="flex items-center justify-between gap-2">
        <h3 id={`${id}-title`} className="m-0 text-[12px] font-semibold text-tx">{label}</h3>
        <button
          type="button"
          onClick={() => onChange({ text: '' }, true)}
          disabled={!hasText}
          className="xc-focus inline-flex h-6 items-center gap-1 rounded px-1.5 text-[11px] text-tx-subtle hover:bg-ui-hover hover:text-fn-red-ink disabled:pointer-events-none disabled:opacity-40"
          aria-label={`${label}: metni temizle`}
          title="Temizle"
        >
          <Eraser size={12} aria-hidden="true" />
          Temizle
        </button>
      </div>

      <div role="toolbar" aria-label={`${label} biçimi`} className="flex flex-col gap-2">
        <div className="flex items-center gap-1.5">
          <div className="flex overflow-hidden rounded-md border border-field-line">
            <ToggleButton icon={Bold} label="Kalın" pressed={note.bold} onClick={() => style({ bold: !note.bold })} />
            <ToggleButton icon={Italic} label="İtalik" pressed={note.italic} onClick={() => style({ italic: !note.italic })} border />
            <ToggleButton icon={Underline} label="Altı çizili" pressed={note.underline} onClick={() => style({ underline: !note.underline })} border />
          </div>
          <div className="ml-auto w-[96px]">
            <AlignPicker value={note.align} onChange={(align) => style({ align })} />
          </div>
        </div>

        <div className="flex items-center gap-1.5">
          <label htmlFor={`${id}-size`} className="sr-only">Yazı boyutu</label>
          <select
            id={`${id}-size`}
            value={note.fontSize}
            onChange={(e) => style({ fontSize: Number(e.target.value) })}
            className={`${selectClass} w-[72px] flex-none`}
          >
            {NOTE_FONT_SIZES.map((s) => <option key={s} value={s}>{s} px</option>)}
          </select>

          <label htmlFor={`${id}-font`} className="sr-only">Yazı tipi</label>
          <select
            id={`${id}-font`}
            value={note.fontFamily}
            onChange={(e) => style({ fontFamily: e.target.value as FixedNoteFontFamily })}
            className={`${selectClass} min-w-0 flex-1`}
          >
            {NOTE_FONTS.map((f) => <option key={f.value} value={f.value}>{f.label}</option>)}
          </select>
        </div>

        <div className="flex flex-wrap items-center gap-2">
          <div role="radiogroup" aria-label="Yazı rengi" className="flex items-center gap-1">
            {NOTE_COLOR_PRESETS.map((c) => {
              const active = note.color.toUpperCase() === c.value
              return (
                <button
                  key={c.value}
                  type="button"
                  role="radio"
                  aria-checked={active}
                  aria-label={c.label}
                  title={c.label}
                  onClick={() => style({ color: c.value })}
                  className={`xc-focus h-5 w-5 rounded-full border border-ui-line ${active ? 'ring-2 ring-ui-accent ring-offset-1' : ''}`}
                  style={{ backgroundColor: c.value }}
                />
              )
            })}
          </div>
          <label className="inline-flex items-center gap-1 text-[11px] text-tx-muted">
            <input
              type="color"
              value={isHexColor(note.color) ? note.color : '#000000'}
              onChange={(e) => { if (isHexColor(e.target.value)) style({ color: e.target.value.toUpperCase() }) }}
              className="xc-focus h-6 w-7 cursor-pointer rounded border border-field-line bg-ui-card p-0.5"
            />
            Özel renk
          </label>
        </div>
      </div>

      <label htmlFor={`${id}-text`} className="sr-only">{label} metni</label>
      <textarea
        id={`${id}-text`}
        rows={3}
        value={note.text}
        placeholder={hint}
        aria-describedby={`${id}-count`}
        onChange={(e) => onChange({ text: clampNoteText(e.target.value) }, false)}
        className="min-h-[72px] w-full resize-y rounded-[5px] border border-field-line bg-ui-card px-2 py-1.5 leading-snug placeholder:text-tx-subtle placeholder:not-italic placeholder:font-normal placeholder:no-underline focus:border-ui-accent focus:outline-none focus:ring-2 focus:ring-ui-soft-line"
        style={{
          fontWeight: note.bold ? 700 : 400,
          fontStyle: note.italic ? 'italic' : 'normal',
          textDecoration: note.underline ? 'underline' : 'none',
          fontSize: `${note.fontSize}px`,
          color: isHexColor(note.color) ? note.color : undefined,
          fontFamily: noteFontCss(note.fontFamily),
          textAlign: note.align,
        }}
      />
      <div id={`${id}-count`} className="flex justify-between text-[11px] text-tx-subtle">
        <span>{lines}/{NOTE_MAX_LINES} satır</span>
        <span className={note.text.length >= NOTE_MAX_CHARS ? 'text-fn-orange-ink' : ''}>
          {note.text.length}/{NOTE_MAX_CHARS}
        </span>
      </div>
    </section>
  )
}

function ToggleButton({
  icon: Icon,
  label,
  pressed,
  onClick,
  border = false,
}: {
  icon: LucideIcon
  label: string
  pressed: boolean
  onClick: () => void
  border?: boolean
}) {
  return (
    <button
      type="button"
      aria-pressed={pressed}
      aria-label={label}
      title={label}
      onClick={onClick}
      className={`flex h-[28px] w-[30px] items-center justify-center transition-colors focus-visible:outline-2 focus-visible:-outline-offset-2 focus-visible:outline-ui-accent ${border ? 'border-l border-field-line' : ''} ${
        pressed ? 'bg-ui-soft text-ui-ink' : 'bg-ui-card text-tx-subtle hover:bg-ui-hover hover:text-tx-2'
      }`}
    >
      <Icon size={14} strokeWidth={pressed ? 2.4 : 1.8} aria-hidden="true" />
    </button>
  )
}
