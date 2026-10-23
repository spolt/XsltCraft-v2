# ADR 008 — Kullanıcı XSLT'si için worker süreç izolasyonu (Saxon-HE 12.10 / IKVM)

**Durum:** Önerildi (2026-10-23) · Hedef sürüm 2.0.0 · İlgili: ADR 007 (motor yönlendirme), ADR 003 (kısmen yerine geçti)

> Bu ADR'deki projeler, sınıflar, ayarlar ve testler **planlanan**dır (`feature/xslt2-engine`, W1–W4); sayısal sınırlar uygulamada testle kesinleşir.

## Bağlam

**K4 başarısız: tek istek Api sürecini öldürür.** W0 spike'ında (Saxon-HE 12.10, Win + Linux `sdk:10.0`) sonsuz özyineleme süreci düşürdü (`0xC00000FD` / `134`). .NET'te `StackOverflowException` yakalanamaz, thread öldürülemez (`Thread.Abort` yok); büyük stack'li thread yalnız eşiği öteler. Bugün de anonim `/api/preview/raw` + .NET özyinelemesi aynı açığa sahiptir.

**Süreç içi savunma (b) yetersiz.** `TraceListener` derinlik sınırı (K4b; p95 +~4 ms) template/`xsl:function` özyinelemesini yakalanabilir hataya çevirir, ama G0 incelemeleri (reviewer + security-reviewer, ~35 vektör) şunları gösterdi:

| Çökme / kilitlenme yolu (G0 security-reviewer, toplam 7 yol raporlandı) | Not |
|---|---|
| Satır içi fonksiyon özyinelemesi (`function($g,$n){...}`) | `enter()` üretmez; 64 MB stack'te bile çöker |
| `fn:transform` + `stylesheet-text` iç özyinelemesi | İç stylesheet trace'siz → sınır atlanır |
| `use-when` / `static` satır içi özyineleme | **Derleme anında** (validate-xslt de etkilenir) |
| `_select` gölge öznitelik + string birleştirme | Kaynak taramasında `function(` görünmez |
| `xsl:try`/`xsl:catch` ile sınır sıfırlama | ~75 sn CPU sonra çöker (K-1, kritik) |
| 50k iç içe LRE stylesheet | Derleme anında, ~82 sn |

Durdurulamaz CPU: `for $i in 1 to 2e9` (`enter` yok); ReDoS ~4,5 sn/çağrı; bellek ikiye katlama 536M karakter / 3,7 sn; stylesheet iç entity 30 KB → 30M karakter (413 MB). Kaçak thread öldürülemediği için süreç içi semafor kalıcı dolar.

**Spike ölçümleri (12.10):** soğuk açılış 724 ms; sıcak p95 6,3 ms (Win) / 11,2 ms (Linux); RSS +73 / +84 MB; 8 paralel render bit-aynı (K7); Linux 53/53 test; stack bütçesi 1 MB'ta ~200, 64 MB'ta ~5000 fonksiyon derinliği (32 MB ve kırılma noktası ölçülmedi).

## Karar

### 1. Paket, lisans, proje yapısı

| Öğe | Karar |
|---|---|
| Saxon | `IKVM.Maven.Sdk 1.12.2` + `<MavenReference Include="net.sf.saxon:Saxon-HE" Version="12.10" />`; transitif `xmlresolver 5.3.3` (+data) sabit |
| Lisanslar | Saxon MPL-2.0, IKVM MIT, OpenJDK GPLv2+CE, xmlresolver Apache-2.0 |
| Derleme uyarıları | `IKVM0100` (opsiyonel bağımlılık) kabul; `IKVM0117` (`AnyURIValue.decode`, yalnız `%`'li collation URI'sinde `NoSuchMethodError`) → catch-all + test |
| `backend/XsltCraft.Xslt.Saxon/` | **Tek** IKVM/Maven csproj'u. `SaxonXsltEngine`, `SaxonProcessorFactory` (tek sandbox; profiller `Isolated` / `InProcessGrid`), `SaxonEngineOptions`, `DepthLimitingTraceListener`, `LimitedJavaWriter`, `XmlReaderTreeBridge`, `SaxonDiagnostics`, `RestrictedCollationResolver`. Yalnız Application'a referans verir |
| `XsltCraft.XsltWorker` (exe) | `Program`, `Hosting/*` (`EngineThread` 64 MB, `RequestHandler`, `CompileCache` LRU 16, `ProcessHardening`). Referans: Application, Contracts, Xslt.Saxon — **Infrastructure yok**. Host builder yok, appsettings okumaz |
| `XsltCraft.XsltWorker.Contracts` | Bağımlılıksız wire DTO, `FrameCodec`, `ProtocolVersion` |
| Infrastructure | `Xslt/Worker/*` (`XsltWorkerPool`, `WorkerProcess`, `WorkerProcessStartInfoFactory`, `XsltWorkerLocator`, `XsltWorkerOptions`, `XsltWorkerMetrics`, `WorkerXsltHost : IXsltIsolatedHost`), `XsltUserGate`, `Xslt/InProcess/*` (grid) |
| Api | Worker'a build-sırası referansı (`ReferenceOutputAssembly=false`), çıktı `$(OutDir)xsltworker\`; Xslt.Saxon'a doğrudan referans yok |

Katman kuralları `LayeringTests` ile bloke edilir (Maven/IKVM yalnız Xslt.Saxon; App/Domain/Contracts → Saxon yok; Worker ↛ Infrastructure; Saxon 10.9 hiçbir csproj'da yok).

### 2. IPC: stdin/stdout çerçeve protokolü
`"XC"`(2B) · ver(1B) · type(1B: Hello/Request/Response/Shutdown) · headerLen(u32 LE) + header JSON (STJ source-gen, ≤ 64 KB) · bodyCount(1B) + {len u32 LE + ham UTF-8}*. Gövdeler JSON'a gömülmez. `MaxRequestFrame` 12 MB, `MaxResponseFrame` 24 MB; ihlal → kill.
- **Hello** `{protocol, workerVersion, saxonVersion, pid}` · **Request** `{id, op: render|validate|ping, engine: dotnet|saxon, xsltSha256, limits{deadlineMs, maxOutputChars, maxStylesheetDepth, maxInputDepth, maxInputNodes}}` · **Response** `{id, ok, failure{kind,message,line,column,errorCode}, timings, engine, cacheHit, workingSetBytes, gcHeapBytes, recycleRequested}`.
- stdout yalnız protokoldür: `Console.SetOut(Null)`, Java `System.out/err` yönlendirilir; stderr 8 KB halka tampon, içeriği loglanmaz. Ebeveyn stdin kapanınca worker çıkar.

### 3. Havuz ve yaşam döngüsü
`PoolSize` 2 (VPS'te doğrulanacak), paralel sıcak başlatma, Hello + ısınma → Ready; `/health` worker'a bağlı değil. Worker başına tek eşzamanlı istek; yumuşak afinite (worker başına son 16 hash). Recycle: 500 istek / 30 dk / RSS eşiği / `recycleRequested`; spawn-before-retire; yeniden başlatma backoff 0,5 sn → 30 sn. Kesici: 60 sn'de 5 çökme → 60 sn açık (503). `XsltWorker:Enabled=false` (yerel geliştirme): 1.0 süreç içi `DotNetXsltEngine`, 2.0 → 503.

### 4. Sınırlar

| Boyut | Sınır → sonuç |
|---|---|
| Süre | Raw render 10 sn, UserTemplate 8 sn, validate 5 sn → `Process.Kill(entireProcessTree)` → `WaitForExit` 2 sn → respawn (`Timeout`); worker içinde ayrıca işbirlikçi deadline (listener `enter()`) |
| Bellek | `DOTNET_GCHeapHardLimit` 512 MB, `DOTNET_gcServer=0`; Api 200 ms'de WorkingSet yoklar, 768 MB → kill (`MemoryLimit`); worker OOM'u yakalar → yanıt + recycle + çıkış |
| CPU önceliği | `BelowNormal`; Linux `oom_score_adj=1000` |
| Çıktı | 8M karakter (`LimitedJavaWriter` + `LimitedTextWriter`) → `OutputLimit` |
| Girdi | Raw XSLT 2 MB, XML 5 MB; Validate 1 MB. XML derinlik ≤ 512, düğüm ≤ 1M, ≤ 10M karakter. Stylesheet derinlik ≤ 256, XPath ≤ 8 KB, parantez ≤ 64; `REGEX_BACKTRACKING_LIMIT` 100 000 |
| Kuyruk | `MaxQueue` 16, `QueueWaitMs` 2000 → 503 `Busy` + `Retry-After: 2`; hazır worker yok → 503 `EngineUnavailable` |
| Kullanıcı başı kapı | Raw/Validate aynı anda 1, UserTemplate 2 (doğrulanacak, bkz. açık soru 8) → 429 `UserBusy`; rate limit `xslt-preview`/`xslt-validate` 120/dk |
| Kötüye kullanım kesicisi | 10 dk'da ≥ 3 `Timeout`/`EngineCrashed`/`MemoryLimit` → kullanıcıya 10 dk 429 |

İstemci isteği iptal ederse (`RequestAborted`) worker öldürülmez; yanıt atılır.

### 5. Süreç sertleştirme
- **Ortam:** `Environment.Clear()` + beyaz liste (`DOTNET_ROOT`, GC ayarları, `DOTNET_EnableDiagnostics=0`, boş `TMPDIR`/`HOME`, `PATH=/usr/bin:/bin`, `LANG`/`LC_ALL`/`TZ`/invariant; Windows'ta `SystemRoot`/`windir`/`SystemDrive`/`TEMP`/`TMP`). Secret asla geçmez.
- **CWD:** boş geçici dizin (göreli URI'ler `appsettings.json`'a ulaşamaz).
- **Api:** Linux'ta `prctl(PR_SET_DUMPABLE, 0)` (`Security:NonDumpable`) — aynı kullanıcıdaki worker Api'nin `/proc` bellek/ortamını okuyamaz.
- **Test-ops:** `--test-ops` (crash-stackoverflow, sleep, alloc, stdout-garbage, render-unscreened) yalnız testlerde; havuz asla geçirmez (test ile bloke edilir).

### 6. Saxon sandbox (her iki profil)
`new Processor(false)`; `ALLOWED_PROTOCOLS` geçersiz token (**tuzak: `""` = "all"**; `"xsltcraft-none"` vs `"-"` testle seçilir); fırlatan `ResourceResolver` (config + compiler + transformer — `null` dönmek varsayılana düşer), `UnparsedTextURIResolver`, `CollectionFinder`, `ResultDocumentHandler`; `ENVIRONMENT_VARIABLE_RESOLVER` boş; `DISABLE_XSL_EVALUATE=true`; `ALLOW_EXTERNAL_FUNCTIONS=false` (`system-property('java.*')` sızıntısını engelleyen tek katman — ayrıca test); `ALLOW_SYNTAX_EXTENSIONS=false`; `XPATH_VERSION_FOR_XSLT=20`; `RestrictedCollationResolver` (`saxon.sf.net/collation?class=` reddi); sınırlı `ErrorReporter`/`MessageHandler`, `setTraceFunctionDestination(null)`. Java parser `disallow-doctype-decl` **stylesheet'e uygulanmaz** → stylesheet ve girdi XML derlemeden önce .NET `SecureXslt.ReaderSettings` ile parse edilir (DTD reddi, fail-closed) ve `XmlReaderTreeBridge` ile Saxon ağacına aktarılır. Derinlik sınırı **transform başına**, yapışkan (sıfırlanmaz) ve `XPathException` olmayan `java.lang.Error` alt sınıfıyla atılır (`xsl:catch` yakalayamaz) + deadline. Hata mesajları Java sınıf/yol adı içermez; `Throwable` → `EngineInternal`.

### 7. Grid: süreç içi korumalı yürütme ve bozulmuş mod
Grid (ADR 007) Api sürecinde `IXsltInProcessHost` → `InProcessXsltHost` (`InProcessGrid` profili) ile koşar:

| Öğe | Değer |
|---|---|
| Guard (`XsltExecutionGuard`) | semafor `MaxConcurrent` 4, `QueueWaitMs` 2000 → 503; ayrı thread 16 MB; `WaitAsync` 8 sn → `Timeout` (kaçak thread bitene kadar slotu tutar; `xslt.inproc.runaway` gauge) |
| Girdi | ≤ 5 MB, `MaxDepth` 64, ≤ 200k düğüm, ≤ 6M karakter; çıktı 8M karakter |
| Önbellek | `IMemoryCache` 32 giriş; `SaxonWarmupService` soğuk açılışı gizler |
| Kullanıcı başı | aynı anda 1 → 429; 120/dk; 10 dk'da ≥ 2 timeout → 10 dk 429 |
| **Bozulmuş mod** (kabul) | Kaçak thread ≥ `RunawayMax` (2) → grid worker'a yönlenir; kaçaklar boşalınca süreç içine döner; worker yoksa 503 |
| Takılı kaçak | **`Xslt:Grid:RestartOnStuckRunaway = false`** (sahip kararı): 15 dk takılı kaçakta yalnız `Critical` log; Api yeniden başlatılmaz |

Api sürecinde env secret'lar bulunduğundan `EnvVar_Empty_InProcess` ve `SystemProperty_Blocked_InProcess` bloke eden testlerdir. Api RSS +75–85 MB, imaj ~590 MB (IKVM iki kopya: Api + worker).

### 8. Kabul edilmiş risk (sahip)
> Grid önizlemesi Api süreci içinde çalışır. XPathPortability sınırlarına (ADR 007 §5) rağmen süper-doğrusal maliyetli bir XPath 1.0 bağlaması, iptal edilemeyen bir thread'i girdi sınırlarıyla (≤ 5 MB / ≤ 200k düğüm / derinlik 64) orantılı süre boyunca meşgul edebilir. Etki üst sınırı `RunawayMax` (2) çekirdektir; eşiğe ulaşıldığında grid otomatik olarak worker'a yönlenir (bozulmuş mod), takılı kaçak yalnız loglanır. Bu risk, grid'in kullanıcı XSLT'si değil generator çıktısı olması ve canlı önizleme gecikmesi karşılığında bilerek kabul edilmiştir.

### 9. Docker, CI, tedarik zinciri
- Dockerfile `syntax=1.7`; BuildKit cache mount (`/root/.nuget/packages`, `/root/.m2`, IKVM cache). Restore `-r linux-x64 --locked-mode` (NETSDK1047); worker önce publish → `/app/publish/xsltworker`, Api publish da IKVM içerir (`--self-contained false`). `.dockerignore`: `spike/`, `**/*.received.*`.
- compose: `init: true`, `cap_drop: [ALL]`, `no-new-privileges`, `pids_limit: 256`, `mem_limit`.
- Maven Central bağımlılığı: `MavenReference` sürüm pin, `packages.lock.json` + `--locked-mode`, `maven-hashes.txt` (SHA-256; CI karşılaştırır — Saxon-HE-12.10.jar sha1 `1d6492dbfa32ddb2cd432fa3fbb940bf8137a5d2`), base image digest, SBOM'a Maven bileşenleri.
- CI filtresi `Category!=Eval&Category!=Measurement&Category!=Corpus`; worker entegrasyon testleri CI'da bloke eder; spike testleri CI'da koşmaz.

### 10. Gözlemlenebilirlik
Meter `XsltCraft.Xslt` (`XsltWorkerMetrics`; `xslt.inproc.runaway` gauge), yapısal log: `xsltHash` (ilk 16 hex), rota, motor, `kind`, süreler — **içerik asla loglanmaz**. Kill/respawn/kesici/bozulmuş mod geçişleri Warning; takılı kaçak Critical. Metrik adları T1.3/T2.4b'de kesinleşir.

## Alternatifler

| Alternatif | Değerlendirme |
|---|---|
| (b) Yalnız süreç içi savunma (büyük stack + listener + XsltSafety) | Reddedildi: 7 çökme yolu, `xsl:try` bypass, derleme-anı özyinelemesi, durdurulamaz CPU döngüleri; kaçak thread öldürülemez. (b) önlemleri worker içinde derinlemesine savunma olarak kalır |
| (c) Raw=DotNet varsayılan, Saxon bayrak arkasında kapalı | Reddedildi: ürün hedefini (2.0 render) karşılamaz, imzalı risk kabulü + tarihli worker borcu doğurur |
| Sidecar konteyner (ayrı servis) | Ertelendi: daha güçlü cgroup/netns izolasyonu, ama tek VPS compose'da ek işletim yükü ve kimlik doğrulamalı ağ IPC'si gerektirir |
| seccomp / `unshare` (ağ ve sistem çağrısı kısıtı) | **Sonraki adım:** bu sürümde ağ izolasyonu yok; sandbox (protokol + resolver) tek savunma |
| Grid de worker'da | Sahip reddetti (ADR 007) |

## Sonuçlar
- **+** Kullanıcı XSLT'si Api'yi düşüremez; worker ölümü yalnız o isteği etkiler (timeout → kill → respawn).
- **+** Saxon/IKVM tek csproj'da, tedarik zinciri kilitli ve hash'li.
- **−** İmaj ~590 MB, Api RSS +75–85 MB, IPC gecikmesi, iki ek proje ve süreç yönetimi kodu.
- **−** Windows yerel geliştirmede `oom_score_adj`/`prctl` yok; sınırlar Api tarafı yoklamayla uygulanır.
- Asset IDOR (roadmap 0.5, `BuildAssetBase64Async` sahiplik kontrolü) **bu sürümde T3.1 ile kapatılıyor**.

## Kalan riskler

| Risk | Durum |
|---|---|
| `PreviewTheme` ve `theme/{id}/xslt-content` anonim, tema hattı süreç içi .NET | T-izi (tema izolasyonu) |
| `/api/xpath/evaluate` anonim, süreç içi `XPathEvaluator` | Kapsam dışı; ayrı iş |
| Grid süreç içi CPU (§8) | Kabul edildi; bozulmuş mod |
| Ağ izolasyonu yok | seccomp/`unshare` sonraki adım |

## Ölçümler
_T2.3'te doldurulacak_ (`Category=Measurement`): soğuk/sıcak p50/p95 (süreç içi grid, worker Raw), IPC ek yükü, Api ve worker RSS, imaj boyutu, stack bütçesi (16/64 MB kırılma noktası), timeout→respawn süresi, grid kaçak senaryosu.

| Ölçüm | Hedef | Win | Linux |
|---|---|---|---|
| — | — | — | — |

## Açık sorular (doğrulanacak)
1. `PoolSize` 2 prod VPS çekirdek/bellek bütçesine uygun mu?
2. `ALLOWED_PROTOCOLS` değeri: `"xsltcraft-none"` mı `"-"` mi (`AllowedProtocols_Value_BlocksAllSchemes`)?
3. `REGEX_BACKTRACKING_LIMIT` 100 000 ReDoS'u keserken meşru regex'leri bozmuyor mu? `FEATURE_SECURE_PROCESSING` etkisi?
4. Java `System.setOut/setErr` IKVM'de stdout protokolünü güvenle koruyor mu?
5. IKVM dönüştürülmüş assembly + `ikvm/<rid>` home iki seviyeli ProjectReference ile Api çıktısına iniyor mu (yedek: Api/Worker'a IKVM runtime PackageReference) — `IkvmFlowTests`.
6. Süreç içi grid için 16 MB stack yeterli mi (ölçülmedi)?
7. `[Authorize]` sonrası eski bundle 401: nginx `index.html` `no-cache` mı?
8. UserTemplate kullanıcı başı eşzamanlılık: v2 planındaki worker-grid değeri (2) mi, süreç içi grid değeri (1) mi?

## Doğrulama (planlanan testler)

| Alan | Testler |
|---|---|
| Havuz / IPC | `FrameCodecTests.{RoundTrip_LargeBodies, OversizeFrame_Rejected, BadMagic_Rejected, PartialReads}`; `XsltWorkerPoolTests.{Startup_HandshakeReady, ProtocolMismatch_Rejected, Timeout_KillsAndRespawns, Crash_EngineCrashed_PoolRecovers, MemoryWatchdog_Kills, QueueFull_Busy, Affinity_PrefersHashOwner, Recycle_SpawnBeforeRetire, StdoutGarbage_ProtocolViolation, StderrFlood_DoesNotBlock, Dispose_KillsAll, Breaker_OpensAfterRepeatedCrashes}`; `WorkerLifecycleTests.ParentStdinClosed_WorkerExits` |
| Sertleştirme | `WorkerProcessStartInfoTests.{Env_WhitelistOnly_NoSecrets, Cwd_IsEmptyScratch, TestOps_NeverPassed}` |
| İzolasyon | `WorkerIsolationTests.{TryCatchRecursion_Unscreened_ApiPoolSurvives, CpuLoop_KilledByTimeout, MemoryDoubling_KilledByMemoryLimit}`, `NestedParens50k/NestedLre50k_Unscreened_PoolSurvives` |
| Sandbox | `SaxonSandboxTests.{Stylesheet_WithInternalDtd_RejectedBeforeSaxon, Stylesheet_BillionLaughs_Rejected, ParseXml_InternalEntity_RejectedBySaxonParserLayer}`, `FullSandbox_BlocksVector(name)`, `FullMinus{Resolvers,Protocols,RuntimeIo,Dynamic,NoDtd}_*`, `RelativeUri_CannotReachCwd`, `LocalTcpListener_ZeroConnections`, `SystemProperty_JavaProps_BlockedByAefLayerAlone`, `Transform_InnerStylesheet_StillSandboxed`, `SaxonCollationClass_Rejected`, `Collation_PercentEscape_CompileErrorNotCrash` |
| Listener / limitler | `DepthLimitTests.{Limit_IsSticky_AfterFirstTrip, TryCatch_CannotCatchOrResetLimit, Deadline_StopsTemplateLoop, ListenerIsPerTransform_NoThreadStatic}`; `SaxonLimitsTests.{Redos_FailsWithRegexLimit, Output_OverLimit_OutputLimit, InputXml_TooDeep_Xml, InputXml_TooManyNodes_Xml}` |
| Teşhis | `SaxonDiagnosticsTests.{Messages_NoJavaClassNamesOrPaths, Throwable_MapsToEngineInternal, SandboxViolation_InCauseChain_Security, Warnings_Filtered, CompileErrorList_LineColumn}`, `XslMessage_And_Trace_ProduceNoStderr` |
| Grid süreç içi | `InProcessXsltHostTests.{Timeout_ReturnsTimeout_CountsRunaway, RunawayMax_DegradesToWorker, RunawaysDrained_ReturnsInProcess, OutputLimit, InputDepthOver64_Xml, Busy_WhenSlotsFull, Warmup_LoadsSaxon}`, `EnvVar_Empty_InProcess`, `SystemProperty_Blocked_InProcess` |
| Katman / paket | `LayeringTests` (`NoSaxon10_InAnyCsproj` dahil), `IkvmFlowTests.{ApiOutput_ContainsSaxonAndIkvmHome, WorkerOutput_ContainsSaxonAndIkvmHome}` |
