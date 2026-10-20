# Architecture — Backend

> Kod-gerçeği. Bağlam: `docs/ecc/context/tech-stack.md`, kurallar: `docs/ecc/standards.md`, kararlar: `docs/ecc/decisions/`.

## Katmanlar (Clean Architecture)
`backend/XsltCraft.slnx` — 4 üretim + 2 test projesi.

- **`XsltCraft.Api`** (`backend/XsltCraft/`) — ASP.NET Core Web API. Controller'lar, `Program.cs` pipeline, rate limiting, CORS, auth.
- **`XsltCraft.Application`** — iş mantığı. `Preview/` (XSLT generator), `Ai/` (prompt pipeline), `Validation/` (UBL-TR kuralları + `XsltSafety`), `XPath/`, `Services/`, `Interfaces/`. **Infrastructure'a bağımlı değil.**
- **`XsltCraft.Domain`** — entity'ler (POCO).
- **`XsltCraft.Infrastructure`** — EF Core (`AppDbContext`), storage, XSLT motorları, AI sağlayıcıları, auth, DI. Application interface'lerini implemente eder.
- **Testler:** `XsltCraft.Tests` (generator/preview/eval), `XsltCraft.Application.Tests` (AI pipeline + Verify golden).

Bağımlılık yönü: `Api → Application → Domain`, `Infrastructure → Application/Domain`. DI tek yerde: `Infrastructure/DependecyInjection/ServiceCollectionExtensions.cs`.

## Request pipeline (`Program.cs`)
Sıra: `UseCors(dev|prod)` → `UseRateLimiter` → `UseAuthentication` → `UseAuthorization` → `MapControllers`.
- **Auth:** JWT Bearer (`AddAuthentication(JwtBearerDefaults...)`).
- **Rate limiting:** `auth-sensitive` + `auth-register` (fixed-window); `ai-assistant` (per-user partition policy). Endpoint'lerde `[EnableRateLimiting]`/`RequireRateLimiting`.
- **CORS:** `dev` ve `prod` adlı politikalar (`AllowedOrigins`).
- **Migration:** açılışta `db.Database.Migrate()` — migration'lar otomatik uygulanır.

## Controller haritası (`XsltCraft/Controllers/`)
Auth (`AuthController`, `UsersController`), admin (`AdminController` temalar, `AdminUsersController`, `AdminFeatureFlagsController`), şablon (`TemplateController`, `UserXsltTemplateController`, `UserSnippetsController`), önizleme/üretim (`PreviewController`, `RenderController`, `DocumentController`), araçlar (`UblTrController`, `XPathController`, `AssetsController`), AI (`AiAssistantController`).

## XSLT üretim & render (en kritik yol)
- **Generator:** `Application/Preview/XsltGeneratorService.cs` (`IXsltGeneratorService`) — `Generate` (V1 section), `GenerateV2` (grid), `GenerateFromJson` (dispatch). Çıktı `version="2.0"`, UBL-TR namespace'leri. `Validate()` → `XsltSafety.FindThreat` (fail-closed güvenlik) + `XslCompiledTransform.Load` (derlenebilirlik).
- **Hazır tema logo/imza yuvaları** (`Application/ThemeSlots/`, saf servisler): yüklemede `IThemeUploadProcessor` → önceden gömülü görsel bloğu kararı (`IThemeOverridesProcessor`: 409 kaldır/koru) → `IThemeSlotAnalyzer` (çapa tespiti `ThemeAnchorDetector`, temadaki statik görseller + rol önerisi `ThemeImageDetector` (admin 409'da logo/imza/tasarım seçer; seçilen `<img>` `data-xc-placeholder` ile yuva olur — R10; "kullanıcı kaldırabilir" → `data-xc-removable`, compose `hideThemeImages`; "temadan kaldır" → analizden önce `ThemeImageStripper`: tasarım görseli silinir, logo/imzanın `src`'si saydam görselle değişir), kurallar R1–R10 `ThemeSlotRules`, ekleme noktası `InsertionPointResolver`, işaretler `ThemeSlotMarkers`, örnek belgeyle render doğrulaması). Kaynak, `XsltSourceMap` + `SourceEdits` ile yeniden serileştirilmeden cerrahi düzenlenir. Katalog `Template.SlotCatalog` (jsonb); etiketler `ThemeSlotLabels`'ta (DB'de değil). Kullanıcı görselleri `IThemeAssetComposer` (`POST /api/preview/compose`, `ThemeComposeController`) ile `DEV-MODE-OVERRIDES` bloğuna JSON veri + sabit `ThemeAssetRuntime.js` (EmbeddedResource; `<`, `>`, `&` içermez) olarak eklenir. Sunucunun değiştirdiği tema kaydedilmeden önce son kapıdan geçer (`ThemeUploadProcessor`: `XsltSafety.FindThreat` + `SecureXslt.Compile`; başarısız analizde görsel kaldırma/koru kararı → 422). Compose isteği doğrulaması `ThemeComposeRequestValidator`'da. Hazır tema XSLT erişimi tek kural: `Application/Themes/IThemeAccessGuard`. Render ortak `Application/Xslt/IXsltHtmlRenderer` (tema/ham önizleme `XslCompiledTransform` ile — Saxon değil).
- **İki render motoru** (`Infrastructure/Xslt/`):
  - `XslCompiledTransform` (XSLT 1.0) — önizleme/doğrulama; `XsltSettings(enableDocumentFunction:false, enableScript:false)` ile sertleştirilmiş. Kullanım: `PreviewController`, `XsltCompiler`, `TemplateCache`.
  - **Saxon HE 10.9** (XSLT 2.0) — `XsltTemplateRenderer.RenderAsync` free-theme render; `XmlResolver.ThrowingResolver` ile harici çözümleme reddedilir.
- **Güvenlik:** her XML/XSLT parse noktasında XXE guard (`DtdProcessing.Prohibit`, `XmlResolver=null`); kullanıcı XPath'i `XsltSafety` ile taranır. Detay: `docs/ecc/context/constraints.md`.

## Storage & dosya-tabanlı template motoru
- **Object storage:** `IStorageService` — `LocalStorageService` (dev), `S3StorageService`/MinIO (prod). `.xslt`/asset storage'a yazılır, DB `storagePath` tutar.
- **Dosya-tabanlı template'ler (legacy):** `Infrastructure/Templates/` (`TemplateRegistry`/`Cache`/`DiscoveryEngine`/`HotReload`) + `FileTemplateRepository` — `ContentRoot/templates/<guid>/template.xslt` keşfeder, `IsFreeTheme=true`. `/api/render` bu yolu kullanır (DB kullanıcı template'i değil).

## Notlar / tuzaklar
- Application static service / Infrastructure bağımlılığı eklenmemeli (bkz. `standards.md`).
- `Generate` `(string? Xslt, string? Error)` döndürür — fail-closed; çağıran `Error`'ı kontrol etmeli.
- Yeni AI/render/parse noktası eklerken güvenlik checklist: `.claude/skills/xss-xxe-checklist`.
