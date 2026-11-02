# Eval Case 010 — Grid motor paritesi (.NET ↔ Saxon)

Bu case **yeni test eklemez**; mevcut bloke eden parite testleri case'i kanıtlar. Tasarım: ADR 007 A5
(`docs/ecc/decisions/007-xslt-engine-routing.md`) — `Grid=Auto`'nun ön koşulu.

## Değişmezler
1. Her grid golden vakası (`backend/XsltCraft.Tests/__snapshots__/GridV2/Html.{fixture}.{xml}.verified.txt`; vaka listesi
   snapshot dosyalarından türetilir) önizlemenin gerçek yolundan (`GenerateV2` → `XsltRoute.Grid` → süreç içi host)
   **hem .NET hem Saxon** ile render edilir; normalize HTML iki motorda eşit **ve** .NET golden'ına eşit.
2. `#qrvalue` (GİB karekod yükü) normalize edilmeden **bayt-aynı**.
3. Kayıtlı şablon önizlemesi (`XsltRoute.UserTemplate`, gerçek worker, Saxon) canlı grid önizlemesiyle aynı normalize HTML'i verir.

## Kanıtlayan testler
- `backend/XsltCraft.XsltWorker.Tests/Grid/GridEngineParityTests.cs` —
  `GridRender_DotNetAndSaxon_NormalizedHtmlEqualAndMatchesGolden`, `GridRender_DotNetAndSaxon_QrValueByteIdentical`,
  `Cases_CoverRequiredXmls_AndQrFixtures` (yardımcılar: `GridParityKit.cs`).
- `backend/XsltCraft.XsltWorker.Tests/Grid/UserTemplateWorkerParityTests.cs` (`WorkerIntegration`) —
  `UserTemplate_Worker_Equals_Grid_InProcess`.

## Çalıştırma
```bash
cd backend
dotnet test XsltCraft.XsltWorker.Tests --filter "FullyQualifiedName~Grid"
```
