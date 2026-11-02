import { useCallback, type SetStateAction } from 'react'
import { create } from 'zustand'
import type { BankInfoItem } from '../services/previewService'
import type { ThemeSlotOption } from '../services/templateService'
import { defaultImg, type ImgState } from '../components/theme-assets/imageState'
import { defaultFixedNotes, type FixedNotesState } from '../components/theme-assets/fixedNoteState'

export interface ThemeDraftMeta {
  name: string
  slots: ThemeSlotOption[]
  documentType: 'Invoice' | 'Despatch'
}

export interface ThemeDraftXml {
  content: string
  fileName: string
}

export interface ThemeDraftSaved {
  id: string
  updatedAt: string
  /** Kullanıcının kayıtta verdiği ad; sonraki kayıt dialogunun önerisi. */
  name?: string
}

/** `useDraftField` ile okunup yazılan alanlar; XSLT alanları yalnız aşağıdaki eylemlerle değişir. */
export interface ThemeDraftFields {
  meta: ThemeDraftMeta | null
  logo: ImgState
  signature: ImgState
  bankInfo: BankInfoItem[]
  fixedNotes: FixedNotesState
  /** null → belge türüne göre örnek XML. */
  xml: ThemeDraftXml | null
  /** Temada Notlar alanı var mı; null → geçerli metinden henüz yanıt alınmadı. */
  fixedNotesSupported: boolean | null
}

interface ThemeDraftData extends ThemeDraftFields {
  themeId: string | null
  notesTouched: boolean
  workingXslt: string | null
  xsltEdited: boolean
  composedKey: string | null
  saved: ThemeDraftSaved | null
  baselineXslt: string | null
  /** Süren kayıt (`runDraftSave`); reddedilmez, bitince temizlenir. */
  saveInFlight: Promise<void> | null
}

interface ThemeDraftState extends ThemeDraftData {
  /** Her bağlama ve silmede artar: aynı temanın yeni açılışı, eski açılışın geç yazımlarından ayrılır. */
  session: number
  /** Taslağı boş haliyle temaya bağlar (yalnız ThemeDraftScope). */
  bind: (themeId: string) => void
  /** Taslağı siler (yalnız ThemeDraftScope). */
  reset: () => void
  /** Sunucudan ilk metin: çalışma metni, baseline ve anahtar birlikte yazılır; metin zaten varsa yok sayılır. */
  loadXslt: (xslt: string, composedKey: string, session: number) => void
  /** Editörde elle değişiklik. */
  setManualXslt: (xslt: string) => void
  /** AI tüm belge yazar, görsel/banka/not bloğunu düşürebilir: anahtar sıfırlanır, compose varlıkları yeniden işler. */
  applyAiXslt: (xslt: string) => void
  /** Compose sonucu: metin ve anahtar birlikte yazılır; `xsltEdited` değişmez. */
  setComposedXslt: (xslt: string, composedKey: string, session: number) => void
  /** Kayıt sonucu; kayıt sürerken taslak silindiyse yok sayılır. */
  markSaved: (session: number, saved: ThemeDraftSaved, baselineXslt: string | null) => void
}

const emptyDraft = (): ThemeDraftData => ({
  themeId: null,
  meta: null,
  logo: defaultImg(),
  signature: defaultImg(),
  bankInfo: [{ bankName: '', iban: '' }],
  fixedNotes: defaultFixedNotes(),
  notesTouched: false,
  xml: null,
  fixedNotesSupported: null,
  workingXslt: null,
  xsltEdited: false,
  composedKey: null,
  saved: null,
  baselineXslt: null,
  saveInFlight: null,
})

/** Tema Kullan ↔ Geliştirici Modu ortak taslağı: bellek içi, ömrü ThemeDraftScope kadar. */
export const useThemeDraftStore = create<ThemeDraftState>((set) => ({
  ...emptyDraft(),
  session: 0,

  bind: (themeId) => set((s) => ({ ...emptyDraft(), themeId, session: s.session + 1 })),
  reset: () => set((s) => ({ ...emptyDraft(), session: s.session + 1 })),

  // İstek başladıktan sonra taslak silindiyse geç gelen sonuç yok sayılır.
  loadXslt: (xslt, composedKey, session) => set((s) => (
    s.session === session && s.workingXslt === null ? { workingXslt: xslt, baselineXslt: xslt, composedKey } : s
  )),
  setManualXslt: (xslt) => set((s) => (s.themeId ? { workingXslt: xslt, xsltEdited: true } : s)),
  applyAiXslt: (xslt) => set((s) => (s.themeId ? { workingXslt: xslt, xsltEdited: true, composedKey: null } : s)),
  setComposedXslt: (xslt, composedKey, session) => set((s) => (s.session === session ? { workingXslt: xslt, composedKey } : s)),
  markSaved: (session, saved, baselineXslt) => set((s) => (s.session === session ? { saved, baselineXslt } : s)),
}))

/**
 * Tema Kullan ve Geliştirici Modu kayıtlarını sıraya sokar: `saved` süren kayıt bitmeden okunmaz, ikinci kayıt
 * ikinci şablon oluşturmaz. Beklerken taslak silindiyse iş çalışmaz. İşin hatası çağırana gider; hatalı kayıttan
 * sonra bekleyen, `saved` hâlâ boş olduğundan yeni şablon oluşturur.
 */
export async function runDraftSave(task: (saved: ThemeDraftSaved | null) => Promise<void>): Promise<void> {
  const store = useThemeDraftStore
  const { session } = store.getState()
  // Birden çok bekleyen uyanırsa ilki hattı aynı tick'te alır, diğerleri yeniden bekler.
  while (store.getState().session === session && store.getState().saveInFlight) {
    await store.getState().saveInFlight
  }
  if (store.getState().session !== session) return

  let finish = () => {}
  const inFlight = new Promise<void>((resolve) => { finish = resolve })
  store.setState({ saveInFlight: inFlight })
  try {
    await task(store.getState().saved)
  } finally {
    finish()
    store.setState((s) => (s.saveInFlight === inFlight ? { saveInFlight: null } : s))
  }
}

/**
 * Taslak alanı için `useState` benzeri erişim. Setter oluşturulduğu oturuma bağlıdır: yarım kalan bir yükleme
 * sonucu silinmiş ya da yeniden açılmış taslağa yazılmaz. `fixedNotes` yazımı `notesTouched`'ı açar.
 */
export function useDraftField<K extends keyof ThemeDraftFields>(key: K) {
  const value = useThemeDraftStore((s) => s[key] as ThemeDraftFields[K])
  const session = useThemeDraftStore((s) => s.session)

  const setValue = useCallback((action: SetStateAction<ThemeDraftFields[K]>) => {
    useThemeDraftStore.setState((s) => {
      if (s.session !== session) return s
      const next = typeof action === 'function'
        ? (action as (prev: ThemeDraftFields[K]) => ThemeDraftFields[K])(s[key])
        : action
      const patch: Partial<ThemeDraftData> = { [key]: next }
      return key === 'fixedNotes' ? { ...patch, notesTouched: true } : patch
    })
  }, [key, session])

  return [value, setValue] as const
}
