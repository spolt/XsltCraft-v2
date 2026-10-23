# ADR 007 — XSLT motor yönlendirme: rota başına motor, XSLT 2.0 tavanı

**Durum:** Önerildi (2026-10-23) · Hedef sürüm 2.0.0 (major) · Kısmen yerine geçtiği: ADR 003 · İlgili: ADR 008 (worker izolasyonu)

> Bu ADR'deki sınıf/anahtar/test adlarının çoğu **planlanan**dır (`feature/xslt2-engine`, W1–W4). Kod-gerçeği olanlar ayrıca belirtilir.

## Bağlam

**Doküman/kod drifti.** README (`:12`, `:34`, `:70`), CLAUDE.md, ADR 003 ve `context/tech-stack.md` "önizleme Saxon HE 10.9 (XSLT 2.0)" diyordu; kod ise ilk günden farklıydı:

| Olgu | Kanıt |
|---|---|
| `Saxon-HE 10.9.0` paketi ilk commit'ten beri csproj'larda; tek kullanıcısı `XsltTemplateRenderer.RenderAsync` (`/api/render`, free-theme) | `abd2557` (ilk commit), `a8b15f3` (monorepo taşıması, paket aynen taşındı) |
| Grid, ham, tema önizlemesi ve doğrulama ilk günden `XslCompiledTransform` (yalnız XSLT 1.0) | `e23d1e7` `PreviewController`; bugün `SecureXslt.Compile` → `IXsltHtmlRenderer` |
| Saxon-HE 10.9 (IKVM 8.1 / .NET Framework) net10'da çalışmıyor: `Compile` → `FileStream` ctor `MissingMethodException` | W0 spike; paket `7444c40`'ta Domain/Infrastructure'dan kaldırıldı, `/api/render` `SecureXslt`'e taşındı. `/api/render` hattı (ölü kod) **2.0.0'da kaldırılıyor** (T4.6) |
| Kodla uyumlu tek doküman `architecture/backend.md:28` ("XslCompiledTransform — Saxon değil") | — |

**Tetikleyici vaka (gib-general).** GİB `general.xslt` (`version="2.0"`) ham XSLT editöründe render edilemiyor. `version="2.0"` .NET'te **ileri-uyumlu modu** açar: derleme geçer (bu yüzden "XSLT ✓" yeşil), `exists()` hatası ancak `Transform` sırasında atılır, `xsl:character-map` ise **sessizce yok sayılır** (yanlış çıktı, hata yok). Ekip bunu biliyordu: `ThemeUploadProcessor.cs:161` "1.0 motoru, `exists()` kullanılamaz" ipucu verir; `ThemeUploadProcessorTests`/`ThemeSlotAnalyzerTests` gib-general'in **reddedilmesini** test eder. Roadmap İz S (S1–S6) bu işi planlamıştı.

**Generator semantiği.** Grid generator çıktısı `version="2.0"` ilan eder ama bugün 1.0 motorunda koşar. Saxon'da farklılaşan yerler (T1.4 golden taban çizgisi `d1c086d` ile sabitlendi): `value-of` çoklu düğümü birleştirir (`//cbc:Note`, `//cbc:ID`, "Hesaplanan KDV" `//cac:TaxTotal/cbc:TaxAmount` her gerçek faturada çok değerli), çoklu düğümde `number`/`contains`/`substring`/`string-length` → `XPTY0004`, `sum`/`format-number` → `FORG0001`, round-half-even farkları (0.125 → 0,12 vs 0,13).

## Karar

### 1. Motorlar ve paket
Saxon-HE **12.10** (`net.sf.saxon:Saxon-HE`, MPL-2.0) — `IKVM.Maven.Sdk 1.12.2` ile; yalnız açık kaynak (SaxonCS/PE/EE yok). Saxon 13.x Java 17 gerektirdiği için kapsam dışı. .NET motoru `XslCompiledTransform` (yalnız `SecureXslt.Compile` üzerinden) kalır. Paket/izolasyon ayrıntısı: ADR 008.

### 2. Rotalar

| Rota (`XsltRoute`) | Uçlar | Yürütme yeri | Motor seçimi |
|---|---|---|---|
| **Raw** | `POST /api/preview/raw`, `POST /api/preview/validate-xslt` | Worker süreç havuzu (ADR 008) — 1.0 dahil | `Xslt:Engine:Raw` |
| **UserTemplate** (planlanan enum üyesi) | `POST /api/preview/user-template/{id}` | Worker | `Xslt:Engine:Grid` (grid ile aynı mod) |
| **Grid** | `POST /api/preview` (generator çıktısı) | Api süreci içi, korumalı (ADR 008 §Grid) | `Xslt:Engine:Grid` |
| **Theme** | tema yükleme/analiz/compose, `PreviewTheme` | Api süreci içi, .NET (`IXsltHtmlRenderer` değişmez) | Sabit DotNet — **T-izi (tema izolasyonu)** ayrı karar |

Kullanıcının yazdığı XSLT (Raw, Validate, UserTemplate) **hiçbir zaman** Api sürecinde çalışmaz. Grid'de script bloğu (v3 Faz 4 Kademe 2/3) içeren ağaç → her zaman worker; Kademe 1 süreç içi kalır.

### 3. Sürüm algılama (sniff) ve modlar
`XsltVersionSniffer` (W1'de geliştiriliyor) kök `xsl:stylesheet|transform/@version` veya LRE `@xsl:version`'ı `SecureXslt.ReaderSettings` ile okur (invariant ondalık; okunamaz → `null`).

| Mod | Raw (varsayılan **Auto**) | Grid / UserTemplate (varsayılan **DotNet**) |
|---|---|---|
| `Auto` | `< 2.0`/okunamaz → .NET (bit-aynı çıktı), `= 2.0` → Saxon, `> 2.0` → `Unsupported` | Generator `version="2.0"` → Saxon |
| `DotNet` | Bugünkü davranış (ileri-uyumlu mod) | Bugünkü davranış |
| `Saxon` | Hep Saxon | Hep Saxon |

- Validate, Raw modunu izler. Tek yapılandırma kaynağı `appsettings` (`Xslt:Engine:*`); DB bayrağı yok.
- Grid'in `Auto`'ya geçişi **2.0.0 etiketinden önce**, dalda/staging'de yapılır: T3.3 `GridEngineParityTests` yeşil + G3 güvenlik onayı + süreç içi p95 kabulü. Ara `v2.0.0-rc.N` etiketi kullanılmaz; doğrudan 2.0.0.
- `DotNet` **acil anahtar** olarak 2.0.x boyunca korunur (her iki anahtar için).

### 4. XSLT 2.0 tavanı
Saxon `Feature.XPATH_VERSION_FOR_XSLT = 20`: `let`, satır içi `function()`, `!`, map sözdizimi derlemede reddedilir (`XPST0003` → "XPath 3.0 sözdizimi desteklenmiyor (XSLT 2.0 hedeflenir)"). `version > 2.0` ve 3.0 yapıları (`iterate`, `map`, `array`, `mode`, `accumulator`, `assert`, `where-populated`, `on-empty`, `on-non-empty`, `global-context-item`, `context-item`, `fork`, `math:`) → `XsltFailureKind.Unsupported`.

### 5. Çift-motor güvenli grid kuralı
Grid çıktısı `version="2.0"` kalır ama içeriği **iki motorda aynı sonucu veren** altkümeyle sınırlıdır; böylece `Grid=DotNet` acil anahtarı ve indirilen dosya güvenle çalışır.

| Kural (planlanan, T3.2) | Kod |
|---|---|
| Bağlamalar yalnız XPath 1.0 fonksiyonları (`XPathPortability.Analyze`, `XPathExpression.Compile`) | `XPATH_NOT_PORTABLE` |
| İfade uzunluğu ≤ 512 | `XPATH_TOO_LONG` |
| Parantez ≤ 16, iç içe yüklem ≤ 3, yüklem içinde tek seviye N-sınıfı eksen | `XPATH_TOO_COMPLEX` |
| Ağaç başına ≤ 500 bağlama / ≤ 64 KB | `BLOCKTREE_TOO_LARGE` |
| `value-of`/biçimlendirme/test bağlamaları tek değere indirgenir (`(x)[1]`), `format-number(number(...))` | G1–G4 |
| `xsl:output`, `decimal-format`, namespace bildirimleri değişmez | G6 |

`BlockTreeValidator` (v3 2.12) ve `XPathExpressionValidator` (v3 4.2) aynı sınır tablosunu kullanır. **İndirilen `.xslt` 1.0-taşınabilir kalır** (alıcı tarafındaki 1.0 görüntüleyiciler).

### 6. XsltSafety modları (planlanan, T1.2)
Fail-closed tam parse (`SecureXslt.ReaderSettings`; parse hatası/DOCTYPE = tehdit; regex yedeği kalkar). `Scan(xslt, mode)` → bulgular `{Code, Severity: Security|Unsupported, Message, Line, Column}`; `FindThreat` Store modu olarak korunur.

| Mod | Engeller | Kullanım yerleri |
|---|---|---|
| **Execute** | Security + Unsupported | generator `Validate` (`XsltGeneratorService`), `XsltExecutionService`, worker içi |
| **Store** | Yalnız Security | `UserXsltTemplateController`, `AdminController`, `ThemeUploadProcessor`, `ThemeSlotAnalyzer`, `ThemeOverridesProcessor`, `ThemeAssetComposer`, `FixedNoteInjector` |

Security: `xsl:` eleman izin listesi; `import/include/import-schema/use-package/result-document/source-document/evaluate/merge*/try/catch`, `xi:include`, `msxsl:script`, `saxon.sf.net`/`java:`/`clitype:`; `use-when`, `static="yes"`, `_` gölge öznitelikleri; yasak fonksiyonlar (`document`, `doc*`, `unparsed-text*`, `collection`, `environment-variable`, `json-doc`, `parse-xml*`, `transform`, `load-xquery-module`, `function-lookup`), `ad#N`, `function(`, `$v(`; boyut (derinlik ≤ 256, ifade ≤ 8 KB, parantez ≤ 64). XsltSafety derinlemesine savunmadır; asıl sınır Saxon sandbox + worker'dır (ADR 008).

### 7. Uç sözleşmesi (2.0.0, kırıcı)
`Preview`, `PreviewRaw`, `ValidateXslt` metot seviyesinde `[Authorize]` (bugün anonim; `PreviewUserTemplate` zaten `[Authorize]`); rate limit `xslt-preview` 120/dk/kullanıcı, `xslt-validate` 120/dk. Tek hata gövdesi `{error, line, column, kind, engine}`; `UserBusy`/rate → 429, `Busy`/`EngineUnavailable` → 503 + `Retry-After`, beklenmeyen → 500. `validate-xslt`'te `200 {valid:false}` yalnız içerik hatası içindir.

## Alternatifler

| Alternatif | Neden reddedildi |
|---|---|
| Önce .NET, hata olursa Saxon'a düş | İleri-uyumlu mod `xsl:character-map`'i **sessizce yutar**, `exists()` ancak runtime'da patlar → yanlış çıktı hata üretmez, fallback tetiklenmez |
| Tek motor anahtarı (`Xslt:Engine`) | Raw ve Grid'in risk profili ve parite olgunluğu farklı; grid'i bağımsız geri alabilmek gerekiyor |
| DB feature flag | Acil anahtar için yapılandırma yeterli; ek yönetim yüzeyi ve çok-örnek tutarlılığı getirir |
| Grid'de XSLT 2.0 fonksiyonlarına izin | Çift-motor paritesini ve `DotNet` acil anahtarını bozar; indirilen dosya 1.0 alıcılarda kırılır |
| Grid de worker'da | **Sahip reddetti:** grid kaynağı generator'dır (kullanıcı XSLT'si değil) ve XPathPortability ile XPath 1.0'a sınırlıdır; canlı canvas önizlemesi yüksek frekanslıdır ve küçük worker havuzunu (2) doyurur. Kalan CPU riski korumalı süreç içi yürütme + bozulmuş modla kabul edildi (ADR 008) |
| Saxon-HE 10.9 NuGet / SaxonCS | 10.9 net10'da çalışmıyor; SaxonCS ticari (yalnız açık kaynak kararı) |

## Sonuçlar
- **+** gib-general ve diğer 2.0 stylesheet'ler ham editörde doğru render edilir; 1.0 stylesheet'ler bit-aynı kalır.
- **+** Dokümanlar kod-gerçeğine döner (W4/T4.2: README, CLAUDE.md, tech-stack, backend.md, constraints; roadmap S1–S6 "ADR 007/008 ile yerine geçti").
- **−** 2.0.0 kırıcı: anonim önizleme/doğrulama kalkar (eski bundle 401 alır), hata sözleşmesi değişir, 3.0 yapıları `Unsupported`. CHANGELOG `### BREAKING`; aynı sürümde UI/UX yeniden tasarımı.
- **−** Grid golden'ları Saxon'a geçişte bilinçli güncellenir (G1–G4 çıktıyı değiştirir); T3.2, v3 registry refactor'undan önce birleşir.
- **−** İki motor, iki yürütme yeri: parite testleri bloke eden kapıdır; Theme rotası T-izi'ne kadar 1.0'da kalır (gib-general tema olarak hâlâ reddedilir).

## Doğrulama (planlanan testler; ✓ = mevcut)

| Alan | Testler |
|---|---|
| Sniff / seçim | `XsltVersionSnifferTests.Sniff_{1_0,2_0,3_0,Missing,Garbage}`, `XsltEngineSelectorTests.Selector_AutoMatrix` |
| .NET kontratı | `DotNetEngine_Output_BitIdenticalToXsltHtmlRenderer`, `DescribeXsltError_Forwarder_SameOutput`, `XsltDiagnosticContractTests` |
| Rotalar | `XsltExecutionServiceTests.{Raw_V1_DotNetInWorker, Raw_V2_Saxon, Raw_V3_Unsupported, Threat_SecurityWithLine, Oversize_Rejected, Grid_DefaultDotNet, WorkerDisabled_Fallbacks}`, `UserTemplate_Worker_Equals_Grid_InProcess` |
| Grid paritesi | `GridV2GoldenTests` ✓ (`d1c086d`), `GridV2SaxonGoldenTests`, `GridEngineParityTests` (bloke eden; `#qrvalue` bayt-aynı), `DeepParens_RejectedBeforeCompile` |
| XsltSafety | `XsltSafetyTests`, `XsltSafetyBypassTests`, `XPathTokenizerTests`, `XsltSafetyFixtureTests` (Store'da 0 Security), `CorpusDryRunTests` (`Category=Corpus`) |
| Eval / e2e | case008–011 + `EngineParityTests` (non-blocking); `e2e/xslt-engine.spec.ts` (`unauth_raw_401`, `v2_forEachGroup_renders`, `recursion_error_then_next_ok`) |
