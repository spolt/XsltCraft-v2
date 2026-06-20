# Engineering Standards

Agent'lar (özellikle `reviewer`) bu standartları referans alır. Mevcut Clean Architecture + DI pattern'inden türetilmiştir.

## Mimari katmanlar
- **Bağımlılık yönü tek yönlü:** `Api → Application → Domain`; `Infrastructure → Application/Domain`. **`Application` katmanı Infrastructure'a ASLA bağımlı olmaz.**
- Application iş mantığını **interface** üzerinden tüketir (`IStorageService`, `IAiAssistantProvider`, `IXsltGeneratorService`); somut tipi `new`'lemez.
- DI kaydı tek yerde (`XsltCraft.Infrastructure/DependecyInjection/ServiceCollectionExtensions.cs`). Yeni servis interface + kayıt ile gelir.

## Kod stili
- **Static service / global state'ten kaçın** — test edilemez ve gizli bağımlılık yaratır.
- **Feature-based klasörleme** (`Ai/`, `Preview/`, `Prompts/`) — teknik-katman değil özellik etrafında topla.
- **Dosyaları küçük tut**; tek sorumluluk. Büyüyen switch/handler'ları ayrı metoda böl.
- Nullable enabled; `?`/null kontrolü açık. Magic string yerine sabit/enum.
- `CancellationToken` akışı zorunlu (özellikle streaming/IO); varsayılan parametre verme.

## Güvenlik (ödün verilmez)
- Her yeni XML/XSLT parse noktası `docs/ecc/context/constraints.md`'deki guard pattern'i kullanır (XXE/injection).
- Authorization: controller/endpoint seviyesinde `[Authorize]`; kaynak sahipliği (IDOR) kontrolü.
- Exception'lar prod'da generic mesaj + `LogError`; ham `ex.Message` yalnız dev.

## Test
- Yeni davranış → test. Generator değişiklikleri `XsltCraft.Tests`; AI pipeline `XsltCraft.Application.Tests` (Verify golden).
- Üretilen XSLT her zaman derlenebilir olmalı (`XslCompiledTransform.Load` geçer).
- Golden snapshot değişimi **bilinçli** kabul edilir (`.received` → `.verified` gözden geçirilerek).

## PR hijyeni
- Pre-commit hook'u atlama (`--no-verify` yok). `console.log`, hardcoded URL, secret, `.xslt` commit etme.
- `dotnet format --verify-no-changes` ve frontend lint temiz olmalı (CI bunları kontrol eder).
