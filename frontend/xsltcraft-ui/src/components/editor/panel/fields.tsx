import { createContext, useContext, useId } from 'react'
import { Check, Link2 } from 'lucide-react'
import { useXmlStore } from '../../../store/xmlStore'
import MatchInfo from './MatchInfo'

/*
 * "Seçili blok" panelinin ortak alan yardımcıları (UI 2.0, token renkleri).
 * Tüm blok panelleri bunları kullanır; mantık panellerde, burada yalnız sunum.
 */

/** Field etiketinin id'si — içteki kontroller aria-labelledby ile bağlanır. */
const FieldLabelContext = createContext<string | undefined>(undefined)

const INPUT_CLS =
  'h-8 w-full min-w-0 rounded-md border border-field-line bg-ui-card px-2.5 text-[12px] text-tx outline-none transition-colors placeholder:text-tx-subtle focus:border-ui-accent focus:ring-2 focus:ring-ui-soft'

/** Panel bölümü: başlık (+ sağda soluk meta) ve içerik. */
export function PanelSection({ title, meta, children }: { title: string; meta?: React.ReactNode; children: React.ReactNode }) {
  const id = useId()
  return (
    <section aria-labelledby={id} className="flex min-w-0 flex-col gap-2">
      <div className="flex items-center justify-between gap-2">
        <h3 id={id} className="m-0 text-[12px] font-semibold text-tx-2">{title}</h3>
        {meta !== undefined && <span className="text-[11px] text-tx-muted">{meta}</span>}
      </div>
      {children}
    </section>
  )
}

export function Field({ label, children }: { label: string; children: React.ReactNode }) {
  const id = useId()
  return (
    <div className="flex min-w-0 flex-1 flex-col gap-1">
      <span id={id} className="text-[11.5px] font-medium text-tx-muted">{label}</span>
      <FieldLabelContext.Provider value={id}>{children}</FieldLabelContext.Provider>
    </div>
  )
}

/** Field'ları yan yana dizer. */
export function FieldRow({ children }: { children: React.ReactNode }) {
  return <div className="flex min-w-0 gap-2">{children}</div>
}

export function TextInput({
  value,
  onChange,
  placeholder,
  ariaLabel,
  mono,
}: {
  value: string
  onChange: (v: string) => void
  placeholder?: string
  ariaLabel?: string
  mono?: boolean
}) {
  const labelledBy = useContext(FieldLabelContext)
  return (
    <input
      type="text"
      value={value}
      onChange={(e) => onChange(e.target.value)}
      placeholder={placeholder}
      aria-label={ariaLabel}
      aria-labelledby={ariaLabel ? undefined : labelledBy}
      spellCheck={mono ? false : undefined}
      className={`${INPUT_CLS}${mono ? ' font-mono' : ''}`}
    />
  )
}

export function NumberInput({
  value,
  onChange,
  min,
  max,
  step,
}: {
  value: number
  onChange: (v: string) => void
  min?: number
  max?: number
  step?: number
}) {
  const labelledBy = useContext(FieldLabelContext)
  return (
    <input
      type="number"
      min={min}
      max={max}
      step={step}
      value={value}
      onChange={(e) => onChange(e.target.value)}
      aria-labelledby={labelledBy}
      className={`${INPUT_CLS} tabular-nums`}
    />
  )
}

/**
 * XPath alanı: mono giriş + "XML ağacından seç" (startXPathSelection) + eşleşme bilgisi.
 * `match="nodes"` satır kaynakları için eşleşen düğüm sayısını, `value` çözülen değeri gösterir.
 */
export function XPathInput({
  value,
  onChange,
  placeholder,
  match = 'value',
  contextXPath,
  noun,
  ariaLabel,
}: {
  value: string
  onChange: (v: string) => void
  placeholder?: string
  match?: 'value' | 'nodes'
  contextXPath?: string
  noun?: string
  ariaLabel?: string
}) {
  const hasXml = useXmlStore((s) => s.xmlFiles.some((f) => f.id === s.activeXmlId))
  const isSelecting = useXmlStore((s) => s.isSelecting)
  const startXPathSelection = useXmlStore((s) => s.startXPathSelection)
  const labelledBy = useContext(FieldLabelContext)

  return (
    <div className="flex min-w-0 flex-col gap-1.5">
      <div className="flex gap-1.5">
        <input
          type="text"
          value={value}
          onChange={(e) => onChange(e.target.value)}
          placeholder={placeholder}
          aria-label={ariaLabel}
          aria-labelledby={ariaLabel ? undefined : labelledBy}
          spellCheck={false}
          className="h-[34px] min-w-0 flex-1 rounded-md border border-ui-soft-line bg-ui-card px-2.5 font-mono text-[12px] text-tx outline-none transition-colors placeholder:text-tx-subtle focus:border-ui-accent focus:ring-2 focus:ring-ui-soft"
        />
        {hasXml && (
          <button
            type="button"
            title="XML ağacından seç"
            aria-label="XML ağacından seç"
            aria-pressed={isSelecting}
            onClick={() => startXPathSelection(onChange)}
            className={`xc-focus flex h-[34px] w-[34px] flex-none items-center justify-center rounded-md border transition-colors ${
              isSelecting
                ? 'border-ui-soft-line bg-ui-soft text-ui-ink'
                : 'border-field-line bg-ui-card text-tx-2 hover:bg-ui-hover'
            }`}
          >
            <Link2 size={15} strokeWidth={1.8} aria-hidden="true" />
          </button>
        )}
      </div>
      <MatchInfo xpath={value} mode={match} contextXPath={contextXPath} noun={noun} />
    </div>
  )
}

export function Select<T extends string>({
  value,
  onChange,
  options,
}: {
  value: T
  onChange: (v: T) => void
  options: readonly { value: T; label: string }[]
}) {
  const labelledBy = useContext(FieldLabelContext)
  return (
    <select
      value={value}
      onChange={(e) => onChange(e.target.value as T)}
      aria-labelledby={labelledBy}
      className={`${INPUT_CLS} cursor-pointer px-2`}
    >
      {options.map((o) => (
        <option key={o.value} value={o.value}>
          {o.label}
        </option>
      ))}
    </select>
  )
}

export function ColorInput({
  value,
  onChange,
  placeholder,
}: {
  value: string
  onChange: (v: string) => void
  placeholder?: string
}) {
  // Normalize: color picker requires a valid 6-digit hex; fall back to #000000
  const pickerValue = /^#[0-9a-fA-F]{6}$/.test(value) ? value : '#000000'
  const labelledBy = useContext(FieldLabelContext)

  return (
    <div className="flex min-w-0 items-center gap-1.5">
      <label className="relative h-8 w-8 flex-none cursor-pointer rounded-md border border-field-line bg-ui-card p-1 focus-within:border-ui-accent focus-within:ring-2 focus-within:ring-ui-soft">
        <span className="sr-only">Renk seç</span>
        <input
          type="color"
          value={pickerValue}
          onChange={(e) => onChange(e.target.value)}
          className="absolute inset-0 h-full w-full cursor-pointer opacity-0"
        />
        {/* Kullanıcının seçtiği renk (veri) — örnek kutusu */}
        <span aria-hidden="true" className="block h-full w-full rounded" style={{ background: pickerValue }} />
      </label>
      <input
        type="text"
        value={value}
        onChange={(e) => onChange(e.target.value)}
        placeholder={placeholder ?? '#000000'}
        aria-labelledby={labelledBy}
        spellCheck={false}
        className={`${INPUT_CLS} flex-1 font-mono`}
      />
    </div>
  )
}

/** Görsel onay kutusu. */
export function CheckMark({ checked }: { checked: boolean }) {
  return (
    <span
      aria-hidden="true"
      className={`inline-flex h-4 w-4 flex-none items-center justify-center rounded border-[1.5px] transition-colors ${
        checked ? 'border-ui-accent bg-ui-accent text-ui-accent-fg' : 'border-tx-subtle bg-ui-card text-transparent'
      }`}
    >
      <Check size={11} strokeWidth={3} />
    </span>
  )
}

/** Gizli native checkbox + görsel kutu; klavye odağı etiketin çevresinde halka çizer. */
const CHECK_LABEL_CLS =
  'relative flex cursor-pointer items-center gap-2 rounded has-[:focus-visible]:outline-2 has-[:focus-visible]:outline-offset-2 has-[:focus-visible]:outline-ui-accent'

export function Checkbox({
  label,
  checked,
  onChange,
}: {
  label: string
  checked: boolean
  onChange: (v: boolean) => void
}) {
  return (
    <label className={CHECK_LABEL_CLS}>
      <input type="checkbox" checked={checked} onChange={(e) => onChange(e.target.checked)} className="sr-only" />
      <CheckMark checked={checked} />
      <span className="text-[12px] text-tx-2">{label}</span>
    </label>
  )
}

/** Liste satırındaki aç/kapa onay kutusu (buton; aria-pressed). */
export function CheckToggle({ checked, onToggle, label }: { checked: boolean; onToggle: () => void; label: string }) {
  return (
    <button
      type="button"
      aria-pressed={checked}
      aria-label={label}
      title={label}
      onClick={(e) => { e.stopPropagation(); onToggle() }}
      className="xc-focus flex h-5 w-5 flex-none items-center justify-center rounded"
    >
      <CheckMark checked={checked} />
    </button>
  )
}

/** Küçük ikon butonu (taşı/sil vb.). */
export function IconButton({
  label,
  onClick,
  disabled,
  danger,
  pressed,
  children,
}: {
  label: string
  onClick: () => void
  disabled?: boolean
  danger?: boolean
  pressed?: boolean
  children: React.ReactNode
}) {
  return (
    <button
      type="button"
      aria-label={label}
      title={label}
      aria-pressed={pressed}
      disabled={disabled}
      onClick={(e) => { e.stopPropagation(); onClick() }}
      className={`xc-focus flex h-6 w-6 flex-none items-center justify-center rounded transition-colors disabled:opacity-30 ${
        danger ? 'text-fn-red-ink hover:bg-fn-red-tint' : pressed ? 'bg-ui-soft text-ui-ink' : 'text-tx-muted hover:bg-ui-hover hover:text-tx'
      }`}
    >
      {children}
    </button>
  )
}

/** "+ Kolon ekle" tarzı metin butonu. */
export function AddButton({ onClick, children }: { onClick: () => void; children: React.ReactNode }) {
  return (
    <button
      type="button"
      onClick={onClick}
      className="xc-focus self-start rounded px-1 py-1 text-[12px] font-semibold text-ui-ink hover:underline"
    >
      {children}
    </button>
  )
}

/** Satır listesi kabı (mockup: tek kenarlıklı, satırlar arası ince çizgi). */
export function ListBox({ children }: { children: React.ReactNode }) {
  return (
    <div className="flex min-w-0 flex-col divide-y divide-ui-line overflow-hidden rounded-md border border-ui-line bg-ui-card">
      {children}
    </div>
  )
}

/** Satırın sağındaki soluk mono XPath. */
export function MonoPath({ value }: { value: string }) {
  return (
    <span className="max-w-[124px] flex-none truncate font-mono text-[10.5px] text-tx-muted" title={value}>
      {value || '—'}
    </span>
  )
}

/** Düzenlenebilir öğe kartı (başlık satırı + gövde). */
export function ItemCard({ title, actions, children }: { title: React.ReactNode; actions?: React.ReactNode; children: React.ReactNode }) {
  return (
    <div className="flex min-w-0 flex-col gap-2 rounded-md border border-ui-line bg-ui-card p-2.5">
      <div className="flex items-center justify-between gap-2">
        <span className="min-w-0 truncate text-[11.5px] font-medium text-tx-muted">{title}</span>
        {actions && <div className="flex items-center gap-0.5">{actions}</div>}
      </div>
      {children}
    </div>
  )
}

export function InfoNote({ children }: { children: React.ReactNode }) {
  return <p className="m-0 rounded-md bg-ui-side px-2.5 py-2 text-[11.5px] leading-relaxed text-tx-muted">{children}</p>
}
