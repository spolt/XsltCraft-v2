# Eval Case 009 — Saxon sandbox (K3) ve worker izolasyonu (K4)

Bu case **yeni test eklemez**; aşağıdaki mevcut (bloke eden) testler case'i kanıtlar. Tasarım: ADR 008
(`docs/ecc/decisions/008-xslt-worker-isolation.md` §6).

## Değişmezler
1. **K3 — veri sızıntısı yok:** `XsltSafety` taraması OLMADAN bile üretim Saxon yapılandırması harici kaynak
   (`doc`/`document`/`unparsed-text`/`collection`/`xsl:import`/`include`/`result-document`/`xi:include`/`transform()` iç
   stylesheet), ortam/sistem özelliği, dinamik kod (`xsl:evaluate`, AEF, reflexive), DTD/entity vektörlerini
   **belirli** kind + kod (ya da birebir boş çıktı) ile engeller; secret işaretçisi çıktıda/mesajda yok, loopback
   dinleyicilere bağlantı yok, dosya yazılmaz.
2. **Katman bağımsızlığı:** her sandbox katmanı tek başına çıkarıldığında yalnız o katmanın tek koruma olduğu vektörler sızar.
3. **K4 — süreç izolasyonu:** özyineleme/ReDoS/CPU döngüsü/bellek ikiye katlama/derin iç içe derleme vektörleri worker'ı
   düşürebilir ama Api süreci ayakta kalır ve havuz sonraki isteğe cevap verir; paralel istekler birbirine sızmaz.

## Kanıtlayan testler
- `backend/XsltCraft.Xslt.Saxon.Tests/Sandbox/FullSandboxMatrixTests.cs` — `FullSandbox_BlocksVector` (Isolated) +
  `FullSandbox_BlocksVector_GridProfile` (InProcessGrid) her `K3Vectors.All` vektörü için; `LocalTcpListener_ZeroConnections`,
  `Transform_InnerStylesheet_StillSandboxed`, `XslEvaluate_*`, `StaticExpression_RejectedBeforeSaxon_OnSmallStack`,
  `XslMessage_And_Trace_ProduceNoOutputLeak`, `LayeredSandbox_Full_MatchesProductionFactory`.
- `backend/XsltCraft.Xslt.Saxon.Tests/Sandbox/K3Vectors.cs` — vektör matrisi (tek kaynak).
- `backend/XsltCraft.Xslt.Saxon.Tests/Sandbox/FullMinusLayerTests.cs` — "Full eksi X" (`NoSandbox_Control_*`,
  `FullMinusResolvers_*`, `FullMinusProtocols_*`, `FullMinusRuntimeIo_*`, `FullMinusDynamic_*`, `FullMinusNoDtd_*`,
  `FullMinusXPath20_*`, `RelativeUri_*`).
- `backend/XsltCraft.Xslt.Saxon.Tests/SaxonSandboxTests.cs` — yapılandırma + tek katman kanıtları (SR1/SR3/SR4/SR5).
- `backend/XsltCraft.XsltWorker.Tests/Isolation/WorkerIsolationTests.cs` (`WorkerIntegration`) + `IsolationVectors.cs` —
  `*_PoolSurvives`, `CpuLoop_KilledByTimeout`, `MemoryDoubling_KilledByMemoryLimit`, `ParallelRenders_DistinctInputs_Isolated`.

## Çalıştırma
```bash
cd backend
dotnet test XsltCraft.Xslt.Saxon.Tests
dotnet test XsltCraft.XsltWorker.Tests --filter "FullyQualifiedName~Isolation"
```
