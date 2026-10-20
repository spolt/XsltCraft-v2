# Architecture — Frontend

> Kod-gerçeği. `frontend/xsltcraft-ui/` — Vite + React 19 SPA. Bağlam: `docs/ecc/context/tech-stack.md`, karar: `docs/ecc/decisions/005-react-19.md`, `002-zustand-state.md`, `004-monaco-editor.md`.

## Stack
React 19 · TypeScript 5.9 · Vite 8 · Zustand · TailwindCSS 4 · @dnd-kit (sürükle-bırak) · React Router 7 · Monaco Editor · lucide-react. SSR yok (SPA).

## Klasör yapısı (`src/`)
- **`store/`** — Zustand store'ları: `authStore`, `editorStore` (V2 grid canvas durumu), `xmlStore` (yüklü XML + ağaç), `xsltEditorStore` (dev mode ham XSLT), `aiStore` (flag + panel), `toastStore`.
- **`services/`** — API katmanı (Axios). `apiService` (base client + interceptors), alan servisleri: `templateService`, `previewService`, `ublTrService`, `xpathService`, `assetService`, `snippetService`, `userXsltService`, `profileService`, `adminService`, `adminUserService`, `aiAssistantService` (**NDJSON streaming + AbortController**).
- **`components/`** — `editor/` (V2 canvas), `xslt-editor/` (dev mode), `ai/`, `layout/`, `ui/`, route guard'ları.
- **`pages/`** — route seviyesi.

## Sayfalar / routing
Korumalı (`PrivateRoute`): `DashboardPage`, `EditorPage` (V2 grid tasarımcı), `XsltEditorPage` + `DevModePage` (Monaco ham XSLT), `TemplatesPage`/`DraftsPage`/`MyXsltTemplatesPage`, `ProfilePage`, `ThemeUsePage`, `Playground`. Admin (`AdminRoute`): `admin/{AdminThemesPage, AdminAiPage, AdminSnippetsPage, AdminUsersPage}`. Auth: `auth/{LoginPage, RegisterPage}` (`AuthLayout`). Diğer: `AboutPage`.

## V2 grid canvas editörü (`components/editor/`)
Ürünün kalbi. `Canvas` (A4, serbest X/Y mm yerleşim) → `GridBlockItem`/`BlockCard` (`ResizeHandles` ile px-perfect resize, klavye nudge 1/5mm, undo/redo, Ctrl+D), `BlockPalette` (blok kütüphanesi), `PropertyPanel` (seçili blok + XPath binding), `XmlTreeExplorer` (XML ağacından node→binding), `EditorPreviewPanel` (canlı önizleme, zoom). Sürükle-bırak `@dnd-kit`.

## Dev mode / XSLT editör (`components/xslt-editor/`)
Monaco tabanlı: `XsltEditorToolbar`, `ProblemsPanel` (XML/XSLT/UBL-TR hataları sekmeli), `XPathConsolePanel` (interaktif XPath), `SnippetManagerDialog` (Ctrl+Space autocomplete), `ShareTemplateDialog`/`SaveTemplateDialog`, `ShortcutsDialog`, `XsltEditorPreview` (zoom başlığı). Önizleme `<iframe>`'i yalnız `ScaledPreviewFrame` içinde render edilir; sandbox/başlık/tıkla→satır betiği `previewFrame.ts`'te tek kaynaktır (**postMessage origin + source kontrolü** zorunlu, sandbox çağırandan genişletilemez).

**Tam ekran editör** (`XsltEditorPage`; `hooks/useEditorMaximize`, Ctrl+Shift+F): editör bölmesi yalnız sınıf değişimiyle panel grubunu kaplayan katmana dönüşür — Monaco remount **olmaz**, sağ panel örtülür ama mount kalır (AI sohbeti korunur). Önizleme o sırada `FloatingPreviewWindow`'da (kodun üzerinde yüzen, sürüklenebilir/boyutlandırılabilir mini pencere; `hooks/useFloatingPanel` + `utils/floatingRect`) gösterilir. **Değişmez:** sayfada aynı anda en fazla bir önizleme iframe'i bulunur (yazdırma onu hedefler). Tercih `localStorage['xsltcraft-mini-preview']`.

## Hazır tema logo/imza konumları (`components/theme-assets/`, `components/admin/`)
`ThemeUsePage` + `DevModePage` ortak paneli: `ImagePanel` (yükleme, **Konum** listesi, boyut, hizalama), `BankInfoPanel`, `imageState.ts`. Görselin temaya eklenmesi **sunucuda** (`themeComposeService.composeTheme` → `POST /api/preview/compose`) — önizleme ve indirme aynı koddan geçer; istemcide XSLT'ye betik yazılmaz. Tema Kullan'da `ThemeSlotPreview`: iframe (sandbox `PREVIEW_SANDBOX`, başlık `PREVIEW_IFRAME_TITLE`; genişletilmez) + üst katmanda @dnd-kit bırakma hedefleri. Yuva kutuları `useSlotRects` ile iframe `contentDocument`'ten ölçülür; düzenleme stili `html.xc-edit` sınıfıyla açılır (`@media screen` — yazdırmada görünmez). Sürüklerken iframe `pointer-events:none`. Temadaki görselin yeri (`existing-*` yuvaları) bir `<img>`'dir: kullanıcı görseli onun yerini alır (`isThemeImageSlot`/`usesThemeBox` — boyuta dokunulmadıkça temanın kutusu); düzenleme stili img yuvalarının yerleşimini değiştirmez, gizlenen tema görselini soluk gösterir. Admin izin verdiyse (`slot.removable`) panelde "Temadaki görseli kaldır" (`hiddenThemeImages` → compose `hideThemeImages`). Admin: `ThemeUploadDecisionDialog` (yüklemede 409 → önceden gömülü blok kaldır/koru + temadaki görsellerin rolü), `ThemeSlotReport` (analiz raporu).

## AI paneli (`components/ai/`)
`AiAssistantPanel` (UBL-TR Türkçe asistan; NDJSON akışı, `AbortController` ile iptal, toast), `AiRefactorDialog` (XSLT seçimi → dönüşüm → diff → onayla). **Kullanıcı onayı olmadan otomatik insert yok**; insert öncesi `/api/preview/validate-xslt`. UI'da `aiEnabled` (`useAiStore(s => s.enabled === true)`) kontrolü unutulmamalı.

## Konvansiyonlar
- State: özellik-bazlı küçük Zustand store'ları; selector ile hedefli abonelik (gereksiz re-render yok).
- API: `apiService` üzerinden; streaming uçları (`aiAssistantService`) `BodyWriter`/fetch reader pattern'i ile.
- Pre-commit hook: `console.log` ve hardcoded `localhost` URL (frontend) commit'lenmez — config'i `apiService`/env'de tut.
