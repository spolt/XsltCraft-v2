import type { AssetKind } from '../../services/themeComposeService'
import type { ThemeSlotOption } from '../../services/templateService'
import type { EasyEditAnalyzeResponse } from '../../services/easyEditService'
import { isValidSlotId } from '../../services/easyEditGuards'
import { useDraftField } from '../../store/themeDraftStore'
import { placementSlot, type ImgState } from '../theme-assets/imageState'
import { moveImage, patchImage, replaceWithUpload } from './docImageEdits'
import { restoreExtracted } from './docDraft'

/**
 * Doc kaynağında görsel düzenlemeleri: köken kurallarıyla panel değişiklikleri, kaldır/geri al, yükleme ve
 * sürükle-bırakla taşıma. Yerleşim değişiklikleri önizlemeyi beklemeden yeniler.
 */
export function useDocImages(analysis: EasyEditAnalyzeResponse | null, slots: ThemeSlotOption[], composeNow: () => void) {
  const [logo, setLogo] = useDraftField('logo')
  const [signature, setSignature] = useDraftField('signature')
  const stateOf = (kind: AssetKind) => (kind === 'logo' ? logo : signature)
  const setterOf = (kind: AssetKind) => (kind === 'logo' ? setLogo : setSignature)

  function update(kind: AssetKind, fn: (s: ImgState) => ImgState, immediate = true) {
    setterOf(kind)(fn)
    if (immediate) composeNow()
  }

  /** Paneldeki alan değişikliği (boyut/hiza debounce ile, konum hemen). */
  const change = (kind: AssetKind, patch: Partial<ImgState>) => update(kind, (s) => patchImage(s, patch), 'slot' in patch)

  const upload = (kind: AssetKind, dataUrl: string) => update(kind, (s) => replaceWithUpload(s, dataUrl), false)

  /**
   * Dosyadaki görsel kaldırılınca `remove` isteği gider. Onun yerine yüklenen görsel kaldırılınca dosyadaki görsel
   * dokunulmamış hâline döner (önizlemede zaten yeniden görünür).
   */
  const remove = (kind: AssetKind) => update(kind, (s) => {
    if (s.origin === 'upload' && s.index != null) return restoreExtracted(analysis, s.index) ?? { ...s, dataUrl: '' }
    return { ...s, dataUrl: '' }
  })

  const restore = (kind: AssetKind) => update(kind, (s) => restoreExtracted(analysis, s.index) ?? s)

  /** Önizlemede bırakılan yuvaya doğrudan taşır (yuva kimliği önizleme HTML'inden gelir: biçim denetimi). */
  function move(kind: AssetKind, slot: string) {
    const state = stateOf(kind)
    if (!state.dataUrl || !isValidSlotId(slot) || placementSlot(kind, state, slots) === slot) return
    update(kind, (s) => moveImage(s, slot))
  }

  /**
   * Dosyanın kendi görselleri için önizleme tutamağının aranacağı yuvalar: önce hedef yuva, sonra kendi yuvası. Taşınan
   * görsel yuva işaretini eski yerde (boş kutu) bırakır; tutamak yuvadaki o türden görselden bulunur.
   */
  const staticHandles: Partial<Record<AssetKind, string[]>> = {}
  for (const [kind, s] of [['logo', logo], ['signature', signature]] as const) {
    if (s.origin === 'extracted' && s.dataUrl) staticHandles[kind] = [s.slot, s.homeSlot].filter((x): x is string => !!x)
  }

  return { change, upload, remove, restore, move, staticHandles }
}
