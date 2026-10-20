import { useId, useState } from 'react'
import { ImageOff, TriangleAlert } from 'lucide-react'
import type { ImageDecision, ImageRole, ImageVisibility, OverridesDecision, UploadDecision, UploadDecisionRequest } from '../../services/adminService'

const KIND_LABEL = { logo: 'Logo', signature: 'İmza / Kaşe' } as const

const ROLE_OPTIONS: { value: ImageRole; label: string }[] = [
  { value: 'logo', label: 'Firma logosu' },
  { value: 'signature', label: 'İmza / kaşe' },
  { value: 'design', label: 'Tasarımın parçası' },
]

const VISIBILITY_OPTIONS: { value: ImageVisibility; label: string; designOk: boolean }[] = [
  { value: 'keep', label: 'Görünsün', designOk: true },
  { value: 'removable', label: 'Görünsün, kullanıcı kaldırabilsin', designOk: false },
  { value: 'strip', label: 'Temadan kaldır', designOk: true },
]

interface Props {
  request: UploadDecisionRequest
  busy: boolean
  onDecide: (decision: UploadDecision) => void
  onCancel: () => void
}

/**
 * Tema yüklenirken admin'e sorulanlar:
 * - Önceden gömülü görsel bloğu (DEV-MODE-OVERRIDES): tasarımın parçası mı (koru), önceki bir kullanıcının
 *   logosu/imzası mı (kaldır)?
 * - Temadaki statik görseller: firma logosu / imza-kaşe ise kullanıcının görseli onların yerini alır;
 *   tasarımın parçasıysa dokunulmaz. Analizin önerisi seçili gelir.
 */
export default function ThemeUploadDecisionDialog({ request, busy, onDecide, onCancel }: Props) {
  const { overrides, images } = request
  const [overridesChoice, setOverridesChoice] = useState<OverridesDecision | null>(null)
  // Logo/kaşe varsayılan olarak temadan kaldırılır: çoğunlukla temayı hazırlayan firmaya ait. Tasarım görseline dokunulmaz.
  const [decisions, setDecisions] = useState<ImageDecision[]>(() => images.map((i) => ({
    role: i.suggested, visibility: i.suggested === 'design' ? 'keep' : 'strip',
  })))
  const patch = (i: number, p: Partial<ImageDecision>) => setDecisions((d) => d.map((v, k) => {
    if (k !== i) return v
    const next = { ...v, ...p }
    // Tasarım görselinde "kullanıcı kaldırabilsin" yok.
    return next.role === 'design' && next.visibility === 'removable' ? { ...next, visibility: 'keep' } : next
  }))
  const radioName = useId()
  const selectId = useId()

  const ready = !overrides || overridesChoice !== null
  const submit = () => onDecide({
    existingOverrides: overridesChoice ?? undefined,
    images: images.length > 0 ? decisions : undefined,
  })

  return (
    <div className="fixed inset-0 bg-black/50 flex items-center justify-center z-50" role="dialog" aria-modal="true" aria-labelledby="upload-decision-title">
      <div className="bg-white rounded-xl shadow-xl max-w-lg w-full mx-4 max-h-[90vh] flex flex-col">
        <h3 id="upload-decision-title" className="text-base font-semibold text-gray-900 px-6 pt-6 pb-2">Temadaki görseller</h3>

        <div className="overflow-y-auto px-6 pb-2 flex flex-col gap-5">
          {images.length > 0 && (
            <section>
              <p className="text-sm text-gray-600 mb-3">
                Firma logosu ya da imza/kaşe olarak işaretlediğiniz görselin yeri kullanıcıya konum olarak sunulur:
                temayı kullanan kişi kendi görselini eklediğinde onun yerini alır, eklemezse temadaki görsel kalır.
                Tasarımın parçası olan görsele dokunulmaz. "Temadan kaldır" görseli hiç göstermez: logo/imzanın yeri
                kullanıcının görseli için konum olarak kalır, tasarım görseli tamamen silinir.
              </p>
              <ul className="flex flex-col gap-2">
                {images.map((img, i) => (
                  <li key={img.index} className="flex items-center gap-3 border border-gray-100 rounded-lg p-2 bg-gray-50">
                    {img.previewDataUrl
                      ? <img src={img.previewDataUrl} alt="" className="h-12 w-20 object-contain bg-white border border-gray-100 rounded flex-shrink-0" />
                      : <span className="h-12 w-20 flex items-center justify-center text-gray-300 flex-shrink-0"><ImageOff size={18} /></span>}
                    <div className="text-xs text-gray-500 min-w-0 flex-1">
                      <p className="text-gray-700">Öneri: {img.reason}</p>
                      <p>satır {img.line}</p>
                    </div>
                    <div className="flex flex-col items-end gap-1 flex-shrink-0">
                      <label htmlFor={`${selectId}-${i}`} className="sr-only">{`${i + 1}. görselin rolü`}</label>
                      <select
                        id={`${selectId}-${i}`}
                        value={decisions[i].role}
                        onChange={(e) => patch(i, { role: e.target.value as ImageRole })}
                        className="border border-gray-300 rounded-lg px-2 py-1.5 text-sm bg-white focus:outline-none focus:ring-2 focus:ring-blue-500"
                      >
                        {ROLE_OPTIONS.map((o) => <option key={o.value} value={o.value}>{o.label}</option>)}
                      </select>
                      <label htmlFor={`${selectId}-v${i}`} className="sr-only">{`${i + 1}. görsel temada`}</label>
                      <select
                        id={`${selectId}-v${i}`}
                        value={decisions[i].visibility}
                        onChange={(e) => patch(i, { visibility: e.target.value as ImageVisibility })}
                        className={`border rounded-lg px-2 py-1 text-xs bg-white focus:outline-none focus:ring-2 focus:ring-blue-500 ${
                          decisions[i].visibility === 'strip' ? 'border-red-300 text-red-700' : 'border-gray-200 text-gray-600'}`}
                      >
                        {VISIBILITY_OPTIONS.filter((o) => o.designOk || decisions[i].role !== 'design')
                          .map((o) => <option key={o.value} value={o.value}>{o.label}</option>)}
                      </select>
                    </div>
                  </li>
                ))}
              </ul>
            </section>
          )}

          {overrides && (
            <section>
              <h4 className="text-sm font-semibold text-gray-800 mb-1">Önceden eklenmiş görsel bloğu</h4>
              <p className="text-sm text-gray-600 mb-3">
                Bu dosya daha önce Tema Kullan / Geliştirici Modu ile görsel eklenip indirilmiş bir şablon gibi görünüyor.
                Görseller tasarımın parçasıysa koruyun; önceki bir kullanıcının logosu/imzasıysa kaldırın — aksi halde
                temayı kullanan herkes bu görselleri görür.
              </p>

              {overrides.images.length > 0 && (
                <ul className="flex flex-col gap-2 mb-3">
                  {overrides.images.map((img, i) => (
                    <li key={i} className="flex items-center gap-3 border border-gray-100 rounded-lg p-2 bg-gray-50">
                      {img.dataUrl
                        ? <img src={img.dataUrl} alt="" className="h-12 w-20 object-contain bg-white border border-gray-100 rounded" />
                        : <span className="h-12 w-20 flex items-center justify-center text-gray-300"><ImageOff size={18} /></span>}
                      <div className="text-xs text-gray-600 min-w-0">
                        <p className="font-medium text-gray-800">{img.kind ? KIND_LABEL[img.kind] : 'Görsel'}</p>
                        <p className="truncate">Alan: {img.targets.map((t) => `#${t}`).join(', ')}</p>
                        {(img.width || img.height) && <p>{img.width ?? 'oto'} × {img.height ?? 'oto'} px · {img.alignment}</p>}
                        {img.problem && <p className="text-red-600">{img.problem}</p>}
                      </div>
                    </li>
                  ))}
                </ul>
              )}
              {overrides.bankRows > 0 && (
                <p className="text-xs text-gray-500 mb-3">Blokta ayrıca {overrides.bankRows} satırlık bir banka/IBAN tablosu var.</p>
              )}
              {!overrides.keepable && (
                <p className="text-xs text-amber-800 bg-amber-50 border border-amber-200 rounded-lg px-3 py-2 mb-3 flex gap-1.5">
                  <TriangleAlert size={14} className="flex-shrink-0 mt-px" />
                  <span>Korunamıyor: {overrides.reason ?? 'blok tanınmadı'}. Yalnız kaldırılabilir.</span>
                </p>
              )}

              <div className="flex flex-col gap-1.5 text-sm" role="radiogroup" aria-label="Önceden eklenmiş görsel bloğu">
                <label className="flex items-center gap-2 cursor-pointer">
                  <input type="radio" name={radioName} checked={overridesChoice === 'strip'} onChange={() => setOverridesChoice('strip')} />
                  Kaldır
                </label>
                <label className={`flex items-center gap-2 ${overrides.keepable ? 'cursor-pointer' : 'opacity-40'}`}
                  title={overrides.keepable ? 'Görseller temaya kalıcı olarak gömülür; o alanlar kullanıcıya konum olarak sunulmaz.' : undefined}>
                  <input type="radio" name={radioName} disabled={!overrides.keepable}
                    checked={overridesChoice === 'keep'} onChange={() => setOverridesChoice('keep')} />
                  Temanın parçası olarak koru
                </label>
              </div>
            </section>
          )}
        </div>

        <div className="flex gap-3 justify-end flex-wrap px-6 py-4 border-t border-gray-100">
          <button
            type="button" onClick={onCancel} disabled={busy}
            className="px-4 py-2 text-sm text-gray-700 border border-gray-300 hover:bg-gray-50 rounded-lg transition disabled:opacity-50"
          >
            İptal
          </button>
          <button
            type="button" onClick={submit} disabled={busy || !ready}
            className="px-4 py-2 text-sm text-white bg-blue-600 hover:bg-blue-700 rounded-lg transition disabled:opacity-40"
          >
            Devam et
          </button>
        </div>
      </div>
    </div>
  )
}
