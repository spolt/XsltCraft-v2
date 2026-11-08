# ADR 009 — Kolay düzenle: yüklenen kullanıcı XSLT'sinin kod yazmadan düzenlenmesi

**Durum:** Kabul edildi (2026-11-07) · İlgili: ADR 007 (motor yönlendirme), ADR 008 (worker izolasyonu) · Plan: `docs/ecc/plans/easy-edit-uploaded-xslt.md`

## Bağlam

Hazır temalarda logo, imza, banka bilgisi ve sabit notlar kod yazmadan düzenlenebiliyordu (Tema Kullan, `POST /api/preview/compose`). Kendi XSLT dosyasını getiren kullanıcı ise yalnız XSLT Editör'de, kodla çalışabiliyordu. Hedef: ana ekrandan yüklenen dosyayı aynı altyapıyla düzenlemek, gerektiğinde Editör'e geçip geri dönebilmek ve kaydedilen şablonu son kullanılan modda açmak.

Çıkış noktası olgular:
- Tema analizi (`ThemeSlotAnalyzer`, `ThemeRenderVerifier`) metni derleyip render eder ve `XsltRoute.Theme` (süreç içi) kullanıyordu. Bu yol admin'in yüklediği temalar içindir; kullanıcı dosyası için uygun değildir.
- Compose'un `xslt` dalı yuvaları metindeki işaretlerden çıkarır; katalog yalnız uyarılar için gerekir.
- Önceki Kolay düzenle ayarları zaten metinde durur: görsel rolleri `data-xc-placeholder`, kullanıcı görselleri override bloğundaki JSON, banka bloktaki tablo, notlar `div[data-xc-fixed-note]`.

## Karar

1. **Yüklenen kullanıcı XSLT'si izole Raw rotasında çalışır.** `POST /api/easy-edit/analyze` ve Kolay düzenle önizlemesi (compose `xslt` dalı) derleme/render için `XsltRoute.Raw` (worker havuzu, ADR 008) kullanır. Analiz zincirindeki `route` parametresi zorunludur (`ThemeXsltRuns`, `ThemeRenderVerifier`, `IThemeSlotAnalyzer.AnalyzeAsync`); admin tema yolu `Theme`'de kalır. Analiz ucunda boyut ve yapısal sınırlar, hız sınırı ve ayrı eşzamanlılık kapısı uygulanır.
2. **Metin tek doğru kaynaktır.** Ayarlar metinden geri okunur (`PriorImageRoleReader`, `BankTableReader`, `FixedNoteReader`, `OverridesBlockParser` `hide`/`xs`/`keep`). Analiz sonucu saklanmaz; açılışta bir kez yeniden üretilir. DB'de yalnız `UserXsltTemplate.LastEditMode` (`Editor`/`EasyEdit`) tutulur. Mod geçişi ve yeniden açılış yalnız metni taşır; istemci tarafı aktarım ve analiz önbelleği bellek içidir (`documentHandoffStore`).
3. **Dosyadaki görseller metinde statik taşınır.** Kullanıcının dosyasında hazır bulunan logo/imza, compose'un `staticImages` alanıyla XSLT metninde taşınır ya da silinir (`StaticImageMover`; betik bağımlılığı yok, işaret korunur). Kullanıcının yeni yüklediği görseller (bir serbest görsel dahil) hazır temalardaki gibi `ThemeAssetRuntime.js` ile yerleşir. Dosyada yazılı TR IBAN satırları da metinde düzenlenir (`DocumentBankEditor`). Her compose çağrısı analizin metnini ve kümülatif `staticImages` listesini gönderir; kaydedilen metin son compose çıktısıdır.
4. **Erişim Pro ve üstü.** Koşul `EffectivePlan == Pro` (Editor/Admin aynı muameleyi görür, süresi geçen Pro Free'ye düşer); ayrı entitlement bayrağı açılmadı. Kapı analiz ucundadır (402); compose `xslt` dalına kapı eklenmedi. Free kullanıcı kilitli kart görür, XSLT Editör yolu kullanılabilir kalır. Kayıt kapısı (`CanSaveRawXslt`) değişmedi.

## Sonuçlar

- **+** Kullanıcı dosyası yeni bir yürütme yolu açmadan mevcut izole hattan geçer; admin tema hattı değişmez.
- **+** Şema genişlemesi tek kolondur; analiz formatı değişse de eski kayıtlar etkilenmez. Şablon soru sorulmadan, son modda açılır.
- **+** Statik taşıma sonrası çıktı betikten bağımsızdır; dosyadaki görselin base64'ü tekrar yüklenmez.
- **−** Her açılış ve metin değişikliği bir analiz çağrısıdır (istemci önbelleği değişmeyen metinde tekrarı önler).
- **−** Bellek içi aktarım sayfa yenilemesinde kaybolur; kullanıcı Dashboard'a yönlendirilir, çıkışta uyarı gösterilir.
- **−** Metne yazılan her değişiklikte boyut uyarısı (`assets-large`) ve `xslt-too-large` sınırı devrededir.

## Elenen alternatifler

| Alternatif | Neden elendi |
|---|---|
| Dosyadaki görseli runtime ile taşımak, statik kopyayı boşaltmak (deduper) | Çıktı betiğe bağımlı kalıyor, boşaltılmış `img` artıkları ve iki temsil (statik + JSON) arasında tutarlılık sorunu doğuyordu. Statik taşıma daha sade ve öngörülebilir |
| Sunucu tarafı oturum (analiz/katalog sunucuda tutulur) | Durum yönetimi ve temizlik yükü; çok sekme ve yeniden başlatmada kırılgan. İstemci işaretli metni ve `conditionalSlots`'u taşır |
| Analiz sonucunu DB'de saklamak | Metinle drift riski; analiz kuralları değiştikçe göç gerekir. Metinden geri okuma yeterli |
| Her önizlemede yeniden analiz | Gereksiz maliyet; analiz bir kez, compose her değişiklikte |
| Ayrı entitlement bayrağı | Tek Pro modeli var; ek bayrak yönetim yükü getirir, karşılığı yok |
| Editör modu için `DevModePage`'i genişletmek | `XsltEditorPage` + metin aktarımı yeterli; tema taslak akışı etkilenmez |
