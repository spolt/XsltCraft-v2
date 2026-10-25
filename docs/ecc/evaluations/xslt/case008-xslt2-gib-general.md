# Eval Case 008 — XSLT 2.0 GİB `general.xslt` Raw rotada (Saxon) render edilir

## Bağlam
Kullanıcının asıl sorunu: GİB'in resmî `general.xslt`'si .NET `XslCompiledTransform`'da derlenmiyordu
(`'exists()' is an unknown XSLT function`). XSLT 2.0 motoru (Saxon HE, izole worker — ADR 007/008) sonrası
Raw rota (`/api/preview/raw` → `XsltRoute.Raw`) bu stylesheet'i Saxon ile render etmeli.

## Girdi
- Stylesheet: `backend/XsltCraft.Application.Tests/ThemeSlots/Fixtures/gib-general.xslt` (değiştirilmemiş GİB şablonu).
- XML: `backend/XsltCraft.Application/ThemeSlots/Samples/invoice.xml` (fatura no `TST2026000000001`, ETTN `111d1111-0c11-01bb-123e-1f2f34b567f8`).
- Varyant (test içinde türetilir): `InvoiceTypeCode=IHRACKAYITLI`, `TaxExemptionReasonCode=702`, `KARSIBELGENO` belge
  referansı + bir `cbc:Note` içinde U+0096 (C1 kontrol karakteri).

## Korunması gereken değişmezler
1. Raw rota başarılı; motor `Saxon`, yer `Isolated` (gerçek worker havuzu).
2. Çıktıda fatura no ve ETTN.
3. `exists()` çalışma anında değerlendirilir: varyantta "Karşı Belge No:" dalı ve belge no çıktıda; örnek faturada yok.
4. `xsl:character-map name="a"` uygulanır: U+0080–U+009F çıktıdan silinir (`Semih\u0096Polat` → `SemihPolat`).

## Yasaklar (regresyon sayılır)
- Raw rotayı .NET motoruna düşürmek ya da 2.0 fonksiyonlarını "unknown function" ile reddetmek.
- GİB fixture'ını testi geçirmek için değiştirmek.

## Test
- `backend/XsltCraft.XsltWorker.Tests/Eval/GibGeneralEvalTests.cs` (`[Trait("Category","Eval")]`, `WorkerProcesses` koleksiyonu):
  - `Case008_GibGeneral_RawRoute_RendersOnSaxon_WithInvoiceNumberAndEttn`
  - `Case008_GibGeneral_ExistsBranchAndCharacterMap_EvaluatedAtRuntime`
- Bloke eden kardeş: `PreviewRawGibGeneralTests` (`Api/PreviewEndpointTests.cs`, `WorkerIntegration`) — controller uçtan uca 200.
