# Plan — XsltCraft 2.0 UI yeniden tasarımı

> Durum: **onaylandı** · Tarih: 2026-10-25 · Kaynak: `planner` agent (kod-gerçeğine karşı doğrulandı)
> Tasarım referansı (tek doğru kaynak, HTML mockup): `C:\Users\Semih\AppData\Local\Temp\claude\c--Users-Semih-source-repos-XsltCraft\48b63976-8321-4152-bea3-56401888eb37\scratchpad\xc-ui\project\`
> (`Minimal.dc.html` ve `Studio.dc.html` elenmiş konseptler — uygulanmaz.)

Yalnız görsel/yerleşim değişikliği — servis/store/API/backend davranışı değişmez. Yollar `frontend/xsltcraft-ui/` köküne göredir.

## Sahip kararları (2026-10-25)
1. **Tasarım Alanı sağ paneli:** bu turda **Veri | Konum** iki sekme. Stil ayrımı sonraya (T3b).
2. **Arkasında işlev olmayan kontroller gösterilmez** (Tasarım Alanı'nda Doğrula / UBL-TR sayacı / çıktı boyutu / zoom ± / ızgara-sınır toggle / belge türü değiştirici / otomatik kayıt; XSLT editör ayarlarında Otomatik önizleme / Kaydederken biçimlendir / Problemleri otomatik aç; tema kategori çipleri; "Şifremi unuttum"). Belge türü salt-okunur gösterilir. Uygulanan saf görünüm tercihleri: editör teması, yazı boyutu, satır kaydırma, varsayılan düzen, arayüz rengi, menü daraltılmış başlasın. Durum çubuklarında yalnız mevcut veri.
3. **Fontlar:** `@fontsource/ibm-plex-sans` (400/500/600/700) + `@fontsource/ibm-plex-mono` (400/500), `main.tsx`'te import (dış istek yok).

---

## 1. Envanter (doğrulandı)

- **Kabuk:** `src/components/layout/AppLayout.tsx` (Navbar + Sidebar; `localStorage['sidebarCollapsed']`). `Navbar.tsx`: hamburger, logo, Admin rozeti, Free→"Pro'ya Geç"/Pro rozet, profil linki, **Çıkış** (`api.post('/api/auth/logout')` + `logout()`). `Sidebar.tsx`: w-14/w-56, alt menüler (Tema Kütüphanesi, Yeni Şablon Fatura/İrsaliye, Admin), altta `v{__APP_VERSION__}`. `AuthLayout.tsx`: 4 tema (A–D), yalnız `D` kullanılıyor.
- **Router (`src/App.tsx`):** AppLayout altında `/dashboard, /templates, /drafts, /my-xslt-templates, /profile, /about, /pricing, /admin/*`. Kendi düzeni olanlar: `/editor/new|:id` (EditorPage), `/theme-use/:id`, `/dev-mode/:id`, `/xslt-editor[/:id]`.
- **Sayfalar:** EditorPage 624, XsltEditorPage 1312 (yerel `EditorSidebar`; ThemeUsePage ve DevModePage'de kopyası), DraftsPage 497 / MyXsltTemplatesPage 576 (ikisi de `components/storage/*`), TemplatesPage 248, ThemeUsePage 387, DevModePage 787, Profile 336, Pricing 106, About 77, Dashboard 68, `pages/admin/*` (Themes 461, Snippets 362, Users 930 – detay paneli+modallar mevcut, Usage 246, Ai 357, AiFeedback 234).
- **Tasarım Alanı:** palet `BlockPalette` (180px, `useDraggable` id `palette-{id}`, `data` bağlı). `XmlTreeExplorer` şu an **sağ** `PropertyPanel` içinde sekme; sekme durumu `xmlStore.rightPanelTab` (`startXPathSelection` → `'xml'`, `applyXPathSelection` → `'properties'`). PropertyPanel 2039 satır; konum `GridPositionControls` (mm). Sayfa terk koruması `EditorPage.handleNavigate` (window.confirm).
- **XSLT Editör:** XPath konsolu `components/xslt-editor/XPathConsolePanel.tsx` — XsltEditorPage (state 213–214, `handleEvaluateXPath`/`handleLocateXmlLine` 730–750, buton 969–979, panel 1262–1269), DevModePage (142, 344–347, 557, 757), Monaco bağlam eylemi `xslt-evaluate-xpath` + `onEvaluateXPath` prop'u (`src/components/Xslteditor.tsx` 35, 595–607), servis `services/xpathService.ts`. `xpathXmlIndex.ts` otomatik tamamlama — **korunur**. `showProblems` zaten `useState(false)`; `handleValidateUblTr` paneli açar. Tam ekran/mini önizleme `hooks/useEditorMaximize.ts` (Ctrl+Shift+F), mini tercih `miniPreviewPrefs.ts` (`localStorage['xsltcraft-mini-preview']`). Sağ panel sekmeleri Önizleme | XML Kaynak | AI (+ `aiSplit`). `react-resizable-panels` v4 (`useGroupRef`, `setLayout`).
- **Monaco teması:** `theme="vs-dark"` sabit — `Xslteditor.tsx:769` (fontSize 14), `XsltEditorPage.tsx:1140`, `DevModePage.tsx:663`, `Xmleditor.tsx`, `ai/AiApplyDialog.tsx`, `ai/AiRefactorDialog.tsx`, `ai/MarkdownMessage.tsx`.
- **Tailwind 4:** `src/index.css` yalnız `@import "tailwindcss"`; **`@theme` yok**. `:root` altında `--color-brand-*`, `--color-surface-*`, eski `--xc-*`, `.xc-btn/.xc-input`, FlexLayout override'ları. Sayfalar çoğunlukla sabit `blue-*/gray-*` + inline style.
- **Tercih saklama:** yalnız `sidebarCollapsed` ve mini önizleme. Kalıcı Zustand deseni `authStore` (`persist`). Arayüz/editör tercih altyapısı **yok**.
- **Veri (yeni endpoint yok):** `getUserXsltTemplates()` (updatedAt), `getMyTemplates()` (documentType, blockTree, hasStoredXslt), `entitlementStore` (usage + limitler), `getUsageReport/getUsageDaily(from,to)` (5 metrik; önceki dönem = aynı endpoint kaydırılmış aralık). `FreeTheme`'de kategori/XSLT sürümü alanı yok.
- **E2E (`e2e/*.spec.ts`) dayanakları:** `input[autocomplete="username"]`, "Giriş Yap", h1 "XSLT Editör", "XML dosyası yüklendi", `input[type=file][accept=".xsl,.xslt"]` ve `[accept=".xml"]`, "Tam ekran editör", "Mini önizlemeyi göster", "Önizlemeyi mini pencerede göster", 'XML Kaynak' butonu, FloatingPreviewWindow aria'ları. **Aynı accept'li ikinci file input Playwright strict mode'u kırar.**

## 2. Tema altyapısı

- **Token katmanı (W0):** yeni `src/styles/theme.css` (`index.css`'ten import). `:root, [data-ui-theme="lacivert"]`, `[data-ui-theme="mor"]`, `[data-ui-theme="turuncu"]` altında `--ui-*`: bar, bar-fg, bar-muted, bar-border, bar-line, search, btn-line, chip, chip-fg, avatar, avatar-fg, accent, soft, soft-line, ink, bg, side, card, line, hover, grid, panel, panel-fg, panel-muted, panel-line, icon-fg. Değer önceliği: kabuk + yüzey `Dashboard.dc.html` THEMES; editöre özgü chip/avatar/barLine/grid `Main.dc.html`; auth `Login.dc.html`.
- `@theme inline { --color-ui-bar: var(--ui-bar); … }` → `bg-ui-card`, `text-ui-ink`, `border-ui-line`, `bg-ui-accent` çalışma anında temaya göre değişir. **`ui-` öneki zorunlu** (`hover/line/card` Tailwind varyantlarıyla çakışır).
- Sabitler `@theme`: `--color-fn-{blue|orange|green|gray|purple|violet|slate}` + `-tint/-ink/-line` (C nesnesi); `--color-tx` #1B2433, `--color-tx-2` #2E3B50, `--color-tx-muted` #46566E, `--color-tx-subtle` #5B6B82, `--color-field-line` #D9E0EA; `--color-metric-1..5` (#2a78d6, #eb6834, #1baf7a, #eda100, #e87ba4); `--font-sans` IBM Plex Sans, `--font-mono` IBM Plex Mono.
- Editör paleti `[data-editor-theme="dark|light|contrast"]` → `--ed-*` (bg, panel, border, seg-bg, text, muted, warn, active-bg; `XsltEditor.dc.html` ED).
- **Eski tokenlar köprülenir:** `--color-brand-primary: var(--ui-accent)`, `--color-surface-page: var(--ui-bg)`, `--xc-accent: var(--ui-accent)` vb. → dokunulmayan diyaloglar da tema alır.
- `.xc-lift` (hover `translateY(-3px)` + gölge, `prefers-reduced-motion` kapalı). İkon kutusu dolumu: `group-hover:bg-(--fn) group-hover:text-white` + kartta `style={{'--fn':'var(--color-fn-purple)'}}`.
- **Tercihler:** yeni `src/store/uiPrefsStore.ts` (Zustand `persist`, ad `xsltcraft-ui-prefs`, version 1):
  ```ts
  uiTheme: 'lacivert'|'mor'|'turuncu' = 'lacivert'
  navCollapsedOnStart: boolean = true
  navCollapsed: boolean          // partialize ile persist DIŞI; başlangıç = navCollapsedOnStart
  editorTheme: 'dark'|'light'|'contrast' = 'dark'
  editorDefaultLayout: 'side'|'half'|'mini'|'editorOnly' = 'side'
  editorFontSize: number = 13    // 11–18
  editorWordWrap: boolean = false
  xsltMenuCollapsed: boolean = false
  ```
- Uygulama: `src/theme/useApplyUiTheme.ts` (App kökünde `document.documentElement.dataset.uiTheme`); FOUC için `index.html` inline script (localStorage JSON'dan `data-ui-theme`) + `lang="tr"`. `src/theme/uiThemes.ts` (id, ad, swatch). `src/theme/monacoThemes.ts`: `defineMonacoThemes(monaco)` → `xc-dark` (bg #1B1E27), `xc-light`, `xc-contrast` (hc-black); `monacoThemeId(editorTheme)`. Eski `sidebarCollapsed` anahtarı kalkar.
- **Ortak ilkeller (W0, `src/components/ui/`):** `IconTile`, `SegmentedTabs` (role=tablist/tab, aria-selected), `Switch` (role=switch, aria-checked), `PageHeader`, `Card`, `ThemeSwatches` (aria-pressed).

## 3. Dalgalar

| Dalga | Görev | Bağımlılık | Paralel |
|---|---|---|---|
| W0 | T0 Tema altyapısı + ilkeller | — | önce |
| W1 | T1.1 AppShell · T1.2 WorkspaceHeader + AdminLayout | W0 | T1.1‖T1.2 |
| W2 | T2.1 Dashboard · T2.2 Tema Kütüphanesi · T2.3 Taslaklarım/Şablonlarım · T2.4 Profil/Planlar/Hakkında | T1.1 | hepsi ‖ |
| W2' | T2.5 Auth | yalnız W0 | W1 ile ‖ |
| W3 | T3 Tasarım Alanı | W0 + T1.2 | W2/W4/W5 ile ‖ |
| W4 | T4 XSLT Editör + DevMode (XPath konsolu kaldırma) | W0 + T1.2 | ‖ |
| W5 | T5.1 Tema Kullan · T5.2 Admin · T5.3 Kullanım Raporu | T1.2 (5.1), T1.1+T1.2 (5.2/5.3) | ‖ |
| W6 | T6 Temizlik, metin taraması, e2e seçicileri, doğrulama | hepsi | tek |

**Dosya sahipliği:** `index.css`/`index.html`/`theme.css` yalnız T0. `App.tsx` yalnız T1.2 (T0 yalnız `useApplyUiTheme()` çağrısı). `Xslteditor.tsx`/`AiAssistantPanel`/`DevModePage`/`xslt-editor/*`/`components/ai/*` yalnız T4. `PropertyPanel`/`BlockPalette`/`XmlTreeExplorer`/`Canvas`/`components/editor/*` yalnız T3. `components/storage/*` yalnız T2.3. `UpgradeModal` yalnız T2.4. `components/admin/*` yalnız T5.2/T5.3. `components/ui` W0 sonrası salt-okunur (ihtiyaç olursa kendi klasöründe oluştur).

## 4. Görevler

### T0 — Tema altyapısı (W0)
Yeni: `src/styles/theme.css`, `src/store/uiPrefsStore.ts`, `src/theme/{uiThemes.ts,useApplyUiTheme.ts,monacoThemes.ts}`, `src/components/ui/{IconTile,SegmentedTabs,Switch,PageHeader,Card,ThemeSwatches}.tsx`. Değişen: `src/index.css`, `index.html`, `src/App.tsx` (yalnız `useApplyUiTheme()`), `src/main.tsx` (font importları), `src/components/ui/ToastContainer.tsx` (renk). Paketler: `@fontsource/ibm-plex-sans`, `@fontsource/ibm-plex-mono`.
Kabul: konsolda `document.documentElement.dataset.uiTheme='mor'` → eski `--color-brand-*` kullanan yerler de değişir; build+lint temiz; davranış değişmez. Risk: `@theme inline` olmazsa sınıflar sabit derlenir.

### T1.1 — AppShell (Dashboard kabuğu)
Yeni: `src/components/shell/{AppHeader,QuickSearch,SideNav,SideNavItem,NewTemplateMenu,ProfileCard,SettingsSection,navItems}.tsx|ts`, `src/hooks/useRecentWork.ts`, `src/utils/workRoutes.ts`. Değişen: `components/layout/AppLayout.tsx`. (Navbar/Sidebar T6'da silinir.) Ref: `Dashboard.dc.html`, `DashboardExpanded.dc.html`.
- Header: logo + ortada "Şablonlarına hızlı git…" (Ctrl/⌘K, `preventDefault`); odakta listbox — boşken "Son açılanlar" (updatedAt ilk 5), yazınca istemci filtresi; ok/Enter; `role=combobox/listbox/option`. Sağda buton yok.
- `useRecentWork`: `getUserXsltTemplates` + `getMyTemplates` birleşimi, modül seviyesinde 60 sn önbellek. `workRoutes.ts`: taslak/XSLT "aç" yolu (DraftsPage açma mantığını **birebir** kopyala, temadan gelen taslak dahil) + kaynak etiketi ("Tasarım alanı" / "Temadan" / "XSLT").
- SideNav: daraltılmış 88px (44px ikon kutusu + kısa etiket), açık 256px, `navCollapsed`. Öğeler/renk: Dashboard (accent), XSLT Editör (purple), Şablonlarım (violet), Taslaklarım (gray), Yeni Şablon (blue; menü: e-Fatura/e-Arşiv → `/editor/new`, e-İrsaliye → `/editor/new?type=despatch`), Tema Kütüphanesi (green; `/templates?type=Invoice`), Admin Paneli (slate; yalnız `role==='Admin'`; `/admin/themes`).
- Alt: ProfileCard (baş harfler, ad, "Profil ve plan", plan çipi; Free → "Pro'ya geç" `/pricing`; Admin → "Admin"; tıklayınca menü: Profilim, Planlar, **Çıkış yap** — `handleLogout` birebir). Ayarlar (`aria-expanded`): arayüz rengi (3) + "Menü daraltılmış başlasın" Switch; daraltılmışta Ayarlar'a basınca genişle+aç. Hakkında. "XsltCraft v{__APP_VERSION__} · Yenilikler" (`/about#yenilikler`).
Kabul: varsayılan daraltılmış; 3 temada doğru; çıkış çalışır; tüm eski rotalara erişim; Admin olmayan Admin'i görmez.

### T1.2 — WorkspaceHeader + AdminLayout
Yeni: `src/components/shell/WorkspaceHeader.tsx`, `src/components/shell/useLogout.ts` (T1.1 ile paylaşılır), `src/components/layout/AdminLayout.tsx`. Değişen: `src/App.tsx` (`/admin/*` → AppLayout içinde `<Route element={<AdminLayout/>}>`). Ref: `Main.dc.html` header, `XsltEditor.dc.html` header, `AdminShell.dc.html` (91–104).
- WorkspaceHeader: bar tokenları; logo → `/dashboard`; modül sekmeleri **Tasarım Alanı** (`/editor/new`), XSLT Editör (`/xslt-editor`), Tema Kütüphanesi (`/templates?type=Invoice`), Şablonlarım (`/my-xslt-templates`). Prop: `active`, `onNavigate?(path)` (kaydedilmemiş değişiklik koruması; yoksa `navigate`), `center`/`actions` slotları. Avatar menüsü: Dashboard, Profilim, Çıkış.
- AdminLayout: "Admin Paneli" başlığı + renkli NavLink sekmeleri (Hazır Şablonlar, Snippet Kütüphanesi, Kullanıcılar, Kullanım Raporu, AI Asistan, AI Geri Bildirim; `aria-current="page"`) + `<Outlet/>`.
Kabul: admin rotaları + `/admin` yönlendirmesi aynen; modül sekmeleri doğru sayfaya.

### T2.1 — Dashboard
`src/pages/DashboardPage.tsx` + yeni `src/components/dashboard/{XsltEditorCard,MyTemplatesCard,ShortcutTiles,RecentWorkTable}.tsx`. Ref `Dashboard.dc.html`. Selamlama; XSLT Editör kartı ("Editörü aç" → `/xslt-editor`; "XSLT yükle" gizli `<input type=file accept=UPLOAD_ACCEPT.xslt>` → `navigate('/xslt-editor',{state:{pendingXsltFile:File}})` — alıcıyı T4 uygular; son 3 XSLT → `/xslt-editor/:id`); Şablonlarım kartı (ilk 4, XSLT+taslak, çip "XSLT"/"Taslak", "Tüm şablonlar"); 4 kısayol (Yeni e-Fatura blue, Yeni e-İrsaliye orange, Tema Kütüphanesi green, Taslaklarım gray); Son çalışmalar (ilk 5; Ad/Tür/Kaynak/Güncellendi/Aç). Hover `.xc-lift` + IconTile. Veri yalnız `useRecentWork`. Kabul: linkler eski rotalara; boş durum metni.

### T2.2 — Tema Kütüphanesi
`src/pages/TemplatesPage.tsx`. Ref `AppShell.dc.html` (isThemes). PageHeader; SegmentedTabs `?type=Invoice|Despatch` (yoksa replace ile Invoice); sayı etiketi; kart ızgarası (thumbnail/yer tutucu, Ücretsiz/Pro `isPremium`, "Önizle" mevcut `setSelectedTheme`, "Kullan"/"Pro ile aç" mevcut `gatedUse`). Kategori çipi yok. Kabul: önizleme, Pro kapısı, `/theme-use/:id` aynen; iframe beyaz.

### T2.3 — Taslaklarım / Şablonlarım
`src/pages/DraftsPage.tsx`, `src/pages/MyXsltTemplatesPage.tsx`, `src/components/storage/*`, yeni `src/components/storage/LibraryTabsHeader.tsx`. Ref `AppShell.dc.html` (isDrafts). Ortak başlık + sekmeler "Taslaklarım" (`/drafts`) | "Şablonlarım (XSLT)" (`/my-xslt-templates`) — rotalar/mantık ayrı kalır. Sol klasör kartı; araç çubuğu (arama, sırala, Toplu yükle); tablo (IconTile, ad+meta, Tür, Klasör noktası, Güncellendi, aria-label'lı ikon işlemler, mevcut satır içi silme onayı); XSLT satırları mono. Kabul: klasör/favori/taşıma/paylaşma/toplu yükleme/not akışları aynen.

### T2.4 — Profil, Planlar, Hakkında, UpgradeModal
`src/pages/{ProfilePage,PricingPage,AboutPage}.tsx`, `src/components/UpgradeModal.tsx`. Ref `AppShell.dc.html` (isProfile/isPricing/isAbout). Profil: mevcut form/servisler; plan kartı kullanım çubukları `entitlementStore` (limit 0 → "Sınırsız"); Google kartı yalnız veride varsa; Tehlikeli alan. Planlar: mevcut satırlar, ilk satır "Tasarım alanında şablon tasarla". Hakkında: başlık kartı (`__APP_VERSION__`), 5 özellik kartı ("Tasarım Alanı" dahil), `id="yenilikler"` sürüm notları. Kabul: profil/şifre/hesap silme aynen.

### T2.5 — Auth
> **Geri alındı (2026-10-26, sahip kararı):** Giriş/Kayıt yeniden tasarımı istenmemişti. Eski `AuthLayout` (tema D) + `LoginPage`/`RegisterPage` git’ten geri getirildi; yalnız indigo vurgular `--ui-accent`/`--ui-soft-line` tokenlarına bağlandı (font IBM Plex global).
`src/components/layout/AuthLayout.tsx` (A–D kaldır, iki sütunlu tek düzen), `src/pages/auth/{LoginPage,RegisterPage}.tsx` (yalnız stil/sarmalayıcı). Ref `Login.dc.html`, `Register.dc.html`. Sol panel (auth tokenları, metin, 3 özellik, ThemeSwatches → uiPrefs), sağ form; alan adları, `autocomplete`, "Giriş Yap", Google bileşeni korunur. "Şifremi unuttum" eklenmez. Kabul: giriş/kayıt/Google çalışır; e2e login geçer.

### T3 — Tasarım Alanı (EditorPage)
Değişen: `src/pages/EditorPage.tsx`, `src/components/editor/{BlockPalette,XmlTreeExplorer,PropertyPanel,Canvas,GridBlockItem,EditorPreviewPanel}.tsx`. Yeni: `src/components/editor/{EditorLeftPanel,CanvasToolbar,DesignStatusBar,paletteIcons}.tsx|ts`. Ref `Main.dc.html`.
- Header: `WorkspaceHeader active="design" onNavigate={handleNavigate}`; orta: inline ad düzenleme, belge türü çipi, "Kaydedilmedi"/"Kaydediliyor…"; eylemler: Geri al / Yinele (aria-label), Önizle, Kaydet (ikincil), XSLT İndir (birincil). "‹ Geri" kalkar; ☰ → sol panel daraltma.
- Sol panel `EditorLeftPanel` (296px): sekmeler Bloklar | XML Ağacı — **aktif sekme `xmlStore.rightPanelTab`'tan türetilir** (`'xml'` → XML Ağacı, aksi Bloklar); store API'sine dokunma; `isSelecting` true iken panel kapalıysa aç. Bloklar: arama + gruplar (e-Fatura/e-İrsaliye blokları, Temel, Yapı, Medya, Düzen), 2 sütun kart buton + lucide; **`useDraggable` id ve `data` aynen**; belge türü salt-okunur. XML Ağacı: dosya çipi, XML yükleme + çoklu XML seçimi header'dan buraya (mevcut `xmlInputRef`/`setActiveXml`), mono ağaç, XPath seçim bannerı + İptal korunur.
- PropertyPanel: XML sekmesi kalkar; başlık "Seçili blok" + etiket + tip adı (mono); sekmeler **Veri** (bloğa özel paneller olduğu gibi) | **Konum** (`GridPositionControls`, mm). Üstteki Field/TextInput/Checkbox/ColorInput yardımcıları tokenlarla yeniden stillenir (21 panel tek seferde).
- Canvas: üstte CanvasToolbar ("A4 · Dikey", salt-okunur zoom %); A4 beyaz; ızgara `var(--ui-grid)`; seçili çerçeve + etiket çipi `var(--ui-accent)`.
- DesignStatusBar: blok sayısı, seçili blok + mm konum/boyut, "5 mm ızgara", kayıt durumu (Canvas alt bilgisi buraya).
- NamePromptModal, DragOverlay, başarı bildirimleri tokenlarla.
Kabul: paletten sürükle-bırak, taşıma, boyutlandırma, 5 mm yaslama; XPath bağlama (sağ XPath butonu → sol ağaç → seçim → değer); geri al/yinele, kaydet, indir (402/429 dahil), terk koruması; `editorStore`/`xmlStore` diff yok.

### T4 — XSLT Editör + DevMode
Değişen: `src/pages/XsltEditorPage.tsx`, `src/pages/DevModePage.tsx`, `src/components/Xslteditor.tsx`, `src/components/Xmleditor.tsx`, `src/components/xslt-editor/{XsltEditorToolbar,XsltEditorPreview,ProblemsPanel,SaveTemplateDialog,ShareTemplateDialog,SnippetManagerDialog,ShortcutsDialog}.tsx`, `src/components/ai/*`. Yeni: `src/components/xslt-editor/{XsltSideMenu,XsltEditorSettings,LayoutSwitcher,XsltStatusBar,PreviewDock}.tsx`. Silinen: `XPathConsolePanel.tsx`, `services/xpathService.ts` (başka import yoksa). `xpathXmlIndex.ts` kalır. Ref `XsltEditor.dc.html` (+ Ai/Split/Popup varyantları).
1. **XPath konsolunu kaldır:** sayfadaki state, `handleEvaluateXPath`, (yalnız konsola hizmet ediyorsa) `handleLocateXmlLine`, buton, panel, `Terminal` importu; `Xslteditor`'dan `xslt-evaluate-xpath` eylemi + `onEvaluateXPath` prop'u; DevModePage'de aynı; ShortcutsDialog'da XPath satırı.
2. **Kabuk:** yerel `EditorSidebar` silinir; `WorkspaceHeader active="xslt"`; orta: dosya adı (mono), `version` özniteliğinden "XSLT 2.0/1.0" çipi, "Kaydedilmemiş değişiklik"; eylemler yalnız **Şablonlarıma kaydet** (`requestSave`) + **İndir** (`handleDownload`).
3. **Sol menü `XsltSideMenu`:** 56/264px, `xsltMenuCollapsed`. Üstte belirgin **XSLT YÜKLE** ve **BELGE XML YÜKLE**. **Sayfa kökünde tek XSLT + tek XML gizli input**; tüm butonlar ve boş durum kartları `ref.click()` ile tetikler (e2e strict mode). Sekmeler Dosyalar | Snippet Kütüphanesi (Dosyalar: XSLT satırı ad+kirli+KB, XML satırı — görüntüleme için `xmlFileName` state'i; XML satırı → sağ panel XML Kaynak. Snippet: `userSnippets` + "Yönet" (SnippetManagerDialog); tıklayınca mevcut `insertTextAtLineRef` ile imleç satırına ekle — imzayı doğrula). Altta **Ayarlar** (`XsltEditorSettings`): editör teması (Koyu varsayılan/Açık/Yüksek kontrast), varsayılan düzen (4), yazı boyutu (11–18), arayüz rengi, Satır kaydırma Switch.
4. **Editör araç çubuğu** (`--ed-*`, varsayılan koyu): Biçimlendir, Açıklama, Problemler (hata + compat sayaç), **UBL-TR kontrol** (`handleValidateUblTr` + hata rozeti), boşluk, LayoutSwitcher, "Önizle" (yalnız Yalnız editör modunda → mini), Yazdır + Kısayollar ikonları, AI Asistan toggle (yalnız `aiEnabled`). Eski `XsltEditorToolbar` kalkar ya da buna dönüşür; zamanlama popover'ı status bar'a; kilit ve önizleme hata bannerları korunur.
5. **Düzen modları** (`useEditorMaximize` API'si değişmeden): mod = `max.maximized ? (max.miniOpen ? 'mini' : 'editorOnly') : splitMode`. side: `useGroupRef().setLayout` ≈65/35; half: 50/50; mini: `max.enter({withMini:true})`; editorOnly: `max.enter()` + `max.setMiniOpen(false)`. Varsayılan düzen iki dosya ilk yüklendiğinde **bir kez**. Mini'de "Sağ panele geri al" → side, kapat → editorOnly. **Maximize katman hilesi aynen — editör düğümü ağaçta yer değiştirmez** (Monaco remount → undo kaybı).
6. **PreviewDock:** başlık "Önizleme" + süre (ms); sekmeler Önizleme | XML Kaynak (Biçimli/Ham korunur); yenile (`runPreview`); "Mini pencereye al" (aria-label **"Önizlemeyi mini pencerede göster"** korunur). Motor adı yalnız yanıtta varsa.
7. **AI paneli:** sağda ayrı sütun (340px), önizleme yanında görünür. `openAiBlank`/`openAiForProblem`/`askAi` → `setAiOpen(true)`. `aiMounted` ile mount kalır, `hidden` ile gizlenir. `aiSplit` + sürükleme kodu kalkar. **Aynı anda en fazla bir önizleme iframe'i** (yazdırma onu hedefler). AiAssistantPanel açık kart temasıyla.
8. **Problemler paneli** varsayılan kapalı (mevcut); `--ed-*` ile stillenir.
9. **XsltStatusBar:** önizleme süresi (→ TimingPopover), "N hata · M uyarı" (→ Problemler), imleç satırı, "UTF-8", "Düzen: {ad}".
10. **Monaco:** `Xslteditor` + XML Kaynak editörüne `beforeMount={defineMonacoThemes}`, `theme={monacoThemeId(editorTheme)}`, `fontSize`/`wordWrap` tercihten. AI diyaloglarında `vs-dark` → `xc-dark`.
11. **Boş durumlar** (Faz 1/1b): yeni kabuk içinde iki kart; **h1 "XSLT Editör" ve "XSLT/XML dosyası yüklendi — …" metinleri birebir**.
12. **Dashboard'dan gelen dosya:** `location.state?.pendingXsltFile` → `handleXsltFile` içeriği `loadXsltFile(file: File)`'a çıkarılır ve çağrılır; ardından `navigate(pathname,{replace:true,state:null})`.
13. **DevModePage:** XPath konsolu kalkar, `WorkspaceHeader` + tokenlar; yerleşim yeniden tasarlanmaz.
Kabul: yükleme doğrulaması, debounce'lu önizleme, kaydet/güncelle, kilit heartbeat, indir, yazdır, UBL-TR + problem satırına gitme; AI sohbeti mod/sekme değişiminde kaybolmaz; Ctrl+Shift+F; görsel ekleme bağlam menüsü; "XPath konsolu" araması boş.

### T5.1 — Tema Kullan
`src/pages/ThemeUsePage.tsx`, `src/components/theme-assets/{ImagePanel,BankInfoPanel,AlignPicker,DraggableAssetChip}.tsx` (yalnız stil). Ref `ThemeUse.dc.html`. Yerel `EditorSidebar` silinir → `WorkspaceHeader active="themes"`; eylemler: Yazdır, mevcut XSLT editörü/geliştirici modu linki, Şablonlarıma kaydet, XSLT İndir. Breadcrumb "Tema Kütüphanesi / {ad}". Solda akordeon (`aria-expanded`): Logo (`ImagePanel`), İmza/Kaşe (`ImagePanel`), Banka/IBAN (`BankInfoPanel`). Ortada "Kendi XML'ini yükle" (`handleXmlFile`) + `ThemeSlotPreview`. Sağda tema bilgisi + mevcut state'ten kontrol listesi. Kabul: DnD yuvaları, klavye/ekran okuyucu duyuruları, `data-xc-*`, kaydet/indir aynen; e2e `theme-compose.spec` geçer.

### T5.2 — Admin sayfaları
`src/pages/admin/{AdminThemesPage,AdminSnippetsPage,AdminUsersPage,AdminAiPage,AiVisionCard,AdminAiFeedbackPage}.tsx`, `src/components/admin/{ThemeSlotReport,ThemeUploadDecisionDialog}.tsx`. Ref `AdminShell.dc.html`. Kullanıcılar: istatistik kartları (`totalCount`, `activeCount`, `inactiveCount`, `totalActivityCount`), filtreli tablo, satır seçilince **mevcut detay paneli** sağda aside; modallar yeniden stillenir. Diğerleri: kart/tablo/form stili. Kabul: rol/plan/şifre/sil/oluştur, tema yükleme kararı, snippet CRUD, AI ayarları, feedback aynen.

### T5.3 — Kullanım Raporu (SVG, yeni kütüphane yok)
`src/pages/admin/AdminUsagePage.tsx`, yeni `src/components/admin/usage/{MetricCard,Sparkline,UsageAreaChart,ChartTooltip,usageChartMath}.tsx|ts`. Ref `AdminShell.dc.html` 289–399. Presetler 7/14/30 + mevcut tarih alanları "Özel". `Promise.all`: rapor + günlük + önceki dönem günlük. 5 metrik kartı (button, aria-pressed, toplam, önceki döneme göre %, sparkline; `metric-1..5`). Alan grafiği (SVG viewBox 1000×260, `preserveAspectRatio=none`, `vector-effect=non-scaling-stroke`): alan+çizgi, önceki dönem kesikli (toggle), ortalama noktalı + "Ort." etiketi, en yüksek gün işareti, tek y ekseni 5 güzel tick, ~7 x tick, gün sütunu hover tooltip (5 metrik + önceki dönem + fark), `role="img"` + özet aria-label. Seçili metrik sütunu vurgulu kullanıcı tablosu. Dipnot "grid-canvas" → "Tasarım Alanı". Kabul: toplamlar mevcut rapor değerleriyle aynı; boş veride çökmez (sıfıra bölme).

### T6 — Temizlik + doğrulama
Sil: `Navbar.tsx`, `Sidebar.tsx`, `EditorSidebar` kalıntıları, kullanılmayan `.xc-*`/FlexLayout CSS'i (önce grep), `loginpage_invoice_design.png` importu. Sabit renk taraması: `grep -E "blue-(50|600|700)|bg-gray-900|#185FA5|#E0DDD8"` → token. E2E seçicileri (`e2e/editor-maximize.spec.ts`, `upload-validation.spec.ts`) yeni aria'lara göre (senaryo değişmez). `CHANGELOG.md` 2.0.0 UI notu. Son kontrol: `grep -riE "grid (tasar|canvas)|ızgara editör" src` boş.

## 5. "Tasarım Alanı" metni
Kodda "Grid Tasarımcı" yok. Eklenen/değişen UI metinleri: WorkspaceHeader sekmesi (T1.2); `PricingPage.tsx:14` "Grid canvas ile şablon tasarla" → "Tasarım alanında şablon tasarla" (T2.4); AboutPage "Tasarım Alanı" kartı (T2.4); `AdminUsagePage.tsx:242` "grid-canvas üretim indirmeleri" → "Tasarım Alanı üretim indirmeleri" (T5.3); QuickSearch/Dashboard/taslak listesi kaynak etiketi "Tasarım alanı" (`workRoutes.ts`); auth sol paneli (T2.5). Tip/kod adlarına (`GridBlock`, `canDownloadGridXslt`, `grid-canvas` droppable id, `gridSnap`) **dokunulmaz**.

## 6. Developer ortak kuralları
1. **İşlev değişmez.** `src/services/*` ve mevcut `src/store/*`'a dokunma (istisna: yeni `uiPrefsStore`, `xpathService` silme, T4 `loadXsltFile` çıkarımı + `xmlFileName` görüntüleme state'i). Handler/rota/API'yi taşı, yeniden yazma.
2. DnD id/data, `data-xc-*`, file input `accept`, `autocomplete`, e2e'nin dayandığı metin/aria korunur; zorunlu değişim T6'da spec'e yansır.
3. İkonlar yalnız `lucide-react`; emoji/unicode glif ikon yok (paletteki ⊞ ₸ vb. → lucide).
4. Renkler yalnız `--ui-*`, `--ed-*`, `--color-fn-*`, `--color-tx-*`; inline gerekirse `var(--ui-…)`, hex yok. **Fatura/A4 önizleme ve iframe zemini her zaman beyaz.**
5. Erişilebilirlik: gerçek `<button>/<a>/<label>`; ikon butonda `aria-label`; sekmede `role=tablist/tab` + `aria-selected`; toggle'da `aria-pressed` ya da `role=switch`; akordeonda `aria-expanded`; `:focus-visible` halkası; AA kontrast (turuncu temada bar beyaz, metin `--ui-bar-fg`); `prefers-reduced-motion`.
6. Dosyalar küçük; yeni bileşenler özellik klasörüne (`components/shell`, `components/xslt-editor`, …); 1000+ satırlık sayfalarda yeni UI ayrı bileşene.
7. Yeni npm bağımlılığı yok (istisna: fontsource paketleri). Grafikler SVG.
8. Her görev sonunda `npm run build` + `npm run lint` temiz; görevin sayfası 3 temada gözle kontrol.
9. Monaco editörünü ağaçta taşıyan ya da koşullu sarmalayan değişiklik yok (remount).
10. Test yazımı yok (yalnız UI); e2e seçici güncellemesi T6'da.

## 7. Riskler ve doğrulama
Riskler: Monaco remount (undo kaybı); iki önizleme iframe'i (yazdırma yanlış hedef); sol panele taşınan XML ağacında XPath bağlamanın kırılması; Çıkış/Pro CTA kaybı; modül sekmelerinin kaydedilmemiş değişiklik korumasını atlaması; aynı accept'li ikinci file input; `@theme inline` eksikliği.
Doğrulama: her görevde `cd frontend/xsltcraft-ui && npm run build && npm run lint`; `git diff --stat src/services src/store`; elle kontrol (3 temada: giriş/çıkış; Dashboard linkleri + Ctrl K; Tasarım Alanı sürükleme/XPath bağlama/kaydet/indir/terk uyarısı; XSLT Editör iki dosya, 4 düzen, Ctrl+Shift+F, AI açıkken mod değişimi, UBL-TR + problem satırı, yazdır; Tema Kullan logo sürükle + indir; Admin kullanım grafiği hover + 7/14/30). İsteğe bağlı `npm run test:e2e` (T6 sonrası).

## 8. Temel aşama çıktısı — API özeti (W0+W1 tamamlandı, 2026-10-25)

Dosyalar (`frontend/xsltcraft-ui/`): `src/styles/theme.css`, `src/store/uiPrefsStore.ts`, `src/theme/{uiThemes,useApplyUiTheme,monacoThemes}.ts`, `src/components/ui/{IconTile,SegmentedTabs,Switch,PageHeader,Card,ThemeSwatches}.tsx`, `src/components/shell/{AppHeader,QuickSearch,SideNav,SideNavItem,NewTemplateMenu,ProfileCard,SettingsSection,WorkspaceHeader,headerParts,MenuPopover}.tsx`, `src/components/shell/{navItems,useLogout,userDisplay}.ts`, `src/components/layout/AdminLayout.tsx`, `src/hooks/useRecentWork.ts`, `src/utils/workRoutes.ts`. `Navbar.tsx`/`Sidebar.tsx` artık kullanılmıyor (T6'da silinecek).

- **`useUiPrefsStore`** (`xsltcraft-ui-prefs` v1): `uiTheme` ('lacivert'|'mor'|'turuncu'), `navCollapsedOnStart` (true), `navCollapsed` (persist dışı), `editorTheme` ('dark'|'light'|'contrast'), `editorDefaultLayout` ('side'|'half'|'mini'|'editorOnly'), `editorFontSize` (11–18, 13), `editorWordWrap`, `xsltMenuCollapsed`. Aksiyonlar: `setUiTheme, setNavCollapsedOnStart, setNavCollapsed, toggleNavCollapsed, setEditorTheme, setEditorDefaultLayout, setEditorFontSize (clamp), setEditorWordWrap, setXsltMenuCollapsed, toggleXsltMenuCollapsed`. Export: `EDITOR_FONT_MIN/MAX`, `UiThemeId`, `EditorThemeId`, `EditorLayoutId`. `useApplyUiTheme` `<html>`'e `data-ui-theme` + `data-editor-theme` yazar → `--ed-*` her yerde kullanılabilir.
- **Token sınıfları:** `bg-ui-{bar,search,card,bg,side,soft,accent,hover,chip,avatar,panel,grid}`, `text-ui-{bar-fg,bar-muted,ink,chip-fg,avatar-fg,panel-fg,panel-muted,icon-fg}`, `border-ui-{line,soft-line,bar-border,btn-line,panel-line}`; metin `text-tx`, `text-tx-2`, `text-tx-muted`, `text-tx-subtle`, `border-field-line`; işlev renkleri `bg-fn-X`, `bg-fn-X-tint`, `text-fn-X-ink`, `border-fn-X-line` (X: blue, orange, green, gray, purple, violet, slate, red); `bg-metric-1..5`; `font-mono`; editör `bg-ed-{bg,panel,seg,active}`, `border-ed-border`, `text-ed-{text,muted,warn}`; gölge `shadow-[var(--ui-shadow-rest|lift|pop)]`.
- **`--fn` deseni:** `fnStyle(color)` (`theme/uiThemes.ts`; color ek olarak `'accent'`) → `--fn`, `--fn-tint`, `--fn-ink`, `--fn-line`; Tailwind'de `bg-(--fn-tint)`, `text-(--fn)`, `group-hover:bg-(--fn)`. `.xc-lift` (hover/focus 3px kalkar + gölge + kenar `--fn-line`, reduced-motion kapalı). `.xc-focus` (focus-visible halkası; koyu zeminde `--xc-focus-ring`). `UI_THEMES`, `EDITOR_THEMES` (Türkçe adlar).
- **`components/ui`:** `IconTile` (`icon`, `color?` yoksa üstteki `--fn`, `size?` xs22/sm26/md30/lg44/xl56, `filled?`, `hoverFill?` varsayılan true — kapsayıcıda `group`, `className?`); `SegmentedTabs<T>` (`items {id,label,icon?,count?}`, `value`, `onChange`, `ariaLabel`, `variant?` 'pill'|'underline', `idPrefix?`; role=tab + ok/Home/End); `Switch` (`checked`, `onChange`, `label`, `hideLabel?`, `disabled?`, `tone?` 'light'|'dark'); `PageHeader` (`title`, `description?`, `icon?`, `iconColor?`, `actions?`, `children?`); `Card` (`as?`, `lift?`, `fn?`, `padding?` none|sm|md|lg + HTML attr); `ThemeSwatches` (`value?`/`onChange?` yoksa store, `variant?` 'list'|'dots', `ariaLabel?`; koyu panelde dots için `style={{'--xc-swatch-offset':'var(--ui-panel)'}}`).
- **`WorkspaceHeader`** (`components/shell/WorkspaceHeader.tsx`): `active?` ('design'|'xslt'|'themes'|'templates'), `onNavigate?(path)` (logo/sekme/avatar gezinmeleri — kaydedilmemiş değişiklik koruması için), `leading?`, `center?`, `actions?`. Avatar menüsü: Dashboard, Profilim, Çıkış yap. Parçalar `headerParts.tsx`: `HeaderButton` (`variant` primary|secondary|icon + `icon`; icon'da aria-label zorunlu), `HeaderChip` (`mono?`), `HeaderStatus`, `HeaderDivider`.
- **`MenuPopover`:** `open`, `onClose`, `anchorRef`, `items {id,label,icon?,to?|onSelect?,tone?:'danger'}`, `label`, `placement?`, `id?`, `onNavigate?` (portal, role=menu, ok/Esc).
- **`useLogout()`**: eski Navbar ile birebir.
- **`useRecentWork(opts?: {lazy?})`** → `{ items: WorkItem[], loading, error, reload }` (yeniden eskiye, taslak+XSLT). `WorkItem {key,id,kind:'draft'|'xslt',name,updatedAt,documentType:'Invoice'|'Despatch'|null,source,sourceLabel,to,isShared}`. 60 sn modül önbelleği; `loadRecentWork(force?)`, `invalidateRecentWork()`.
- **`workRoutes.ts`:** `WorkSource` ('design'|'theme'|'xslt'), `SOURCE_LABEL` ("Tasarım alanı"/"Temadan"/"XSLT"), `SOURCE_COLOR`, `SOURCE_ICON`, `DOC_TYPE_LABEL`, `draftOpenPath(id)`, `xsltOpenPath(id)`, `draftSource(t)`.
- **`monacoThemes.ts`:** `defineMonacoThemes(monaco)` (beforeMount, idempotent) → `xc-dark` (#1B1E27), `xc-light`, `xc-contrast`; `monacoThemeId(editorTheme)`.
- Not: avatar menüsündeki Çıkış `onNavigate` korumasından geçmez (editörde kaydedilmemiş değişiklikte uyarısız çıkış) — T3/T4 isterse logout öncesi korumayı `onNavigate` ile sarmalayabilir.
