import { create } from 'zustand'
import { persist } from 'zustand/middleware'

export type UiThemeId = 'lacivert' | 'mor' | 'turuncu'
export type EditorThemeId = 'dark' | 'light' | 'contrast'
export type EditorLayoutId = 'side' | 'half' | 'mini' | 'editorOnly'
/** XSLT editör sağ paneli: Önizleme ve AI ayrı sütunlarda | tek sütunda sekmeli. */
export type XsltRightPanelMode = 'split' | 'tabs'

/** Sağ panel boyutları (dış panel grubunun yüzdesi). */
export const XSLT_AI_SIZE_DEFAULT = 30
export const XSLT_TABS_SIZE_DEFAULT = 40
const XSLT_PANEL_SIZE_MIN = 10
const XSLT_PANEL_SIZE_MAX = 55

export const EDITOR_FONT_MIN = 11
export const EDITOR_FONT_MAX = 18

/** Saf görünüm tercihleri (arayüz rengi, menü, editör görünümü). İşlev/veri tutmaz. */
interface UiPrefsState {
  uiTheme: UiThemeId
  /** Uygulama açılışında ana menü daraltılmış mı başlasın (kalıcı). */
  navCollapsedOnStart: boolean
  /** Ana menünün o anki durumu — kalıcı DEĞİL; açılışta navCollapsedOnStart'tan türetilir. */
  navCollapsed: boolean
  editorTheme: EditorThemeId
  editorDefaultLayout: EditorLayoutId
  /** 11–18 arası (clamp'lenir). */
  editorFontSize: number
  editorWordWrap: boolean
  /** XSLT editör sol menüsü daraltılmış mı. */
  xsltMenuCollapsed: boolean
  /** XSLT editör sağ panel modu (Ayarlar). */
  xsltRightPanelMode: XsltRightPanelMode
  /** "Yan yana" modunda AI sütununun genişliği (%; kullanıcı sürükleyince güncellenir). */
  xsltAiPanelSize: number
  /** "Tek panel (sekmeli)" modunda sağ sütunun genişliği (%). */
  xsltTabsPanelSize: number

  setUiTheme: (theme: UiThemeId) => void
  setNavCollapsedOnStart: (value: boolean) => void
  setNavCollapsed: (value: boolean) => void
  toggleNavCollapsed: () => void
  setEditorTheme: (theme: EditorThemeId) => void
  setEditorDefaultLayout: (layout: EditorLayoutId) => void
  setEditorFontSize: (size: number) => void
  setEditorWordWrap: (value: boolean) => void
  setXsltMenuCollapsed: (value: boolean) => void
  toggleXsltMenuCollapsed: () => void
  setXsltRightPanelMode: (mode: XsltRightPanelMode) => void
  setXsltAiPanelSize: (size: number) => void
  setXsltTabsPanelSize: (size: number) => void
}

type PersistedPrefs = Omit<
  UiPrefsState,
  | 'navCollapsed'
  | 'setUiTheme'
  | 'setNavCollapsedOnStart'
  | 'setNavCollapsed'
  | 'toggleNavCollapsed'
  | 'setEditorTheme'
  | 'setEditorDefaultLayout'
  | 'setEditorFontSize'
  | 'setEditorWordWrap'
  | 'setXsltMenuCollapsed'
  | 'toggleXsltMenuCollapsed'
  | 'setXsltRightPanelMode'
  | 'setXsltAiPanelSize'
  | 'setXsltTabsPanelSize'
>

const clampPanelSize = (n: unknown, fallback: number) =>
  typeof n === 'number' && Number.isFinite(n)
    ? Math.min(XSLT_PANEL_SIZE_MAX, Math.max(XSLT_PANEL_SIZE_MIN, Math.round(n * 10) / 10))
    : fallback

const clampFont = (n: number) =>
  Number.isFinite(n) ? Math.min(EDITOR_FONT_MAX, Math.max(EDITOR_FONT_MIN, Math.round(n))) : 13

// 1.x'teki eski menü anahtarı artık kullanılmıyor.
try {
  localStorage.removeItem('sidebarCollapsed')
} catch {
  /* depolama erişilemez — yoksay */
}

export const useUiPrefsStore = create<UiPrefsState>()(
  persist(
    (set) => ({
      uiTheme: 'lacivert',
      navCollapsedOnStart: true,
      navCollapsed: true,
      editorTheme: 'dark',
      editorDefaultLayout: 'side',
      editorFontSize: 13,
      editorWordWrap: false,
      xsltMenuCollapsed: false,
      xsltRightPanelMode: 'split',
      xsltAiPanelSize: XSLT_AI_SIZE_DEFAULT,
      xsltTabsPanelSize: XSLT_TABS_SIZE_DEFAULT,

      setUiTheme: (uiTheme) => set({ uiTheme }),
      setNavCollapsedOnStart: (navCollapsedOnStart) => set({ navCollapsedOnStart }),
      setNavCollapsed: (navCollapsed) => set({ navCollapsed }),
      toggleNavCollapsed: () => set((s) => ({ navCollapsed: !s.navCollapsed })),
      setEditorTheme: (editorTheme) => set({ editorTheme }),
      setEditorDefaultLayout: (editorDefaultLayout) => set({ editorDefaultLayout }),
      setEditorFontSize: (size) => set({ editorFontSize: clampFont(size) }),
      setEditorWordWrap: (editorWordWrap) => set({ editorWordWrap }),
      setXsltMenuCollapsed: (xsltMenuCollapsed) => set({ xsltMenuCollapsed }),
      toggleXsltMenuCollapsed: () => set((s) => ({ xsltMenuCollapsed: !s.xsltMenuCollapsed })),
      setXsltRightPanelMode: (xsltRightPanelMode) => set({ xsltRightPanelMode }),
      setXsltAiPanelSize: (size) => set({ xsltAiPanelSize: clampPanelSize(size, XSLT_AI_SIZE_DEFAULT) }),
      setXsltTabsPanelSize: (size) => set({ xsltTabsPanelSize: clampPanelSize(size, XSLT_TABS_SIZE_DEFAULT) }),
    }),
    {
      name: 'xsltcraft-ui-prefs',
      version: 1,
      partialize: (s): PersistedPrefs => ({
        uiTheme: s.uiTheme,
        navCollapsedOnStart: s.navCollapsedOnStart,
        editorTheme: s.editorTheme,
        editorDefaultLayout: s.editorDefaultLayout,
        editorFontSize: s.editorFontSize,
        editorWordWrap: s.editorWordWrap,
        xsltMenuCollapsed: s.xsltMenuCollapsed,
        xsltRightPanelMode: s.xsltRightPanelMode,
        xsltAiPanelSize: s.xsltAiPanelSize,
        xsltTabsPanelSize: s.xsltTabsPanelSize,
      }),
      merge: (persisted, current) => {
        const p = (persisted ?? {}) as Partial<PersistedPrefs>
        const merged = { ...current, ...p }
        return {
          ...merged,
          editorFontSize: clampFont(merged.editorFontSize),
          // v1 kayıtlarında bu alanlar yoktur → varsayılan; bozuk değerler de aralığa çekilir.
          xsltRightPanelMode: merged.xsltRightPanelMode === 'tabs' ? 'tabs' : 'split',
          xsltAiPanelSize: clampPanelSize(merged.xsltAiPanelSize, XSLT_AI_SIZE_DEFAULT),
          xsltTabsPanelSize: clampPanelSize(merged.xsltTabsPanelSize, XSLT_TABS_SIZE_DEFAULT),
          navCollapsed: merged.navCollapsedOnStart,
        }
      },
    },
  ),
)
