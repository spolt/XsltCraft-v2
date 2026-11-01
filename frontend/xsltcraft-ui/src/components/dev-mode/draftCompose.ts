import type { ThemeSlotOption } from '../../services/templateService'
import type { ThemeDraftFields, ThemeDraftMeta } from '../../store/themeDraftStore'
import { countNotes, toComposeNotes } from '../theme-assets/fixedNoteState'
import { filledBankRows, hiddenThemeImages, toComposeAssets } from '../theme-assets/imageState'

export type DraftAssets = Pick<ThemeDraftFields, 'logo' | 'signature' | 'bankInfo' | 'fixedNotes'> & {
  notesTouched: boolean
}

/** Taslağın compose isteğine giden alanları. Dokunulmayan not null gider (metne dokunulmaz), boşaltılan [] (notlar silinir). */
export function draftComposeFields(draft: DraftAssets, slots: ThemeSlotOption[]) {
  return {
    assets: toComposeAssets(draft.logo, draft.signature, slots),
    bankInfo: filledBankRows(draft.bankInfo),
    hideThemeImages: hiddenThemeImages(draft.logo, draft.signature, slots),
    fixedNotes: draft.notesTouched ? (toComposeNotes(draft.fixedNotes) ?? []) : null,
  }
}

export type DraftComposeFields = ReturnType<typeof draftComposeFields>

/** `composedKey` ile karşılaştırılan anahtar: metne son işlenmiş varlık durumu. */
export const draftComposeKey = (fields: DraftComposeFields) => JSON.stringify(fields)

/** Hiç varlık işlenmemiş ham tema metninin anahtarı. */
export const emptyDraftComposeKey = () =>
  draftComposeKey({ assets: [], bankInfo: [], hideThemeImages: [], fixedNotes: null })

/** Tema bilgisi alınamadığında belge türü metindeki ad alanından anlaşılır. */
export const documentTypeFromXslt = (xslt: string): ThemeDraftMeta['documentType'] =>
  xslt.includes('DespatchAdvice-2') ? 'Despatch' : 'Invoice'

/** Düzenle panelinin ilk açılış kararı (K5). */
export function draftHasContent(draft: DraftAssets): boolean {
  return !!draft.logo.dataUrl
    || !!draft.signature.dataUrl
    || !!draft.logo.hideThemeImage
    || !!draft.signature.hideThemeImage
    || filledBankRows(draft.bankInfo).length > 0
    || countNotes(draft.fixedNotes) > 0
}
