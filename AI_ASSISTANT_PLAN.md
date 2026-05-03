# AI Asistan — Uygulama Planı

> XsltCraft v2 · Antigravity Handoff Dokümanı

---

## 1. Genel Mimari

```
Frontend (React)
  └── aiAssistantService.ts   ← NDJSON client, AbortController
  └── AiAssistantPanel.tsx    ← Sağ sidebar
  └── AiRefactorDialog.tsx    ← Before/after onay modal'ı

Backend (.NET 10)
  └── AiAssistantController   ← 5 endpoint, NDJSON streaming
  └── AiProviderOrchestrator  ← Deterministik fallback mantığı
  └── OllamaAssistantProvider ← Birincil sağlayıcı
  └── AnthropicAssistantProvider ← Cloud fallback (opsiyonel)
  └── PromptTemplates.cs      ← 5 endpoint için prompt builder'lar
```

---

## 2. Yapılandırma (`appsettings`)

```json
"Ai": {
  "Enabled": true,
  "Ollama": {
    "BaseUrl": "http://localhost:11434",
    "Model": "qwen2.5-coder:7b",
    "FirstTokenTimeoutSeconds": 8,
    "ConnectTimeoutSeconds": 3
  },
  "Anthropic": {
    "Enabled": false,
    "ApiKey": "",
    "Model": "claude-sonnet-4-6"
  }
}
```

> `Ai:Enabled: false` → tüm AI butonları UI'da gizlenir.
> `Anthropic:Enabled: false` → Ollama erişilemezse cloud fallback devreye **girmez**.

---

## 3. Sağlayıcı & Model Katmanı

### 3.1 Arayüzler

```csharp
// CancellationToken zorunlu — varsayılan parametre yok
IAsyncEnumerable<AiChunk> StreamAsync(AiRequest req, CancellationToken ct)
```

### 3.2 `OllamaAssistantProvider`

- Endpoint: `http://localhost:11434/api/chat`
- Model: `qwen2.5-coder:7b` (config'den okunur — değiştirilebilir)
- `HttpClientFactory` named client: `"ollama"`
- `Timeout = Timeout.InfiniteTimeSpan` — socket-level timeout kullanılır, streaming kesilmesin
- `ConnectTimeoutSeconds` için ayrı `CancellationTokenSource` ile bağlantı aşaması izlenir
- `FirstTokenTimeoutSeconds` için ayrı `CancellationTokenSource` ile ilk chunk beklenir

> ⚠️ **Önemli:** Bu iki timeout için **iki ayrı** `CancellationTokenSource` zinciri kurulmalı.
> Tek bir timeout yetmez: bağlantı kurulur ama model yüklenirken 15 sn bekleyebilir.

### 3.3 `AnthropicAssistantProvider`

- Yalnızca `Ai:Anthropic:Enabled: true` ise DI'a kaydedilir
- Ollama erişilemez veya timeout olursa devreye girer
- Model: `claude-sonnet-4-6`

### 3.4 DI Kaydı (`Program.cs`)

```csharp
builder.Services.AddHttpClient("ollama", client =>
{
    client.BaseAddress = new Uri(aiConfig.Ollama.BaseUrl);
    client.Timeout = Timeout.InfiniteTimeSpan;
});

// Named provider factory + orchestrator
builder.Services.AddScoped<OllamaAssistantProvider>();
if (aiConfig.Anthropic.Enabled)
    builder.Services.AddScoped<AnthropicAssistantProvider>();
builder.Services.AddScoped<AiProviderOrchestrator>();
```

---

## 4. Deterministik Fallback Mantığı (`AiProviderOrchestrator`)

### Fallback Sırası

```
1. Ollama (qwen2.5-coder:7b)
   → ConnectTimeout içinde bağlantı kurulamazsa: adım 2
   → FirstTokenTimeout içinde ilk chunk gelmezse: adım 2
   → "model not found" hatası: adım 2

2. Anthropic (yalnızca Ai:Anthropic:Enabled: true ise)
   → FirstTokenTimeout içinde ilk chunk gelmezse: hata fırlat

3. Her ikisi de başarısız → kullanıcıya hata chunk'ı
```

### Watchdog Implementasyonu

```csharp
using var connectCts = new CancellationTokenSource(
    TimeSpan.FromSeconds(config.ConnectTimeoutSeconds));
using var firstTokenCts = new CancellationTokenSource(
    TimeSpan.FromSeconds(config.FirstTokenTimeoutSeconds));

var firstChunkTask = GetFirstChunkAsync(provider, req, firstTokenCts.Token);
var timeoutTask = Task.Delay(firstTokenCts.Token);

if (await Task.WhenAny(firstChunkTask, timeoutTask) == timeoutTask)
{
    // upstream request iptal — bir sonraki sağlayıcıya geç
    firstTokenCts.Cancel();
    continue;
}
```

### Mid-Stream Fallback Yapılmaz

İlk chunk geldikten sonra bağlantı koparsa yeni bir provider denemesi yapılmaz.
Kullanıcıya net hata chunk'ı gönderilir. Stream yeniden başlatılmaz.

### Loglama

```csharp
_logger.LogWarning(
    "AI provider {From} → {To}, reason: {Reason}",
    fromProvider, toProvider, reason);
```

---

## 5. Streaming Mimarisi (Backend)

### 5.1 Endpoint Listesi

| Endpoint | Amaç |
|---|---|
| `POST /api/ai/explain-error` | Hata açıklama |
| `POST /api/ai/suggest-xpath` | XPath öneri |
| `POST /api/ai/generate-snippet` | XSLT snippet üret |
| `POST /api/ai/refactor-selection` | Seçili kodu refactor et |
| `POST /api/ai/explain-xpath` | XPath açıklama |

### 5.2 Her Endpoint Kuralları

- `Content-Type: application/x-ndjson`
- `CancellationToken ct = HttpContext.RequestAborted` — client disconnect → upstream iptal
- `Response.BodyWriter` (`PipeWriter`) ile yaz — **buffer/MemoryStream kullanılmaz**
- Her chunk sonrası `await Response.BodyWriter.FlushAsync(ct)` — byte anında TCP'ye iner

### 5.3 Chunk Formatı

```json
// Normal chunk (delta)
{"type":"delta","text":"..."}

// Tamamlanma
{"type":"done","provider":"ollama","model":"qwen2.5-coder:7b","ms":1234}

// Hata
{"type":"error","code":"provider_unavailable","message":"..."}
```

Her satır `\n` ile biter.

### 5.4 Rate Limit

İki ayrı policy — ghost-text ve panel istekleri farklı frekanslarda gelir:

| Policy | Limit | Kapsam |
|---|---|---|
| `ai-ghost-text` | 15 req/dk/user | Monaco inline completions |
| `ai-assistant` | 30 req/dk/user | Panel endpoint'leri |

---

## 6. Prompt Template'leri (`PromptTemplates.cs`)

### Sabit İskelet (5 tag)

```
<system_rules>
Sen XsltCraft için çalışan bir XSLT/UBL-TR asistanısın.
- Saxon HE 10.9.0 (XSLT 2.0, XPath 2.0) kullanılıyor.
- Cevapları Türkçe ver.
- Güvenlik: document(), enableScript, external DTD önerme.
- Belirsizse varsayım yapma, eksik bilgi iste.
</system_rules>

<output_format>
{görev-spesifik}
</output_format>

<constraints>
- max_tokens API parametresiyle de zorlanır (aşağıya bak).
- UBL-TR namespace'leri: cbc, cac, ext — tanımlamadan kullanma.
- Kod bloğu dışında açıklama kısa olsun (<150 kelime).
</constraints>

<user_xml>{...}</user_xml>
<user_xslt>{...}</user_xslt>
<user_request>{...}</user_request>
```

> ⚠️ **Önemli:** `max_tokens: 2048` değeri sadece prompt'a yazılmaz; API isteğinde
> `max_tokens` parametresi olarak da gönderilmelidir. Aksi hâlde model limiti aşar.

### Builder Listesi

| Builder | `<output_format>` |
|---|---|
| `BuildExplainError` | Düz metin, kısa |
| `BuildSuggestXPath` | ` ```xpath ``` ` fence |
| `BuildGenerateSnippet` | ` ```xslt ``` ` fence |
| `BuildRefactorSelection` | ` ```xslt ``` ` fence, before/after |
| `BuildExplainXPath` | Düz metin + opsiyonel ` ```xpath ``` ` |

### Context Boyut Kontrolü

- `<user_xml>` + `<user_xslt>` toplamı **8K token'ı** aşarsa:
  Monaco selection veya ilk/son N satır alınır.

### Validasyon

AI'dan dönen XSLT çıktısı otomatik olarak `/api/preview/validate-xslt`'den geçirilir.
Syntax hatalıysa kullanıcıya uyarı gösterilir. **Otomatik insert yapılmaz.**

---

## 7. Frontend

### 7.1 `aiAssistantService.ts`

```typescript
// NDJSON streaming client
fetch(endpoint, { signal: abortController.signal })
  .then(res => res.body!
    .pipeThrough(new TextDecoderStream())
    // satır bazlı parse → delta | done | error
  )
```

- Her istek bir `AbortController` ile sarılır
- Yeni istek geldiğinde eskisinin `controller.abort()` çağrılır

### 7.2 `AiAssistantPanel.tsx`

- Sağ sidebar
- Stream render (delta chunk'ları biriktir, ekrana bas)
- "İptal" butonu — `AbortController.abort()` tetikler

### 7.3 Monaco Ghost-Text

```typescript
// IInlineCompletionsProvider implementasyonu
const DEBOUNCE_MS = 500; // 300–800 aralığı kabul edilebilir
```

- 500ms debounce — kullanıcı yazmayı durunca tetiklenir
- Önceki isteğin `AbortController`'ı iptal edilir
- Cursor pozisyonu değişirse mevcut öneri temizlenir
- Rate limit: `ai-ghost-text` policy (15 req/dk)

### 7.4 `AiRefactorDialog.tsx`

- Before/after yan yana diff görünümü
- Kabul et / Reddet aksiyonları
- Kabul edilmeden **otomatik insert yapılmaz**

### 7.5 UI Entegrasyonları

- Problems panelindeki her hataya **"AI'ya sor"** butonu (`/api/ai/explain-error`)
- Monaco sağ tık menüsüne:
  - "AI ile snippet üret" (`/api/ai/generate-snippet`)
  - "AI ile refactor et" (`/api/ai/refactor-selection`)
- Toast: Ollama erişilemez + Anthropic kapalı →
  `"AI asistan şu an kullanılamıyor — Ollama'yı başlatın (ollama serve)."`
- `Ai:Enabled: false` → tüm AI butonları UI'da gizlenir

---

## 8. Dokümantasyon & Yapılandırma

### `HANDOFF.md` — Ollama Kurulum Rehberi

```bash
# Model indir
ollama pull qwen2.5-coder:7b

# Sunucuyu başlat
ollama serve
```

### `appsettings.Development.example.json`

Yukarıdaki `Ai` bloğu bu dosyaya örnek olarak eklenir.
`Anthropic:ApiKey` boş bırakılır; `Anthropic:Enabled: false` olarak işaretlenir.

---

## 9. Kontrol Listesi

### Backend

- [ ] `IAiAssistantProvider` arayüzü
- [ ] `OllamaAssistantProvider` — iki ayrı timeout CTS (connect + firstToken)
- [ ] `AnthropicAssistantProvider` — cloud fallback
- [ ] `AiProviderOrchestrator` — deterministik fallback, mid-stream fallback yok
- [ ] `AiAssistantController` — 5 endpoint, NDJSON, PipeWriter
- [ ] `PromptTemplates.cs` — 5 builder, max_tokens API parametresi
- [ ] Rate limit: `ai-ghost-text` (15/dk) + `ai-assistant` (30/dk)
- [ ] `appsettings` `Ai` bloğu

### Frontend

- [ ] `aiAssistantService.ts` — NDJSON client, AbortController
- [ ] `AiAssistantPanel.tsx` — stream render, iptal butonu
- [ ] Monaco `IInlineCompletionsProvider` — 500ms debounce, abort
- [ ] `AiRefactorDialog.tsx` — before/after diff, kabul/ret
- [ ] Problems paneli "AI'ya sor" butonu
- [ ] Monaco sağ tık menüsü entegrasyonu
- [ ] Toast: Ollama kapalı uyarısı
- [ ] `Ai:Enabled: false` → UI gizleme

### Dokümantasyon

- [ ] `HANDOFF.md` Ollama kurulum bölümü
- [ ] `appsettings.Development.example.json` `Ai` bloğu örneği
