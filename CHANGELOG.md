# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

---

## [1.1.0] - 2026-05-01

### Added
- **`PromptRegistry`** (`Ai/PromptRegistry.cs`): Assembly embedded resource'lardan lazy-loading ile UBL-TR prompt içeriklerini yükleyen statik registry. YAML front-matter (`id`, `triggers`) regex ile parse edilir; pattern'ler `IReadOnlyDictionary<string, PromptPattern>` olarak double-check lock ile önbelleklenir.
- **`PromptPattern` record** (`Ai/PromptRegistry.cs`): `Id`, `Triggers`, `Content` üçlüsünü temsil eden değer tipi — Madde 2 `PatternSelector` tarafından tüketilecek.
- **`AiMode` enum** (`Ai/AiMode.cs`): `Refactor | Assistant` — Madde 3 `BuildMessages` için zemin.
- **Embedded markdown prompt dosyaları** (`Prompts/`):
  - `Core/Identity.md` — Mühendis rolü, UBL-TR namespace tabanı, görev tanımı, güvenlik kuralları.
  - `Core/Constraints.md` — Çıktı formatı ve kısıtlamalar (eski `CommonConstraints` içeriği).
  - `Patterns/InvoiceNote.md` — `cbc:Note` görünürlük pattern'i.
  - `Patterns/SupplierPartyPerson.md` — `AccountingSupplierParty` kişi bilgisi pattern'i.
  - `Patterns/CustomerPartyPerson.md` — `AccountingCustomerParty` kişi bilgisi pattern'i.
  - `Patterns/SupplierPartyAddress.md` — `AccountingSupplierParty` adres/iletişim pattern'i.
  - `Patterns/CustomerPartyAddress.md` — `AccountingCustomerParty` adres/iletişim pattern'i.
  - `Patterns/InvoiceHeader.md` — Fatura başlık alanları pattern'i (15 alan, [A]/[B] sınıflandırması).
  - `Patterns/InvoiceLine.md` — InvoiceLine tablo kolonları pattern'i (18 kolon + değişken tablosu).
  - `Patterns/LegalMonetaryTotal.md` — Dip toplamları pattern'i (15 satır + SGK değişken tablosu).

### Changed
- `XsltCraft.Application.csproj`: `<EmbeddedResource Include="Prompts\**\*.md" />` eklendi; `<InternalsVisibleTo Include="XsltCraft.Application.Tests" />` ile test projesine internal erişim açıldı.
- `Ai/PromptTemplates.cs`: ~535 satır `SystemRules`/`CommonConstraints` const string kaldırıldı; `Build()` ve `BuildAssistant()` thin wrapper'lara indirgendi. `BuildMessages(req, mode)` internal unified builder eklendi.
- `Ai/PatternSelector.cs`: Türkçe karakter folding (ı→i, ş→s, ç→c, ğ→g, ü→u, ö→o) + substring trigger matching + XSLT content signal (+1 skor) ile en fazla 4 pattern seçimi; fallback: `invoice-header + invoice-line`.
- Tüm 8 pattern `.md` trigger listeleri compound trigger'lara dönüştürüldü (overlap giderme: `satıcı kişi` / `satıcı adres`, `alıcı kişi` / `alıcı adres` vb.).
- `XsltCraft.slnx`: `XsltCraft.Application.Tests` projesi solution'a eklendi.

### Tests
- **`XsltCraft.Application.Tests`** projesi oluşturuldu (xunit 2.9.3, Verify.Xunit 31.12.5, net10.0).
- `PromptRegistryTests` (5 test): Identity/Constraints non-empty + 8 pattern yükleme + count = 8 doğrulaması.
- `PatternSelectorTests` (12 test): 10 `TheoryData` case, XSLT signal testi, max-4 pattern sınırı.
- `BuildMessagesGoldenTests` (3 Verify snapshot): `Refactor_RefactorSelection`, `Assistant_FirstTurn`, `Assistant_ThirdTurn_WithHistory` — mesaj listesi tamamen snapshot'lanmış.
- **Toplam: 26/26 test geçti.**

> **Sprint 1 (P0) tamamlandı.** Tüm 4 madde (`PromptRegistry`, `PatternSelector`, `BuildMessages`, golden testler) `develop` branch'ine merge edildi.

---

## [1.0.0] - 2026-04-26

### Added
- **AI Asistan**: XSLT editörüne entegre AI asistan paneli (`AiAssistantPanel`); sohbet geçmişi, NDJSON streaming yanıt, iptal (AbortController) ve toast bildirimleri. Sağ sidebar'dan açılır; UBL-TR e-Fatura/e-Arşiv bağlamında Türkçe yanıt verir.
- **AI Refactor Diyaloğu**: Editörde seçili XSLT bölümü için before/after diff önizlemeli `AiRefactorDialog`; kullanıcı onayı sonrası seçili aralık güvenle değiştirilir.
- **AI Provider Orchestrator**: Birincil sağlayıcı Ollama (`qwen2.5-coder:7b`), opsiyonel cloud fallback Gemini (`GeminiAssistantProvider`). `ConnectTimeout` ve `FirstTokenTimeout` için iki ayrı `CancellationTokenSource` zinciri ile streaming kesilmesi engellenir.
- **AI Endpoint'leri** (`/api/ai`):
  - `GET /api/ai/status` — AI etkin mi (UI butonlarını gizler/gösterir, `[AllowAnonymous]`).
  - `POST /api/ai/assistant` — Sohbet endpoint'i, NDJSON streaming, rate-limit `ai-assistant`.
  - `POST /api/ai/refactor-selection` — Seçili XSLT aralığını refactor eder.
- **Admin Feature Flag Yönetimi** (`/api/admin/feature-flags`): `GET|PUT /ai` (AI özelliğini açıp kapama), `GET /ai/health` (sağlayıcı sağlık durumu), `GET|PUT /ai/provider` (aktif sağlayıcı seçimi), `GET /ai/usage?date=YYYY-MM-DD` (günlük token/istek kullanımı).
- **Admin AI sayfası**: `/admin/ai` — feature flag toggle, sağlayıcı sağlık paneli, sağlayıcı seçimi, günlük kullanıcı bazlı token/istek kullanım raporu.
- **`FeatureFlag` entity ve migration** (`AddFeatureFlag`): Veritabanı tabanlı feature flag altyapısı; AI özelliği için runtime toggle.
- **`UserAiUsage` entity ve migration** (`AddAiUsageAndFlagValue`): Kullanıcı başına günlük AI istek sayısı ve token bütçesi takibi (`IAiTokenBudgetService`).
- **Toast bildirim sistemi**: Global `toastStore` (Zustand) ve `ToastContainer` bileşeni; AI hata/iptal akışları başta olmak üzere uygulama genelinde kullanılabilir.
- **AI rate limiting**: `Program.cs` içinde `ai-assistant` policy — kullanıcı başına eş zamanlı AI streaming isteklerini sınırlar.
- **Editör AI entegrasyonu**: `XsltEditorPage` ve `Xslteditor` bileşenlerinde AI panel toggle, seçim tabanlı refactor tetikleyici ve `ProblemsPanel` AI önerisi entegrasyonu.

### Changed
- `appsettings.json` ve `appsettings.Development.example.json` dosyalarına `Ai` bölümü (Ollama + Gemini config) eklendi.
- Sidebar'a "AI Asistan" ve admin altına "AI Yönetimi" menü öğeleri eklendi.

---

## [0.9.0] - 2026-04-25

### Added
- **Taslaklarım Önizleme Paneli**: Taslaklarım listesinde her satırda önizleme ikonu (Eye) eklendi; tıklandığında sağda iframe split-panel açılır, varsayılan e-Fatura XML ile blok ağacı render edilir. "Editörde Düzenle" butonuyla doğrudan editöre geçiş sağlanır.
- **Şablonlarım Önizleme Paneli**: XSLT şablonları listesinde aynı split-panel önizleme akışı; XSLT içeriği `POST /api/preview/raw` üzerinden render edilir. "XSLT Editörde Aç" butonuyla editöre geçiş.
- **`TemplatePreviewPanel` bileşeni**: Taslaklarım ve Şablonlarım sayfaları arasında paylaşılan yeniden kullanılabilir önizleme paneli; şablon adı, yükleniyor göstergesi, aksiyon butonu ve kapat kontrolü içerir.
- **`POST /api/preview/user-template/{id}` endpoint'i**: Kullanıcının blok ağacı tabanlı (V1 ve V2) şablonlarını backend'de önizlemek için yeni endpoint; V1/V2 format tespiti ve XSLT üretimi backend'de gerçekleşir.
- **`previewFromUserTemplate` servis fonksiyonu**: Frontend'de yeni endpoint'i çağıran `previewService.ts` fonksiyonu.
- **Admin Kullanıcı Detay Paneli**: Kullanıcı listesinde ⋯ menüsüne "Detaylar" seçeneği eklendi; sağdan kayan panel ile kullanıcı profili, istatistikler (kaydetme/indirme sayısı), son giriş/aktivite tarihi ve aksiyon butonları gösterilir.
- **Kullanıcı adına göre arama**: Admin kullanıcı listesi arama kutusuna `username` alanı eklendi; e-posta, isim ve kullanıcı adına göre filtreleme destekleniyor.

### Changed
- Admin panel kullanıcı listesinde kullanıcı adı (`@username`) birincil isim olarak gösteriliyor; e-posta ikincil satıra taşındı.
- Admin kullanıcı action menu dropdown'u viewport sınırını aşan durumlarda yukarı açılacak şekilde `getBoundingClientRect` + `position:fixed` ile konumlandırılıyor.
- Sidebar'da admin "Temalar" menü etiketi → "Hazır Şablonlar" olarak güncellendi.

### Fixed
- **V1/V2 blok ağacı tespiti (`XsltGeneratorService`)**: `GenerateFromJson` her zaman V1 mantığını çalıştırıyordu; V2 şablonlarında (`version: 2`) boş XSLT üretilip önizleme boş görünüyordu. `version` alanına göre `GenerateV2()` / `Generate()` dallanması eklendi.
- **Editör sayfası açılmıyordu (`EditorPage`)**: Taslaklarım'dan şablon açıldığında sayfa boş kalıyordu; üç ayrı hata vardı:
  - `handleNamePromptConfirm` (`useCallback`) `if (isLoading) return` erken dönüşünden sonra tanımlanıyordu → React hook sırası ihlali → bileşen çöküyordu. `useCallback` erken dönüş öncesine taşındı.
  - `isLoading` başlangıç değeri `false` olduğu için Canvas, Zustand store'undaki eski blokları yükleme ekranı gösterilmeden önce render ediyordu. `useState(!!routeTemplateId)` ile mevcut şablon açılışında yükleme ekranı anında gösterilmesi sağlandı.
  - Yükleme başlamadan önce `resetTree()` çağrılmadığından önceki editör oturumunun bayat blokları yeni şablonun üzerine biniyordu.

---

## [0.8.0] - 2026-04-25

### Added
- **Admin Kullanıcı Yönetimi**: `/admin/users` sayfası; kullanıcı listesi (kullanıcı adı, e-posta, rol, durum, kayıt tarihi, son login, kullanım sayıları), rol atama (Admin / Editor / User), aktif/pasif toggle, manuel kullanıcı oluşturma, şifre sıfırlama ve silme.
- **Editor rolü**: `UserRole` enum'una `Editor` değeri eklendi (yetki kapsamı sonraki sürümde netleşecek).
- **Kullanım takibi**: `UserActivity` tablosu — kullanıcı başına kaydetme ve indirme olayları izleniyor; admin paneli özet kartında toplam aktivite sayısı görünür.
- **Kullanıcı son login takibi**: `User.LastLoginAt` alanı; her başarılı girişte güncellenir.
- **Admin özet kartları**: Aktif kullanıcı sayısı, pasif kullanıcı sayısı ve toplam aktivite sayısı (tüm sistem toplamı, sayfa bazlı değil).
- **Kullanıcı adı ile giriş**: Giriş ekranı artık e-posta yerine kullanıcı adı (username) kullanır. Kayıt ekranına kullanıcı adı alanı eklendi; Google ile giriş yapan yeni kullanıcılar için e-posta önekinden otomatik benzersiz kullanıcı adı üretilir.

### Changed
- `POST /api/auth/login` artık `email` yerine `username` alanı alır.
- `POST /api/auth/register` artık zorunlu `username` alanı içerir (3-30 karakter, harf/rakam/alt çizgi).
- Admin paneli "Kullanıcıları Yönet" menü öğesi kenar çubuğuna eklendi.

### Security
- Pasif kullanıcılar login ve refresh endpoint'lerinde 403/401 döner; mevcut access token süresi dolana kadar çalışmaya devam eder.
- Şifre sıfırlama ve kullanıcı deaktivasyonunda tüm refresh token'lar revoke edilir.
- Admin kendi hesabını silemez, deaktive edemez veya rolünü değiştiremez.

---

## [0.7.0] - 2026-04-24

### Added
- **Admin Snippet Kütüphanesi**: Admin kullanıcılar `/admin/snippets` sayfasından tüm kullanıcılara görünecek global snippet'lar oluşturabilir, düzenleyebilir ve silebilir. `UserSnippet.IsPublic` alanı etkinleştirildi — veritabanı migration gerekmedi.
- **Kütüphane bölümü (SnippetManagerDialog)**: Kullanıcı snippet yöneticisinde kişisel snippet'ların altında "XsltCraft Kütüphanesi" başlığıyla ayrılmış bölüm; kütüphane snippet'ları mor yıldız ikonu ile işaretli, düzenleme/silme butonları gizli.
- **Admin paneli Snippet Kütüphanesi menüsü**: Ana kenar çubuğundaki Admin Paneli accordion'una "Snippet Kütüphanesi" alt öğesi eklendi (`/admin/snippets`); XSLT Editör kenar çubuğuna da kütüphane ikonu bağlantısı eklendi.
- `GET /api/user-snippets` artık kullanıcının kendi snippet'larına ek olarak tüm `IsPublic=true` snippet'ları da döner (kişisel önce, kütüphane sonra).
- `GET|POST|PUT|DELETE /api/admin/snippets` endpoint'leri — yalnızca Admin rolüne açık; PUT/DELETE yalnızca `IsPublic=true` kayıtlara izin verir.

---

## [0.6.1] - 2026-04-23

### Fixed
- **GİB karekod JSON tek satır**: Yeni şablon oluşturmada karekod bloğu eklendiğinde üretilen XSLT'deki JSON payload artık tek satırda yazılıyor. Önceki çok satırlı yapıda virgüller arasında boşluk/satır sonu oluşuyordu; bu durum karekodun hatalı okunmasına yol açabiliyordu.

---

## [0.6.0] - 2026-04-22

### Added
- **Blok kenar boşlukları**: `GridBlockLayout`'a `marginTop`, `marginBottom`, `marginLeft`, `marginRight` (mm) alanları eklendi; PropertyPanel'de "Kenar Boşlukları" bölümünden düzenlenebilir. Değerler önizleme çıktısında `padding + box-sizing:border-box` olarak uygulanır — canvas konumunu ve sweep algoritmasını etkilemez.
- **Z-index overlay katmanı**: `zIndex > 0` olan bloklar sweep akışından ayrılır; `.page` üzerine `position:absolute` ile render edilir. Birden fazla overlay bloğu CSS `z-index` sırasıyla üst üste binebilir; canvas konumlarıyla birebir örtüşür.

### Fixed
- **Divider/Spacer band-spanning**: `Divider` ve `Spacer` tipleri genişliklerinden bağımsız olarak her zaman sweep band'ini kapatır. Geniş ama full-width eşiğini aşmayan Divider'ların (ör. 155 mm) sonraki dar blokları kendi kolonuna emmesi önlendi; yan yana dizilim doğru çalışıyor.
- **İlk blok kolon hizalaması**: Kolon içindeki ilk bloğun `margin-top` değeri band tepesinden (Y=0) değil, bloğun kendi Y koordinatından hesaplanıyor (`bgl.Y - bandTop`). Farklı Y'lerdeki bloklar artık canvas'taki konumlarıyla hizalanıyor.
- **Full-width blok sonrası yan yana dizilim**: Full-width bir bloktan (ör. Fatura Satırları) sonra gelen dar bloklar kendi band'lerinde yan yana yerleşebiliyor; önceki band sweep'i tarafından emilme sorunu giderildi.

---

## [0.5.1] - 2026-04-21

### Fixed
- **V2 grid-canvas preview taşma**: `editorStore.addBlock` ve `updateBlockGridLayout` artık `clampToPage` ile A4 sınırlarına kilitleniyor (drag / resize / PropertyPanel input tek noktadan).
- **V2 blok üst üste binme**: `XsltGeneratorService.BuildBodyV2` şerit (band) + sütun akış algoritmasıyla yeniden yazıldı. Bloklar Y'ye göre taranır, X-kesişimine göre sütunlara eklenir; birden çok sütunla kesişen geniş blok (ör. Fatura Satırları) yeni bir şerit başlatır. `autoHeight` blok büyüdüğünde altındaki bloklar ve sonraki şeritler doğal olarak aşağı kayar — üst üste binmez, WYSIWYG korunur.
- **.page CSS**: `position:relative` + `overflow:hidden` eklendi; A4 dışı taşmalar kırpılır.

### Removed
- `GroupIntoRows` / `RenderV2Row` ölü kodu (~80 satır) temizlendi.

---

## [0.5.0] - 2026-04-19

### Added
- **UBL-TR İş Kuralları Doğrulama**: 40+ UBL-TR 2.1 iş kuralı motoru (`UblTrBusinessRuleService`); zorunlu alan, tutar tutarlılığı ve vergi doğrulaması; ihlaller satır numarasıyla listeleniyor
- **Sekmeli Problemler Paneli**: XML ayrıştırma hataları, XSLT doğrulama hataları ve UBL-TR iş kuralı ihlalleri tek panelde; sekme başlıklarında badge sayacı
- **XPath Konsolu**: Monaco editörlü interaktif XPath 1.0 sorgu paneli; namespace-aware; sonuçlar node tipi, adı ve değeriyle gösteriliyor
- **XML Kaynak Sekmesi**: Editörde XSLT ↔ XML kaynak görünümü arasında hızlı geçiş
- **XSLT Profiler**: Transform süresi ölçümü, yavaş bölge tespiti ve performans önerileri
- **Snippet Kütüphanesi**: Kullanıcıya özel XSLT snippet oluşturma/düzenleme/silme; `Ctrl+Space` ile editörde autocomplete; tam CRUD API (`/api/user-snippets`)
- **Önizleme Zoom Kontrolü**: %50–%200 arası 7 kademe zoom (ZoomIn / ZoomOut / %100 reset); önizleme paneli başlığında kompakt kontrol çubuğu
- **Seçili Satırları Biçimlendirme**: Sağ tık menüsünden seçili satırları XML-fragment farkındalıklı biçimlendirme; geçersiz fragmentleri geçici root ile sarar
- **Klavye Kısayolları Diyaloğu**: Tüm editör kısayollarını listeleyen `ShortcutsDialog`; toolbar'dan erişilebilir
- **About Sayfası**: Uygulama versiyonu, teknoloji stack'i ve bağlantılar
- `UblTrController`: `POST /api/ubltr/validate` endpoint'i
- `XPathController`: `POST /api/xpath/evaluate` endpoint'i
- `UserSnippetsController`: Snippet CRUD endpoint'leri
- `XPathEvaluator`: Namespace-aware XPath 1.0 değerlendirici
- `AddUserSnippet` migration: `UserSnippets` tablosu
- `docker-compose.prod.yml`, `Dockerfile`'lar, `nginx.conf`, `update.sh` — production altyapısı
- `vite-env.d.ts`: Vite ortam değişkeni tip tanımları

### Fixed
- Pre-commit hook false-positive: `vite-env.d.ts` artık `.env` kalıbıyla eşleşmiyor
- Pre-push hook false-positive: `${POSTGRES_PASSWORD}` env var referansı artık secret olarak işaretlenmiyor
- `dotnet format` whitespace hataları: `XPathModels.cs`, `XPathEvaluator.cs`, `XsltGeneratorService.cs`

---

## [0.4.0] - 2026-04-16

### Added
- **V2 Grid Canvas**: Bloklar artık A4 sayfası üzerinde serbest X/Y koordinatlarıyla (mm) yerleştiriliyor; eski bölüm tabanlı (V1) editörün yerini aldı
- **Satır-gruplu akış render**: Önizleme XSLT'sinde `z-index=0` bloklar Y-aralığı örtüşme tabanlı satır gruplama algoritmasıyla `flex-row` olarak render ediliyor; `autoHeight` bloklar altındakileri aşağı itiyor
- **Overlay katmanı**: `z-index>0` olan bloklar `position:absolute` ile üst üste bindirilebilir; yüksek z-index öndedir
- **ResizeHandles**: Seçili bloğu 8 yönde pixel-perfect yeniden boyutlandırma
- **Klavye navigasyonu**: Seçili blok yön tuşlarıyla 1 mm, `Shift` ile 5 mm hareket ettirilebilir
- **Undo/Redo**: `Ctrl+Z` / `Ctrl+Y` ile 20 adım geri/ileri
- **Blok kopyalama**: `Ctrl+D` / `duplicateBlock` ile seçili blok +5 mm offset ile kopyalanır
- **Z-index kontrolü**: Property panel'den blok öne/arkaya gönderilir; negatif değer engellendi
- **PartyInfo bölünmesi**: Palette'te "Satıcı Bilgileri" ve "Alıcı Bilgileri" olarak iki ayrı hazır blok; `configOverride` pipeline ile önceden yapılandırılmış gelir
- **Türkçe blok etiketleri**: Canvas üzerindeki tüm blok adları Türkçeleştirildi
- **MinIO depolama**: Faz 6 — MinIO kurulumu ve secret yönetimi; dosya depolama S3-uyumlu MinIO'ya taşındı
- **XSLT editör iyileştirmeleri**: Auto-format, folding range provider ve UI geliştirmeleri
- **XML dosya boyutu doğrulaması**: Yüklenen XML dosyaları için boyut sınırı ve istek limiti artırıldı
- `gridSnap.ts`: `snapToGrid`, `clampToPage`, `pxToMm` yardımcı fonksiyonları
- `treeMigration.ts`: V1 (bölüm tabanlı) → V2 (grid tabanlı) otomatik migrasyon yardımcısı
- `canvasRefs.ts`: `canvasPageRef`, `canvasScaleRef`, `CANVAS_DROPPABLE_ID` paylaşılan modül ref'leri

### Changed
- Tüm blok tipleri için varsayılan `autoHeight: true`; yalnızca Divider, Spacer, GibKarekod, GibLogo sabit yükseklik
- Divider varsayılan yüksekliği 3 mm → 10 mm
- V2 sayfa stili: `height: auto; min-height: 297mm; overflow: hidden` — içerik uzadığında sayfa büyür, print'te doğal sayfalama
- Palette VERİ kategorisinden "KDV Özeti" ve "Toplamlar" kaldırıldı
- `addBlock` aksiyonu isteğe bağlı `configOverride` parametresi kabul ediyor

### Fixed
- Önizlemede auto-height blokların altındaki blokların üst üste binmesi giderildi
- Yön tuşlarının ilk hareketten sonra yanıt vermemesi (stale closure) `useRef` pattern ile düzeltildi
- Negatif `z-index` değerinin bloğu canvas'tan gizlemesi engellendi
- TypeScript build hataları: `config` cast, `ResizeHandles` çift `cursor`, kullanılmayan import
- V1 `SectionComponent.tsx` kaldırıldı (V2'de kullanılmıyor)
- `canvasRefs.ts` ayrı dosyaya alınarak ESLint `react-refresh/only-export-components` uyarısı giderildi

---

## [0.3.1] - 2026-03-29

### Fixed
- `SectionComponent.tsx` `toggleLayout` fonksiyonunda `width` property'sinin nesne spread ile çakışmasından kaynaklanan TypeScript derleme hatası giderildi (`TS2783: 'width' is specified more than once`)

---

## [0.3.0] - 2026-03-29

### Added
- **Toplu seçim/silme**: Şablonlarım ve Taslaklarım sayfalarında çoklu satır seçimi ve toplu silme işlevi eklendi
- **Çok sütunlu blok gruplama**: Editörde 3 sütunlu bölümlerde her bloğa sütun ataması yapılabiliyor (`col` property); aynı sütundaki bloklar dikey sıralanıyor
- **Sütunlar arası drag-drop**: `ColDropZone` bileşeni ile bloklar sürüklenerek sütunlar arasında taşınabiliyor
- **3 sütun genişlik oranı**: Eşit %33.33 yerine Sütun1=%40, Sütun2=%30, Sütun3=%30 olarak ayarlandı (`2/5` ve `3/10` genişlik değerleri eklendi)
- **Self-contained XSLT**: Image bloğu asset'leri önizleme ve XSLT indirme sırasında `base64 data URI` olarak gömülüyor; indirilen XSLT harici sunucu bağımlılığı olmadan çalışıyor
- **GİB Karekod formatı**: QR içeriği lowercase alan adları, YOLCUBERABERFATURA koşulları ve quoted değerlerle güncellendi
- `BlockLayout.col` (frontend) ve `BlockLayoutDto.Col` (backend) eklendi; sütun bazlı blok konumlandırması destekleniyor
- `BlockWidth` tipine `2/5` ve `3/10` değerleri eklendi

### Changed
- `xsl:stylesheet version="1.0"` → `version="2.0"` olarak güncellendi
- `unitCode` xsl:when değerleri okunabilirlik için alt alta yazılıyor (`AppendLine`)
- `RenderColGrouped` backend metodu: aynı sütundaki bloklar `display:block;width:100%` wrapper ile dikey diziliyor

### Fixed
- Aynı sütuna eklenen blokların yan yana değil, üst üste görünmesi sağlandı
- Çok sütunlu bölümlerde layout değiştirildiğinde mevcut blokların sütun genişliği doğru güncelleniyor

---

## [0.2.0] - 2026-03-28

### Added
- Varsayılan UBL 2.1 e-Fatura XML örneği eklendi (`default-invoice.xml`)
- ThemeUsePage: Kullanıcıdan XML beklenmeden doğrudan önizleme açılıyor
- EditorPage (Yeni Şablon): Sayfa açılışında XML store varsayılan fatura ile önyükleniyor
- CI pipeline: `ci.yml` ve `release.yml` workflow'ları eklendi

### Changed
- ThemeUsePage artık Phase-1 XML yükleme ekranını atlar; "XML Değiştir" butonu toolbar'da kalır
- EditorPage yeni şablon modunda XML store her açılışta sıfırlanır ve varsayılan XML yüklenir

### Fixed
- Frontend lint hataları giderildi (24 error → 0)
- CI'da frontend `working-directory` yolu düzeltildi
- `dotnet format` whitespace uyumsuzlukları düzeltildi

### Security
- pre-commit hook: `.github/workflows/` dizini secret taramasından muaf tutuldu

---

## [0.1.0] - 2024-02-15

### Added
- Initial XsltCraft monorepo structure
- React 19 + TypeScript frontend with TailwindCSS
- C# .NET 10 backend with PostgreSQL
- XSLT template editor with real-time validation
- UBL 2.1 e-Fatura standard support
- E-İrsaliye (shipping invoice) support
- JWT + Google OAuth authentication
- Local storage (dev) and S3 storage (prod) integration
- Template saving and versioning
- Block-based template designer

### Changed
- Migrated to new GitHub repository (XsltCraft-v2)
- Restructured monorepo with frontend/backend separation

### Fixed
- TypeScript strict mode compliance
- EditorStore state management
- XSLT namespace resolution

### Security
- JWT token validation
- API endpoint authentication

---

## Format Guide

Use these sections in your entries:

- **Added** — New features
- **Changed** — Changes in existing functionality
- **Deprecated** — Soon-to-be removed features
- **Removed** — Removed features
- **Fixed** — Bug fixes
- **Security** — Security fixes and improvements

Example entry:

```markdown
## [1.2.0] - 2024-03-01

### Added
- Support for XSD schema validation
- Batch template export

### Fixed
- Auth token expiration handling
- XSLT variable scope resolution
```

---

**Maintainer**: @spolt
