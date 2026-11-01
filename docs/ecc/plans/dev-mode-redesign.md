# Geliştirici Modu yeniden tasarımı — plan

Tarih: 2026-10-31 · Durum: sahip kararları ve mimari kararlar tamam, uygulanıyor · Kök: `frontend/xsltcraft-ui/src`

Kapsam: `/dev-mode/:templateId` ekranı, Tema Kullan ↔ Geliştirici Modu geçişi ve compose yürütme yerindeki güvenlik düzeltmesi. XSLT Editör'ün görünümü ve davranışı değişmez. Yeni görsel dil yok; UI 2.0 parçaları yeniden kullanılır.

## Sahip kararları

| # | Konu | Karar |
|---|---|---|
| K1 | Düzenle panelinin yeri | **Sol sütun, Tema Kullan gibi.** `XsltSideMenu`'nün yerini alır. |
| K2 | Kaydet sonrası | Sayfada kalır; sonraki kayıtlar aynı şablonu günceller. |
| K3 | Tema Kullan ↔ Geliştirici Modu | **Çift yönlü.** İki ekran arası geçişlerde yapılanlar, bu iki ekrandan çıkılmadığı sürece kaybolmaz. Elle yapılan XSLT değişiklikleri dahil. |
| K4 | Sürükle-bırak ve AI Asistan | İkisi de Dalga 2. |
| K5 | Düzenle paneli açılışı | Taslak doluysa açık, boşsa kapalı. |
| K6 | "XSLT Yükle" | Bu ekranda yok (tema XSLT'sinin üzerine yazar). |
| S1 | `xslt` modlu compose'un önizlemesi | **Backend'de izole yola alınır:** `request.ThemeId is null ? XsltRoute.Raw : XsltRoute.Theme`. 1 backend testi + güvenlik incelemesi. |

**S1 gerekçesi:** `ThemeComposeController` HTML'i hep `XsltRoute.Theme` ile üretiyor; bu rota 1.0 ve 2.0 yapısı kullanmayan metni `.NET` ile API süreci içinde çalıştırıyor. Yakalanamayan özyineleme giriş yapmış herhangi bir kullanıcının API'yi düşürmesine yol açabilir (ADR-008; PoC çalıştırılmadı).

## Yerleşim

```
WorkspaceHeader: sekmeler | Tema [XSLT 2.0][Geliştirici Modu] Kaydedilmemiş | Tema Kullan'a dön · Düzenle · Şablonlarıma kaydet · [İndir]
┌───────────────┬──────────────────────┬──────────────────┐
│ Düzenle       │ XsltEditorToolbar    │ PreviewDock      │
│ > Logo        │ Monaco               │ Önizleme|XML     │
│ > İmza/Kaşe   │                      │ (genişliğe sığ)  │
│ > Banka/IBAN  │ ProblemsPanel (açık) │                  │
│ > Sabit notlar│                      │                  │
│ > Belge/dosya │                      │                  │
│ > Snippet     │                      │                  │
│ > Ayarlar     │                      │                  │
├───────────────┴──────────────────────┴──────────────────┤
│ XsltStatusBar                                           │
└─────────────────────────────────────────────────────────┘
```

## Mimari kararlar

1. **Sol menü işlevleri:** Sol panelde varlık bölümlerinin altına ayrı `AssetAccordionSection`'lar: "Belge ve dosyalar" (dosya bilgisi, XML değiştir), "Snippet Kütüphanesi", "Editör ayarları". Snippet listesi `XsltSideMenu`'den `SnippetLibraryList` olarak çıkarılır. `XsltEditorSettings`'e isteğe bağlı `hideRightPanelMode`. XSLT Editör aynı görünür. Panel kapalıyken bu bölümlere "Düzenle" ile ulaşılır.
2. **Taslak store (K3):** `store/themeDraftStore.ts`, bellek içi, tek kaynak. Alanlar: `themeId`, `meta {name, slots, documentType}`, `logo`, `signature`, `bankInfo`, `fixedNotes`, `notesTouched`, `xml {content, fileName} | null`, `workingXslt`, `xsltEdited`, `composedKey`, `saved {id, updatedAt} | null`, `baselineXslt`.
3. **Yaşam döngüsü:** Layout route. `<Route element={<ThemeDraftScope/>}>` `/theme-use/:templateId` ve `/dev-mode/:templateId` rotalarını sarar. `useLayoutEffect` temayı bağlar, cleanup taslağı sıfırlar. Store `themeId` ≠ URL iken `null` render edilir; bu A→B tema atlamasında sayfayı yeniden mount eder. Pathname izleyicisi kullanılmaz (child effect yarışı). Çıkış ve logout unmount ile kapsanır; `authStore` değişmez. Kilit serbest bırakma (`releaseEditingLock`) da bu cleanup'tadır, DevMode unmount'unda değil.
4. **Elle düzenleme ayrımı:** `@monaco-editor/react` 4.7.0'da controlled değer `executeEdits` ile yazılır ve `onChange` **tetiklemez**; `readOnly` dalı ise tetikler. Bu yüzden **DevMode editörüne `readOnly` verilmez.** `onChange` ve AI uygulaması `setManualXslt`, compose `setComposedXslt` çağırır; ek bayrak yok. Biçimlendirme, açıklama ve snippet `executeEdits` ile gider ve `onChange` tetikler (doğru davranış).
5. **Store erişimi:** `useDraftField(key)` `[value, setter]` döner, setter updater fonksiyonu da kabul eder. `fixedNotes` setter'ı `notesTouched`'ı kendisi açar. ThemeUse diff'i neredeyse yalnız `useState` satırlarıdır.
6. **Compose yeniden çalışma:** `useDevModeCompose` tek effect'tir: `desiredKey !== composedKey` iken 1200 ms ya da `composeNow` sonrası `xslt` modunda ister (`includeHtml:false`, XML gönderilmez). Metin değişince cleanup isteği iptal eder, bozuk metinde bekler. Yazım metni ve anahtarı atomik günceller. Bu, Tema Kullan'da eklenen logonun dönüşte metne girmesini de sağlar.
7. **Tema Kullan + `workingXslt`:** `useThemeCompose`'a isteğe bağlı `xslt`. Guard `!themeId && !xslt`; gövde ikisinden yalnız birini gönderir; `fixedNotes` tipi `null` da alır. `xsltEdited` iken `xslt` modunda çalışır, notlar üç durumlu gider (`notesTouched ? (toComposeNotes(s) ?? []) : null`). Taslak boşken istek gövdesi bugünküyle birebir aynı kalır. Tema adı, slot ve belge türü `meta`'dan okunur. Bu modda yalnız "bazı faturalarda görünmeyebilir" uyarısı kaybolur (kabul edildi).
8. **Bozuk `workingXslt` ile Tema Kullan:** Geçiş engellenmez. ThemeUse hook'a iki parametreyi de `undefined` geçer; istek gitmez, `html` boş kalır, İndir/Kaydet mevcut koşulla pasifleşir. `role="alert"` şeridi ve "Geliştirici Modu'na dön" düğmesi gösterilir. Well-formed olup derlenmeyen metinde sunucu 400 döner, mevcut `compose.error` şeridine aynı düğme eklenir.
9. **Düzen:** Sol panel `PanelGroup`'a **girmez**: `w-80` sabit `aside`, gizlemek için `hidden` sınıfı; tam ekranda görünürlük türetilir. Yalnız iç grup `[editör | önizleme]` panel grubudur, `collapsible` yok, rAF gerekmez. Grup ilk render'dan itibaren mount kalır; `XsltEditor` metin hazır olunca bir kez mount olur, hazır durumu geri dönmez. Tam ekran yalnız sınıf değişimiyle. Önizleme bölmesi sabit `minSize="340px"`.
10. **AI yeri (T8):** Önizleme bölmesinde Önizleme | AI Asistan sekmesi; `xsltRightPanelMode`'a bakmaz; 4. sütun yok. Tam ekranda AI açılırsa önce tam ekrandan çıkılır.
11. **Sayfa boyutu:** `DevModePage` hedef ≤400 satır. Parçalar: `useDevModeDocument`, `useDevModeCompose`, `useRawPreview`, `useXsltDiagnostics`, `useTemplateSave`, `DevAssetsPanel`, `DevToolsSections`.

## Plandaki düzeltmeler (uygularken dikkat)

- `ThemeUsePage:111` Despatch temada her mount'ta XML'i örnekle eziyor; K3'ü bozar. XML `xml ?? örnek(docType)` olarak türetilir.
- `ThemeUsePage:349`'daki "Tema Kütüphanesi" `Link`'i header korumasını atlar; `confirm` alır.
- DevMode kilit `acquire` etmez. `Update` kilidi kaydedene yazar (`UserXsltTemplateController:320`); serbest bırakma scope cleanup'ında. 423 yalnız toast, salt-okunura geçilmez (karar 4).
- `xsltEditorStore`'un tüketicisi yok, `frontend.md` aksini söylüyor. Yeniden kullanılmaz; T11'de düzeltilir.
- Kabul edilen sınır: Compose'dan sonra Ctrl+Z `onChange` tetikler, metin elle düzenlenmiş sayılır, anahtar geri dönmez; logo bir sonraki varlık değişikliğine kadar eksik kalır.

## Teknik bulgular

- `xslt` modunda `fixedNotes` kabul edilir. `null` notlara dokunmaz, `[]` eski notları siler. `xmlContent` yalnız `includeHtml:true` iken zorunlu. Sınırlar: XSLT 4 MB, gövde 6 MB; XML yükleme sınırı zaten 1 MB.
- `FixedNoteWriter` bozuk XSLT'de `supported=false` döner; compose öncesi istemcide well-formed denetimi şart, `fixedNotesSupported` yalnız geçerli metinden güncellenir.
- `theme-compose` dakikada 60 istek. Debounce 1200 ms; sürükle-bırak ve stil değişimi `composeNow`.
- Compose, overrides bloğunu bütünüyle yeniden yazar. Panel metni: "Değişiklikler editördeki XSLT'ye otomatik işlenir. Görsel/banka bloğu ve panelden yazılan sabit notlar her değişiklikte yeniden yazılır; diğer düzenlemeleriniz korunur." `DEV-MODE-OVERRIDES` adı kullanıcıya gösterilmez.
- `xslt-content` 402'de yükseltme akışı: `parseGateError` → `openUpgradeModal` → `/templates`.

## Görevler

### Dalga 1

| Sıra | Görev | Agent | Paralel |
|---|---|---|---|
| 0 | S1 controller + 1 test; ardından security-reviewer (Kapı 0) | general-purpose | T1 ile |
| 1 | T1 | refactorer | Görev 0 ile |
| 2 | T2, T3 | refactorer, general-purpose | birbirleriyle |
| Kapı A | T3 incelemesi | reviewer + security-reviewer | |
| 3 | T4, T5 | general-purpose | birbirleriyle |
| 4 | T6 | general-purpose | sıralı |
| 5 | T7 | general-purpose | sıralı |
| 6 | 3 e2e senaryosu | test-engineer | sıralı |
| Kapı B | Son inceleme | reviewer | |

- **T1 Saf yardımcılar.** Yeni `components/xslt-editor/xsltText.ts`: `extractXmlParseErrors`, `XML_DECL_FIX`, `xmlDeclarationProblem`, `looksMinified`, `formatXmlForDisplay`, `buildErrorRegion`, `stylesheetVersion`, `formatSize`, `snippetPlainText`. `XsltSideMenu`'den `SnippetLibraryList`. `SPLIT_LAYOUTS` ve `separatorCls` → `editorLayouts.ts`. XsltEditorPage diff'i yalnız taşıma ve import.
- **T2 Hook'lar.** `hooks/useRawPreview.ts` (html, loading, error, ms, timings, engine, refresh; eski yanıt sıra sayacıyla atılır; satırlı hata callback'le iletilir). `hooks/useXsltDiagnostics.ts` (doğrulama debounce'ları, compat, declProblem, UBL-TR, problem listeleri, sayaçlar, marker'lar). XsltEditorPage bu görevde değişmez.
- **T3 Taslak.** Store, `components/dev-mode/ThemeDraftScope.tsx`, `App.tsx` layout route, `useDraftField`, ThemeUsePage bağlantısı (kararlar 2, 3, 5, 7, 8 + düzeltmeler), `useThemeCompose`'a `xslt`. Tema Kullan'ın görünümü aynı kalır, `xsltEdited` iken "Geliştirici Modu'ndaki XSLT düzenlemeleri uygulanıyor" uyarısı eklenir. Kaydedilmemiş `xsltEdited` varken Tema Kullan'da üst menü, kütüphane bağlantısı ve çıkış `confirm` sorar, `beforeunload` uyarır; iki ekran arası geçiş sormaz. Yalnız varlık girilmişse bugünkü davranış (uyarı yok).
- **T4 `hooks/useDevModeCompose.ts`.** Karar 6. Bozuk XSLT'de panelde "XSLT düzeltilince uygulanacak". Uyarılar state'te (panelde); `field` iletilir; 402 tek işleyiciye gider.
- **T5 `components/dev-mode/DevAssetsPanel.tsx`, `DevToolsSections.tsx`.** `AssetAccordionSection`, `ImagePanel hideTitle`, `BankInfoPanel hideTitle`, `FixedNotesPanel`, `imageState`, `fixedNoteState`; `XsltEditorSettings hideRightPanelMode`. `handleNoteChange` mantığı ThemeUsePage'den kopyalanır. `ThemeInfoAside` ve `themeChecklist` alınmaz.
- **T6 DevModePage kabuğu.** `useDevModeDocument`: `getTemplate` → `meta`; `workingXslt` yoksa `composeTheme({themeId, ...taslak, includeXslt:true})`; 402 yükseltme akışı, 402 dışı hatada `fetchThemeXslt`'e düş. XML: taslak, yoksa belge türüne göre örnek; Faz 1 kalkar; `getTemplate` başarısızsa XSLT'deki `DespatchAdvice-2`'ye bakılır. Kabuk: karar 9; `XsltEditorToolbar`, `PreviewDock`, `FloatingPreviewWindow`, `XsltStatusBar`, `ProblemsPanel`, `useEditorMaximize`, tıkla→satıra git, sürüm çipi, XML bildirim problemi. Koruma: `isDirty = workingXslt !== baselineXslt`, `guardedNavigate`, `confirmLeave`, `beforeunload`. Tek XML input; en fazla bir `PREVIEW_IFRAME_TITLE` iframe'i.
- **T7 Kaydetme.** `hooks/useTemplateSave.ts`: `SaveTemplateDialog`, `createUserXsltTemplate`, `updateUserXsltTemplate`, `canSaveRawXslt`, `parseGateError`/`openUpgradeModal`, Ctrl+S. `saved` store'da; ikinci kayıt günceller; Tema Kullan'ın kaydı da `saved` varsa günceller. 402/409/423 XSLT Editör metinleriyle. Kayıt baseline'ı sıfırlar.

### Dalga 2 (sıralı)

- **T8 AI.** Karar 10. `AiAssistantPanel`, `AiRefactorDialog`, `ProblemsPanel.onAskAi`; `openAiForProblem`'ın metin kurma kısmı `xsltText.ts`'e. AI uygulaması `setManualXslt`; sohbet sekme değişiminde korunur.
- **T9 Alanları göster + sürükle-bırak.** `ThemeSlotPreview`, `DraggableAssetChip`, `slotDnd`. "Alanları göster" açıkken ana bölmeyi örten %100 ölçekli görünüm; `PreviewDock showFrame={false}`, editör `inert` ama yer değiştirmez, `DndContext` ilk render'dan itibaren koşulsuz sarar, tam ekrana geçişte kapanır. `useDevModeCompose`'un yuva değişiminde `composeNow` çağırması.
- **T10 XsltEditorPage'i hook'lara geçir.** Bağımsız, geri alınabilir.
- **T11 Doküman.** `docs/ecc/architecture/frontend.md`, ADR-007 (compose rotası), `CHANGELOG.md`.

## İnceleme kapıları (yalnız yüksek/kritik)

- **Kapı 0 (security-reviewer):** `xslt` modlu render'ın süreç içine düşen yolu kaldı mı; `XsltSafety`/`SecureXslt` atlanıyor mu?
- **Kapı A (reviewer + security-reviewer):** Taslak boşken Tema Kullan istek gövdesi aynı mı? `themeId` ile `xslt` birlikte gidiyor mu? Taslak logout'ta ve tema değişiminde sızıyor mu? İptal sonrası geç yanıt store'a yazıyor mu?
- **Kapı B (reviewer):** Kullanıcı metni ezilebiliyor mu? Monaco remount oluyor mu? İkinci iframe ya da XML input var mı? Pro'da iki kayıt tek şablon mu? 402/409/423 doğru mu? Çıkış koruması tüm yolları kapsıyor mu?

## Sınırlar

- Monaco geri-al geçmişi sayfa geçişinde sıfırlanır.
- Yenileme onaylanırsa taslak gider (görseller nedeniyle `sessionStorage` kullanılmıyor).
- Tarayıcı Geri ile iki ekranın dışına çıkış korunmaz (`BrowserRouter` ile `useBlocker` yok; XSLT Editör'de de böyle).
- Worker kapalı ortamda 2.0 bildiren tema Raw'da `EngineUnavailable` verir (mevcut davranış).
- Değişmeyen kurallar: istemci XSLT'ye compose metni yazmaz, açıklama yorumu yok, görseller base64, sandbox `PREVIEW_SANDBOX` sabitinden.

## Test

Yalnız kritik. Backend: S1 için 1 test. `e2e/dev-mode.spec.ts`:
1. Kaydedilmemiş düzenlemeyle header gezinmesi `confirm` sorar; iptalde metin durur.
2. Geciktirilmiş compose yanıtı sırasında yazılır: metin ezilmez, varlık değişikliği uygulanır.
3. Tema Kullan ↔ Geliştirici Modu gidiş-dönüşünde (buton ve tarayıcı Geri) varlıklar ve elle düzenleme korunur; Tema Kullan'dan inen XSLT düzenlemeyi içerir; kütüphaneden yeniden açılış boş başlar.

Kapı: `npm run build`, `npm run lint`, `npm run test:e2e`.
