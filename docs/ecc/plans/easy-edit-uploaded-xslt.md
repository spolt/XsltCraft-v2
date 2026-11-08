# Plan — Ana ekrandan XSLT yükle → Kolay düzenle | XSLT Editör

> Durum: sahip onayladı (2026-11-07); K1–K13 önerilerle kabul. Mockup: "Ana ekrandan akış" tuvali.

## Sahip kararları (verildi)
- Kolay düzenle **Pro ve üstü** kullanıcılara açık. Hiyerarşi Admin → Editor → Pro → Free, tek Pro modeli var; **yeni entitlement bayrağı açılmaz**.
  - Koşul `EffectivePlan == Pro`. `EntitlementPolicy` Editor/Admin için de `EffectivePlan = Pro` döndürür, süresi geçen Pro ise Free'ye düşer.
  - Free kullanıcı kilitli kart ve upsell görür; Editör yolu açık kalır.
- Dashboard paneli başlığı: **"XSLT dosyanla çalış"**.
- Kayıtlı şablon **soru sormadan**, son çalışılan modda (Kolay düzenle / Editör) açılır.
- Tek soru adımında tespit edilen logo/imza görselleri **hazır yüklü** gelir; aynı görsel yeniden yüklenmeden **sürükle-bırakla** başka konuma taşınır ya da oraya da konur.

## Temel ilke
**Metin tek doğru kaynaktır.** Kolay düzenle ayarları metinden geri okunabilir:
- logo, imza ve gizleme → XsltCraft bloğundaki JSON;
- banka → bloktaki tablo;
- sabit notlar → `div[data-xc-fixed-note]`;
- görsel rolleri → `data-xc-placeholder` işaretleri.

Mod geçişi ve yeniden açılış yalnız metni taşır; analiz sonucu kalıcı saklanmaz.

## Akış
```
Dashboard: XSLT yuvası doldu
  → istemci parse kontrolü (bozuksa "Dosya hatalı" + satır; sunucuya gitmez)
  → Pro ve üstü değilse kilitli kart (çağrı yok)
  → POST /api/easy-edit/analyze { xslt }   [Authorize, rate limit, boyut, ThemeComposeXsltLimits, ayrı analiz kapısı, 402]
       sunucu: BOM/UTF-8 → XsltSafety → önceki blok incele + geri oku + temizle → görselleri bul
               → rol: önceki işaret > istemci kararı > öneri → AnalyzeAsync(route: Raw)
               → not desteği → görsel taşınabilir mi → verdict
       yanıt: verdict, işaretli xslt, slots(+conditional), images(+previewDataUrl, movable), previous{images,bank,notes}, features
  → kartlar: Kolay düzenle (çipler) | XSLT Editör
Kolay düzenle ile aç → handoff store → /easy-edit/:draftId
  → gerekiyorsa tek soru modalı (rol değişirse yeniden analyze)
  → çıkarılan görseller logo/imza varlığı olarak önceden yüklü (origin=extracted)
  → önizleme/indirme: POST /api/preview/compose { xslt: işaretli metin, assets(+extraSlots/keepThemeImage), conditionalSlots } (Raw)
Kolay → Editör: compose(includeXslt) → bileşik metin handoff → XsltEditorPage (pendingContent, dirty)
Editör → Kolay: metin değişmediyse önbellekteki analiz; değiştiyse yeniden analyze
Kaydet: UserXsltTemplate { ..., lastEditMode }  (CanSaveRawXslt gate aynen)
Yeniden aç: lastEditMode=EasyEdit && isProOrAbove → /easy-edit/template/:id (soru yok); aksi → /xslt-editor/:id
```

## Kodda doğrulananlar
1. **Tema analizi XSLT'yi çalıştırır.**
   - Yalnız ayrıştıran adımlar (görsel tespiti, çapalar, işaret yazımı) `SecureXslt.ReaderSettings()` kullanır.
   - `AnalyzeAsync` / `ThemeRenderVerifier` / `RenderProblemAsync` ise metni derleyip render eder. Bunlar `ThemeXsltRuns` üzerinden `XsltRoute.Theme` kullanır.
   - **Kullanıcı dosyası için analiz `XsltRoute.Raw` (izole worker + `IXsltSafetyScanner`) kullanmalı.**
2. **Compose `xslt` dalı katalog istemez.**
   - Yuvaları metindeki işaretlerden çıkarır (`ThemeSlotMarkers.SlotIdsIn`).
   - Katalog yalnız `CatalogDefaults` ve `ConditionalSlots` uyarısı için kullanılır.
   - `ThemeUsePage`'in düzenlenmiş-xslt yolu kataloğu zaten istemcide taşıyor (`themeDraftStore.meta.slots`).
3. **Görsellerin base64'ü hazır.** `ThemeImageDetector` görselin tam data URL'sini `PreviewDataUrl` olarak verir. Compose tarafında `ThemeImageValidator` yalnız PNG/JPEG, ≤500 KB ve ≤4000 px kabul eder.
4. **Önceki roller dosyada saklı.** Roller `data-xc-placeholder="logo|signature"` işaretlerinde durur ve `Clean` öncesinde okunabilir. Bu yüzden yeniden açılışta soru gerekmez. `OverridesBlockParser` `hide` alanını okumaz. Banka satırlarını geri okuyan bir okuyucu da yok.
5. **Yetki ve gate.** `EntitlementPolicy`: Editor ve Admin aynı muameleyi görür. Plan DB'den okunur. Compose `xslt` dalında entitlement gate'i yok (DevMode ücretsiz temalar bunu kullanıyor).
6. **Frontend durumu.**
   - `store/xsltEditorStore.ts` kullanılmıyor; `XsltEditorPage` yerel `useState` ile çalışıyor.
   - `ThemeDraftScope` çıkışta taslağı sıfırlıyor.
   - Örnek XML: `src/assets/default-invoice.xml` / `default-despatch.xml`; backend tarafında `ThemeSlots/Samples/invoice.xml`.
   - Kayıtlı şablon açılışı iki yerde: `utils/workRoutes.ts` → `xsltOpenPath`, ve `MyXsltTemplatesPage`.

## Dalgalar

### W1 — Backend analiz çekirdeği
*Kapı: security-reviewer + reviewer.*

- **1.1 `[backend]`** Analize rota parametresi eklenir:
  - `ThemeXsltRuns.RenderAsync/CompileAsync(route, …)`;
  - `ThemeRenderVerifier` rota alır;
  - `IThemeSlotAnalyzer.AnalyzeAsync(…, XsltRoute route = XsltRoute.Theme)`.

  Admin yolu varsayılanla aynı kalır.
- **1.2 `[backend]`** `ThemeSlots/ThemeDocumentTypeSniffer.cs`: `ThemeComposeController` içindeki despatch tespiti buraya taşınır.
- **1.3 `[backend]`** Geri okuyucular; hiçbiri XSLT çalıştırmaz.
  - `Analysis/PriorImageRoleReader.cs`: `data-xc-placeholder` → rol listesi, `Clean` öncesinde çalışır.
  - `Overrides/BankTableReader.cs`: `BankTableXml` → `BankInfoInput`; `SecureXslt.ReaderSettings()` kullanır, `MaxBankRows` ile sınırlı.
  - `Compose/FixedNoteReader.cs`: `div[data-xc-fixed-note]` → `FixedNoteInput`.
  - `OverridesBlockParser`: `hide`, `xs` ve `keep` alanlarını da okur.
- **1.4 `[backend]`** `Application/EasyEdit/` klasörü:
  - `UploadedXsltAnalyzer.cs`, `EasyEditModels.cs`, `EasyEditVerdictMapper.cs`.
  - Verdict'ler: `Suitable / Partial / Unsuitable / Invalid / Rejected / Unavailable`.
  - Akış: BOM → UTF-8 → `XsltSafety.FindThreat` → sniffer → `IThemeOverridesProcessor.Inspect` (geri oku + Strip) → `DetectImages` + rol önceliği (logo/imza: `Removable=true`, `Strip=false`) → `AnalyzeAsync(Raw)` → `FixedNoteWriter.Apply(…).Supported` → `IThemeImageValidator` ile `Movable` → çipler (`ThemeSlotLabels.Group`).
  - Yanıtta en fazla 20 görsel olur. Görsel verisi loglanmaz.
- **1.5 `[backend]`** ~~Yeni entitlement~~ — **düştü.**
  - Gate koşulu `IEntitlementService.GetAsync(...).EffectivePlan == MembershipPlan.Pro`.
  - Frontend'de karşılığı `isProOrAbove(e)` yardımcısı (`effectivePlan === 'Pro'`).
  - `MembershipOptions`, `MeController` ve appsettings değişmez.
- **1.6 `[backend]`** `Controllers/EasyEditController.cs` → `POST /api/easy-edit/analyze`.
  - Kontrol sırası:
    1. Authorize
    2. rate limit
    3. RequestSizeLimit
    4. `MaxXsltBytes`
    5. `imageRoles` doğrulaması
    6. 402 gate (Pro ve üstü)
    7. `ThemeComposeXsltLimits.Check`
    8. ayrı analiz kapısı (429 + `Retry-After`)
    9. servis
- **1.7 `[backend]`** DI ve politikalar:
  - `IUploadedXsltAnalyzer` singleton olarak kaydedilir.
  - Analiz kapısı `ThemeComposeGate` sınıfının ayrı bir örneğidir; `XsltExecutionOptions.EasyEditAnalyze.MaxConcurrent` (öneri 4).
  - Rate limit politikası `easy-edit-analyze`.

### W2 — Compose genişletmesi + DB
*Kapı: security-reviewer + reviewer.*

- **2.1 `[backend]`** "Taşı / buraya da koy":
  - `ThemeComposeAssetDto.ExtraSlots` (≤3) ve `KeepThemeImage` eklenir; `ThemeComposeRequest.ConditionalSlots` yalnız `xslt` dalında, ≤32 değer.
  - Dokunulan yerler: validator, `ThemeAssetComposer` (`xs`, `keep`) ve yeni `Compose/PlaceholderSourceDeduper.cs`. Deduper, aynı kaynaklı statik kopyayı `BlankImage` ile değiştirir; `keep` varsa dokunmaz.
  - `ThemeAssetRuntime.js` görseli ek yuvalara klonlar ve `keep` türünü gizlemez.
  - `themeId` dalının davranışı değişmez.
- **2.2 `[database]`** Yeni `Domain/Entities/XsltEditMode.cs { Editor, EasyEdit }` ve `UserXsltTemplate.LastEditMode`.
  - Kolon string olarak dönüştürülür, maksimum 20 karakter, varsayılan `Editor`.
  - Migration adı `AddUserXsltTemplateLastEditMode`. Önce build alınır; `--no-build` tuzağına dikkat.
- **2.3 `[backend]`** DTO'lar ve `UserXsltTemplateController`:
  - `lastEditMode` için whitelist: `editor|easyEdit`.
  - Update'te null gelirse değer değişmez; bulk işlemler `Editor` yazar.
  - `GateSaveAsync` aynen kalır.

### W3 — Frontend temeli
*Kapı: reviewer.*

- **3.1** Servisler:
  - yeni `easyEditService.ts`;
  - `entitlementService.ts` → `isProOrAbove(e)` yardımcısı;
  - `themeComposeService.ts` → `extraSlots`, `keepThemeImage`, `conditionalSlots`;
  - `userXsltService.ts` → `lastEditMode`.
- **3.2** Yeni `store/documentHandoffStore.ts`: kalıcı olmayan Zustand store'u. `draftId` başına tek seferlik tüketilir ve bir analiz önbelleği tutar. `themeDraftStore.bind` anahtarı `theme:<id>` / `doc:<draftId>` olur.
- **3.3** Yeni `components/easy-edit/EasyEditScope.tsx` ve rotalar:
  - `/easy-edit/:draftId` ve `/easy-edit/template/:templateId`;
  - handoff yoksa Dashboard'a yönlendirilir ve toast gösterilir.
- **3.4** `ThemeUsePage` ayrıştırılır:
  - gövde `components/theme-use/EasyEditWorkspace.tsx`'e taşınır;
  - kaynak bağdaştırıcısı `easyEditSource.ts`;
  - yeni sayfa `pages/UploadEasyEditPage.tsx`;
  - ortak bileşen `components/easy-edit/DocumentBar.tsx`.

  Tema akışı ve mevcut e2e testleri değişmeden kalır.

### W4 — Frontend özellikleri
*Kapı: reviewer + test-engineer.*

- **4.1 Dashboard:**
  - `XsltWorkspace` başlığı değişir ve "Boş editörü aç" eklenir;
  - `FileDropZone`'da XML isteğe bağlı olur ve kartlar gösterilir;
  - `editorFiles.ts` örnek XML ekler;
  - yeni `hooks/useUploadAnalysis.ts` (AbortController) ve `components/dashboard/EditChoiceCards.tsx`.

  Kart durumları: analiz ediliyor / uygun / kısmen uygun / uygun değil / dosya hatalı / güvenlik reddi / geçici hata / kilitli (upsell).
- **4.2** `components/easy-edit/ImageRolesDialog.tsx`: `ROLE_OPTIONS` paylaşılan bir modüle çıkarılır.
  - Modal yalnız işaretsiz görsel ya da önceki ekleme varsa açılır; kayıtlı şablonda asla açılmaz.
  - Taşınamayan görsel için "yerinde kalır" notu gösterilir; aynı rolde birden çok görsel varsa uyarı verilir.
- **4.3** Önceden yüklü görseller ve sürükle-bırak:
  - `imageState.ts`: yeni `origin`, `extraSlots`, `keepThemeImage` alanları. Dokunulmamış extracted görsel için asset gönderilmez.
  - `ThemeSlotPreview` placements yapısı tür → yuva listesine geçer.
  - Dokunulan diğer dosyalar: `slotDnd.ts`, drag-end; bırakınca "Buraya taşı / Buraya da koy" seçimi (klavyeyle erişilebilir).
  - `ImagePanel` "dosyadan alındı" rozeti gösterir.
- **4.4** `ThemeInfoAside` kaynak varyantı: analiz sonucu ve "Görsel seçimlerini değiştir" bağlantısı. Sol panele "Daha fazlası mı lazım?" kutusu eklenir.
- **4.5** `XsltEditorPage`:
  - `DocumentBar` ve mod anahtarı eklenir;
  - `pendingContent` handoff girişi;
  - kapatılabilir öneri bandı;
  - "Kolay düzenle'ye geç" butonu;
  - kilit ve heartbeat `hooks/useEditingLock.ts`'e taşınır.
  - İsteğe bağlı: `data-xc-slot` satırları için gutter işareti.
- **4.6** Kaydet ve yeniden aç:
  - kaydetme `lastEditMode` değerini yazar;
  - açılış yolu `xsltOpenPath(id, lastEditMode, isProOrAbove)` olur (`useRecentWork` ve `MyXsltTemplatesPage`);
  - yetkisiz kullanıcı şablonu Editör'de açar ve bir toast görür.

### W5 — Belgeler
- `docs/ecc/architecture/{backend,frontend,database}.md` güncellenir.
- İsteğe bağlı ADR: `009-easy-edit-uploaded-xslt.md`.
- CHANGELOG'a yalnız özellik notu girer.

## Açık kararlar (öneri kalın)
| # | Konu | Öneri |
|---|---|---|
| K1 | Katalog compose'a nasıl gider | **İstemci işaretli metin + `conditionalSlots` taşır.** Sunucu oturumu ve her önizlemede yeniden analiz seçenekleri elendi. |
| K2 | Analiz sonucu saklanır mı | **Hayır, yalnız `LastEditMode` saklanır.** Açılışta bir kez analiz yapılır. |
| K3 | Görsel varlıkları | **İlk sürümde oturumda tutulur.** Kalıcı kütüphane sonraki faz (Asset + IStorageService). |
| K4 | Tek soru modalının yeri | **Kolay düzenle ekranı.** Sağ panelden yeniden açılabilir. |
| K5 | Editör modu hangi sayfada | **XsltEditorPage + metin handoff.** DevModePage genişletilmez. |
| K6 | Dosyadaki logo/imza | **(2026-11-07 revize) XSLT metninde statik taşınır.** İşaretleme hedef konuma taşınır; boşaltma yok, betik bağımlılığı yok. "Buraya da koy" metne ikinci kopya ekler (boyut uyarısı devrede). Kullanıcının yeni yüklediği görseller hazır temalardaki gibi betikle yerleşir. |
| K7 | "Buraya da koy" | **İlk sürümde, varlık başına ≤3 ek konum.** |
| K8 | Free kullanıcıda analiz | **Yapılmaz.** Kilitli kart gösterilir. |
| K9 | Compose `xslt` dalına gate | **Eklenmez.** Gate analiz uç noktasında. |
| K10 | `LastEditMode` ne zaman yazılır | **Kayıtta.** |
| K11 | Güvenlik reddi ayrı durum mu | **Evet.** Ayrıca "geçici hata / tekrar dene" durumu eklenir. |
| K12 | XML'siz "Editörde aç" | **Örnek XML eklenir** ("Örnek fatura XML" etiketiyle). |
| K13 | Paylaşılan şablonda son mod | **Şablon başına tek kolon.** |

## Dikkat edilecek noktalar
- **Rota:** Kullanıcı dosyası analizi Raw rotada kalmalı (testle kilitlenecek). Admin analiz yolu bundan etkilenmemeli.
- **Kaynak sınırları:** Ayrıştırmadan önce yapısal sınırlar uygulanmalı. Ayrı kapı ve hız sınırı gerekli; kapasite ölçülerek ayarlanmalı.
- **Çıktı:** Boyut hedefi ~250 KB; `assets-large` uyarısı korunmalı. Runtime JS gömülebilirlik kurallarına uymalı. Çıktıda namespace'ler ve `version="2.0"` korunmalı; görseller base64 kalmalı ve yorum olmamalı.
- **Kullanıcıya uyarılar:** Önceki blok tanınmazsa ya da aynı rolde birden çok görsel varsa modal açık uyarı vermeli. PNG/JPEG dışı veya büyük görseller yerinde kalır.
- **Oturum:** Bellek içi oturum sayfa yenilenince kaybolur. Bu durumda yönlendirme yapılır ve `beforeunload` uyarısı gösterilir.
- **Yanıt ve log içeriği:** Görsel verisi ya da dosya içeriği yanıtlara ve loglara girmemeli; güvenlik reddi mesajı ayrıntı vermemeli.

## Kritik testler
1. `ThemeSlotAnalyzerTests`: `route: Raw` verilince tüm yürütmeler Raw olur; varsayılan Theme kalır.
2. `XsltWorker.Tests/Api/EasyEditRouteTests.cs`: yalnız Raw yürütme; sınır aşımı → 400 ve yürütme yok; tehdit → Rejected; kapı dolu → 429; Free → 402.
3. Gate testi (`EasyEditRouteTests` içinde): Free ve süresi geçmiş Pro → 402; Pro, Editor ve Admin geçer.
4. `Application.Tests/EasyEdit/UploadedXsltAnalyzerTests.cs`: önceki işaret → rol; compose gidiş-dönüşü; `Movable`; verdict eşlemesi.
5. `ThemeComposeRequestValidatorTests` + `ThemeAssetComposerTests`: ExtraSlots, Keep, ConditionalSlots; base64'ün tek kopya kalması; aynı kaynaklı kopyanın boşaltılması; `themeId` dalının değişmemesi.
6. e2e `theme-compose.spec.ts`: taşı, buraya da koy, keep.
7. `LastEditMode` controller testi: varsayılan değer, update ve null davranışı, liste DTO'su.
8. e2e `easy-edit.spec.ts`: yükle → Kolay düzenle → kaydet → Şablonlarım'dan soru sorulmadan açılış; Free kullanıcıda kilitli kart.
