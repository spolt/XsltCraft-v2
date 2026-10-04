# Architecture — AI System

> Kod-gerçeği. Opsiyonel, feature-flagged. Bağlam: `docs/ecc/context/tech-stack.md`. **Anthropic sağlayıcısı YOKTUR.**

## Sağlayıcılar (`Infrastructure/Ai/`)
- **Birincil:** `OllamaAssistantProvider` — `qwen2.5-coder:3b`.
- **Yedek:** `GeminiAssistantProvider` — `gemini-2.5-flash` (`Ai:Gemini:Enabled`, varsayılan kapalı).
- **Orkestrasyon:** `AiProviderOrchestrator` — tercihe göre sıralar (örn. "gemini" → Gemini önce, Ollama fallback). Ollama erişilemezse/ilk token gelmezse diğerine düşer.
- Arayüz: `IAiAssistantProvider.StreamAsync(req, prompt, ct) : IAsyncEnumerable<AiChunk>` — `CancellationToken` zorunlu, varsayılan parametre yok. `SupportsVision`: Gemini = API key var; Ollama = `Ai:Ollama:VisionModel` dolu.

## Ekran görüntüsü / vision (1.11.0)
- **Hibrit model:** metin sohbeti `Ai:Ollama:Model`'de (`qwen2.5-coder`) kalır; yalnız görselli mesaj `VisionModel`'e (`qwen2.5vl:3b`) gider — kendi `VisionNumCtx`/`VisionKeepAlive`/ilk-token süresi ve daha küçük bağlam bütçesiyle (`OllamaOptions.VisionContextBudget`).
- **Yönlendirme:** `ProviderRouting.ResolveVision(preferred, ollamaVision, geminiVision, geminiFallback)` — `auto`/`gemini` → Gemini önce, yerel yedek; `ollama` → yerel önce, Gemini yalnız `ai.vision_gemini_fallback` açıksa. Karar `AiVisionRouter : IAiVisionAvailability`'de (DB bayrakları `AiFlagKeys`, appsettings fallback); orkestratör ve gate aynı kararı kullanır. Boş liste = kullanılamaz.
- **Kapı:** `Application/Ai/Vision/AiVisionGate` — kota kontrolünden ÖNCE: uygunluk → plan limiti (`MaxAiImagesPerMessage`: Free 1, Pro 3; teknik tavan `Ai:Vision:MaxImagesPerMessage`) → `AiVisionThrottle` (kullanıcı başına 10/dk) → `AiImageValidator` (PNG/JPEG magic byte = beyan MIME, APNG red, boyut başlıktan `Application/Imaging/ImageHeaderReader`, ≤2048 px / 4.2 MP, ≤1.5 MB / toplam 4 MB).
- **Geçicilik:** görsel DB/storage/log'a yazılmaz; yalnız eklendiği turda gönderilir. Geçmişte `AssistantMessage.ImageCount` → prompt'ta yer tutucu. Görselli turda system'e `Prompts/Core/Vision.md` eklenir (Constraints'ten sonra; metin-only prefix değişmez) ve intent `Code`'a zorlanır.
- **Kota:** `AiUsageAccounting` — görselli istekte hiçbir sağlayıcı çıktı üretmezse hak yanmaz; **istemci iptali sayılır**. Görsel başına `TokenCostPerImage` eklenir.
- **Payload:** Gemini `inline_data` part'ı metinden önce (görsel part'ında `text` yok); Ollama `messages[].images` yalnız son user mesajında. `BuildPayload` saf + `XsltCraft.Infrastructure.Tests`.
- **Frontend:** `ChatComposer` (📎, Ctrl+V, sürükle-bırak) → `utils/imageAttachment.prepareImage` (canvas ile ≤1536 px, PNG/JPEG küçüğü, ≤1.5 MB, EXIF silinir). Admin: `AiVisionCard` (`/admin/ai`).

## Streaming kuralları (kritik)
- **NDJSON:** `Response.BodyWriter` (`PipeWriter`) + her chunk sonrası `FlushAsync`. Buffer/`MemoryStream` **kullanma** — byte anında TCP'ye iner.
- **İki ayrı timeout:** `ConnectTimeoutSeconds` (TCP/TLS) + `FirstTokenTimeoutSeconds` (model warmup). Tek timeout yetmez.
- **Mid-stream fallback YOK:** ilk chunk geldikten sonra bağlantı koparsa yeni provider denenmez; kullanıcıya hata chunk'ı (model halüsinasyonu birleşmesin).

## Yönetim servisleri (`Infrastructure/Ai/`)
- `AiFeatureFlagService` — `ai.enabled` flag'i (DB `FeatureFlag` override eder `appsettings`'i); string/bool bayraklar (`ai.preferred_provider`, `ai.vision_enabled`, `ai.vision_gemini_fallback`); cache TTL ~15 sn.
- `AiTokenBudgetService` — per-user günlük token bütçesi (`UserAiUsage`).
- `AiProviderHealthService` — Ollama/Gemini canlı erişilebilirlik (admin paneli).
- Rate limit: `ai-assistant` (per-user partition, `Program.cs`) + ghost-text policy.

## Prompt pipeline (`Application/Ai/`)
- `PromptRegistry` — embedded markdown yükler: `Prompts/Core/{Identity,Constraints,Vision}.md` + `Prompts/Patterns/*.md` (8 UBL-TR pattern pack: InvoiceNote, InvoiceLine, LegalMonetaryTotal, InvoiceHeader, Supplier/CustomerPartyAddress, Supplier/CustomerPartyPerson).
- `PatternSelector` — tetikleyici-temelli seçim, Türkçe karakter folding, **istek başına ≤4 pattern**.
- `PromptTemplates.BuildMessages(req, mode)` — sistem+kullanıcı mesajını kurar, context'i kırpar (char limiti), XSLT özetini enjekte eder, geçmişi ekler. **Golden snapshot ile kilitli** (`Application.Tests/Ai/__snapshots__`).
- `IntentClassifier` — Code vs Smalltalk (kural-temelli).
- `XsltSummarizer` — XSLT yapısını regex ile özetler (`XDocument.Parse` kullanmaz; invalid/mid-edit XSLT'de güvenli).
- Modeller: `AiModels` (`AiRequest`, `AiChunk`, `AiTaskKind`, `AssistantMessage`), `AiMode` (Assistant/Refactor), `AiOptions`.

## Uçlar
- `AiAssistantController` — `GET /api/ai/status` (anon), `POST /api/ai/assistant` (NDJSON, rate-limited), `POST /api/ai/refactor-selection`.
- `AiAssistantController` — `GET /api/ai/status` → `{enabled, vision}`; `POST /api/ai/assistant` gövdesi `images:[{mimeType,data}]` alır (`[RequestSizeLimit(8 MB)]`).
- `AdminFeatureFlagsController` — `/api/admin/feature-flags/ai` (flag, provider, health, günlük kullanım, `ai/vision`). UI: `/admin/ai`.

## Güvenlik (prompt injection yüzeyi)
- AI çıktısı **kullanıcı onayı olmadan insert/exec edilmez**; insert öncesi `/api/preview/validate-xslt`.
- Kullanıcı metni sistem talimatıyla karışmamalı; üretilen/refactor edilen XSLT yine `XsltSafety` + render guard'larından geçer.
- **Görseldeki metin talimat değildir** (`Vision.md`); görselde görülen değerler koda literal yazılmaz, XPath'e bağlanır.
- Görselli soruların geri bildirimi `[ekran görüntüsü ekli]` işaretini taşır ve **global örneğe terfi edilemez** (cevap kullanıcıya özel görsel verisini yansıtabilir).

## Yeni AI özelliği eklerken
1. `AiTaskKind` ekle (`AiModels.cs`). 2. `PromptTemplates.Build` switch'ine `<output_format>` ekle. 3. Controller + rate-limit policy. 4. Frontend `aiAssistantService` + panel/Monaco. 5. `aiEnabled` kontrolü. 6. Golden snapshot güncelle (bilinçli kabul).
