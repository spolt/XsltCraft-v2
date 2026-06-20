# Eval Case 001 — XPath konsolu (namespace-farkında değerlendirme)

## Hedef
`IXPathEvaluator` (`XPathEvaluator`) — XPath konsolu (`/api/xpath/evaluate`) ve binding çözümlemesinin temeli. Daha önce testi yoktu; bu eval regresyon ağı kurar.

## Girdi
Namespace-doğru minimal UBL-TR fatura (`n1`/`cbc`/`cac` kökte tanımlı) — bkz. `XPathEvalTests.UblInvoice`.

## Korunan değişmezler
1. `/n1:Invoice/cbc:ID` → tek node, değer `FAT2026000001`, ad `cbc:ID` (prefix korunur).
2. `//cac:AccountingSupplierParty/cac:Party/cac:PartyName/cbc:Name` → `ACME Ltd` (derin binding).
3. `count(//cac:InvoiceLine)` → atomic `"2"` (skaler fonksiyon).
4. Eşleşmeyen geçerli ifade → `Kind="empty"`, boş `Items`.
5. **Geçersiz ifade exception fırlatmaz** → `Kind="error"`, `Error` dolu (kullanıcıya güvenli sonuç).

## Yasaklar (regresyon)
- Namespace çözümlemesini bozmak (prefix kaybı / yanlış URI eşleme).
- Geçersiz XPath'te exception sızdırmak (500 yerine "error" dönmeli).

## Test
`XsltCraft.Tests/Eval/XPathEvalTests.cs` — `[Trait("Category","Eval")]`. Lokal: `dotnet test --filter "Category=Eval"`.
