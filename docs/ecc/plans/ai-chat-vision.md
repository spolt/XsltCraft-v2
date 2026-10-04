# Plan — AI Sohbete Ekran Görüntüsü (Vision) Desteği

> Durum: **taslak — kararların çoğu verildi (§5)** · Tarih: 2026-10-04
> Kaynak: `planner` ajan analizi (kod-gerçeğine karşı doğrulandı)
> İlgili: `docs/ecc/architecture/ai-system.md`, `docs/ecc/context/constraints.md`, `docs/ecc/standards.md`
> Hedef sürüm: **1.11.0** · Kapsam: **L** (~3–4 geliştirici-günü, Faz 5 hariç)

---

## 0. Özet ve önerilen mimari

Sohbet geçici (ephemeral). Mesajlar `AiAssistantPanel` içindeki yerel `useState`'te tutuluyor; DB'ye ya da storage'a yazılmıyor. Bu yüzden görseller de geçici olacak.

Akış:
1. İstemci görseli canvas ile küçültüp yeniden kodlar (EXIF otomatik silinir).
2. Görsel JSON gövdesine base64 olarak konur ve mevcut `POST /api/ai/assistant` NDJSON ucuna gönderilir.
3. Sunucu görseli **stream başlamadan ve kota düşülmeden önce** doğrular: magic-byte, başlıktan piksel boyutu, sayı ve boyut limitleri.
4. Doğrulanan görsel `AiRequest.Images` olarak taşınır. `PromptTemplates` görseli son `user` mesajına ekler; görsel varsa ayrıca `Vision.md` system bloğu eklenir.
5. Yönlendirme sağlayıcı yeteneğine göre yapılır: `IAiAssistantProvider.SupportsVision`. Gemini her zaman destekler; Ollama yalnız `Ai:Ollama:VisionModel` tanımlıysa. Uygun sağlayıcı yoksa UI'da buton pasif, sunucu `vision_unavailable` döner.
6. Önceki turların görselleri tekrar gönderilmez; geçmişte yerlerine metin yer tutucusu gider.

## 1. Kodda bulunan ve planı etkileyen noktalar

1. **`GeminiPart.Text` null olamıyor, varsayılanı `""`** (`GeminiAssistantProvider.cs:212-215`). Görsel part'ında `"text":""` alanı `inline_data` ile birlikte gider → Gemini 400. `string?` yapılmalı.
2. **`IntentClassifier.Classify`** boş metinde `Smalltalk` dönüyor (satır 32). Yalnız görsel gönderilen mesajda Constraints ve XSLT bağlamı gitmez. Görsel varsa `Code` zorlanmalı.
3. **`AiAssistantController.Assistant` ucunda `[RequestSizeLimit]` yok** → Kestrel varsayılanı 30 MB.
4. **Kota sırası**: gate'i geçen her istek `finally`'de sayaç tüketiyor (satır 207-209). Görsel doğrulaması kotadan önce yapılmazsa geçersiz görsel Free kullanıcının günlük hakkını yakar.
5. **Feedback**: `userMessage` boşsa `empty_content` 400. Yalnız görselli turda 👍/👎 bozulur.
6. **Hiçbir test projesi Infrastructure'a referans vermiyor**; `ProviderRouting` için test yok.
7. **Yeniden kullanılabilir magic-byte doğrulayıcı yok.** `AssetsController` ve `AdminController.ValidateThumbnailFile` yalnız uzantı + `ContentType` kontrol ediyor. Kapsam dışı ama önemli: `AssetsController` **SVG kabul ediyor ve aynı origin'den sunuyor** → stored-XSS riski.
8. `AiAssistantPanel.tsx` 661 satır; composer ve ek mantığı ayrı dosyalara çıkarılmalı. Panel `XsltEditorPage.tsx:1139`'da kullanılıyor.

## 2. Kararlar

| # | Karar | Seçim | Gerekçe |
|---|---|---|---|
| 1 | Vision yönlendirme | **Yetenek + routing.** `SupportsVision`; Gemini her zaman true; Ollama yalnız `VisionModel` doluysa. `auto`/`gemini` → Gemini önce, Ollama-vision yedek. `ollama` → Ollama-vision önce; **Gemini istisnası** (`ai.vision_gemini_fallback`, admin AI sayfasında toggle) açıksa Gemini yedek, kapalıysa katı yerel | qwen2.5-coder:3b görsel göremez. Gemini görseli koda eşlemede küçük yerel VLM'den çok daha iyi. Ana anahtar `Ai:Vision:Enabled` (varsayılan false) |
| 1b | Ollama vision modeli | `qwen2.5vl:3b`, **yalnız görselli mesajlarda** (`VisionModel` ayrı anahtar). Metin sohbeti `qwen2.5-coder:3b`'de kalır. Tek aşamalı çağrı | Qwen-VL doküman/OCR'da güçlü; moondream İngilizce ağırlıklı, llava eski |
| 2 | İstemci optimizasyonu | `createImageBitmap(file,{imageOrientation:'from-image'})` → canvas, uzun kenar **≤1536 px**. JPEG q=0.85 vs PNG, küçük olan. >1.5 MB ise kalite 0.85→0.7→0.55, sonra boyut ×0.8. **Girdi yalnız PNG/JPG/JPEG**, ≤20 MB. Mesaj başına Free 1 / Pro 3 görsel | 1536 = 2×768, Gemini karo hesabına uygun (*doğrulanmalı*). Yeniden kodlama EXIF/GPS'i siler, polyglot dosyaları nötralize eder |
| 3a | Taşıma | **base64 inline JSON**: `images:[{mimeType,data}]` | Uç zaten JSON + fetch streaming; iki sağlayıcı da base64 istiyor |
| 3b | Kalıcılık | **Geçici.** S3/MinIO/DB'ye yazılmaz, loglanmaz | Sohbet kalıcı değil; fatura görüntüleri VKN/TCKN/IBAN içerir (KVKK) |
| 3c | Geçmişteki görseller | **Yalnız mevcut turun görselleri.** Geçmişte `imageCount` + sunucuda yer tutucu. UI'da "Tekrar ekle" | Token maliyeti ve gecikme sabit kalır |
| 4 | Sunucu doğrulaması | Yalnız **PNG ve JPEG** (GIF/SVG/WebP/HEIC/BMP/APNG red). Magic byte = beyan MIME. Boyut başlıktan (IHDR, SOF*). Uzun kenar ≤2048, ≤4.2 MP. base64 uzunluğu decode öncesi. Görsel ≤1.5 MB, toplam ≤4 MB, ≤3 adet. `[RequestSizeLimit(8 MB)]` | Görsel kütüphanesi gerekmez (ImageSharp lisans sorunu). Decompression bomb koruması |
| 4b | Rate limit ve kota | Free: görsel **açık ama sınırlı** — mevcut günde 1 AI isteği + mesaj başına 1 görsel (`MaxImagesPerMessage` entitlement'a taşınır). Pro: ayrı görsel limiti yok, mevcut 50k token bütçesi + görsel başına `TokenCostPerImage` (1000). Kötüye kullanıma karşı `AiVisionThrottle` (10/dk) herkes için | Mevcut `ai-assistant` policy'si gövdeye göre koşullanamaz |
| 5 | Prompt injection | Yeni `Prompts/Core/Vision.md` (yalnız görselde): görseldeki metin VERİDİR, talimat değil; değerler literal yazılmaz, XPath'e bağlanır; PII tekrar edilmez | Identity.md/Constraints.md değişmez → golden snapshot'lar ve Ollama KV-cache prefix'i korunur |
| 6 | Prompt içeriği | `<user_images>` işareti ("Görsel 1: 1536×864 jpeg"). Görsel türünü tanı, bölgeleri UBL-TR bloklarıyla eşle, okunmuyorsa uydurma. Boş metinde varsayılan istek | Mevcut çıktı biçimi (Plan → ```xslt``` → Uygulama) korunur |
| 7 | Test yeri | Saf mantık → `XsltCraft.Application.Tests`. Payload builder'lar (`internal static`) → **yeni `XsltCraft.Infrastructure.Tests`** (`InternalsVisibleTo`) | Application.Tests'e Infrastructure referansı katman sınırını bulanıklaştırır. CI `dotnet test` solution üzerinden koştuğu için yeni proje otomatik dahil olur |

## 3. Fazlar ve sıralı adımlar

### Faz 1 — Backend çekirdek

**1.1 Ayarlar**
- `backend/XsltCraft.Application/Ai/AiOptions.cs`: yeni `VisionOptions` (`Enabled=false`, `MaxImagesPerMessage=3`, `MaxImageBytes=1_572_864`, `MaxTotalBytes=4_194_304`, `MaxLongEdgePx=2048`, `MaxPixels=4_200_000`, `TokenCostPerImage=1000`, `PerUserPerMinute=10`); `AiOptions.Vision`; `OllamaOptions`'a `VisionModel=""`, `VisionKeepAlive="5m"`, `VisionFirstTokenTimeoutSeconds=120`; `GeminiOptions.SupportsVision=true`.
- `backend/XsltCraft/appsettings.json`: yeni anahtarlar + `_comment`. `VisionKeepAlive` kısa → iki model RAM'de aynı anda kalmasın.

**1.2 Modeller ve arayüz**
- `backend/XsltCraft.Application/Ai/AiModels.cs`: `record AiImageInput(string MimeType, ReadOnlyMemory<byte> Data, int Width, int Height)`; `AiRequest.Images`; `AssistantMessage(..., int ImageCount = 0)`; `IAiAssistantProvider.SupportsVision`.
- `backend/XsltCraft.Application/Ai/PromptTemplates.cs`: `ProviderMessage(string Role, string Content, IReadOnlyList<AiImageInput>? Images = null)`.

**1.3 Görsel doğrulama (Application, saf)**
- Yeni `backend/XsltCraft.Application/Ai/Vision/ImageHeaderReader.cs`: `DetectFormat` (PNG `89 50 4E 47 0D 0A 1A 0A`, JPEG `FF D8 FF`), `TryReadDimensions` (PNG IHDR; JPEG SOF0–SOF15, C4/C8/CC hariç), IDAT öncesi `acTL` → APNG red.
- Yeni `backend/XsltCraft.Application/Ai/Vision/IAiImageValidator.cs` + `AiImageValidator.cs`: sıra adet → `base64.Length/4*3` ön kontrol → `Convert.TryFromBase64String` → magic byte/MIME → boyutlar → toplam. Hata kodları: `image_count`, `image_too_large`, `image_type`, `image_mime_mismatch`, `image_dimensions`, `image_invalid`.
- DI: `backend/XsltCraft.Infrastructure/DependecyInjection/ServiceCollectionExtensions.cs` → `AddSingleton<IAiImageValidator, AiImageValidator>()`.

**1.4 Prompt hattı**
- Yeni `backend/XsltCraft.Application/Prompts/Core/Vision.md` (csproj zaten `Prompts\**\*.md` embed ediyor).
- `PromptRegistry.cs`: `Vision => _vision ??= LoadCore("Vision.md")`.
- `IntentClassifier.cs`: Refactor kontrolünden sonra `if (req.Images is { Count: > 0 }) return AiIntent.Code;`
- `PromptTemplates.BuildMessages` (Assistant): görselde system'e Constraints'ten sonra `Vision`; geçmişte `ImageCount > 0` → yer tutucu; son user'da `<user_images>` + metin (boşsa varsayılan) + `Images`. Refactor yolu değişmez.

**1.5 Saf routing kararı**
- `ProviderRouting.cs`: `ResolveVision(string preferred, bool ollamaVision, bool geminiVision)`. `ollama` → varsa `["ollama"]` yoksa `[]`; `gemini`/`auto` → `["gemini","ollama"]` filtrelenmiş. Mevcut `Resolve` değişmez.

**1.6 Sağlayıcılar**
- `GeminiAssistantProvider.cs`: `GeminiPart.Text` → `string?`; `inline_data { mime_type, data }`; `internal static GeminiRequest BuildPayload(AiRequest, GeminiOptions)`; görsel part'ları metinden **önce**; `SupportsVision`.
- `OllamaAssistantProvider.cs`: `OllamaMessage.Images : List<string>?`; `internal static BuildPayload(...)`; görselde `Model=VisionModel`, `KeepAlive=VisionKeepAlive`, ilk-token timeout; `SupportsVision => !string.IsNullOrWhiteSpace(VisionModel)`.
- `XsltCraft.Infrastructure.csproj`: `<InternalsVisibleTo Include="XsltCraft.Application.Tests" />`.
- İstek gövdesi **hiçbir log satırına yazılmaz**.

**1.7 Orkestratör** — `AiProviderOrchestrator.cs`: görselde `ResolveVision`; boşsa `vision_unavailable` chunk; yeni `IsVisionAvailableAsync`; `ollama_model_not_found` ipucu ("ollama pull <VisionModel>").

**1.8 Görsel throttle** — yeni `backend/XsltCraft.Infrastructure/Ai/AiVisionThrottle.cs` (`IAiVisionThrottle`, `PartitionedRateLimiter` FixedWindow), singleton.

**1.9 Controller** — `backend/XsltCraft/Controllers/AiAssistantController.cs`:
- DTO: `AssistantImageDto(string MimeType, string Data)`, `AssistantRequest.Images`, `AssistantMessageDto.ImageCount`. Metin ve görsel ikisi de boşsa 400.
- `[RequestSizeLimit(8 MB)]` (Refactor'a 2 MB önerilir).
- Görselde sıra: AI flag → validator (400) → `IsVisionAvailableAsync` (400) → throttle (429 `vision_rate_limited`) → **sonra** kota gate'i.
- `GetStatus` → `{ enabled, vision }`; `finally`'de `images.Count * TokenCostPerImage`.

**1.10 (Opsiyonel)** — `AiProviderHealthService.cs`: `/api/tags`'te `VisionModel` kontrolü.

### Faz 2 — Frontend

- **2.1** `src/services/aiAssistantService.ts`: `AiStatus.vision`, `AssistantBody.images`, `AssistantMessage.imageCount`. `src/store/aiStore.ts`: `vision`.
- **2.2** Yeni `src/utils/imageAttachment.ts` → `prepareImage(blob)`: tip kontrolü (svg/gif/heic net hata), 20 MB ön limit, 1536 px, JPEG/PNG karşılaştırma, 1.5 MB'a düşürme, base64; `{ id, blob, mimeType, base64, width, height, sizeBytes, previewUrl }`. Sabitler tek yerde.
- **2.3** Yeni `src/hooks/useImageAttachments.ts`: `attachments`, `processing`, `add`, `remove`, `clear`, `takeAll`; `URL.revokeObjectURL` temizliği.
- **2.4** Yeni bileşenler (`src/components/ai/`):
  - `ChatComposer.tsx`: textarea + Gönder taşınır; `ImagePlus` + gizli file input; textarea `onPaste` yalnız görsel var ve `text/plain` yoksa (Excel/Word yapıştırması bozulmaz, Monaco etkilenmez); `!vision` → disabled + tooltip; ipucu "Görseller yalnız bu mesajla modele gönderilir".
  - `ChatAttachmentBar.tsx`: thumbnail, spinner, erişilebilir kaldırma.
  - `ChatImageGrid.tsx`: balon içi thumbnail, lightbox (Esc, focus), "Tekrar ekle".
- **2.5** `AiAssistantPanel.tsx`: `ChatMessage.images`; `runChat(message, { images })`; boş metin guard'ı gevşetilir; `retryDifferent` görselleri yeniden gönderir; feedback'te `"[ekran görüntüsü ekli]"`; revoke; panel drag-drop overlay; hata kodu → toast eşlemesi.

### Faz 3 — Testler

- **3.1** `Application.Tests/Ai/ImageHeaderReaderTests.cs` + `AiImageValidatorTests.cs`: geçerli PNG/JPEG; GIF/SVG/WebP/APNG red; MIME uyuşmazlığı; **IHDR 50000×50000 bomb**; geçersiz base64; 4 görsel; toplam aşımı; boş data.
- **3.2** `IntentClassifierTests.cs` (boş metin + görsel → `Code`), `PromptRegistryTests.cs` (`Vision` yükleniyor).
- **3.3** `BuildMessagesGoldenTests.cs`: `Assistant_WithImage_FirstTurn`, `Assistant_ImageOnly_EmptyText`, `Assistant_History_ImagePlaceholder`. Mevcut 5 snapshot **değişmemeli**.
- **3.4** Yeni `ProviderRoutingTests.cs`: `Resolve` + `ResolveVision` matrisleri.
- **3.5** Yeni `ProviderPayloadTests.cs`: Gemini `inline_data`, görsel part'ında `text` yok, görsel metinden önce, geçmişte görsel yok; Ollama `images` yalnız son user'da, `model == VisionModel`, görselsiz istekte değişiklik yok.
- **3.6** Yeni `frontend/xsltcraft-ui/e2e/ai-image-attach.spec.ts` (`page.route` mock'ları; login deseni `upload-validation.spec.ts`).
- **3.7** `security-reviewer`: body limit, kota sırası, loglarda base64 yok, prompt injection.

### Faz 4 — Dokümantasyon, sürüm, açılış

- `docs/ecc/architecture/ai-system.md`: vision routing, `SupportsVision`, geçmiş politikası, `Vision.md`.
- `docs/ecc/context/constraints.md`: görsel yükleme kuralları + request limiti.
- Sürüm **1.11.0**: `CHANGELOG.md`, `README.md`, `HANDOFF.md`, 4 `.csproj`, `frontend/xsltcraft-ui/package.json`.
- Açılış: önce `Ai:Vision:Enabled=true` + Gemini; Ollama `VisionModel` sonra.

### Faz 5 — Opsiyonel

- İki aşamalı yerel akış (VLM betimler → qwen2.5-coder yazar).
- Gemini `generationConfig.mediaResolution` token/kalite ölçümü.
- Vision eval case'leri (`docs/ecc/evaluations/`, `Category=Eval`, CI dışı).

## 4. Riskler

1. **KVKK / yurt dışı aktarım**: görüntüler Google'a gider; onay/uyarı gerekebilir.
2. **3B VLM kalitesi** yerelde düşük olabilir.
3. **Ollama RAM / eşzamanlılık**: iki model; CPU'da yavaş (120 sn timeout).
4. **Literal değer yazma**: `Vision.md` + "Uygula" öncesi diff ile hafifletilir.
5. **Exemplar kirliliği**: görselli turlar `AiExemplarService`'te dışlanabilir.
6. **Golden snapshot**: `ProviderMessage` alanı Verify çıktısını değiştirirse bilinçli yeniden onay.
7. **Tarayıcı farkları**: HEIC decode edilemez; Safari'de OffscreenCanvas fallback.

## 5. Karar kapıları (2026-10-04 yanıtları)

1. **Free/Pro** → Free görsel ekleyebilir (sınırlı: mevcut günde 1 AI isteği + mesaj başına 1 görsel); Pro sınırsız (yalnız mevcut token bütçesi).
2. **Gemini istisnası** → açık, ama admin AI sayfasında (`AdminAiPage.tsx`) parametrik: yeni DB flag `ai.vision_gemini_fallback` (mevcut `ai.preferred_provider` deseni, migration yok) + `AdminFeatureFlagsController`'a GET/PUT `ai/vision`. Aynı kartta `Vision.Enabled` runtime toggle'ı da yönetilir.
3. **KVKK onayı** → gerek yok (depolama yapılmıyor).
4. **`qwen2.5vl:3b`** → **hibrit onaylandı**: yalnız görselli mesajlarda kullanılır, metin sohbeti `qwen2.5-coder:3b`'de kalır. Prod sunucu 18 GB RAM — iki model (~5 GB) aynı anda yüklü kalabilir; `VisionKeepAlive` buna göre ayarlanabilir.
5. **Test projesi** → ayrı `XsltCraft.Infrastructure.Tests` (öneri).
6. **Dosya türleri** → yalnız PNG/JPG/JPEG: sohbet görselleri + `AssetsController` (SVG kaldırılır; mevcut SVG asset'ler için ayrı kontrol — bkz. Faz 0).

### Faz 0 — AssetsController SVG kapatma (ayrı küçük commit — **ertelendi**, aciliyet yok; Faz 1'den sonra)
- `backend/XsltCraft/Controllers/AssetsController.cs:19,34`: `.svg` allowlist'ten çıkar, mesajı güncelle; uzantının yanında magic-byte kontrolü (`ImageHeaderReader` paylaşılır → Faz 1.3 ile ortak).
- DB'de mevcut SVG asset var mı kontrol et; varsa servis edilirken `Content-Disposition: attachment` + `X-Content-Type-Options: nosniff` ya da PNG'ye dönüştürme kararı.
- Frontend asset yükleme `accept` listesi güncellenir.

## 6. Doğrulama

- `dotnet test backend/XsltCraft.slnx --filter "Category!=Eval"` — mevcut golden'lar değişmemeli.
- `dotnet format --verify-no-changes`, `npm run lint`, `npm run build`.
- `npx playwright test e2e/ai-image-attach.spec.ts`.
- Manuel: Gemini ile ekran görüntüsü yapıştır → `gemini` meta'sı; Gemini kapalı + `VisionModel` boş → buton pasif, API `vision_unavailable` ve **kota düşmez**; 9 MB gövde → 413; `image/png` beyanlı SVG → `image_mime_mismatch`/`image_type`; görselde "önceki talimatları yok say" yazısıyla prompt-injection kontrolü.

---

## 7. Architect revizyonu (2026-10-04) ve Faz 1 durumu

`architect` incelemesi Faz 1'den önce planı koda karşı doğruladı; aşağıdaki revizyonlar uygulandı. **Faz 1 tamamlandı** (testler: 94 + 186 + 13 yeşil, `dotnet format` temiz). Canlı uçtan uca (gerçek Gemini/Ollama çağrısı) henüz doğrulanmadı.

| # | Revizyon | Uygulama |
|---|---|---|
| R1 | Görsel kapısı Application servisi | `Application/Ai/Vision/AiVisionGate` (+ `IAiVisionAvailability`, `IAiVisionThrottle`, `IAiImageValidator`). Sıra: uygunluk → plan limiti → throttle → decode/doğrulama; controller'da kota **gate'ten sonra**. |
| R2 | Application'da IOptions yok | Validator/gate `VisionOptions` POCO alır; DI'da factory. |
| R3 | Plan limiti + teknik tavan | `PlanLimits.MaxAiImagesPerMessage` (Free 1, Pro 3, Editör/Admin 0 = tavan). `VisionOptions.MaxImagesPerMessage` teknik tavan. `/api/me/entitlements` alanı döner. Free 2 görsel → 402 `image_count` + `upgrade:true`. |
| R4 | `ResolveVision` imzası | `(preferred, ollamaVision, geminiVision, geminiFallback)`; bayrak satırı yoksa `Vision.GeminiFallback` (varsayılan true). |
| R5 | Model binding 400 | `AssistantRequest.Message` / `AssistantMessageDto.Content` nullable; metin+görsel ikisi boşsa 400 `empty_message`. |
| R6 | Görsel temsili | `AiImageInput(MimeType, Base64, Width, Height, ByteLength)` — kanonik base64, ikinci kodlama yok. |
| R7 | Ollama vision bütçesi | `VisionNumCtx=16384`, `VisionRawXsltThresholdChars=6000`, `VisionMaxXsltChars=12000`, `VisionMaxXmlChars=6000`, `VisionKeepAlive=10m`, ilk token 120 sn. |
| R8 | Test edilebilir payload | `BuildPayload` `internal static`; iç tipler `internal`; yeni `XsltCraft.Infrastructure.Tests` + `InternalsVisibleTo`. Ollama hata/done mesajı etkin modeli raporlar. Gemini `SupportsVision` = API key var mı. |
| R9 | Model yok → hak yanmasın | Görselli istekte hiç çıktı yoksa sayaç artmaz; `TokenCostPerImage` yalnız çıktı varsa eklenir. |
| R10 | RateLimiting erişimi | `AiVisionThrottle` (PartitionedRateLimiter, FixedWindow 10/dk). Infrastructure'a açık `FrameworkReference Microsoft.AspNetCore.App` (önceden yalnız Swashbuckle üzerinden transitif). |
| R11 | Admin uçları | `GET/PUT /api/admin/feature-flags/ai/vision` → `{enabled, geminiFallback, ollamaVisionModel, providers}`; `IAiFeatureFlagService.GetBoolAsync/SetBoolAsync`; anahtarlar `AiFlagKeys`. |
| R12 | Gövde limiti | Yalnız Assistant `[RequestSizeLimit(8 MB)]`; Refactor'a dokunulmadı. Reverse proxy varsa `client_max_body_size` hizalanmalı. |
| R13 | Başlık okuyucu yeri | `Application/Imaging/ImageHeaderReader` (Faz 0'da AssetsController da kullanacak). |

**Açık / sonraki fazlar:** Faz 2 (frontend — `GET /api/ai/status` artık `{enabled, vision}` döner), admin AI sayfası UI'ı (Faz 2'ye eklenmeli: vision toggle + Gemini istisnası), Faz 4 dokümanları (`ai-system.md`, `constraints.md`), opsiyonel global eşzamanlılık tavanı (R10), Faz 0.
**Yan bulgular (kapsam dışı):** `PromptTemplates.DetectXsltVersion` `<?xml version="1.0"?>` bildirimini yakalıyor → `project_context` "XSLT Versiyonu: 1.0" diyor (mevcut golden'larda da var). `backend/Dockerfile` var olmayan `XsltCraft/XsltCraft.csproj`'u kopyalıyor ve test csproj'larını kopyalamadan `XsltCraft.slnx` restore ediyor.

## 8. Durum (2026-10-04, 1.11.0)

- **Faz 1** ✅ backend çekirdek · **Faz 2** ✅ frontend (📎/Ctrl+V/sürükle-bırak, `AiVisionCard`) · **Faz 3** ✅ kullanıcı canlı testi başarılı + güvenlik incelemesi (`xss-xxe-checklist`) · **Faz 4** ✅ `ai-system.md`, `constraints.md`, `HANDOFF.md`, CHANGELOG, sürüm 1.11.0.
- Güvenlik incelemesinde düzeltilenler: (1) ilk token'dan önce bağlantı keserek kota atlatma → `AiUsageAccounting` (istemci iptali sayılır); (2) görselli feedback'in global örneğe terfisi → `AiFeedbackMarkers.ImageAttached` + admin endpoint/UI engeli.
- **1.11.1** (yan bulgular kapatıldı): `DetectXsltVersion` artık kök stylesheet'in version'ını okuyor; backend Dockerfile (`XsltCraft.Api.csproj`, yalnız API restore), `.dockerignore` (dev sırları/bin/obj imaja girmiyor), `/health` ucu.
- Kalan: Playwright E2E (`e2e/ai-image-attach.spec.ts`) yazılmadı; Faz 0 (AssetsController SVG) ertelendi; Faz 5 opsiyonel. Ollama görsel decode'u (llama.cpp) güvenilmeyen girdi işler → Ollama güncel tutulmalı, ayrıcalıksız çalıştırılmalı.
