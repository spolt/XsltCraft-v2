# Architecture — Backend

> Kod-gerçeği. Bağlam: `docs/ecc/context/tech-stack.md`, kurallar: `docs/ecc/standards.md`, kararlar: `docs/ecc/decisions/`.

## Katmanlar (Clean Architecture)
`backend/XsltCraft.slnx` — 7 üretim + 5 test projesi.

| Proje | Rol |
|---|---|
| **`XsltCraft.Api`** (`backend/XsltCraft/`) | ASP.NET Core Web API: controller'lar, `Program.cs`, rate limiting, CORS, auth; `Xslt/` (`XsltProblemResults` hata sözleşmesi, `RateLimitPolicies`), `Preview/` (`PreviewAssetEmbedder`, `GeneratedXsltCache`) |
| **`XsltCraft.Application`** | İş mantığı: `Preview/` (generator, `XPathPortability`), `Xslt/Engines/` (`IXsltEngine`, `DotNetXsltEngine`, sniffer/selector, `XsltFailureKind`), `Xslt/Execution/` (`XsltExecutionService`, `XsltExecutionGuard`, port'lar), `Validation/` (UBL-TR + `XsltSafety`), `Ai/`, `XPath/`. **Infrastructure'a bağımlı değil** |
| **`XsltCraft.Domain`** | Entity'ler (POCO) |
| **`XsltCraft.Infrastructure`** | EF Core, storage, AI sağlayıcıları, auth, DI; `Xslt/Worker/*` (havuz, `WorkerXsltHost`), `Xslt/InProcess/*` (grid host'u, bozulmuş mod), `Xslt/XsltUserGate` |
| **`XsltCraft.Xslt.Saxon`** | Saxon-HE 12.10 / IKVM — repodaki **tek** IKVM/Maven projesi; sandbox (`SaxonProcessorFactory`), limitler, teşhis. Yalnız Application'a referans |
| **`XsltCraft.XsltWorker`** | İzole worker exe'si (stdin/stdout çerçeve protokolü, 64 MB motor thread'i). Infrastructure'a referans **vermez** |
| **`XsltCraft.XsltWorker.Contracts`** | Bağımlılıksız IPC sözleşmesi (`FrameCodec`, mesajlar) |
| Testler | `XsltCraft.Tests` (generator, XsltSafety, rotalar, `LayeringTests`, eval), `XsltCraft.Application.Tests` (AI pipeline + Verify golden), `XsltCraft.Infrastructure.Tests` (süreç içi host, guard, user gate), `XsltCraft.Xslt.Saxon.Tests` (sandbox/K3, limitler, IKVM akışı), `XsltCraft.XsltWorker.Tests` (gerçek süreç havuzu, izolasyon, grid paritesi, uç testleri) |

Bağımlılık yönü: `Api → Application → Domain`, `Infrastructure → Application/Domain/Contracts/Xslt.Saxon`, `XsltWorker → Contracts/Xslt.Saxon (→ Application)` (`LayeringTests` bloke eder). DI tek yerde: `Infrastructure/DependecyInjection/ServiceCollectionExtensions.cs` (`AddXsltExecution`).

## Request pipeline (`Program.cs`)
Sıra: `UseCors(dev|prod)` → `UseAuthentication` → `UseRateLimiter` → `UseAuthorization` → `MapControllers` (rate limiter kimlikten sonra: kullanıcı bölümlü politikalar gerçekten kullanıcı bazlı).
- **Auth:** JWT Bearer (`AddAuthentication(JwtBearerDefaults...)`).
- **Rate limiting:** `auth-sensitive` + `auth-register` (IP bazlı); `ai-assistant`, `theme-compose`, `auth-refresh`, `activity`; XSLT: `xslt-preview` / `xslt-validate` (120/dk/kullanıcı), `theme-preview` (30/dk kullanıcı, anonimde IP). `ForwardedHeaders` yok — nginx arkasında IP politikaları tek kovaya düşer.
- **Süreç sertleştirme:** `ProcessHardening.Apply` (Linux `PR_SET_DUMPABLE=0`).
- **CORS:** `dev` ve `prod` adlı politikalar (`AllowedOrigins`).
- **Migration:** açılışta `db.Database.Migrate()` — migration'lar otomatik uygulanır.

## Controller haritası (`XsltCraft/Controllers/`)
Auth (`AuthController`, `UsersController`), admin (`AdminController` temalar, `AdminUsersController`, `AdminFeatureFlagsController`), şablon (`TemplateController`, `UserXsltTemplateController`, `UserSnippetsController`), önizleme/üretim (`PreviewController`, `ThemeComposeController`; `RenderController`/`DocumentController` 2.0.0'da kaldırıldı), araçlar (`UblTrController`, `XPathController`, `AssetsController`), AI (`AiAssistantController`).

## XSLT üretim & render (en kritik yol)
- **Generator:** `Application/Preview/XsltGeneratorService.cs` (`IXsltGeneratorService`) — `Generate` (V1 section), `GenerateV2` (grid), `GenerateFromJson` (dispatch). Çıktı `version="2.0"`, UBL-TR namespace'leri, içerik **çift-motor güvenli** XPath 1.0 altkümesi (`XPathPortability`; taşınamaz bağlamalı blok → `xc-binding-error` yer tutucusu + `Warnings`). `Validate()` → `XsltSafety` Execute + `SecureXslt.Compile` (derlenebilirlik). Görseller **her zaman base64 gömülü** (`PreviewAssetEmbedder`: sahiplik/erişilebilir tema kontrolü; URL yedeği yok); çıktıya açıklama yorumu yazılmaz. İndirilen XSLT önbelleği `templates/{id}.b64.xslt` (`GeneratedXsltCache`).
- **Hazır tema logo/imza yuvaları** (`Application/ThemeSlots/`, saf servisler): yüklemede `IThemeUploadProcessor` → önceden gömülü görsel bloğu kararı (`IThemeOverridesProcessor`: 409 kaldır/koru) → `IThemeSlotAnalyzer` (çapa tespiti `ThemeAnchorDetector`, temadaki statik görseller + rol önerisi `ThemeImageDetector` (admin 409'da logo/imza/tasarım seçer; seçilen `<img>` `data-xc-placeholder` ile yuva olur — R10; "kullanıcı kaldırabilir" → `data-xc-removable`, compose `hideThemeImages`; "temadan kaldır" → analizden önce `ThemeImageStripper`: tasarım görseli silinir, logo/imzanın `src`'si saydam görselle değişir), kurallar R1–R10 `ThemeSlotRules`, ekleme noktası `InsertionPointResolver`, işaretler `ThemeSlotMarkers`, örnek belgeyle render doğrulaması). Kaynak, `XsltSourceMap` + `SourceEdits` ile yeniden serileştirilmeden cerrahi düzenlenir. Katalog `Template.SlotCatalog` (jsonb); etiketler `ThemeSlotLabels`'ta (DB'de değil). Kullanıcı görselleri `IThemeAssetComposer` (`POST /api/preview/compose`, `ThemeComposeController`) ile `DEV-MODE-OVERRIDES` bloğuna JSON veri + sabit `ThemeAssetRuntime.js` (EmbeddedResource; `<`, `>`, `&` içermez) olarak eklenir. Yüklenen tema önce örnek belgeyle derlenip render edilir (`ThemeUploadProcessor`; başarısızsa 422 — karar sorulmadan). Sunucunun değiştirdiği tema kaydedilmeden önce son kapıdan geçer (`ThemeUploadProcessor`: `XsltSafety.FindThreat` + `SecureXslt.Compile`; başarısız analizde görsel kaldırma/koru kararı → 422). Compose isteği doğrulaması `ThemeComposeRequestValidator`'da. Hazır tema XSLT erişimi tek kural: `Application/Themes/IThemeAccessGuard`. Tema yükleme/analiz/compose render'ı `Application/Xslt/IXsltHtmlRenderer` (`SecureXslt`, .NET) ile kalır (T-izi).

## XSLT yürütme (önizleme / doğrulama) — ADR 007, ADR 008
Tüm önizleme/doğrulama uçları `IXsltExecutionService.ExecuteAsync(XsltRunRequest)`'ten geçer: boyut (karakter + UTF-8 bayt) → `XsltSafety` Execute → sürüm sniff (`> 2.0` → `Unsupported`) → motor seçimi → yer → `XsltUserGate` → host.

| Rota | Uç | Yer | Motor |
|---|---|---|---|
| Raw (+ Validate) | `preview/raw`, `preview/validate-xslt` | Worker (`WorkerXsltHost` → `XsltWorkerPool`) | `Xslt:Engine:Raw` = `Auto` (1.0 → .NET, 2.0 → Saxon) |
| UserTemplate | `preview/user-template/{id}` | Worker | `Xslt:Engine:Grid` |
| Grid | `preview` | Süreç içi (`InProcessXsltHost` + `XsltExecutionGuard`); kaçak ≥ 2 → worker'a (bozulmuş mod) | `Xslt:Engine:Grid` = `DotNet` (2.0.0) |
| Theme | `preview/theme/{id}` | Süreç içi, grid sınırları | Sabit .NET |

- **Motorlar:** `DotNetXsltEngine` (`XslCompiledTransform`, yalnız `SecureXslt`) ve `SaxonXsltEngine` (Saxon-HE 12.10, sandbox `SaxonProcessorFactory`; profiller `Isolated` worker'da, `InProcessGrid` süreç içinde). Saxon/IKVM yalnız `XsltCraft.Xslt.Saxon`'da.
- **Hata sözleşmesi:** `XsltFailureKind` → `XsltProblemResults` (`{error, line, column, kind, code, engine}`; içerik 400, `Busy`/`EngineUnavailable` 503 + `Retry-After`, `UserBusy` 429, `EngineInternal` 500). Başarı gövdesi `warnings` taşır.
- **Ayarlar:** `appsettings` `Xslt` (`Engine`, `Limits`, `Grid`, `UserGate`) ve `XsltWorker` (`Enabled`, `PoolSize` 2, süre/bellek). `XsltWorker:Enabled=false` → 1.0 süreç içi .NET, 2.0 → 503.
- **Güvenlik:** her XML/XSLT parse noktasında XXE guard; Saxon yolunda tek yapılandırma `SaxonProcessorFactory` + stylesheet ön okuma. Detay: `docs/ecc/context/constraints.md`.

## Storage
- **Object storage:** `IStorageService` — `LocalStorageService` (dev), `S3StorageService`/MinIO (prod). `.xslt`/asset storage'a yazılır, DB `storagePath` tutar. Dosya-tabanlı template motoru (`Infrastructure/Templates/`, `/api/render`) 2.0.0'da kaldırıldı.

## Notlar / tuzaklar
- Application static service / Infrastructure bağımlılığı eklenmemeli (bkz. `standards.md`).
- `Generate` `(string? Xslt, string? Error)` döndürür — fail-closed; çağıran `Error`'ı kontrol etmeli.
- Yeni AI/render/parse noktası eklerken güvenlik checklist: `.claude/skills/xss-xxe-checklist`.
