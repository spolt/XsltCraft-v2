# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

## [1.9.0] - 2026-07-13

### Changed
- **AI — sağlayıcıya özel bağlam bütçesi: model artık şablonun TAMAMINI görebiliyor** (`AiContextBudget`, `PromptTemplates.BuildAssistant(req, budget)`, `AiOptions`): Önceden her sağlayıcıya aynı kırpılmış bağlam gidiyordu — 6K karakter üzeri XSLT yapısal özete indirilip 16K'da kırpılıyordu, 1M token pencereli Gemini bile yalnız özeti görüyordu. Artık her sağlayıcı kendi penceresine göre bütçe alır: **Gemini tam `.xslt` dosyasını HAM gönderir** (400K karaktere kadar → tipik ~250KB GİB şablonu bütünüyle), **Ollama** yapılandırılabilir bütçeyle çalışır. "X alanını değiştir/kaldır" tarzı sorulara doğru yanıt için şablonun tamamı modelin önünde olur. Tüm sınırlar appsettings'ten ayarlanır; varsayılan bütçe eski davranışı birebir korur (golden snapshot'lar değişmedi).
- **AI — büyük şablonda otomatik Gemini yönlendirmesi** (`ProviderRouting`, `AiProviderOrchestrator`): `auto` modda XSLT `LargeXsltGeminiThresholdChars`'ı (varsayılan 64K karakter) aşarsa istek Gemini'ye öncelikli yönlenir (tam dosyayı gördüğü için), Ollama yedek kalır. Açık tercih (`ollama`/`gemini` flag'i) her zaman kazanır; `0` ile kapatılır.
- **Ollama bağlam penceresi büyütüldü** (`appsettings*.json`): `NumCtx 8192/16384 → 32768` (qwen2.5-coder 3b/7b native üst sınırı) ve XSLT ham bütçesi `MaxXsltChars: 64000` — 64K karaktere kadar şablon Ollama'ya da özetlenmeden gider. KV cache maliyeti 3b ≈ +1.2GB, 7b ≈ +1.8GB.

### Tests
- **`ContextBudgetTests`** (9 case): büyük bütçede tam ham dosya gönderimi, varsayılanda özetleme, limit aşımında kırpma + 6 `ProviderRouting` yönlendirme senaryosu. Application.Tests: 98 → **107** yeşil; mevcut golden snapshot'lar bayt-bayt korundu.

## [1.8.0] - 2026-07-13

### Added
- **AI sohbet — önerilen değişikliği editöre uygula** (`utils/xsltApply.ts`, `components/ai/AiApplyDialog.tsx`, `AiAssistantPanel.tsx`, `XsltEditorPage.applyAiChange`): Asistan yanıtında bir XSLT kod bloğu varsa mesaj altında **"Uygula"** düğmesi çıkar. Tıklanınca Monaco `DiffEditor` ile eski/yeni karşılaştırması gösterilir, `POST /api/preview/validate-xslt` ile doğrulanır (geçersizse "Yine de uygula") ve kabul edilince editördeki XSLT'ye uygulanır (tek adımda `Ctrl+Z` ile geri alınabilir). Hedefleme algoritması (`computeApplyTarget`): tam stylesheet → tüm belge; aktif seçim dokümanda varsa → seçim; blok bir `xsl:template` ise aynı `match`/`name`/`mode` imzalı tek template → o template; aksi hâlde **no-match** (tahmin yok, "Panoya kopyala").
- **AI sohbet — işe yaradı / işe yaramadı geri bildirimi** (`AiFeedback` entity + migration, `IAiFeedbackService`, `POST/PUT /api/ai/feedback`): Her tamamlanmış asistan yanıtı için 👍/👎 oy. "Uygula" örtük pozitif kaydeder. 👎 seçilince iki seçenek açılır: **"Farklı yaklaşım dene"** (önceki yanıtın işe yaramadığı bilgisiyle otomatik yeniden sorar) ve **"Detay vereyim"** (input'a odaklanır). Free kullanıcı da oy verebilir (kota tüketmez).
- **AI öğrenme — geçmiş başarılı örnekler few-shot enjeksiyonu** (`ExemplarScorer`, `IAiExemplarService`, `AiRequest.Exemplars`, `PromptTemplates`): Kullanıcının (ve admin onaylı global havuzun) pozitif geri bildirimleri, yeni soruya token-örtüşmesiyle (embedding YOK, v1) skorlanıp en alakalı 1-2 örnek prompt'a eklenir. Örnekler **system mesajına değil ilk user bağlam mesajına** (`<successful_examples>`) girer — Ollama prefix KV-cache prefix'i korunur, hız düşmez.
- **Admin — AI geri bildirim havuzu** (`AdminAiFeedbackController` → `/api/admin/ai-feedback`, `pages/admin/AdminAiFeedbackPage.tsx`, `/admin/ai-feedback`): Geri bildirimleri listeler/arar; kaliteli pozitif örnekleri **global havuza** terfi eder (tüm kullanıcıların prompt'una girer) veya siler. Terfi öncesi kişisel/fatura verisi uyarısı gösterilir.

### Changed
- **AI çıktı formatı** (`Prompts/Core/Constraints.md`): Model kod değişikliği önerirken ilgili `xsl:template`'in TAM yeni halini tek bir ```xslt bloğunda vermeye yönlendirildi (diff/kesit değil; match/name attribute'unu değiştirmeden) — "Uygula" hedeflemesini güvenilir kılar.
- **Versiyon hizalama**: `package.json`, `XsltCraft.Api.csproj`, `XsltCraft.Application.csproj`, `XsltCraft.Domain.csproj`, `XsltCraft.Infrastructure.csproj` ve README rozeti `1.7.4 → 1.8.0`.

### Tests
- **`ExemplarScorerTests`** (7 case): token eşiği, top-2 skor+yenilik sıralaması, Türkçe folding, boş/kısa istek. **`BuildMessagesGoldenTests.Assistant_WithExemplars`** golden case'i eklendi; mevcut 4 snapshot Constraints değişikliği için yeniden kabul edildi. Application.Tests: 82 test yeşil.

## [1.7.4] - 2026-07-12

### Added
- **XSLT Editör — AI sohbetini önizleme ile bölünmüş kullanma** (`XsltEditorPage.tsx`): AI sekmesindeyken sağ üstteki **"Önizlemeyle Böl"** düğmesiyle sağ panel dikey olarak ikiye ayrılır — **üstte canlı önizleme, altta AI sohbet** — böylece sohbet ederken önizleme aynı anda görülebilir. Aradaki tutamaç sürüklenerek oran ayarlanır (varsayılan 50/50, her pane min. %15). Bölme özel bir flexbox + pointer-drag ile uygulanır (`react-resizable-panels`'ın çalışma-anında panel ekleme/çıkarma davranışı beyaz ekrana yol açtığından kütüphane bu bölmede kullanılmadı); sürükleme boyunca önizleme iframe'i ve sohbet Monaco editörleri `pointer-events:none` yapılır — aksi halde fare iframe üzerine gelince `pointermove` kesilip sürükleme takılıyordu. Sohbet paneli sabit `key` ile mount'ta tutulur; "Böl" açılıp kapanınca konuşma korunur.

### Changed
- **AI sohbet — Enter ile gönder** (`AiAssistantPanel.tsx`): Mesaj göndermek artık yalnızca **Enter** ile yapılır (önceki `Ctrl+Enter` kaldırıldı); **Shift+Enter** yeni satır ekler. IME kompozisyonu (`isComposing`) sürerken gönderim engellenir. Input altına kalıcı ipucu (`Enter ile gönder · Shift+Enter ile yeni satır`) ve güncellenmiş placeholder eklendi.
- **AI sohbet — daha okunur yazı** (`AiAssistantPanel.tsx`): Mesaj balonları ve metin blokları `text-xs → text-sm` (12→14px), boş-durum ipucu büyütüldü, kod blokları Monaco font boyutu 12→13, input alanı 12→14px.
- **Versiyon hizalama**: `package.json`, `XsltCraft.Api.csproj`, `XsltCraft.Application.csproj`, `XsltCraft.Domain.csproj`, `XsltCraft.Infrastructure.csproj` ve README rozeti `1.7.3 → 1.7.4`.

## [1.7.3] - 2026-07-12

### Fixed
- **XSLT Editör — önizleme yatay kaydırması sayfanın sol kenarına ulaşamıyordu** (`XsltEditorPreview.tsx`): Kaydırılabilir önizleme alanı, ölçeklenmiş (transform: `scale`) A4 sayfasını flex `justify-center` ile ortalıyordu. Monaco editörü genişletilip önizleme paneli daraltıldığında (ya da yakınlaştırıldığında) sayfa kapsayıcıdan daha geniş hale geliyor; ortalanan bir flex öğesi taşınca taşma iki yana eşit dağılıyor, oysa kaydırma kapsayıcısı yalnızca sona (sağa) kaydırabildiği için sayfanın **sol** kenarı erişilemez oluyordu. Hizalama `justify-content: safe center` ile değiştirildi: içerik sığdığında yine ortalanır, taştığında başlangıç hizalamasına düşerek her iki kenara da kaydırılabilir.

### Changed
- **Versiyon hizalama**: `package.json`, `XsltCraft.Api.csproj`, `XsltCraft.Application.csproj`, `XsltCraft.Domain.csproj`, `XsltCraft.Infrastructure.csproj` ve README rozeti `1.7.2 → 1.7.3`.

## [1.7.2] - 2026-07-06

### Fixed
- **XSLT Editör — AI sohbeti sekme değişiminde sıfırlanıyordu** (`XsltEditorPage.tsx`): AI paneli, Önizleme/XML Kaynak sekmesine geçilince React ağacından unmount edildiği için tüm konuşma kayboluyordu. Panel artık ilk açılıştan sonra mount'ta tutulur ve aktif değilken CSS (`hidden`) ile gizlenir; sekmeler arası geçişte sohbet (ve süren streaming yanıtı) korunur. Düz AI açılışı (`openAiBlank`) remount tetiklemez; taze sohbet yalnızca "Yeni sohbet" (↺) düğmesiyle ya da Problemler panelinden "AI'ya sor" akışında (bilinçli `key` değişimi) başlar.
- **AI, seçili XSLT bölgesine dair soruda alakasız yanıt veriyordu** (`PromptTemplates`, `Prompts/Core/Identity.md`): Kullanıcının editörde seçtiği XSLT, XML seçiminin aksine prompt'ta kendi etiketli bloğunu almıyor, yalnızca `XsltSummarizer`'a ipucu olarak geçiyordu; büyük şablonlarda seçili satırlar tüm template içinde erir ve 16K `Clip` penceresinde kırpılabiliyordu. Assistant prompt'una `<user_xslt_selection>` bloğu eklendi (tam stylesheet korunur, seçim ek ve ham blok olarak sunulur) ve sistem yönergesine "kullanıcı bir bölge seçtiyse yanıtını ona odakla" kuralı eklendi. Böylece dosya boyutundan bağımsız olarak model doğru bölgeye yanıt verir.

### Changed
- **Versiyon hizalama**: `package.json`, `XsltCraft.Api.csproj`, `XsltCraft.Application.csproj`, `XsltCraft.Domain.csproj`, `XsltCraft.Infrastructure.csproj` ve README rozeti `1.7.0 → 1.7.2`.

### Tests
- **`BuildMessagesGoldenTests.Assistant_WithXsltSelection`** eklendi: seçim varken prompt'ta `<user_xslt_selection>` bloğunun ve odaklanma yönergesinin yer aldığını doğrular; mevcut üç golden snapshot sistem-mesajı değişikliğine göre yenilendi. Application.Tests: 89 → **90**.

## [1.7.0] - 2026-06-28

### Added
- **Şablonlarım — klasör olarak toplu XSLT yükleme** (`UserXsltTemplateController.BulkUpload` → `POST /api/user-xslt-templates/bulk`, `BulkUploadUserXsltRequest`, `components/storage/BulkUploadModal.tsx`, `MyXsltTemplatesPage`): Tek "Toplu Yükle" modalından birden çok `.xslt` dosyası **veya** bir disk klasörü (`webkitdirectory`) seçilip **yeni ya da mevcut** bir klasöre tek seferde aktarılır. Her dosya sunucuda **fail-closed** doğrulanır (iyi-biçimli XML — `DtdProcessing.Prohibit` + `XmlResolver=null` — ve `XsltSafety.FindThreat`); geçersizler atlanıp dosya-bazında raporlanır, geçerliler tek transaction'da yazılır. Pro-gated (`GateSaveAsync`; Free → 402 upsell), sınırlar ≤100 dosya ve ≤2 MB/dosya. Yükleme bitince hedef klasöre yönlendirir.
- **Şablonlarım — toplu sabit not ekleme** (`IFixedNoteInjector`/`FixedNoteInjector`, `UserXsltTemplateController.BulkAddFixedNote` → `POST /api/user-xslt-templates/bulk-add-note`, `BulkAddFixedNoteRequest`, `components/storage/BulkAddNoteModal.tsx` + `BulkNoteResultDialog.tsx`): Toplu seçilen şablonların **koşulsuz** not gösterim döngüsüne (`//n1:Invoice/cbc:Note` ve `//n1:DespatchAdvice/cbc:Note`) sabit bir not metni gömülür. Snippet, döngünün **kendi gövdesinden türetilir** (şablonun `<b>Not:</b>`/`<tr><td>` stili korunur) ve döngüden hemen sonra **tam bir kez** render edecek şekilde dosyaya cerrahi olarak yerleştirilir (dosyanın geri kalanı byte-byte korunur — tüm XSLT yeniden serialize edilmez). Atası `xsl:if/when/choose` olan **koşullu (SGK vb.) notlara dokunulmaz**. Gizli marker yorumları (`<!-- xc:fixed-note -->`) ile idempotenttir: varsayılan **"değiştir"** modu mevcut sabit notu günceller (çoğalmaz), **"yeni not olarak ekle"** modu blok içine yeni satır ekler. Şablon başına sonuç (eklendi / not bölümü yok / kilitli / hata) bir diyalogda gösterilir ve "Önizle" ile mevcut önizleme paneline yönlendirilir. Owner-scoped (IDOR), başkasınca aktif düzenlenen şablonlar atlanır, Pro-gated.

### Changed
- **"Sabit Not Ekle" modalı sadeleştirildi** (`BulkAddNoteModal.tsx`): Kullanıcıya XPath/iç-işleyiş detayı veren teknik bilgi kutusu kaldırıldı. Ayrıca yeni toplu-işlem bildirimlerine süre (`durationMs: 5000`) eklendi.
- **Versiyon hizalama**: `package.json`, `XsltCraft.Api.csproj`, `XsltCraft.Application.csproj`, `XsltCraft.Domain.csproj`, `XsltCraft.Infrastructure.csproj` ve README rozeti `1.6.0 → 1.7.0`.

### Fixed
- **Toplu yükleme 30 MB istek sınırında başarısız oluyordu** (`BulkUploadModal.tsx`): Çok sayıda büyük XSLT'nin (gerçek GİB e-fatura şablonları ~250 KB+) tüm içeriği tek JSON gövdesinde gönderiliyor ve Kestrel'in varsayılan 30 MB istek-gövdesi sınırını aşıp bağlantı resetine ("provisional headers / 0 B transferred") yol açıyordu. Yükleme artık boyut-bazlı **partilere** bölünüyor (`buildBatches`: ≤10 MB ham içerik **ve** ≤50 dosya/istek); partiler sırayla gönderilip sonuçlar toplanır, "yeni klasör" tek kez oluşturulup tüm partiler aynı `folderId`'yi kullanır, buton parti ilerlemesini gösterir.
- **Sabit not, `xsl:variable` içindeki not döngüsüne gömülebiliyordu** (`FixedNoteInjector`): Bir `<xsl:variable>` (ör. `vareczanehizmetbedeli`) içinde tanımlı `//n1:Invoice/cbc:Note` döngüsü, atasında `if/when/choose` olmadığı için yanlışlıkla hedef seçilip not, çıktıya render edilmeyen bir değer-hesabına gömülüyordu. Tespit artık atasında **`variable`/`param`/`with-param`** de bulunan döngüleri dışlar — yalnız görünür gösterim döngüsü seçilir. Ek olarak enjeksiyon **kendini onarır** (`StripMarkerBlock`): mevcut marker bloğu önce çıkarılıp not doğru konuma yeniden yerleştirilir → "değiştir" çalıştırıldığında daha önce yanlış yere gömülmüş notlar otomatik düzelir; "ekle" modunda önceki notlar korunup birlikte taşınır.

### Security
- **Toplu uçlar için fail-closed XSLT doğrulama** (`UserXsltTemplateController`, `FixedNoteInjector`): Toplu yüklenen her XSLT, kalıcılaştırılmadan önce `DtdProcessing.Prohibit` + `XmlResolver=null` ile ayrıştırılır ve `XsltSafety.FindThreat` taramasından geçirilir (tekil `Create` yolunda bulunmayan kontrol — boşluk kapatıldı). Sabit not metni **kullanıcı girdisidir**; yalnızca `xsl:text` değer düğümü olarak gömülür (XML kaçışı otomatik) → markup/XSLT injection engellenir; enjeksiyon sonucu tekrar `XsltSafety` ile fail-closed taranır.

### Tests
- **`FixedNoteInjectorTests`** (`XsltCraft.Application.Tests/Xslt/`, 9 case): koşullu/koşulsuz döngü ayrımı, **`xsl:variable` içindeki döngülerin atlanması**, tek-kez render, XML kaçışı, idempotent "değiştir", "ekle" modu, **yanlış konuma gömülen notun kendini onarması**, Invoice + DespatchAdvice yolları, `NoNotesSection`/malformed XSLT. Application.Tests: 80 → **89**.

## [1.6.0] - 2026-06-21

### Added
- **Taslaklarım & Şablonlarım — arama, klasörleme ve favoriler** (`Folder` entity + `FolderKind`, `FolderController`, `components/storage/*`, `DraftsPage`/`MyXsltTemplatesPage`): Her iki XSLT şablon deposuna **anlık isim araması**, **düz (tek seviye) klasörler** (oluştur/yeniden adlandır/renk/sil — klasör silinince şablonlar silinmez, "Tümü"ne düşer), satır içi ve **toplu klasöre taşıma**, **favori (★)** ve sıralama (son güncelleme / oluşturulma / ad) eklendi. Klasörler `FolderKind {Draft, XsltTemplate}` ile alana göre izoledir; cross-kind taşıma engelli. Aktif klasör URL parametresinde (`?folder=`) tutulur, sol klasör kenar çubuğu + ortak `useTemplateLibrary` filtre/sıralama hook'u iki sayfada paylaşılır. Yeni uçlar: `GET/POST/PUT/DELETE /api/folders`, `PATCH /api/templates/{id}/folder|favorite`, `PATCH /api/user-xslt-templates/{id}/folder|favorite`. Sahiplik (IDOR) backend-zorunlu; paylaşılan XSLT'lerde klasör/favori bilgisi sahibe özeldir, listede sızmaz.
- **Rol yönetimi + XsltCraft Pro üyelik modeli** (`MembershipPlan`, `IEntitlementService`/`EntitlementPolicy`, `IUsageQuotaService`, `MeController`, `BillingController`): Mevcut `UserRole {User, Editor, Admin}` enum'una **dik** bir `Plan {Free, Pro}` ekseni eklendi (`User.Plan` + `User.PlanExpiresAt`). Etkin yetkiler rol + plan + abonelik geçerliliğinden hesaplanır (`EntitlementPolicy.Compute`, saf/test edilebilir); **Editör ve Admin tüm kotaları bypass eder**, süresi geçen Pro otomatik Free'ye düşer. Politika appsettings `Membership` bölümünden ayarlanır (limitlerde `0 = sınırsız`). Gate'ler **backend-zorunlu** (frontend yalnız UX):
  - **Standart (Free):** grid-canvas şablonu tasarlar/taslak kaydeder ama **XSLT indiremez** (402 → upsell); ham XSLT içeriğine erişemez; ücretsiz temaları kullanır/indirir; XSLT Editör'de düzenler/indirir ama **Şablonlarım'a kaydedemez** (Pro); AI **günde 1 soru**.
  - **Pro:** tam erişim ama **günde 3 indirme** (4.'te 429) + **50.000 token/gün**; ücretli temaların kilidi açılır.
  - Yeni günlük sayaçlar `UserAiUsages` tablosunda (`AiRequestCount`, `TemplateExportCount`; UTC gece yarısı sıfırlanır). `AiTokenBudgetService` → plan-bilinçli `UsageQuotaService` olarak genişletildi.
- **Ücretli/ücretsiz tema ayrımı** (`Template.IsPremium`, `AdminController`, `AdminThemesPage`): Admin hazır şablonlar panelinde tema "Ücretli" işaretlenebilir. Tema kütüphanesinde ücretli temalar **önizlenebilir** fakat "Bu temayı kullan" kilitlidir (Pro gerektirir); kart üzerinde "Ücretli" rozeti ve kilitli buton gösterilir.
- **Pro upsell akışı + admin grant** (`UpgradeModal`, `PricingPage` `/pricing`, `entitlementStore`, `PATCH /api/admin/users/{id}/plan`): Gate'li her işlemde açılan global yükseltme modalı; Free vs Pro karşılaştırma sayfası; Navbar'da plan rozeti / "Pro'ya Geç" CTA. Ödeme entegrasyonu (iyzico/PayTR) **Faz 2** — şimdilik `POST /api/billing/checkout` stub'dır ve Pro, admin panelinden (kullanıcı planı + bitiş tarihi) elle tanımlanır.
- **Admin Kullanım Raporu** (`IUsageReportService`, `AdminUsageController`, `AdminUsagePage` `/admin/usage`): Kullanıcı başına token, AI soru, kaydetme ve indirme — geçmişe dönük ve anlık. `UserAiUsages` (günlük token/istek/export) + `UserActivities` (save/download olayları) birleştirilir. Tarih aralığı (Bugün / Son 7-30-90 gün / özel), özet kartları + kullanıcı bazlı tablo + toplam satırı, günlük toplam trendi ve **CSV indirme**. Uçlar: `GET /api/admin/usage/report`, `GET /api/admin/usage/daily`. Sol menüye "Kullanım Raporu" eklendi.

### Changed
- **Versiyon hizalama**: `package.json`, `XsltCraft.Api.csproj`, `XsltCraft.Application.csproj`, `XsltCraft.Domain.csproj`, `XsltCraft.Infrastructure.csproj` ve README rozeti `1.5.0 → 1.6.0`.

### Security
- **Klasör uçları için sunucu tarafı girdi doğrulama** (`FolderController`): Klasör adı uzunluk sınırı (≤100) ve renk **allowlist**'i (`blue|emerald|amber|rose|violet|slate`; eşleşmeyen → `null`) backend'de zorlanır — ham `DbUpdateException`/500 ve API'yi tüketen başka istemcilerde olası stored-XSS yüzeyi önlenir. (security-reviewer bulgusu.)
- **Ham XSLT sızıntı noktaları kapatıldı** (`PreviewController`): `POST /api/preview/xslt` (kaydedilmemiş grid şablonu indirme yolu) artık `[Authorize]` + indirme kotasına tabidir; `GET /api/preview/theme/{id}/xslt-content` (geliştirici modu) **ücretli temalarda** Pro yetkisi ister. Plan kontrolleri JWT claim'ine değil **DB'ye** dayanır (para-ilişkili gate'ler otoritatif); ücretli tema XSLT'si yetkisiz kullanıcıya sunulmaz.

---

## [1.5.0] - 2026-06-20

### Added
- **AI — soru-bilinçli XSLT template retrieval** (`XsltSummarizer`, `TextFold`, `AiAssistantPanel`, `Xslteditor`): Büyük XSLT'lerde asistan, kullanıcının sorusunu bilmeden körlemesine ilk birkaç template'i gönderiyordu; geç sıradaki template'lerin gövdesini model hiç görmüyordu. Artık metin seçimi yoksa template'ler kullanıcının sorusuna **ve** XSLT editöründeki imleç satırına göre skorlanır (`RankTemplates`/`ScoreTemplate`: imza eşleşmesi +5, gövde +1, imleci kapsayan template +100; UBL prefix'leri `cbc:`/`cac:` local-name'e indirgenir) ve en alakalı template'ler tam gövdesiyle inline edilir. Inline bütçe 4K→8K char. Soru/imleç sinyali yoksa eski belge-sırası davranış korunur. XSLT seçimi + imleç satırı (`XsltSelection`/`XsltCursorLine`) artık assistant moduna uçtan uca taşınıyor (önceden yalnız XML seçimi gönderiliyordu).
- **ECC geliştirme çerçevesi (Faz 0–2)** (`CLAUDE.md`, `.claude/agents`, `.claude/skills`, `docs/ecc/**`): Native Claude Code konvansiyonuyla hafif bir agent/skill/doküman katmanı. Agentlar: `reviewer`, `security-reviewer`, `test-engineer`, `architect`, `planner`, `refactorer`. Skill'ler: `review-checklist`, `xss-xxe-checklist`, `playwright-e2e`. Dokümanlar: bağlam (`context/*`), mimari (`architecture/{backend,frontend,database,ai-system}.md`), kararlar (ADR 001–006), `standards.md`, `workflows.md`. Üretim koduna dokunmaz; geliştirme akışını standartlaştırır.
- **XSLT/XPath eval harness** (`backend/XsltCraft.Tests/Eval/`, `docs/ecc/evaluations/**`): `XsltEvalTests` (yapısal golden + SSRF reddi/benign regresyon case'leri) ve `XPathEvalTests` (namespace çözümleme, `count()`, boş sonuç, geçersiz ifade → "error"). `[Trait("Category","Eval")]` ile işaretli; CI'da **non-blocking** ayrı job, ana test koşusu `--filter Category!=Eval`.
- **Playwright E2E** (`frontend/xsltcraft-ui/e2e/`, `playwright.config.ts`, `.mcp.json`): `@playwright/test` ile commit'li smoke spec'leri (register→login→dashboard, korumalı sayfa yükleme, auth'suz→login redirect — 3/3 yeşil) + interaktif Playwright MCP sunucusu. `test:e2e` script'i eklendi.

### Changed
- **AI `max_tokens` 2048 → 4096** (`appsettings.json`, `appsettings.Development.example.json`, `AiOptions.cs`): Hem Ollama hem Gemini için yanıt üst sınırı iki katına çıkarıldı; uzun açıklamalar "max_tokens limitine ulaşıldı" ile kesilmiyor. (Ollama tarafında `NumCtx` 8192 sabit kaldı.)
- **`Fold` mantığı tekilleştirildi** (`TextFold`): Türkçe karakter folding'i `IntentClassifier`, `PatternSelector` ve `XsltSummarizer` arasında üç ayrı kopyaydı; tek `TextFold.Fold` yardımcısına çıkarıldı (tutarlı normalizasyon, duplicate logic giderme).
- **Versiyon hizalama**: `package.json`, `XsltCraft.Api.csproj`, `XsltCraft.Application.csproj`, `XsltCraft.Domain.csproj`, `XsltCraft.Infrastructure.csproj` ve README rozeti `1.4.0 → 1.5.0`.

### Security
- **XSLT SSRF / yerel dosya okuma açığı kapatıldı** (`XsltSafety`, `XsltGeneratorService`, `XsltTemplateRenderer`, `AdminController`): Kullanıcının binding XPath'i üretilen XSLT'ye verbatim gömülüyor ve Saxon render yolu `document()`/`doc()`/`unparsed-text()`/`collection()` fonksiyonlarını çalıştırıyordu → `/api/render` üzerinden SSRF/dosya-okuma. `XsltSafety.FindThreat` fail-closed tarama (XPath attribute'ları + harici import) ekler; generator doğrulaması ve admin tema yükleme tehdidi reddeder; Saxon `XsltCompiler`/`DocumentBuilder` `XmlResolver.ThrowingResolver` kullanır. Testler: `XsltSafetyTests` + eval case002 (reddi + benign regresyon).

---

## [1.4.0] - 2026-06-13

### Added
- **XSLT şablon paylaşımı** (`UserXsltTemplateShare` entity, `UserXsltTemplateController`): Şablon sahibi, kayıtlı bir XSLT şablonunu yetkilendirdiği başka kullanıcılarla paylaşabilir. Paylaşılan kullanıcı şablonu görüntüleyebilir **ve düzenleyebilir** (sahip ile aynı yetki seviyesi; ayrı view/edit ayrımı yok). Yeni uç noktalar: `GET/POST /api/user-xslt-templates/{id}/shares`, `DELETE /api/user-xslt-templates/{id}/shares/{userId}` (tümü yalnızca sahip). `GetById`/`Update` yetkisi `OwnerId == userId` yerine `CanAccess` (sahip **veya** paylaşılan); `Delete` ve paylaşım yönetimi sahip-only kalır. `GetAll` artık sahip olunan + benimle paylaşılanları birleştirir (`IsOwner`/`IsShared`/`OwnerName`).
- **Eşzamanlı düzenleme kilidi** (`UserXsltTemplate.EditingUserId`/`EditingHeartbeatAt`): Birden fazla kullanıcının erişebildiği bir şablonda biri aktif düzenlerken diğerlerine şablon **salt-okunur** açılır ve "Şu an X düzenliyor" uyarısı gösterilir. Kalp atışı (heartbeat) tabanlı; 90 sn boyunca yenilenmeyen kilit otomatik serbest kalır. Yeni uç noktalar: `POST /api/user-xslt-templates/{id}/lock/acquire` (al/tazele), `POST .../lock/release`. Editör yüklemede kilit alır, 30 sn'de bir heartbeat atar, ayrılırken serbest bırakır.
- **İyimser çakışma koruması** (`UpdateUserXsltRequest.ExpectedUpdatedAt`, `DateTimeOffset`): Kaydederken istemci yüklediği sürümün `updatedAt`'ini gönderir; DB'deki değer daha yeniyse (UTC, 1 sn tolerans) `409 Conflict` döner ve kullanıcıdan yeniden yükleme istenir. Kilitli kaynak için `Update` `423 Locked` döner.
- **Kullanıcı arama uç noktası** (`UsersController`): `GET /api/users/search?query=…` — paylaşım kişi seçici için, oturumlu her kullanıcıya açık (admin gerekmez), kullanıcı adı/e-posta/isimde arar, kendisi hariç ilk 10 sonucu döndürür.
- **`ShareTemplateDialog`** (`components/xslt-editor/ShareTemplateDialog.tsx`): Debounce'lı kullanıcı arama, avatarlı sonuç listesi ve mevcut paylaşımları kaldırma. `MyXsltTemplatesPage` artık paylaşılan şablonları "Paylaşılan • sahip adı" rozetiyle gösterir; sahip satırlarında Paylaş ikonu, paylaşılan satırlarda silme/yeniden-adlandırma gizli.
- **Şifre değiştirme** (`POST /api/auth/change-password`, `ChangePasswordRequest`, `profileService.changePassword`, `ProfilePage`): Yerel hesaplar mevcut şifrelerini doğrulayıp yeni bir şifre belirleyebilir. Mevcut şifre BCrypt ile doğrulanır; yeni şifre kuralı (en az 8 karakter, 1 büyük harf, 1 rakam) ve "eskiyle aynı olamaz" denetimi uygulanır; Google ile oluşturulmuş (şifresiz) hesaplar reddedilir. Güvenlik için tüm mevcut oturum token'ları iptal edilir, bu oturum için yeni access/refresh token verilir. `auth-sensitive` rate-limit politikasına tabidir.

### Changed
- **Kullanıcı adında nokta desteği** (`AuthController.UsernameRegex`, `RegisterPage`): Kullanıcı adı regex'i `^[a-zA-Z0-9_]{3,30}$` → `^[a-zA-Z0-9_][a-zA-Z0-9_.]{1,28}[a-zA-Z0-9_]$`. Artık nokta içerebilir, ancak başında/sonunda nokta olamaz. Backend ve frontend doğrulama mesajları hizalandı.
- **Kayıt sayfası teması** (`RegisterPage`): `AuthLayout` teması `A → D` (login ile aynı gri-mor tema).
- **Varsayılan Ollama modeli** (`OllamaOptions.Model`): `llama3.1:8b → qwen2.5-coder:3b` — daha düşük RAM kullanımı, daha hızlı prefill (v1.3.0'daki `appsettings` hizalamasıyla tutarlı varsayılan).
- **Versiyon hizalama**: `package.json`, `XsltCraft.Api.csproj`, `XsltCraft.Application.csproj`, `XsltCraft.Domain.csproj`, `XsltCraft.Infrastructure.csproj` ve README rozeti `1.3.2 → 1.4.0`.

### Fixed
- **Docker ENTRYPOINT yanlış assembly** (`backend/Dockerfile`): `dotnet XsltCraft.dll` → `dotnet XsltCraft.Api.dll`. API projesinin assembly adı `XsltCraft.Api` olduğundan container eski isimle başlatılamıyordu.
- **Admin kullanıcılar — aksiyon menüsü dışarı tıklama** (`AdminUsersPage` `ActionMenu`): Menü `position:fixed` olduğu için öğelerine tıklamak "dışarı tıklama" sayılıp menüyü hemen kapatıyordu. Dışarı-tıklama testi artık `btnRef` yerine container `ref`'i üzerinden yapılıyor; menü öğeleri sorunsuz tıklanabiliyor.

---

## [1.3.2] - 2026-06-08

### Improvement
- **Önizlemede görsele tıkla → base64 ara** (`XsltEditorPreview.tsx`, `XsltEditorPage.tsx`): Canlı önizlemede bir görsele tıklandığında, görselin `src` içindeki base64 verisinin ayırt edici bir parçası XSLT editöründe aranıp ilgili satıra gidiliyor. iframe tıklama script'i `<img>` (veya bir üst atası) algıladığında `base64,` sonrasındaki ilk ~48 baytı (çoğu JPEG/PNG'de ortak başlık) atlayıp sonraki ~80 karakteri benzersiz imza olarak gönderir; `exact` bayrağıyla bu parça kısaltılmadan birebir aratılır. Metin tıklamaları eski davranışını korur.
- **İlk satırda XML bildirimi denetimi** (`XsltEditorPage.tsx`): XSLT'nin ilk satırında `<?xml version="1.0" encoding="UTF-8"?>` bildirimi yoksa Problemler panelinde "XML Bildirimi Eksik" hatası (`ruleId: XML_DECL_MISSING`) gösterilir ve toplam hata sayacına dahil edilir. BOM ve baştaki boşluğa toleranslı regex ile tespit edilir.

### Changed
- **"AI'ya sor" — XML bildirimi hatası için kısa yanıt** (`XsltEditorPage.openAiForProblem`): Bu hata için AI'a hedefli prompt gönderiliyor — "çok kısa açıkla, yalnızca eklenecek satırı ve dosyanın ilk 3-4 satırını göster, tüm şablonu tekrar yazma". AI artık tüm şablonu geri basmıyor. Diğer hatalar eskisi gibi detaylı açıklama almaya devam ediyor.
- **Versiyon hizalama**: `package.json`, `XsltCraft.Api.csproj`, `XsltCraft.Application.csproj`, `XsltCraft.Domain.csproj`, `XsltCraft.Infrastructure.csproj` ve README rozeti `1.3.1 → 1.3.2`.

---

## [1.3.1] - 2026-05-16

### Fixed
- **V2 şablon indirme boş body üretiyordu** (`TemplateController.Download`): Endpoint, kayıtlı block tree'yi her zaman V1 (`BlockTreeDto`, section-tabanlı) olarak deserialize edip `generator.Generate(tree)` çağırıyordu. Grid-canvas editörü şablonu V2 formatında (`version: 2`, `blocks[*].gridLayout`, `sections` yok) kaydettiği için V1 üretici `tree.Sections` üzerinde dönerken hiç blok bulamıyor ve `<body><div class="page"></div></body>` çıktısı veriyordu. Fix: `generator.GenerateFromJson(template.BlockTree)` kullanılıyor — JSON'daki `version` alanına göre V1/V2 dispatch otomatik. `/api/preview` ve `/api/preview/xslt` zaten doğru dispatch ediyordu; tutarsızlık yalnızca kayıtlı şablon indirme yolundaydı.

### Changed
- **Versiyon hizalama**: `package.json`, `XsltCraft.Api.csproj`, `XsltCraft.Application.csproj`, `XsltCraft.Domain.csproj`, `XsltCraft.Infrastructure.csproj` ve README rozeti `1.3.0 → 1.3.1`.

---

## [1.3.0] - 2026-05-12

### Added
- **`IntentClassifier`** (`Application/Ai/IntentClassifier.cs`): Kullanıcı mesajını üç dala ayırır — `Smalltalk` ("selam", "teşekkürler", "kimsin"), `General` ("XSLT nedir?", "XPath nasıl çalışır"), `Code` (kod referansı içeren veya varsayılan). Regex/heuristik tabanlı; Türkçe karakter folding'i `PatternSelector` ile aynı. Kelime sınırı kontrolü ile "sehir → hi" gibi false-positive'ler engellendi. `RefactorSelection` task'ı ve `Selection`/`XmlSelection` dolu olduğunda her zaman `Code`.
- **`XsltSummarizer`** (`Application/Ai/XsltSummarizer.cs`): 6.000 karakteri aşan XSLT dosyalarını yapısal özete indirir — stylesheet header + `TEMPLATE INDEX` (imzalar + satır no) + odak template (kullanıcı seçimi varsa) veya ilk birkaç template gövdesi (4K char bütçesi). Regex tabanlı; invalid/mid-edit XSLT'ye toleranslı. Tipik 16K char XSLT için %40-60 küçülme.
- **Ollama `keep_alive`** (`OllamaOptions.KeepAlive`, default `"30m"`): Modelin RAM'de tutulma süresi; cold-start gecikmesini ortadan kaldırır. `OllamaChatRequest` payload'una top-level alan olarak eklendi.
- **Ollama `num_keep`** (`OllamaOptions.NumKeep`, default `1024`): Context window aşıldığında prompt başından korunacak token sayısı — Identity + Constraints (persona/güvenlik) hiçbir koşulda atılmaz.
- **Test suite genişletildi**: `IntentClassifierTests` (26 case — smalltalk/general/code dalları, refactor override, kelime sınırı), `XsltSummarizerTests` (8 case — eşik altı raw, eşik üstü özet, odak template, malformed XML toleransı, küçülme oranı garantisi). Toplam Application.Tests: 27 → 67.

### Changed
- **System prompt sıralaması** (`PromptTemplates.cs`): Eski sıra `Identity → patterns → Constraints → project_context` idi. Yeni sıra **sabit → değişken** olarak yeniden düzenlendi: `Identity → Constraints → patterns → project_context`. Ollama KV prefix cache'i, oturum boyunca Identity + Constraints bloğunu tekrar prefill etmez; ardışık sorularda saniyeler kazanılır.
- **`PromptTemplates.BuildMessages`** (Assistant modu): Niyete göre bağlam paketi şekillendirilir. Smalltalk → sadece Identity (patterns/Constraints/XSLT/XML hepsi atlanır, history 2 çift); General → Identity + Constraints + patterns + project_context (XSLT/XML atlanır, history 5 çift); Code → tam paket + `XsltSummarizer.Compose` ile büyük XSLT özetlenir (history 10 çift).
- **`OllamaOptions.FirstTokenTimeoutSeconds`** (`AiOptions.cs`): Varsayılan `8 → 30`. Yerel model cold-start'ında ilk token gecikmesi 8 sn'yi sık aşıyordu; 3b/7b modeller için daha gerçekçi sınır. `appsettings.json` override değeri de `15 → 30` ve `appsettings.Development.example.json` `8 → 30`.
- **`AiProviderOrchestrator` hata mesajı** (`AiProviderOrchestrator.cs`): Sabit "Ollama'yı başlatın (ollama serve)" suffix'i her durumda eklenmiyor. Mesaj artık hata koduna göre seçilir — `ollama_first_token_timeout` → "Yerel model ısınıyor olabilir, birkaç saniye sonra tekrar deneyin.", `ollama_connect_timeout` / `ollama_unavailable` → "Ollama'yı başlatın (ollama serve)…", `ollama_model_not_found` → "Modeli indirin: 'ollama pull <model>'.". Kullanıcı yanıltıcı "Gemini'ye bağlanılamadı" mesajıyla karşılaşmıyor.
- **Varsayılan Ollama modeli** (`appsettings.json`, README): `qwen2.5-coder:7b → qwen2.5-coder:3b` — daha düşük RAM kullanımı, daha hızlı prefill.
- **Golden snapshot'lar** yeniden onaylandı (3 dosya): `Refactor_RefactorSelection`, `Assistant_FirstTurn`, `Assistant_ThirdTurn_WithHistory` — system prompt sıra değişikliğini yansıtır.
- **Versiyon hizalama**: `package.json`, `XsltCraft.Api.csproj`, `XsltCraft.Application.csproj`, `XsltCraft.Domain.csproj`, `XsltCraft.Infrastructure.csproj` ve README rozeti `1.2.1 → 1.3.0`.

### Performance
- **"Selam" senaryosu**: Eski prompt ~10K token (Identity + tüm patterns + Constraints + project_context + 16K char XSLT + history) → ~30 sn prefill. Yeni: yalnızca Identity → ~300 token → ~1-2 sn.
- **Büyük XSLT teknik sorusu**: 16K char → 6-8K char özet (Katman 2) → prefill %40-60 azalır.
- **Ardışık sorular aynı oturumda**: Sabit prefix Ollama KV cache hit (Katman 3) + model 30 dk RAM'de tutuluyor → ilk token cevabı ~3-5 sn (önceden her seferinde 15-30 sn cold-start).

---

## [1.2.1] - 2026-05-03

### Fixed
- **CI Format check**: `PromptTemplates.cs`, `PatternSelector.cs` ve `BuildMessagesGoldenTests.cs` üzerinde trailing whitespace temizlendi; `dotnet format --verify-no-changes` artık hatasız geçiyor (PR #8 backend job kırmızıdan yeşile döndü).
- **BuildMessages golden snapshots**: v1.2.0'da `Identity.md`'ye eklenen "Selamlama veya XSLT ile ilgisiz mesajlarda kısa ve doğal Türkçe yanıt ver" satırı sistem promptuna yansıdığı için 3 Verify golden snapshot (`Refactor_RefactorSelection`, `Assistant_FirstTurn`, `Assistant_ThirdTurn_WithHistory`) güncel çıktıyla yeniden kabul edildi. Lokal: 27/27 geçti.

### Changed
- **Versiyon hizalama**: `package.json`, `XsltCraft.Api.csproj`, `XsltCraft.Application.csproj`, `XsltCraft.Domain.csproj`, `XsltCraft.Infrastructure.csproj` ve README rozeti `1.2.0 → 1.2.1`.

---

## [1.2.0] - 2026-05-03

### Added
- **Hakkında sayfası** (`AboutPage.tsx`): Placeholder yerine standart bilgi sayfası — versiyon rozeti (`__APP_VERSION__`), uygulama açıklaması, 4 öne çıkan özellik kartı (XSLT Editör, Tema Kütüphanesi, AI Asistan, Taslaklar) ve telif satırı. `lucide-react` ikonları + Tailwind utility class'ları.
- **README** baştan sona güncellendi: V2 grid canvas, AI asistan, UBL-TR doğrulama, XSLT 2.0 / Saxon HE 10.9, MinIO, admin paneli ve güncel API tablosu.

### Changed
- **`OllamaOptions.NumCtx`** (`AiOptions.cs`): Varsayılan `8192`. Sistem promptu (Identity + patterns + Constraints + proje bağlamı) ~3-4K token'a ulaştı; `num_ctx` verilmezse 2048-token varsayılanı promptu keserek pattern kurallarını yutuyordu.
- **`OllamaChatOptions.NumCtx`** (`OllamaAssistantProvider.cs`): `num_ctx` parametresi Ollama `/api/chat` payload'una eklendi; `OllamaOptions.NumCtx`'ten beslenir.
- **`Identity.md`**: Selamlama veya XSLT-dışı mesajlarda kısa-doğal Türkçe yanıt kuralı eklendi (savunma 2. katman — `PatternSelector` fallback'ı kaçırırsa bile model zorla XSLT üretmez).
- **Versiyon hizalama**: `package.json`, `XsltCraft.Api.csproj`, `XsltCraft.Application.csproj`, `XsltCraft.Domain.csproj`, `XsltCraft.Infrastructure.csproj` ve README rozeti `1.1.0 → 1.2.0`.

### Fixed
- **"Selam" → hayali XSLT planı bug'ı** (`PatternSelector.cs`): Hiçbir pattern eşleşmeyen 0–1 kelimelik mesajlarda `Fallback()` artık çağrılmıyor (`[]` döner). Önceki davranış: kullanıcı `"selam"` yazdığında `["invoice-header", "invoice-line"]` injection yapılıyor + Constraints "Plan + xslt kodu" çıktısı zorluyordu → model hayali XSLT değişiklik planı + kod bloğu üretiyordu. Sınır: `wordCount >= 2 ? Fallback() : []`.
- **`PatternSelectorTests`**: Boş istek ve `"selam"` case'leri `[]` beklentisiyle güncellendi (26/26 test geçti).

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
