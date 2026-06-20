# Architecture — AI System

> Kod-gerçeği. Opsiyonel, feature-flagged. Bağlam: `docs/ecc/context/tech-stack.md`. **Anthropic sağlayıcısı YOKTUR.**

## Sağlayıcılar (`Infrastructure/Ai/`)
- **Birincil:** `OllamaAssistantProvider` — `qwen2.5-coder:3b`.
- **Yedek:** `GeminiAssistantProvider` — `gemini-2.5-flash` (`Ai:Gemini:Enabled`, varsayılan kapalı).
- **Orkestrasyon:** `AiProviderOrchestrator` — tercihe göre sıralar (örn. "gemini" → Gemini önce, Ollama fallback). Ollama erişilemezse/ilk token gelmezse diğerine düşer.
- Arayüz: `IAiAssistantProvider.StreamAsync(req, prompt, ct) : IAsyncEnumerable<AiChunk>` — `CancellationToken` zorunlu, varsayılan parametre yok.

## Streaming kuralları (kritik)
- **NDJSON:** `Response.BodyWriter` (`PipeWriter`) + her chunk sonrası `FlushAsync`. Buffer/`MemoryStream` **kullanma** — byte anında TCP'ye iner.
- **İki ayrı timeout:** `ConnectTimeoutSeconds` (TCP/TLS) + `FirstTokenTimeoutSeconds` (model warmup). Tek timeout yetmez.
- **Mid-stream fallback YOK:** ilk chunk geldikten sonra bağlantı koparsa yeni provider denenmez; kullanıcıya hata chunk'ı (model halüsinasyonu birleşmesin).

## Yönetim servisleri (`Infrastructure/Ai/`)
- `AiFeatureFlagService` — `ai.enabled` flag'i (DB `FeatureFlag` override eder `appsettings`'i); cache TTL ~15 sn.
- `AiTokenBudgetService` — per-user günlük token bütçesi (`UserAiUsage`).
- `AiProviderHealthService` — Ollama/Gemini canlı erişilebilirlik (admin paneli).
- Rate limit: `ai-assistant` (per-user partition, `Program.cs`) + ghost-text policy.

## Prompt pipeline (`Application/Ai/`)
- `PromptRegistry` — embedded markdown yükler: `Prompts/Core/{Identity,Constraints}.md` + `Prompts/Patterns/*.md` (8 UBL-TR pattern pack: InvoiceNote, InvoiceLine, LegalMonetaryTotal, InvoiceHeader, Supplier/CustomerPartyAddress, Supplier/CustomerPartyPerson).
- `PatternSelector` — tetikleyici-temelli seçim, Türkçe karakter folding, **istek başına ≤4 pattern**.
- `PromptTemplates.BuildMessages(req, mode)` — sistem+kullanıcı mesajını kurar, context'i kırpar (char limiti), XSLT özetini enjekte eder, geçmişi ekler. **Golden snapshot ile kilitli** (`Application.Tests/Ai/__snapshots__`).
- `IntentClassifier` — Code vs Smalltalk (kural-temelli).
- `XsltSummarizer` — XSLT yapısını regex ile özetler (`XDocument.Parse` kullanmaz; invalid/mid-edit XSLT'de güvenli).
- Modeller: `AiModels` (`AiRequest`, `AiChunk`, `AiTaskKind`, `AssistantMessage`), `AiMode` (Assistant/Refactor), `AiOptions`.

## Uçlar
- `AiAssistantController` — `GET /api/ai/status` (anon), `POST /api/ai/assistant` (NDJSON, rate-limited), `POST /api/ai/refactor-selection`.
- `AdminFeatureFlagsController` — `/api/admin/feature-flags/ai` (flag, provider, health, günlük kullanım). UI: `/admin/ai`.

## Güvenlik (prompt injection yüzeyi)
- AI çıktısı **kullanıcı onayı olmadan insert/exec edilmez**; insert öncesi `/api/preview/validate-xslt`.
- Kullanıcı metni sistem talimatıyla karışmamalı; üretilen/refactor edilen XSLT yine `XsltSafety` + render guard'larından geçer.

## Yeni AI özelliği eklerken
1. `AiTaskKind` ekle (`AiModels.cs`). 2. `PromptTemplates.Build` switch'ine `<output_format>` ekle. 3. Controller + rate-limit policy. 4. Frontend `aiAssistantService` + panel/Monaco. 5. `aiEnabled` kontrolü. 6. Golden snapshot güncelle (bilinçli kabul).
