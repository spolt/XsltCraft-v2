import { useId, type ReactNode } from 'react'
import { ImageUp, Trash2, RefreshCw, Undo2 } from 'lucide-react'
import type { ThemeSlotOption } from '../../services/templateService'
import AlignPicker from './AlignPicker'
import { IMAGE_ACCEPT, usesThemeBox, type ImgState } from './imageState'

interface Props {
  label: string
  state: ImgState
  uploading: boolean
  /** Temanın konumları; boşsa (analiz edilmemiş tema) konum seçimi gösterilmez. */
  slots: ThemeSlotOption[]
  /** Seçili (ya da varsayılan) konum. */
  slotValue: string | null
  onUpload: (e: React.ChangeEvent<HTMLInputElement>) => void
  onChange: (patch: Partial<ImgState>) => void
  onRemove: () => void
  /** Önizlemeye sürüklenebilir tutamak (yalnız Tema Kullan'da). */
  dragHandle?: ReactNode
  /** Temadaki bu türdeki görsel kullanıcı tarafından kaldırılabilir (admin izni). */
  themeImageRemovable?: boolean
  /** Başlığı gizle (başlık dışarıda, ör. akordeon düğmesinde gösteriliyorsa). */
  hideTitle?: boolean
  /** Yalnız Kolay düzenle (doc): kaldırılan dosya görselini geri getirir. */
  onRestore?: () => void
  /** Konum seçimi zorunlu (serbest görsel): varsayılan konum seçeneği yoktur, konumsuz görsel eklenmez. */
  requireSlot?: boolean
}

const ORIGIN_BADGE: Record<string, string> = { extracted: 'Dosyadan alındı', previous: 'Önceki kayıttan' }

const fieldLabel = 'mb-1 block text-[12px] text-tx-muted'
const fieldInput =
  'h-8 w-full rounded-md border border-field-line bg-ui-card px-2.5 text-[12px] text-tx placeholder:text-tx-subtle focus:border-ui-accent focus:outline-none focus:ring-2 focus:ring-ui-soft-line'

/** Logo / imza: yükleme, konum, boyut ve hizalama. */
export default function ImagePanel({
  label, state, uploading, slots, slotValue, onUpload, onChange, onRemove, dragHandle, themeImageRemovable = false,
  hideTitle = false, onRestore, requireSlot = false,
}: Props) {
  const groups = [...new Set(slots.map((s) => s.group))]
  const selectId = useId()
  // Temadaki görselin yerine geçerken kutu temadan gelir; kullanıcı yazınca kendi boyutu geçerli olur.
  const themeBox = usesThemeBox(state, slotValue)
  const fromFile = state.origin === 'extracted'
  const sizePlaceholder = themeBox ? (fromFile ? 'Dosyadaki boyut' : 'Temadaki boyut') : 'Otomatik'
  const badge = state.origin === 'upload' && state.index != null
    ? 'Dosyadaki görselin yerine geçer'
    : state.origin ? ORIGIN_BADGE[state.origin] : undefined
  // Tema kutusundan çıkarken diğer kenar otomatik kalsın (oran korunur).
  const setSize = (side: 'width' | 'height', value: string) =>
    onChange(themeBox
      ? { width: '', height: '', [side]: value, sizeTouched: true }
      : { [side]: value, sizeTouched: true })

  return (
    <section className="flex flex-col gap-3">
      {!hideTitle && (
        <h3 className="text-[12px] font-semibold uppercase tracking-wide text-tx-muted">{label}</h3>
      )}

      {state.dataUrl ? (
        <div className="flex items-center gap-2.5 rounded-md border border-field-line bg-ui-card p-2">
          {dragHandle ?? (
            <img src={state.dataUrl} alt={label} className="max-h-16 rounded bg-white object-contain p-1" />
          )}
          <div className="ml-auto flex flex-col items-start gap-1">
            {badge && (
              <span data-testid="image-origin-badge" className="rounded bg-ui-soft px-1.5 py-0.5 text-[10.5px] font-semibold text-ui-ink">
                {badge}
              </span>
            )}
            <label className="xc-focus flex cursor-pointer items-center gap-1 rounded text-[12px] font-medium text-ui-ink hover:underline focus-within:outline-2 focus-within:outline-ui-accent">
              <RefreshCw size={12} aria-hidden="true" />
              Değiştir
              <input type="file" accept={IMAGE_ACCEPT} className="sr-only" onChange={onUpload} />
            </label>
            <button
              type="button"
              onClick={onRemove}
              className="xc-focus flex items-center gap-1 rounded text-[12px] text-tx-subtle hover:text-fn-red-ink"
            >
              <Trash2 size={12} aria-hidden="true" />
              Kaldır
            </button>
          </div>
        </div>
      ) : (
        <label className="flex cursor-pointer flex-col items-center gap-1.5 rounded-lg border-[1.5px] border-dashed border-field-line bg-ui-side px-3 py-4 text-tx-2 transition-colors hover:border-ui-soft-line hover:bg-ui-soft focus-within:border-ui-accent focus-within:outline-2 focus-within:outline-ui-accent">
          {uploading ? (
            <span className="text-[12px] text-tx-subtle">Yükleniyor…</span>
          ) : (
            <>
              <ImageUp size={20} strokeWidth={1.8} aria-hidden="true" />
              <span className="text-[12.5px] font-semibold">Görsel yükle</span>
              <span className="text-[11.5px] text-tx-muted">PNG / JPG, en fazla 500 KB</span>
            </>
          )}
          <input type="file" accept={IMAGE_ACCEPT} className="sr-only" onChange={onUpload} disabled={uploading} />
        </label>
      )}

      {fromFile && !state.dataUrl && onRestore && (
        <p className="m-0 flex items-center gap-2 text-[12px] text-tx-2">
          Dosyadaki görsel kaldırıldı.
          <button
            type="button"
            onClick={onRestore}
            className="xc-focus inline-flex items-center gap-1 rounded font-semibold text-ui-ink hover:underline"
          >
            <Undo2 size={12} aria-hidden="true" />
            Geri al
          </button>
        </p>
      )}

      {themeImageRemovable && !state.dataUrl && !fromFile && (
        <label className="flex cursor-pointer select-none items-center gap-2 text-[12px] text-tx-2">
          <input
            type="checkbox"
            checked={state.hideThemeImage ?? false}
            onChange={(e) => onChange({ hideThemeImage: e.target.checked })}
            className="h-3.5 w-3.5 rounded border-field-line accent-ui-accent"
          />
          Temadaki görseli kaldır
        </label>
      )}

      <div className="flex flex-col gap-2.5">
        {requireSlot && slots.length === 0 && (
          <p className="m-0 text-[12px] text-tx-muted">Bu belgede görsel eklenebilecek boş bir konum yok.</p>
        )}
        {slots.length > 0 && (
          <div>
            <label htmlFor={selectId} className={fieldLabel}>Konum</label>
            <select
              id={selectId}
              value={slotValue ?? ''}
              onChange={(e) => onChange({ slot: e.target.value || null })}
              className={fieldInput}
              aria-invalid={requireSlot && !!state.dataUrl && slotValue === null ? true : undefined}
            >
              {slotValue === null && (requireSlot
                ? <option value="" disabled>Konum seç</option>
                : <option value="">{fromFile ? 'Dosyadaki yeri' : 'Temanın varsayılan alanı'}</option>)}
              {groups.map((g) => (
                <optgroup key={g} label={g}>
                  {slots.filter((s) => s.group === g).map((s) => (
                    <option key={s.id} value={s.id}>{s.label}</option>
                  ))}
                </optgroup>
              ))}
            </select>
            {requireSlot && state.dataUrl && slotValue === null && (
              <p className="mt-1 mb-0 text-[11.5px] text-fn-orange-ink">
                Görselin belgeye eklenmesi için bir konum seç ya da önizlemedeki bir alana sürükle.
              </p>
            )}
          </div>
        )}

        <div className="grid grid-cols-2 gap-2">
          <div>
            <label htmlFor={`${selectId}-w`} className={fieldLabel}>Genişlik (px)</label>
            <input
              id={`${selectId}-w`}
              type="number" min={10} max={1200} placeholder={sizePlaceholder}
              value={themeBox ? '' : state.width}
              onChange={(e) => setSize('width', e.target.value)}
              className={fieldInput}
            />
          </div>
          <div>
            <label htmlFor={`${selectId}-h`} className={fieldLabel}>Yükseklik (px)</label>
            <input
              id={`${selectId}-h`}
              type="number" min={10} max={1200} placeholder={sizePlaceholder}
              value={themeBox ? '' : state.height}
              onChange={(e) => setSize('height', e.target.value)}
              className={fieldInput}
            />
          </div>
        </div>

        <div>
          <span className={fieldLabel}>
            Hizalama{slots.length > 0 && <span className="text-tx-subtle"> · seçili konum içinde</span>}
          </span>
          <AlignPicker value={state.alignment} onChange={(v) => onChange({ alignment: v })} />
        </div>
      </div>
    </section>
  )
}
