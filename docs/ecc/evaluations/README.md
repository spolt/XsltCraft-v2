# XSLT Eval Harness

XSLT generator (en yüksek riskli alan) için **regresyon ağı**. Her case: bir block-tree girdisi + "korunması gereken" değişmezler (namespace / `version` / format / loop & binding bütünlüğü / güvenlik). AI veya generator değişiklikleri bu değişmezleri kırarsa eval **kırmızıya** döner.

## Yapı
- **Case tanımları (insan-okur):** `docs/ecc/evaluations/xslt/caseNNN.md` — girdi özeti + korunma gereksinimleri + yasaklar.
- **Çalıştırılabilir harness:** `backend/XsltCraft.Tests/Eval/XsltEvalTests.cs` — `[Trait("Category","Eval")]`. Her case için:
  1. `IXsltGeneratorService.Generate` çalıştır.
  2. **Invariant assert** — namespace'ler, `version="2.0"`, beklenen `xsl:for-each`/binding düşmemiş, `msxsl:script` yok; çıktı sertleştirilmiş `XmlReader` (`DtdProcessing.Prohibit`, `XmlResolver=null`) + `XslCompiledTransform` ile derlenir.
  3. **Golden snapshot (Verify)** — çıktının *yapısal projeksiyonu* (`xsl:`/`xmlns:` satırları; CSS hariç) `__snapshots__/*.verified.txt`'e karşı doğrulanır. CSS gürültüsüne dayanıklıdır.

## Çalıştırma
```bash
cd backend
dotnet test --filter "Category=Eval"
```
Golden ilk koşuda `.received.txt` üretir; gözden geçirip `.verified.txt`'e dönüştür (commit'le). Yapısal projeksiyon **bilinçli** değiştiğinde snapshot güncellenir.

## CI
`ci.yml` ana backend job'ı `--filter "Category!=Eval"` ile koşar (eval bloklamaz). Ayrı `eval` job'ı `Category=Eval`'i `continue-on-error: true` ile koşar (uyarı). Olgunlaşınca `continue-on-error` kaldırılıp blocking'e geçilir.

## Genişletme (ileride)
`xslt/` dışında `preview/`, `prompt/` (AI pattern pack), `xpath/` case'leri eklenebilir — bkz. `docs/ecc/context/roadmap.md`.
