import type {
  EasyEditAnalyzeResponse, EasyEditImage, EasyEditPreviousFreeImage, EasyEditPreviousImage,
} from '../../services/easyEditService'
import type {
  AssetKind, ComposeDocumentBankEdit, ComposeFreeImage, ComposeStaticImage,
} from '../../services/themeComposeService'
import type { Alignment } from '../../services/previewService'
import { filterSlotIds, isValidSlotId, safeImageSrc, safeSlotId } from '../../services/easyEditGuards'
import { defaultImg, type ImgState } from '../theme-assets/imageState'
import { defaultFixedNotes, type FixedNotesState } from '../theme-assets/fixedNoteState'
import { defaultFreeImg, isFreeSlot, MAX_FREE_IMAGES, type FreeImgState } from '../theme-assets/freeImageState'
import type { ThemeDraftFields } from '../../store/themeDraftStore'
import { initialDocBankRows, type DocBankItem } from './docBank'

/** Yüklenen/şablondan açılan belgenin compose kaynağı. */
export interface DocComposeSource {
  xslt: string | undefined
  conditionalSlots: string[]
  staticImages: ComposeStaticImage[]
  freeImages: ComposeFreeImage[]
  documentBankEdits: ComposeDocumentBankEdit[]
}

/**
 * Doc kaynağının compose sözleşmesi (compose durumsuzdur):
 * - `xslt` her çağrıda son analizin işaretli metnidir (base). Önceki compose çıktısı ya da ham dosya gönderilmez.
 * - `staticImages` her çağrıda kümülatif tam listedir; `index` = analizdeki `images[].index`.
 * - `freeImages` önceki bloktan gelen serbest görsel dahil tam listedir (analiz onu da metinden siler).
 * - `documentBankEdits` kümülatif tam listedir; `index` = analizdeki `documentBank.rows[].index`.
 * - `assets` (çalışma alanı kurar) önceki bloktan gelen görseller dahil tam listedir: analiz bu görselleri metinden
 *   sildiği için gönderilmezlerse kaybolur. İlk değerleri `initialDocFields` doldurur.
 * - Kaydedilen içerik son compose çıktısıdır (`includeXslt: true`).
 * - Kayıt sonrası yeniden analiz yapılmaz: kayıt Şablonlarım'a yönlendirir; belge yeniden açıldığında yeni analizin
 *   `xslt`'si base olur ve `staticImages` boş listeyle başlar.
 */
export function docComposeSource(
  analysis: EasyEditAnalyzeResponse | null,
  staticImages: readonly ComposeStaticImage[],
  freeImages: readonly ComposeFreeImage[] = [],
  documentBankEdits: readonly ComposeDocumentBankEdit[] = [],
  vacated: ReadonlySet<string> = new Set(),
): DocComposeSource {
  return {
    xslt: analysis?.xslt ?? undefined,
    conditionalSlots: filterSlotIds(analysis?.conditionalSlots),
    staticImages: staticImages.map((s) => ({ ...s, slot: safeSlotId(s.slot) })),
    freeImages: freeImages.filter((f) => isFreeSlot(f.slot, vacated)),
    documentBankEdits: [...documentBankEdits],
  }
}

/**
 * Tek soru modalı kendiliğinden açılır mı: yalnız yüklenen dosyada ve işaretsiz (önerilen rollü) görsel ya da önceki
 * kayıt bulunduysa. Kayıtlı şablon açılışında açılmaz.
 */
export function shouldAskImageRoles(analysis: EasyEditAnalyzeResponse | null, origin: 'upload' | 'template'): boolean {
  if (!analysis || origin !== 'upload') return false
  return (analysis.images ?? []).some((i) => i.source === 'suggested') || analysis.previous != null
}

/** Kolay düzenle'de açılabilen analiz kararları. */
export const isEditableVerdict = (a: EasyEditAnalyzeResponse | null | undefined) =>
  !!a && (a.verdict === 'suitable' || a.verdict === 'partial') && !!a.xslt

const ALIGNMENTS: readonly Alignment[] = ['left', 'center', 'right']
const safeAlignment = (value: unknown, fallback: Alignment): Alignment =>
  ALIGNMENTS.includes(value as Alignment) ? (value as Alignment) : fallback

function imgFromPrevious(p: EasyEditPreviousImage): ImgState | null {
  const dataUrl = safeImageSrc(p.dataUrl)
  if (!p.valid || !dataUrl) return null
  const base = defaultImg()
  return {
    ...base,
    dataUrl,
    width: p.width != null ? String(p.width) : base.width,
    height: p.height != null ? String(p.height) : base.height,
    alignment: safeAlignment(p.alignment, base.alignment),
    slot: safeSlotId(p.slot),
    sizeTouched: p.width != null || p.height != null,
    origin: 'previous',
  }
}

/** Türün düzenlenebilir (taşınabilir) ilk dosya görseli; aynı türdeki diğerleri yerinde kalır. */
export function extractedImageFor(analysis: EasyEditAnalyzeResponse, kind: AssetKind): EasyEditImage | null {
  return (analysis.images ?? []).find((i) => i.role === kind && i.movable && safeImageSrc(i.previewDataUrl) !== null) ?? null
}

/** Dosyanın kendi görselinin dokunulmamış hâli: hiçbir şey gönderilmez, görsel yerinde kalır. */
function imgFromExtracted(image: EasyEditImage): ImgState {
  return {
    ...defaultImg(),
    dataUrl: safeImageSrc(image.previewDataUrl) ?? '',
    width: '',
    height: '',
    origin: 'extracted',
    index: image.index,
    homeSlot: safeSlotId(image.slotId),
  }
}

/** Dosyadaki görselin (`index`) dokunulmamış hâli; analizde yoksa null. */
export function restoreExtracted(analysis: EasyEditAnalyzeResponse | null, index: number | undefined): ImgState | null {
  if (!analysis || index == null) return null
  const image = (analysis.images ?? []).find((i) => i.index === index && i.movable)
  return image && safeImageSrc(image.previewDataUrl) ? imgFromExtracted(image) : null
}

/** Her tür için aktarılan önceki görsel: türdeki ilk geçerli görsel. */
function pickPreviousImages(analysis: EasyEditAnalyzeResponse) {
  const picked = new Map<AssetKind, { source: EasyEditPreviousImage; img: ImgState }>()
  for (const p of analysis.previous?.images ?? []) {
    if (p.kind !== 'logo' && p.kind !== 'signature') continue
    if (picked.has(p.kind)) continue
    const img = imgFromPrevious(p)
    if (img) picked.set(p.kind, { source: p, img })
  }
  return picked
}

/** Türün ilk görsel durumu: önceki kayıttaki görsel, yoksa dosyanın kendi görseli (hazır yüklü), yoksa boş. */
function initialImage(analysis: EasyEditAnalyzeResponse, kind: AssetKind): ImgState {
  const previous = pickPreviousImages(analysis).get(kind)?.img
  if (analysis.previous?.hide?.includes(kind)) return { ...(previous ?? defaultImg()), hideThemeImage: true }
  if (previous) return previous
  const extracted = extractedImageFor(analysis, kind)
  return extracted ? imgFromExtracted(extracted) : defaultImg()
}

/**
 * Rol değişiminden sonraki yeni analizle görsel durumu: kullanıcının yüklediği görsel korunur (dosyadaki görselin
 * yerine geçtiyse yeni analizdeki karşılığı saklanır); diğerleri yeni analizden baştan kurulur (statik düzenlemeler sıfırlanır).
 */
export function imageAfterReanalysis(analysis: EasyEditAnalyzeResponse, kind: AssetKind, current: ImgState): ImgState {
  if (current.origin === 'upload' && current.dataUrl) {
    const extracted = extractedImageFor(analysis, kind)
    return { ...current, index: extracted?.index, homeSlot: extracted ? safeSlotId(extracted.slotId) : undefined }
  }
  return initialImage(analysis, kind)
}

/**
 * Önceki kayıttaki serbest görsel. Boşalmış temadaki görsel yerine (`existing-*`) konmuşsa o kutu yeniden analizde
 * kalmaz: görsel aktarılır, konumu yeniden seçilir (`slot` null).
 */
function freeImgFromPrevious(p: EasyEditPreviousFreeImage): FreeImgState | null {
  const dataUrl = safeImageSrc(p.dataUrl)
  const slot = safeSlotId(p.slot)
  if (!p.valid || !dataUrl || !isValidSlotId(slot)) return null
  const base = defaultFreeImg()
  return {
    ...base,
    dataUrl,
    slot: isFreeSlot(slot) ? slot : null,
    width: p.width != null ? String(p.width) : base.width,
    height: p.height != null ? String(p.height) : base.height,
    alignment: safeAlignment(p.alignment, base.alignment),
    sizeTouched: p.width != null || p.height != null,
    origin: 'previous',
  }
}

/** Önceki kayıttan aktarılan serbest görseller (geçerli olanlardan ilk `MAX_FREE_IMAGES`). */
function pickPreviousFreeImages(analysis: EasyEditAnalyzeResponse) {
  const picked: { source: EasyEditPreviousFreeImage; img: FreeImgState }[] = []
  for (const p of analysis.previous?.freeImages ?? []) {
    if (picked.length >= MAX_FREE_IMAGES) break
    const img = freeImgFromPrevious(p)
    if (img) picked.push({ source: p, img })
  }
  return picked
}

/**
 * Önceki kayıtta Kolay düzenle'nin aktaramadığı görsel var mı (türü tanınmayan, geçersiz ya da aynı türdeki ikinci
 * görsel; geçersiz serbest görsel). Analiz bu görselleri metinden sildiği için kaydedilirse kaybolurlar.
 */
export function hasSkippedPreviousImages(analysis: EasyEditAnalyzeResponse | null | undefined): boolean {
  if (!analysis) return false
  const images = analysis.previous?.images ?? []
  const picked = new Set(Array.from(pickPreviousImages(analysis).values(), (v) => v.source))
  if (images.some((p) => !picked.has(p))) return true
  const free = analysis.previous?.freeImages ?? []
  const pickedFree = new Set(pickPreviousFreeImages(analysis).map((v) => v.source))
  return free.some((p) => !pickedFree.has(p))
}

export const SKIPPED_PREVIOUS_MESSAGE =
  "Bu dosyada Kolay düzenle'nin aktaramadığı önceki görseller var; değişiklikleri kaydetmek için XSLT Editör'ü kullanın."

function initialNotes(analysis: EasyEditAnalyzeResponse): FixedNotesState {
  const notes = defaultFixedNotes()
  for (const n of analysis.notes ?? []) {
    if (n.position !== 'before' && n.position !== 'after') continue
    const { position, ...rest } = n
    notes[position] = { ...notes[position], ...rest }
  }
  return notes
}

/**
 * Panelin banka satırları: belge IBAN'ı bulunduysa belge satırları (`docIndex`'li; `canAdd` ise önceki blok satırları
 * eklenir), yoksa önceki blok satırları. Rol değişimiyle gelen yeni analizde de baştan kurulur.
 */
export function initialBanks(analysis: EasyEditAnalyzeResponse): DocBankItem[] {
  if (analysis.documentBank?.found) return initialDocBankRows(analysis).rows
  const rows = (analysis.previous?.bank ?? []).map((b) => ({ bankName: b.bankName ?? '', iban: b.iban ?? '' }))
  return rows.length > 0 ? rows : [{ bankName: '', iban: '' }]
}

/** Analizden taslağın ilk değerleri: önceki görseller, banka listesi, sabit notlar, konum kataloğu. */
export function initialDocFields(
  analysis: EasyEditAnalyzeResponse,
  name: string,
): Pick<ThemeDraftFields, 'meta' | 'logo' | 'signature' | 'freeImages' | 'bankInfo' | 'fixedNotes' | 'fixedNotesSupported' | 'analysis'> {
  return {
    meta: {
      name,
      slots: (analysis.slots ?? []).filter((s) => isValidSlotId(s.id)),
      documentType: analysis.documentType === 'Despatch' ? 'Despatch' : 'Invoice',
    },
    logo: initialImage(analysis, 'logo'),
    signature: initialImage(analysis, 'signature'),
    freeImages: pickPreviousFreeImages(analysis).map((v) => v.img),
    bankInfo: initialBanks(analysis),
    fixedNotes: initialNotes(analysis),
    fixedNotesSupported: analysis.notesSupported,
    analysis,
  }
}
