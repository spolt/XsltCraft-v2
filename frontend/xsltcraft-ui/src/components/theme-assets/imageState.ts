import type { Alignment, BankInfoItem } from '../../services/previewService'
import type { AssetKind, ComposeAsset } from '../../services/themeComposeService'
import type { ThemeSlotOption } from '../../services/templateService'

/** Tema Kullan / Geliştirici Modu'nda kullanıcının eklediği logo ya da imza. */
export interface ImgState {
  dataUrl: string
  width: string   // input kontrollü kalsın diye string; gönderirken sayıya çevrilir
  height: string
  alignment: Alignment
  /** Seçilen yuva; null → temanın varsayılan konumu. */
  slot: string | null
  /** Kullanıcı boyutu değiştirdi mi — değiştirmediyse temadaki görselin yerine geçerken temanın kutusu kullanılır. */
  sizeTouched?: boolean
  /** Kendi görseli olmadan temadaki bu türdeki görseli kaldır. */
  hideThemeImage?: boolean
}

export const defaultImg = (): ImgState => ({ dataUrl: '', width: '250', height: '150', alignment: 'left', slot: null })

/** Temadaki bir görselin (logo/kaşe) yeri mi — sunucu bu yuvaları "existing-" önekiyle üretir. */
export const isThemeImageSlot = (slot: string | null): boolean => slot?.startsWith('existing-') ?? false

/** Admin, temadaki bu türdeki görselin kullanıcı tarafından kaldırılmasına izin verdi mi. */
export const themeImageRemovable = (kind: AssetKind, slots: ThemeSlotOption[]): boolean =>
  slots.some((s) => s.id === `existing-${kind}` && s.removable)

/** Kaldırılmak istenen temadaki görseller (kendi görseli olan tür zaten temadakinin yerini alır). */
export function hiddenThemeImages(logo: ImgState, signature: ImgState, slots: ThemeSlotOption[]): AssetKind[] {
  return ([['logo', logo], ['signature', signature]] as const)
    .filter(([kind, s]) => s.hideThemeImage && !s.dataUrl && themeImageRemovable(kind, slots))
    .map(([kind]) => kind)
}

/** Boyut alanları temanın kutusunu mu gösteriyor (kullanıcı boyuta dokunmadı ve konum temadaki bir görsel). */
export const usesThemeBox = (state: ImgState, slot: string | null): boolean => isThemeImageSlot(slot) && !state.sizeTouched

export const ASSET_LABEL: Record<AssetKind, string> = { logo: 'Logo', signature: 'İmza / Kaşe' }

/** Sunucu kuralıyla aynı: yalnız PNG/JPEG, en fazla 500 KB. */
export const IMAGE_ACCEPT = 'image/png,image/jpeg'
export const MAX_IMAGE_BYTES = 500 * 1024

export function validateImageFile(file: File): string | null {
  if (file.type !== 'image/png' && file.type !== 'image/jpeg')
    return 'Yalnızca PNG ve JPG görseller kabul edilir.'
  if (file.size > MAX_IMAGE_BYTES)
    return 'Görsel çok büyük (en fazla 500 KB).'
  return null
}

export function readAsDataUrl(file: File): Promise<string> {
  return new Promise((resolve, reject) => {
    const reader = new FileReader()
    reader.onload = (e) => resolve(e.target!.result as string)
    reader.onerror = reject
    reader.readAsDataURL(file)
  })
}

function parseSize(s: string): number | null {
  const n = parseInt(s, 10)
  return Number.isFinite(n) && n >= 10 && n <= 1200 ? n : null
}

/** Temanın bu tür için varsayılan yuvası (katalog yoksa null). */
export function defaultSlotFor(kind: AssetKind, slots: ThemeSlotOption[]): string | null {
  return slots.find((s) => s.defaultFor === kind)?.id ?? null
}

/** Kullanıcının seçtiği ya da varsayılan yuva. */
export function effectiveSlot(kind: AssetKind, state: ImgState, slots: ThemeSlotOption[]): string | null {
  return state.slot ?? defaultSlotFor(kind, slots)
}

export function toComposeAssets(logo: ImgState, signature: ImgState, slots: ThemeSlotOption[]): ComposeAsset[] {
  const assets: ComposeAsset[] = []
  for (const [kind, s] of [['logo', logo], ['signature', signature]] as const) {
    if (!s.dataUrl) continue
    const slot = effectiveSlot(kind, s, slots)
    const themeBox = usesThemeBox(s, slot)
    assets.push({
      kind,
      slot,
      dataUrl: s.dataUrl,
      width: themeBox ? null : parseSize(s.width),
      height: themeBox ? null : parseSize(s.height),
      alignment: s.alignment,
    })
  }
  return assets
}

export function filledBankRows(rows: BankInfoItem[]): BankInfoItem[] {
  return rows.filter((b) => b.bankName.trim() || b.iban.trim())
}
