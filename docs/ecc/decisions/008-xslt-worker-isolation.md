# ADR 008 — Kullanıcı XSLT'si için worker süreç izolasyonu (Saxon-HE 12.10 / IKVM)

**Durum:** Kabul edildi (2026-10-25) · Sürüm 2.0.0 · İlgili: ADR 007 (motor yönlendirme), ADR 003 (yerine geçti)

> Kod-gerçeği (`feature/xslt2-engine`, W1–W3 + G1–G3). Sayısal sınırlar `appsettings.json` (`Xslt`, `XsltWorker`) ve `XsltExecutionOptions` varsayılanlarıdır.

## Bağlam

**K4 başarısız: tek istek Api sürecini öldürür.** W0 spike'ında (Saxon-HE 12.10, Win + Linux `sdk:10.0`) sonsuz özyineleme süreci düşürdü (`0xC00000FD` / `134`). .NET'te `StackOverflowException` yakalanamaz, thread öldürülemez (`Thread.Abort` yok); büyük stack'li thread yalnız eşiği öteler. Eski anonim `/api/preview/raw` + .NET özyinelemesi aynı açığa sahipti.

**Süreç içi savunma (b) yetersiz.** `TraceListener` derinlik sınırı template/`xsl:function` özyinelemesini yakalanabilir hataya çevirir, ama G0 incelemeleri (~35 vektör) 7 çökme/kilitlenme yolu buldu:

| Yol | Not |
|---|---|
| Satır içi fonksiyon özyinelemesi (`function($g,$n){...}`) | `enter()` üretmez; 64 MB stack'te bile çöker |
| `fn:transform` + `stylesheet-text` iç özyinelemesi | İç stylesheet trace'siz → sınır atlanır |
| `use-when` / `static` / gölge `_*` statik ifadeler | **Derleme anında**, XPath 3.1 ile (validate-xslt de etkilenir) |
| `xsl:try`/`xsl:catch` ile sınır sıfırlama | ~75 sn CPU sonra çöker |
| 50k iç içe LRE stylesheet | Derleme anında, ~82 sn |

Durdurulamaz CPU: `for $i in 1 to 2e9` (`enter` yok); ReDoS ~4,5 sn/çağrı; bellek ikiye katlama 536M karakter / 3,7 sn; stylesheet iç entity 30 KB → 30M karakter (413 MB). Kaçak thread öldürülemediği için süreç içi semafor kalıcı dolar.

## Karar

### 1. Paket ve proje yapısı

| Öğe | Karar |
|---|---|
| Saxon | `IKVM.Maven.Sdk 1.12.2` + `MavenReference net.sf.saxon:Saxon-HE 12.10`; transitif `xmlresolver 5.3.3` (+data) sabit. Lisanslar: Saxon MPL-2.0, IKVM MIT, OpenJDK GPLv2+CE, xmlresolver Apache-2.0 |
| Uyarılar | `IKVM0100` (opsiyonel bağımlılık) kabul; `IKVM0117` (`AnyURIValue.decode`, `%`'li collation URI'si) → `RestrictedCollationResolver` `%`'yi reddeder |
| `XsltCraft.Xslt.Saxon` | **Tek** IKVM/Maven csproj'u; yalnız Application'a referans. `SaxonXsltEngine`, `SaxonProcessorFactory` (tek sandbox), `SaxonProfile` (`Isolated` / `InProcessGrid`), `SaxonEngineOptions`, `Sandbox/*`, `Limits/*` (`DepthLimitingTraceListener`, `SaxonLimitError`, `ExecutionBudget`, `LimitedJavaWriter`), `Input/*` (`StylesheetSource`, `XmlReaderTreeBridge`), `Diagnostics/*`, `SaxonProcessStdio`. `packages.lock.json` + `maven-hashes.txt` + `MavenHashVerification.targets` |
| `XsltCraft.XsltWorker` (exe) | `Program` + `Hosting/*`: `WorkerHost`, `EngineThread` (64 MB), `WorkerEngineSet` (Saxon `Isolated` + .NET), `XsltRequestRunner`, `CompileCache` (LRU 16), `JavaStdio`/`StdioGuard`, `ProcessHardening`, `EngineWarmup`. Referans: Contracts, Xslt.Saxon (Application geçişli) — **Infrastructure yok**; host builder yok, appsettings okumaz |
| `XsltCraft.XsltWorker.Contracts` | Bağımlılıksız: `Framing/*` (`FrameCodec`), `FrameLimits`, `Messages/*` (STJ source-gen), `ProtocolVersion` |
| Infrastructure | `Xslt/Worker/*` (`XsltWorkerPool` + partial'lar, `WorkerProcess` + partial'lar, `WorkerProcessStartInfoFactory`, `XsltWorkerLocator`, `WorkerCircuitBreaker`, `HashAffinity`, `StderrRingBuffer`, `XsltWorkerMetrics`, `WorkerXsltHost : IXsltIsolatedHost`), `Xslt/InProcess/*` (grid), `Xslt/XsltUserGate`. Contracts ve Xslt.Saxon'a (süreç içi grid) referans verir |
| Api | Worker'a build-sırası referansı (`backend/build/XsltWorker.targets`, `ReferenceOutputAssembly=false`), çıktı `$(OutDir)xsltworker/`; IKVM Infrastructure üzerinden Api çıktısına iner (`IkvmFlowTests`) |

`LayeringTests` bloke eder: Maven/IKVM yalnız Xslt.Saxon; App/Domain/Contracts → Saxon yok; Xslt.Saxon → yalnız Application; Worker ↛ Infrastructure; `net.sf.saxon` yalnız Xslt.Saxon; Saxon 10.9 hiçbir csproj'da yok.

### 2. IPC: stdin/stdout çerçeve protokolü
`"XC"`(2B) · ver(1B) · type(1B: Hello/Request/Response/Shutdown) · headerLen(u32 LE) + header JSON (≤ 64 KB) · bodyCount(1B, ≤ 8) + {len u32 LE + ham UTF-8}*. İstek çerçevesi ≤ 12 MB, yanıt ≤ 24 MB; ihlal → kill.
- **Hello** `{protocol, workerVersion, saxonVersion: "12.10", pid}` — ısınmadan **sonra** gönderilir (ısınma hatası → çıkış 5, Hello yok). **Request** `{id, op: render|validate|ping, engine, xsltSha256, limits{deadlineMs, maxOutputChars, maxStylesheetDepth, maxInputDepth, maxInputNodes}}` — worker SHA-256'yı kendi hesaplar (`worker.sha256_mismatch`) ve her istekte XsltSafety Execute uygular. **Response** `{id, ok, failure{kind,message,line,column,errorCode}, timings, engine, cacheHit, workingSetBytes, gcHeapBytes, recycleRequested}`.
- stdout yalnız protokoldür: `Console.SetOut(Null)` + Java `System.setOut(null)`, `setErr` ≤ 16 KB (IKVM `System.out` doğrudan fd 1'e yazıyor; negatif kontrol testli). stderr Api'de 8 KB halka tampon, içeriği loglanmaz. Ebeveyn stdin kapanınca worker çıkar. OOM → `MemoryLimit` + `recycleRequested` + çıkış 4.

### 3. Havuz ve yaşam döngüsü
`PoolSize` **2** (kesin: Ollama aynı host'ta). Paralel sıcak başlatma, Hello → Ready (Linux konteynerde ~0,6 sn); `/health` worker'a bağlı değil. Worker başına tek istek; yumuşak afinite (worker başına son 16 hash). Recycle: 500 istek / 30 dk / RSS 512 MB / `recycleRequested`; spawn-before-retire. Yeniden başlatma backoff 0,5 → 30 sn ve kesici (60 sn'de 5 → 60 sn açık, 503) **yalnız** çökme/protokol/handshake hatalarını sayar — `Timeout`/`MemoryLimit` kullanıcı kapısına (§5) aittir. Worker bulunamazsa Api açılır, havuz Warning loglar, `EngineUnavailable` döner.

### 4. Sınırlar

| Rota / işlem | XSLT | XML | Sert süre | Girdi derinlik / düğüm |
|---|---|---|---|---|
| Raw (render) | 5M kr / 5 MiB | 5M kr / 5 MiB | 10 sn | 512 / 1M |
| Validate | 1M kr / 1 MiB | — | 5 sn | — |
| UserTemplate | 2M kr / 2 MiB | 5M kr / 5 MiB | 8 sn | 512 / 1M |
| Grid, Theme (süreç içi) | 6M kr / 6 MiB (base64 görseller) | 6M kr / 5 MiB | 8 sn | 64 / 200k |

| Boyut | Sınır → sonuç |
|---|---|
| Ortak | Çıktı 8M kr (`LimitedJavaWriter` + `LimitedTextWriter`) → `OutputLimit`; stylesheet derinlik ≤ 256; izole istekte XSLT+XML ≤ 11 MiB (12 MB çerçeve); boyut ihlali → `LimitExceeded` (`XsltTooLarge`/`XmlTooLarge`) |
| Süre | Sert süre → `Process.Kill(entireProcessTree)` → respawn (`Timeout`); worker'a giden işbirlikçi deadline sert süreden 500 ms kısa (listener `enter()` + iptal) |
| Bellek | `DOTNET_GCHeapHardLimit` 512 MB, `gcServer=0`; Api 200 ms'de WorkingSet yoklar, 768 MB → kill (`MemoryLimit`) |
| Saxon profilleri | `Isolated`: trace derinliği 1000 / 64 MB thread, stylesheet ≤ 4M kr, girdi ≤ 10M kr. `InProcessGrid`: 256 / 16 MB, stylesheet ≤ rota sınırı (6M), girdi ≤ 6M kr. `REGEX_BACKTRACKING_LIMIT` 100 000 (ReDoS 4 sn → 54 ms) |
| Kuyruk | `MaxQueue` 16, `QueueWaitMs` 2000 → 503 `Busy` + `Retry-After: 2`; hazır worker yok → 503 `EngineUnavailable` + `Retry-After: 5` |
| CPU / OOM önceliği | `BelowNormal` (Linux nice 10); `oom_score_adj=1000` |

İstemci isteği iptal ederse worker öldürülmez; yanıt atılır.

### 5. Kullanıcı kapısı (`XsltUserGate`)
Havuzdan **önce** kayıtlı; kimlik `[Authorize]` kullanıcısı, anonim `theme-preview`'da IP'nin SHA-256 türevi Guid.

| Kural | Değer |
|---|---|
| Şerit başı eşzamanlılık (`Raw`, `Validate`, `UserTemplate`, `Grid` — Theme Grid şeridini kullanır) | her biri 1 → 429 `UserBusy` |
| `MaxIsolatedConcurrentPerUser` (şeritlerden bağımsız izole toplam; Validate muaf, bozulmuş moddaki grid sayılmaz) | 1 → 429 |
| Kesici — izole | 10 dk'da ≥ 3 `Timeout`/`EngineCrashed`/`MemoryLimit` → 10 dk 429 (`UserBlocked`, `Retry-After`) |
| Kesici — süreç içi | 10 dk'da ≥ 2 `Timeout` → 10 dk 429 |
| Grid şeridi | bozulmuş modda worker'a gidebildiği için **iki yerin bloğu da** denetlenir (fail-closed) |
| Sonuç bildirimi | host'un **fiilî** yeri (`XsltHostResult.Place`) ile; istemci iptaliyle doğan süreç içi kaçak `Timeout` sayılır ve izin kaçak bitene kadar tutulur (`XsltRunawayCanceledException`) |

Hız sınırları ADR 007 §7'de.

### 6. Saxon sandbox — `SaxonProcessorFactory` (tek yapılandırma, iki profil)

| Katman | Ayar |
|---|---|
| Protokol | `ALLOWED_PROTOCOLS = "xsltcraft-none"` (**tuzak: `""` = "all"**; `"-"` de engelliyor, okunurluk için seçildi) |
| G/Ç | **Fırlatan** `ResourceResolver` (config + compiler + transformer; `null` dönmek varsayılana düşer), `UnparsedTextURIResolver`, `CollectionFinder`, `ResultDocumentHandler`; boş `ENVIRONMENT_VARIABLE_RESOLVER` |
| Dinamik kod | `DISABLE_XSL_EVALUATE=true`, `ALLOW_EXTERNAL_FUNCTIONS=false` (AEF), `ALLOW_SYNTAX_EXTENSIONS=false`, `XINCLUDE=false`, `DTD_VALIDATION=false` |
| Dil tavanı | `XPATH_VERSION_FOR_XSLT=20` |
| Collation | `RestrictedCollationResolver`: yalnız codepoint, html-ascii-case-insensitive, sorgusunda `%`/`class` olmayan UCA; gerisi **fırlatır** |
| Stylesheet | `StylesheetSource.PreRead`: derlemeden önce .NET `SecureXslt.ReaderSettings` ile tam okuma (DOCTYPE → `Security`, derinlik/karakter → `LimitExceeded`) + **statik ifade reddi** (`use-when`, `xsl:use-when`, gölge `_*`, `static`≠no → `SandboxStylesheetStaticExpression`); Saxon'a `disallow-doctype-decl` SAX okuyucuyla verilir. Saxon `ParseOptions` `disallow-doctype-decl` yalnız `parse-xml` vb. için geçerlidir, stylesheet'e uygulanmaz |
| Girdi XML | `XmlReaderTreeBridge` (`LimitedXmlReader`: derinlik/düğüm/karakter) → Saxon ağacı |
| Derinlik / süre | `DepthLimitingTraceListener` transform başına, yapışkan, `java.lang.Error` alt sınıfı (`SaxonLimitError`; `xsl:catch` yakalayamaz) + deadline; kalıcı trace derlemesi |
| Çıktı kanalları | sessiz Logger, sınırlı `ErrorReporter`/`MessageHandler`, `setTraceFunctionDestination(null)` — stderr'e hiçbir şey gitmez; mesajlar Java sınıf/yol adı içermez, `Throwable` → `EngineInternal` |

**Katman düzeltmeleri (spike/taslak varsayımlarına karşı):**
- **`FEATURE_SECURE_PROCESSING` sandbox katmanı DEĞİLDİR.** `SecureProcessing.Apply` çağrılır ama 12.10'da tek etkisi AEF=false yapmaktır; korumayı açık Feature'lar sağlar.
- **"AEF tek katman" yalnız `system-property('java.*'/'user.dir')` için doğrudur.** Ortam değişkenlerinde AEF ikinci katmandır (birincil boş resolver). **Collation için geçersizdir:** `compare(..., concat('http://saxon','.sf.net/collation?class=X'))` AEF=false iken de classpath sınıfını yükleyip no-arg ctor'unu çağırır → `RestrictedCollationResolver` şarttır (17 vektörde testli).
- K3 katman kanıtları: Resolver ↔ Protocols birbirini yedekler; stylesheet iç entity ve `parse-xml` billion-laughs tek katmanlıdır (NoDtd); göreli URI CWD'ye ulaşamaz.

### 7. Grid: süreç içi korumalı yürütme ve bozulmuş mod
Grid ve Theme rotaları `IXsltInProcessHost` → `InProcessXsltHost` (`InProcessXsltEngines`: .NET + tembel Saxon `InProcessGrid`):

| Öğe | Değer |
|---|---|
| Guard (`XsltExecutionGuard`) | semafor `MaxConcurrent` 3, `QueueWaitMs` 2000 → 503 (`InProcessBusy`); iş başına 16 MB thread (derleme dahil); süre aşımında `Timeout` + iş iptali, kaçak thread bitene kadar slotu tutar; istemci iptali de kaçak sayılır |
| Önbellek | `InProcessCompileCache` (`IMemoryCache`, `SizeLimit` 64M karakter, anahtar derinlik dahil); `SaxonWarmupService` (Grid=DotNet iken atlanır) |
| **Bozulmuş mod** | kaçak ≥ `RunawayMax` (2) → grid worker'a yönlenir (`xslt.inproc.degraded.routed`); kaçaklar boşalınca geri döner; worker yoksa 503 `InProcessDegraded` |
| Takılı kaçak | **`Xslt:Grid:RestartOnStuckRunaway = false`** (sahip kararı): 15 dk takılı kaçakta yalnız `Critical` log; Api yeniden başlatılmaz |
| Api sertleştirme | Linux'ta `prctl(PR_SET_DUMPABLE, 0)` (`Security:NonDumpable`, `Infrastructure/Security/ProcessHardening`); süreç içi Saxon için `SaxonProcessStdio.DiscardJavaStdio`. Api'de env secret'lar bulunduğundan `EnvVar_Empty_InProcess` / `SystemProperty_Blocked_InProcess` bloke eden testlerdir |

### 8. Kabul edilmiş risk (sahip)
> Grid ve Theme önizlemesi Api süreci içinde çalışır. XPathPortability sınırlarına (ADR 007 §5) rağmen süper-doğrusal maliyetli bir XPath 1.0 bağlaması (.NET, çıktı yazmayan) iptal edilemeyen bir thread'i girdi sınırlarıyla (≤ 5 MB / ≤ 200k düğüm / derinlik 64) orantılı süre meşgul edebilir. Etki üst sınırı `RunawayMax` (2) çekirdektir; eşikte grid otomatik olarak worker'a yönlenir, takılı kaçak yalnız loglanır. Grid'in kullanıcı XSLT'si değil generator çıktısı olması ve canlı önizleme gecikmesi karşılığında bilerek kabul edilmiştir.

### 9. Süreç sertleştirme, Docker, CI, tedarik zinciri
- **Worker ortamı:** `Environment.Clear()` + beyaz liste (`DOTNET_ROOT`, GC ayarları, `DOTNET_EnableDiagnostics=0`, boş `TMPDIR`/`HOME`, `PATH=/usr/bin:/bin`, `LANG`/`LC_ALL`/`TZ`/invariant; Windows'ta `SystemRoot`/`windir`/`SystemDrive`/`TEMP`/`TMP`); secret asla geçmez. CWD boş geçici dizin (`xsltcraft-worker-<apiPid>-<n>-<rand8>`, 0700). `--test-ops` havuzdan asla geçmez (testli).
- **Docker:** `syntax=1.7`; cache mount (`/root/.nuget/packages`, `/root/.m2`, `/tmp/ikvm/cache`); restore `-r linux-x64 --locked-mode`; önce worker publish → `/app/publish/xsltworker`, sonra Api `-p:BuildXsltWorker=false`. İmaj ~627 MB (IKVM Api + worker'da iki kopya). compose: `init`, `cap_drop: [ALL]`, `no-new-privileges`, `pids_limit: 512`, `mem_limit: 5g`, `XsltWorker__PoolSize=2`. `update.sh` VPS'teki eski compose yedeğini geri yüklediği için compose değişiklikleri VPS'e elle işlenir.
- **Tedarik zinciri:** `MavenReference` pin, `packages.lock.json` + `--locked-mode`, `maven-hashes.txt` SHA-256 (Maven Central ile eşleşti; uyuşmazlık ve cache zehirlenmesi build'i kırar).
- **CI:** NuGet/Maven/IKVM önbelleği; filtre `Category!=Eval&Category!=Measurement&Category!=Corpus`; worker entegrasyon testleri bloke eder.
- **Gözlemlenebilirlik:** meter `XsltCraft.Xslt` — `xslt.requests{place}`, `xslt.request.duration`, `xslt.queue.wait`, `xslt.worker.{started,ready,exited}`, `xslt.inproc.{active,runaway,degraded,degraded.routed}`. Log: `xsltHash` (ilk 16 hex), rota, motor, `kind`, süreler — **içerik asla loglanmaz**. Kill/respawn/kesici/bozulmuş mod Warning; takılı kaçak Critical.

## Alternatifler

| Alternatif | Değerlendirme |
|---|---|
| (b) Yalnız süreç içi savunma | Reddedildi: 7 çökme yolu, `xsl:try` bypass, derleme-anı özyinelemesi, durdurulamaz CPU; kaçak thread öldürülemez. (b) önlemleri worker içinde derinlemesine savunma olarak kaldı |
| (c) Raw=DotNet, Saxon bayrak arkasında kapalı | Reddedildi: ürün hedefini (2.0 render) karşılamaz |
| Sidecar konteyner | Ertelendi: daha güçlü cgroup/netns izolasyonu, ama tek VPS'te ek işletim yükü ve kimlik doğrulamalı ağ IPC'si |
| seccomp / `unshare` / Landlock | **Sonraki adım:** ağ ve sistem çağrısı izolasyonu yok |
| Grid de worker'da | Sahip reddetti (ADR 007) |

## Sonuçlar
- **+** Kullanıcı XSLT'si Api'yi düşüremez: tüm çökme vektörlerinde Api ayakta, havuz toparlanıyor (`WorkerIsolationTests`).
- **+** Saxon/IKVM tek csproj'da, tedarik zinciri kilitli ve hash'li. Asset IDOR (`PreviewAssetEmbedder`) kapatıldı; görseller her zaman base64 gömülür.
- **−** İmaj ~627 MB, Api RSS +81 MB (Saxon yüklendiğinde), worker IPC sıcak istekte ~7–9 ms, iki ek üretim projesi ve süreç yönetimi kodu.
- **−** Windows yerel geliştirmede `oom_score_adj`/`prctl` yok; sınırlar Api tarafı yoklamayla uygulanır.

## Kalan riskler

| Risk | Durum |
|---|---|
| `transform(stylesheet-text)`: iç stylesheet'te DOCTYPE/iç entity işlenir ve iç özyineleme trace'sizdir — **tek kapı XsltSafety** (`transform` yasak) | Kabul; K3 vektörü testli, worker kill son çare |
| Kesici global: çok hesaplı saldırgan 60 sn'de 5 çökmeyle havuzu 60 sn 503'e düşürebilir | Kabul; kullanıcı kapısı hesap başına sınırlar |
| Worker Api ile **aynı uid**; konteyner root, `/proc/1/environ` okunabilir → worker **gizlilik sınırı değil, DoS sınırıdır** (gizliliği sandbox sağlar) | `USER` non-root + Landlock sonraki adım |
| Çıktı yazmayan süper-doğrusal XPath (.NET) süreç içinde iptal edilemez → kaçak (§8) | Kabul; bozulmuş mod. Kalan sınıf: `string-length(/)` gibi satır başı alt ağaç dize değeri |
| `ForwardedHeaders` yok: Api nginx arkasına alınırsa IP bazlı limitler (`theme-preview` anonim, `auth-sensitive`, `auth-register`) tek kovaya düşer | Açık |
| IKVM dönüşüm önbelleği (`/tmp/ikvm/cache`) hash'siz | Release'te önbelleksiz build ya da doğrulama |
| `PreviewTheme`/tema hattı süreç içi .NET | T-izi; ayrı iş |
| `/api/xpath/evaluate` süreç içi `XPathEvaluator` (G4: `[Authorize]` + `xpath-evaluate` 120/dk/kullanıcı; güvenli okuyucu + grid girdi sınırları; `XPathPortability` maliyet kapısı TOO_LONG/TOO_COMPLEX → 400; ayrı guard 2 slot / 5 sn — kaçak slotu tutar) | Kabul; kalan sınıf §8 ile aynı (kapının geçirdiği süper-doğrusal ifade en çok 2 thread) |
| `/api/assets/{id}/serve` anonim: asset GUID'i erişim anahtarıdır; oturumlu başka kullanıcıya 403 göstermeliktir (oturumsuz aynı istek 200) | Kabul (editör küçük resmi + 2.0 öncesi indirilmiş XSLT'ler bağımlı) |
| Ağ izolasyonu yok | seccomp/`unshare` sonraki adım |

## Ölçümler
Kaynak: `Category=Measurement` (`StackBudgetMeasurementTests`, `GridMeasurementTests`), Windows 11 / .NET 10.0.401 / Debug, 2026-10-24; Linux değerleri T2.5 konteynerinden.

**Yığın kırılma noktası** (derinlik sınırı kapalı; güvenli = son başarılı derinlik):

| Tür | 16 MB | 32 MB | 64 MB |
|---|---|---|---|
| template özyineleme | 5.979 | 12.013 | 23.786 |
| `xsl:function` özyineleme | 10.571 | 20.903 | 42.166 |
| iç içe parantez (derleme) | 5.552 | 11.051 | 22.705 |
| iç içe LRE (derleme) | 9.491 (2,9 sn) | 18.793 (11,6 sn) | 38.443 (54,6 sn) |

Yığın ~doğrusal; trace sınırına pay ~23× (`Isolated` 1000/64 MB, `InProcessGrid` 256/16 MB). Spike'taki "64 MB'ta fn ~5000" muhafazakârdı. Derleme-anı sınırları: XsltSafety parantez ≤ 64 / XPathPortability ≤ 16, ön okuma derinlik ≤ 256.

**Grid kaçak senaryosu** (40k öğe / 200k düğüm / 5,8M kr, worker'da):

| İfade | .NET | Saxon |
|---|---|---|
| `count(//i[@v = //i/@w])` (G3 öncesi kabul ediliyordu) | 8,58 sn | 45,4 sn |
| `count(//i[following::i/@w = @v])` | 9,79 sn | 44,9 sn |
| 3 iç içe yüklem + 6 parantez (doğrusal) | 5 ms | 17 ms |
| **G3 sonrası XPathPortability'nin kabul ettiği en kötü ifade** | **111 ms** | — |

İlk iki sınıf artık `XPATH_TOO_COMPLEX`. **6M kr stylesheet (base64) derleme:** .NET 45–124 ms / ~35 MB geçici, Saxon 149–181 ms / ~78 MB geçici; kalıcı ~11,5 MB → önbellek `SizeLimit`.

**gib-general gecikmesi (Saxon):**

| Yer | Başlangıç | Soğuk ilk istek | Sıcak p50 / p95 |
|---|---|---|---|
| Worker (IPC dahil) | 769 ms (Win) · havuz hazır 587 ms (Linux) | 263 ms (derleme 179) | 10,8 / 15,4 ms |
| Süreç içi (guard thread) | Api soğuk ısınma 1,8 sn, RSS +81 MB | 305 ms (derleme 241) | 3,3 / 6,6 ms |

Linux konteyner: `oom_score_adj` 1000, nice 10, stack overflow çıkış 134 → `EngineCrashed`, 64 MB `maxStackSize` uygulanıyor.

## Açık sorular
1. Konteyneri `USER $APP_UID` (non-root) ile çalıştırmak (+ base image digest, SBOM, Renovate) — sahip onayı bekliyor.
2. IKVM dönüşüm önbelleği release build'de doğrulanmalı mı / kapatılmalı mı?
3. Satır sonu normalizasyonu (`* text=auto`, `*.cs eol=lf` + repo geneli renormalize) — tüm paralel işler bitince ayrı commit.
4. `ForwardedHeaders` (nginx arkasında gerçek istemci IP'si) — IP bazlı limitler için.

## Doğrulama

| Alan | Testler |
|---|---|
| Havuz / IPC | `FrameCodecTests`, `XsltWorkerPoolTests`, `XsltWorkerPoolRecoveryTests`, `XsltWorkerOptionsTests`, `WorkerLifecycleTests`, `WorkerProcessStartInfoTests` (env beyaz liste, CWD, test-ops) |
| Worker motor yolu | `RequestHandlerTests`, `EnginePoolTests` (önbellek, OOM, uçtan uca havuz) |
| İzolasyon | `WorkerIsolationTests` + `IsolationVectors` (gerçek süreç, K4 matrisi; CPU/bellek/özyineleme → Api ayakta) |
| Sandbox | `SaxonSandboxTests`, `Sandbox/K3Vectors` (72 vektör × 2 profil, Full-eksi-X katman kanıtları), `SaxonCollationTests`, `SaxonStdioTests` |
| Listener / limit / teşhis | `DepthLimitTests`, `SaxonXsltEngineTests`, `XmlReaderTreeBridgeTests`, `SaxonDiagnosticsTests` |
| Süreç içi grid | `InProcessXsltHostTests`, `XsltExecutionGuardTests`, `XsltUserGateTests` |
| Katman / paket | `LayeringTests`, `IkvmFlowTests` |
