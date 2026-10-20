import { useId, type ReactNode } from 'react'
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
}

/** Logo / imza: yükleme, konum, boyut ve hizalama. */
export default function ImagePanel({
  label, state, uploading, slots, slotValue, onUpload, onChange, onRemove, dragHandle, themeImageRemovable = false,
}: Props) {
  const groups = [...new Set(slots.map((s) => s.group))]
  const selectId = useId()
  // Temadaki görselin yerine geçerken kutu temadan gelir; kullanıcı yazınca kendi boyutu geçerli olur.
  const themeBox = usesThemeBox(state, slotValue)
  const sizePlaceholder = themeBox ? 'Temadaki boyut' : 'Otomatik'
  // Tema kutusundan çıkarken diğer kenar otomatik kalsın (oran korunur).
  const setSize = (side: 'width' | 'height', value: string) =>
    onChange(themeBox
      ? { width: '', height: '', [side]: value, sizeTouched: true }
      : { [side]: value, sizeTouched: true })

  return (
    <section>
      <h3 className="text-xs font-semibold text-gray-500 uppercase tracking-wide mb-2">{label}</h3>

      {state.dataUrl ? (
        <div className="flex items-center gap-2 mb-3">
          {dragHandle ?? (
            <img src={state.dataUrl} alt={label} className="max-h-16 object-contain border border-gray-100 rounded p-1 bg-gray-50" />
          )}
          <div className="flex flex-col gap-1">
            <label className="cursor-pointer text-xs text-blue-600 hover:underline">
              Değiştir
              <input type="file" accept={IMAGE_ACCEPT} className="hidden" onChange={onUpload} />
            </label>
            <button type="button" onClick={onRemove} className="text-xs text-gray-400 hover:text-red-500 text-left">
              Kaldır
            </button>
          </div>
        </div>
      ) : (
        <label className="cursor-pointer flex items-center gap-2 border border-dashed border-gray-300 rounded-lg px-3 py-3 hover:border-blue-300 hover:bg-blue-50 transition-colors mb-3">
          {uploading
            ? <span className="text-xs text-gray-400">Yükleniyor…</span>
            : <><span className="text-lg">🖼</span><span className="text-xs text-gray-500">Görsel yükle (PNG / JPG, en fazla 500 KB)</span></>}
          <input type="file" accept={IMAGE_ACCEPT} className="hidden" onChange={onUpload} disabled={uploading} />
        </label>
      )}

      {themeImageRemovable && !state.dataUrl && (
        <label className="flex items-center gap-2 text-xs text-gray-600 mb-3 cursor-pointer select-none">
          <input
            type="checkbox"
            checked={state.hideThemeImage ?? false}
            onChange={(e) => onChange({ hideThemeImage: e.target.checked })}
            className="w-3.5 h-3.5 rounded border-gray-300"
          />
          Temadaki görseli kaldır
        </label>
      )}

      <div className="flex flex-col gap-2">
        {slots.length > 0 && (
          <div>
            <label htmlFor={selectId} className="text-xs text-gray-400 mb-0.5 block">Konum</label>
            <select
              id={selectId}
              value={slotValue ?? ''}
              onChange={(e) => onChange({ slot: e.target.value || null })}
              className="w-full text-xs border border-gray-200 rounded px-2 py-1.5 bg-white focus:outline-none focus:ring-1 focus:ring-blue-400"
            >
              {slotValue === null && <option value="">Temanın varsayılan alanı</option>}
              {groups.map((g) => (
                <optgroup key={g} label={g}>
                  {slots.filter((s) => s.group === g).map((s) => (
                    <option key={s.id} value={s.id}>{s.label}</option>
                  ))}
                </optgroup>
              ))}
            </select>
          </div>
        )}

        <div className="flex gap-2">
          <div className="flex-1">
            <label htmlFor={`${selectId}-w`} className="text-xs text-gray-400 mb-0.5 block">Genişlik (px)</label>
            <input
              id={`${selectId}-w`}
              type="number" min={10} max={1200} placeholder={sizePlaceholder}
              value={themeBox ? '' : state.width}
              onChange={(e) => setSize('width', e.target.value)}
              className="w-full text-xs border border-gray-200 rounded px-2 py-1.5 focus:outline-none focus:ring-1 focus:ring-blue-400"
            />
          </div>
          <div className="flex-1">
            <label htmlFor={`${selectId}-h`} className="text-xs text-gray-400 mb-0.5 block">Yükseklik (px)</label>
            <input
              id={`${selectId}-h`}
              type="number" min={10} max={1200} placeholder={sizePlaceholder}
              value={themeBox ? '' : state.height}
              onChange={(e) => setSize('height', e.target.value)}
              className="w-full text-xs border border-gray-200 rounded px-2 py-1.5 focus:outline-none focus:ring-1 focus:ring-blue-400"
            />
          </div>
        </div>

        <div>
          <label className="text-xs text-gray-400 mb-0.5 block">
            Hizalama{slots.length > 0 && <span className="text-gray-300"> · seçili konum içinde</span>}
          </label>
          <AlignPicker value={state.alignment} onChange={(v) => onChange({ alignment: v })} />
        </div>
      </div>
    </section>
  )
}
