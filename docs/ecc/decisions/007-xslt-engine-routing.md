# ADR 007 — XSLT motor yönlendirme: rota başına motor, XSLT 2.0 tavanı

**Durum:** Kabul edildi (2026-10-25) · Sürüm 2.0.0 (major) · Yerine geçtiği: ADR 003 · İlgili: ADR 008 (worker izolasyonu)

> Kod-gerçeği (`feature/xslt2-engine`, W1–W3 + G1–G3). Sınıf/anahtar/test adları koddadır; planlanıp yapılmayanlar ayrıca belirtilir.

## Bağlam

**Doküman/kod drifti.** README, CLAUDE.md, ADR 003 ve `context/tech-stack.md` "önizleme Saxon HE 10.9 (XSLT 2.0)" diyordu; kod ise ilk günden farklıydı:

| Olgu | Kanıt |
|---|---|
| `Saxon-HE 10.9.0` paketi ilk commit'ten beri csproj'larda; tek kullanıcısı `XsltTemplateRenderer.RenderAsync` (`/api/render`, free-theme) | `abd2557` (ilk commit), `a8b15f3` (monorepo taşıması) |
| Grid, ham, tema önizlemesi ve doğrulama ilk günden `XslCompiledTransform` (yalnız XSLT 1.0) | `e23d1e7` `PreviewController` |
| Saxon-HE 10.9 (IKVM 8.1 / .NET Framework) net10'da çalışmıyor: `Compile` → `FileStream` ctor `MissingMethodException` | W0 spike; paket `7444c40`'ta kaldırıldı. `/api/render` hattı, `DocumentController` ve FE `Playground` **2.0.0'da kaldırıldı** (T4.6) |

**Tetikleyici vaka (gib-general).** GİB `general.xslt` (`version="2.0"`) ham XSLT editöründe render edilemiyordu. `version="2.0"` .NET'te **ileri-uyumlu modu** açar: derleme geçer, `exists()` hatası ancak `Transform` sırasında atılır, `xsl:character-map` **sessizce yok sayılır** (yanlış çıktı, hata yok).

**Generator semantiği.** Grid çıktısı `version="2.0"` ilan eder ama 1.0 motorunda koşar. Saxon'da farklılaşan yerler (T1.4 golden `d1c086d`): `value-of` çoklu düğümü birleştirir, çoklu düğümde `number`/`contains`/`substring`/`string-length` → `XPTY0004`, `sum`/`format-number` → `FORG0001`, round-half-even farkları (0.125 → 0,12 vs 0,13).

## Karar

### 1. Motorlar ve paket
Saxon-HE **12.10** (`net.sf.saxon:Saxon-HE`, MPL-2.0) — `IKVM.Maven.Sdk 1.12.2` ile, yalnız `backend/XsltCraft.Xslt.Saxon/` projesinde; SaxonCS/PE/EE yok, 13.x (Java 17) kapsam dışı. .NET motoru `XslCompiledTransform` (`DotNetXsltEngine`, yalnız `SecureXslt` üzerinden). Paket/izolasyon: ADR 008.

### 2. Rotalar (`XsltRoute`, `XsltExecutionService`)

| Rota | Uç | Yer | Motor modu |
|---|---|---|---|
| **Raw** | `POST /api/preview/raw`, `POST /api/preview/validate-xslt` (Validate işlemi) | Worker havuzu — 1.0 dahil | `Xslt:Engine:Raw` (`Auto`) |
| **UserTemplate** | `POST /api/preview/user-template/{id}`; sahibin kendi saklı XSLT'li (ücretsiz tema olmayan) şablonu için `PreviewTheme` | Worker | `Xslt:Engine:Grid` (`DotNet`) |
| **Grid** | `POST /api/preview` (generator çıktısı) | Api süreci içi, korumalı (ADR 008 §7); script bloklu ağaç (`IsUserAuthoredGrid`) → worker | `Xslt:Engine:Grid` (`DotNet`) |
| **Theme** | `POST /api/preview/theme/{id}` (yayınlanmış hazır tema) | Api süreci içi, grid sınırları | Sabit `DotNet` |

- Kullanıcının yazdığı XSLT (Raw, Validate, UserTemplate) **hiçbir zaman** Api sürecinde çalışmaz. İstisna: `XsltWorker:Enabled=false` (yerel geliştirme) → 1.0 süreç içi .NET, 2.0 ve script'li grid → 503 `EngineUnavailable` (`code: WorkerDisabled`).
- Tema yükleme/analiz/compose hattı (`ThemeUploadProcessor`, `ThemeSlotAnalyzer`, `ThemeAssetComposer`) `SecureXslt`/`IXsltHtmlRenderer` ile .NET'te kalır — **T-izi (tema izolasyonu)** ayrı karar.

### 3. Sürüm algılama ve modlar
`XsltVersionSniffer` kök `xsl:stylesheet|transform/@version` veya LRE `@xsl:version`'ı okur (`null` = okunamaz); `> 2.0` → `Unsupported` (`VersionAbove20`, tarayıcıdan bağımsız ikinci savunma). `XsltEngineSelector`:

| Mod | Raw / Validate (varsayılan **Auto**) | Grid / UserTemplate (varsayılan **DotNet**) |
|---|---|---|
| `Auto` | `< 2.0`/okunamaz → .NET (bit-aynı), `= 2.0` → Saxon | `version="2.0"` → Saxon |
| `DotNet` | Hep .NET (ileri-uyumlu mod) | Hep .NET |
| `Saxon` | Hep Saxon | Hep Saxon |

- Tek yapılandırma kaynağı `appsettings` (`Xslt:Engine:*`); DB bayrağı yok. `DotNet` 2.0.x boyunca acil anahtardır.
- **Grid 2.0.0'da `DotNet`'te kalır (sahip kararı, 2026-10-25).** `Auto` geçişinin ön koşulları hazır: `GridEngineParityTests` (10 HTML + 10 QR bayt-aynı, bloke eden), `UserTemplateWorkerParityTests`, G3 onayı. Kalan: süreç içi Saxon p95 kabulü ve ADR 008 §Ölçümler'deki Saxon grid kaçak tablosunun yeniden değerlendirilmesi. `Grid=DotNet` iken `SaxonWarmupService` ısınmayı atlar.

### 4. XSLT 2.0 tavanı
`Feature.XPATH_VERSION_FOR_XSLT = 20`: `let`, satır içi `function()`, `!`, map sözdizimi derlemede reddedilir (`XPST0003` → "XPath 3.0 sözdizimi desteklenmiyor (XSLT 2.0 hedeflenir)"). Bu ayar `use-when`/gölge `_*`/`static` ifadelerine **uygulanmaz** (Saxon bunları XPath 3.1 ile derler) → statik ifadeler motor düzeyinde `StylesheetSource.PreRead` ile reddedilir (ADR 008 §6). `version > 2.0` ve 3.0 yapıları (`iterate`, `map`, `array`, `mode`, `accumulator`, `assert`, `where-populated`, `on-empty`, `on-non-empty`, `global-context-item`, `context-item`, `fork`, `math:`) → `Unsupported`.

### 5. Çift-motor güvenli grid kuralı (`XPathPortability`)
Grid çıktısı `version="2.0"` kalır ama içeriği iki motorda aynı sonucu veren altkümedir; `Grid=DotNet` anahtarı ve indirilen dosya güvenle çalışır. Kontroller derlemeden **önce**, sonra fonksiyon izin listesi + `XPathExpression.Compile`:

| Kural | Kod |
|---|---|
| Yalnız XPath 1.0 çekirdek + XSLT 1.0'ın üç taşınabilir fonksiyonu | `XPATH_NOT_PORTABLE` |
| Uzunluk ≤ 512 | `XPATH_TOO_LONG` |
| Parantez ≤ 16, iç içe yüklem ≤ 3, ifade başı önemsiz olmayan yüklem ≤ 4 (`[n]`, `[local-name()='x']` sayılmaz), `\|` işleneni ≤ 4 | `XPATH_TOO_COMPLEX` |
| Yüklemde N-sınıfı eksen (`//`, `descendant*`, `following/preceding*`) + genel karşılaştırma; `s/p = s/q` küme karşılaştırması; `current()//`, `$v/descendant` | `XPATH_TOO_COMPLEX` |
| Yüklemde yukarı adım (`..`, `parent::`, `ancestor*::`) sonrası `//` veya alt öğe adımı (`../zz`); yukarı eksenin dize değeri (`string(ancestor::*)`) | `XPATH_TOO_COMPLEX` |
| Kardeş ekseni yüklemde yalnız komşu `X-sibling::*[1]`; **tekilleştirme deseni** (`preceding-sibling::X[. = current()]`) reddedilir (66k kardeşte ~70 sn) | `XPATH_TOO_COMPLEX` |
| Tekil bağlamlar `(x)[1]`, `format-number(number(...))` (G1–G4); `format-number` öncesi ön-yuvarlama `round(x*10^d + 1e-6) div 10^d` (G5) | generator |

- **Yumuşak düşüş (G3):** taşınamaz bağlamalı blok, şablonun tamamını reddetmez; yerine görünür `xc-binding-error` yer tutucusu üretilir ve yanıt `warnings: [{blockId, code, xpath}]` taşır (`/api/preview`, `user-template`). İndirmede (`/download`) yer tutuculu XSLT saklanır, uyarı taşınmaz. Frontend `warnings`'i henüz göstermiyor.
- Ağaç başı bağlama/boyut sınırı (`BLOCKTREE_TOO_LARGE`) **uygulanmadı** → v3 `BlockTreeValidator` (2.12). `XPathExpressionValidator` (v3 4.2) `XPathPortability`'yi kullanmalı.
- **İndirilen `.xslt` 1.0-taşınabilir kalır.** "Hesaplanan KDV" varsayılanı yeni bloklarda `/n1:Invoice/cac:TaxTotal/cbc:TaxAmount` (`5a24302`).

### 6. XsltSafety modları
Fail-closed tam parse (`SecureXslt.ReaderSettings`; parse hatası/DOCTYPE = tehdit; regex yedeği yok). `Scan(xslt, mode)` → `{Code, Severity: Security|Unsupported, Message, Line, Column}`; `FindThreat` = Store modu.

| Mod | Engeller | Kullanım yerleri |
|---|---|---|
| **Execute** | Security + Unsupported | generator `Validate`, `XsltExecutionService` (`IXsltSafetyScanner`), worker içi `XsltRequestRunner` |
| **Store** | Yalnız Security | `UserXsltTemplateController`, `AdminController`, `ThemeUploadProcessor`, `ThemeSlotAnalyzer`, `ThemeOverridesProcessor`, `ThemeAssetComposer`, `FixedNoteInjector` |

Security: `xsl:` eleman izin listesi; `import/include/import-schema/use-package/result-document/source-document/evaluate/merge*/try/catch`, `xi:include`, `msxsl:script`, `saxon.sf.net`/`java:`/`clitype:` (ad alanı URI'leri boşluk normalize edilerek); `use-when`/`xsl:use-when`, `static="yes"`, `expand-text`, `_` gölge öznitelikleri (değerler trim'lenir); yasak fonksiyonlar (`document`, `doc*`, `unparsed-text*`, `collection`, `uri-collection`, `environment-variable`, `json-doc`, `parse-xml*`, `transform`, `load-xquery-module`, `function-lookup`; `system-property` yalnız `xsl:*` literal), `ad#N`, `function(`, `$v(`; kullanıcı `xsl:function`'ı yasak/ayrılmış ad alanında olamaz; AVT'ler yorum (`(: :)`) ve `collation` dahil taranır; collation literal'i yalnız codepoint / `%`'siz UCA; boyut (derinlik ≤ 256, ifade ≤ 8 KB, parantez ≤ 64). Store modu zararsız `msxsl:`/`exsl:` fonksiyonlarına ve `system-property('msxsl:version')`'a izin verir. XsltSafety derinlemesine savunmadır; asıl sınır Saxon sandbox + worker'dır (ADR 008).

### 7. Uç sözleşmesi (2.0.0, kırıcı)

| Uç | Yetki | Hız sınırı | Gövde |
|---|---|---|---|
| `POST /api/preview` | `[Authorize]` | `xslt-preview` 120/dk/kullanıcı | 10 MB |
| `POST /api/preview/raw`, `/user-template/{id}` | `[Authorize]` | `xslt-preview` | 12 MB |
| `POST /api/preview/validate-xslt` | `[Authorize]` | `xslt-validate` 120/dk/kullanıcı | 1 MB |
| `POST /api/preview/theme/{id}` | anonim (`ThemeAccessGuard`; yayınlanmamış/başkasının → 404) | `theme-preview` 30/dk kullanıcı, anonimde IP | 6 MB |

- `UseRateLimiter` artık `UseAuthentication`'dan **sonra** (önceden kullanıcı bölümlü politikalar fiilen IP bazlıydı).
- Başarı `200 {html, generationTimeMs, timings, engine, cacheHit, warnings}`. Hata gövdesi `{error, line, column, kind, code, engine}` (`XsltProblemResults`; `error/line/column` adları AI teşhisi için korunur).
- HTTP: içerik hataları (`Xml`, `Compile`, `Runtime`, `Security`, `Unsupported`, `Timeout`, `MemoryLimit`, `OutputLimit`, `LimitExceeded`, `EngineCrashed`) → 400; `Busy`/`EngineUnavailable` → 503 + `Retry-After` (2 / 5 sn); `UserBusy` → 429 (blokta `Retry-After`); `EngineInternal` → 500, jenerik mesaj, satır/kolon/kod yok. Hız sınırlayıcı 429'u `kind: "RateLimited"` döner (`code` alanı yok).
- `validate-xslt`: `200 {valid:true, engine}`; içerik hatası `200 {valid:false, error, line, column, kind, code, engine}`; operasyonel hatalar yukarıdaki 4xx/5xx kodlarıyla.
- Frontend tek adaptör: `services/xsltErrors.ts` (`toXsltError`; `isOperational` → satır işaretlenmez; `WorkerDisabled` `code` ile ayırt edilir).

## Alternatifler

| Alternatif | Neden reddedildi |
|---|---|
| Önce .NET, hata olursa Saxon'a düş | İleri-uyumlu mod `xsl:character-map`'i sessizce yutar, `exists()` ancak runtime'da patlar → fallback tetiklenmez |
| Tek motor anahtarı (`Xslt:Engine`) | Raw ve Grid'in risk profili ve parite olgunluğu farklı; grid bağımsız geri alınabilmeli |
| DB feature flag | Acil anahtar için yapılandırma yeterli; ek yönetim yüzeyi ve çok-örnek tutarlılığı getirir |
| Grid'de XSLT 2.0 fonksiyonlarına izin | Çift-motor paritesini ve `DotNet` anahtarını bozar; indirilen dosya 1.0 alıcılarda kırılır |
| Grid de worker'da | **Sahip reddetti:** grid kaynağı generator'dır ve XPath 1.0'a sınırlıdır; canlı önizleme yüksek frekanslıdır ve 2'lik havuzu doyurur. Kalan CPU riski korumalı süreç içi yürütme + bozulmuş modla kabul edildi (ADR 008) |
| Saxon-HE 10.9 NuGet / SaxonCS | 10.9 net10'da çalışmıyor; SaxonCS ticari |

## Sonuçlar
- **+** gib-general ve diğer 2.0 stylesheet'ler ham editörde Saxon ile doğru render edilir (uçtan uca test: `PreviewEndpointTests`, `engine=Saxon`); 1.0 stylesheet'ler bit-aynı kalır. AI'ın ürettiği 2.0 XSLT `validate-xslt`'ten geçer.
- **+** Generator G1–G5 çıktısı değiştirdi; `.verified` XSLT golden'ları bilinçli güncellendi, HTML snapshot'ları değişmedi.
- **−** 2.0.0 kırıcı: anonim önizleme/doğrulama kalktı (eski bundle 401 alır → nginx `index.html` `no-cache`), hata sözleşmesi değişti, 3.0 yapıları `Unsupported`, tekilleştirme deseni gibi pahalı XPath'ler yer tutucuya düşer.
- **−** İki motor, iki yürütme yeri: parite testleri bloke eden kapıdır; Theme rotası T-izi'ne kadar 1.0'da kalır (gib-general tema olarak hâlâ reddedilir).

## Doğrulama

| Alan | Testler |
|---|---|
| Sniff / seçim / kontrat | `XsltVersionSnifferTests`, `XsltEngineSelectorTests`, `DotNetXsltEngineTests`, `XsltDiagnosticContractTests` |
| Rotalar | `XsltExecutionServiceTests` (Raw/Validate/UserTemplate/Grid/Theme, boyut, `WorkerDisabled`, kapı) |
| Uçlar | `XsltWorker.Tests/Api/{PreviewEndpointTests, PreviewThemeEndpointTests, TemplateAssetEmbeddingTests}` (401, asset IDOR, Kind→HTTP, gib-general) |
| Grid paritesi | `GridV2GoldenTests`, `GridEngineParityTests` + `UserTemplateWorkerParityTests` (bloke eden), `XPathPortabilityTests` (`DeepParens_RejectedBeforeCompile` dahil) |
| XsltSafety | `XsltSafetyTests`, `Safety/{XsltSafetyBypassTests, XPathTokenizerTests}`, `XsltSafetyFixtureTests` (Store'da 0 Security), `CorpusDryRunTests` (`Category=Corpus`) |
| Eval | case008+ ve motor parite eval'i — T4.1 (non-blocking). Planlanan `e2e/xslt-engine.spec.ts` yazılmadı |
