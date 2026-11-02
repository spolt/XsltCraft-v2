# Eval Case 011 — XPath taşınabilirliği / maliyet kapısı (G3) ve blok düzeyinde yumuşak düşüş

## Bağlam
Grid bağlamaları iki motorda (.NET XSLT 1.0 / Saxon 2.0) aynı anlamı vermeli ve DoS üretmemeli. `XPathPortability.Analyze`
taşınamaz ifadeyi `XPATH_NOT_PORTABLE`, kuadratik/pahalı ifadeyi `XPATH_TOO_COMPLEX` ile işaretler. Generator şablonu
reddetmez: yalnız o blok görünür bir yer tutucuyla değişir ve `Warnings`'e `(blockId, code, xpath)` eklenir.

## Girdi
Grid (v2) ağacı: 4 `Text` bloğu — `//cbc:ID`, `//*[..//zz]` (yukarı adım sonrası `//`), `count(//i[@v = //i/@w])`
(iki geniş küme karşılaştırması), satıcı adı.

## Korunması gereken değişmezler
1. `error == null`; tam olarak iki uyarı: `ancestorScan` ve `quadratic` → `XPATH_TOO_COMPLEX`.
2. Kötü ifadeler stylesheet metnine **hiç** girmez; `version="2.0"` + UBL-TR namespace'leri korunur; çıktı sertleştirilmiş reader'la ayrışır.
3. Render: `xc-binding-error` yer tutucusu + kod mesajı; diğer bloklar (fatura no, satıcı adı) render edilir.

## Yasaklar
- Tek kötü bağlama yüzünden tüm şablonu reddetmek ya da kötü ifadeyi stylesheet'e gömmek.

## Test
- `backend/XsltCraft.Tests/Eval/XPathPortabilityEvalTests.cs` — `Case011_ComplexXPath_SoftFailsBlocks_RestOfTemplateRendered` (`Category=Eval`).
- Birim kapsamı (bloke eden): `backend/XsltCraft.Tests/XPathPortabilityTests.cs` — `Portable`, `QuadraticCost_Rejected`,
  `NotPortable_Rejected`, `DeepParens_RejectedBeforeCompile`, `DeepPredicates_Rejected`, `UnportableBinding_SoftFails_BlockOnly`,
  `UnportableBinding_ConditionalText_SoftFails`.
- Güvenlik kardeşi: case002 (harici kaynak fonksiyonları aynı kapıda `XPATH_NOT_PORTABLE` ile düşer).
