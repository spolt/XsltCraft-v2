# Eval Case 001 — Temsili UBL-TR fatura şablonu

## Girdi (block-tree)
Gerçekçi bir e-Fatura baskı şablonunu temsil eden çok bloklu V1 tree:
- `Heading` (statik) — "FATURA"
- `DocumentInfo` — Fatura No (`//cbc:ID`), Fatura Tarihi (`//cbc:IssueDate`)
- `Text` (bound) — satıcı adı (`//cac:AccountingSupplierParty/cac:Party/cac:PartyName/cbc:Name`)
- `Table` (loop) — `iterateOver = //cac:InvoiceLine`, kolonlar: sıra (`cbc:ID`), ürün (`cac:Item/cbc:Name`), miktar (`cbc:InvoicedQuantity`), tutar (`cbc:LineExtensionAmount`)
- `Totals` — ödenecek tutar (`//cac:LegalMonetaryTotal/cbc:PayableAmount`)

Tam girdi kod içinde tek doğru kaynaktır: `XsltEvalTests.BuildCase001Tree()`.

## Korunması gereken değişmezler (invariants)
1. `error == null`, çıktı boş değil ve **derlenebilir** (sertleştirilmiş `XmlReader` + `XslCompiledTransform`).
2. `version="2.0"` korunur.
3. UBL-TR namespace'leri korunur: `n1` (Invoice-2), `cbc` (CommonBasicComponents-2), `cac` (CommonAggregateComponents-2), `ext` (CommonExtensionComponents-2) ve `exclude-result-prefixes="n1 cbc cac ext"`.
4. **Loop bütünlüğü:** `//cac:InvoiceLine` üzerinde en az bir `xsl:for-each` üretilir; iterasyon kaldırılamaz.
5. **Binding bütünlüğü:** bound alanların XPath'leri çıktıda `xsl:value-of` olarak görünür (satıcı adı + tablo kolonları).

## Yasaklar (regresyon sayılır)
- Bir namespace prefix'ini silmek / URI'sini değiştirmek / `version`'ı düşürmek.
- `xsl:for-each` iterasyonunu veya `xsl:value-of` binding'ini "kısaltma" amacıyla kaldırmak.
- Çıktıya `msxsl:script` / `xmlns:msxsl` / harici `<script>` enjekte etmek.

## Golden
Çıktının yapısal projeksiyonu (`xsl:`/`xmlns:` satırları) `__snapshots__/XsltEvalTests.Case001_StructuralSnapshot.verified.txt`'e karşı doğrulanır.
