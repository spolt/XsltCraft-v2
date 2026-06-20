# Eval Case 002 — SSRF / harici kaynak fonksiyonları reddedilir

## Tehdit
Kullanıcı binding XPath'i üretilen XSLT'ye **verbatim** gömülür (`<xsl:value-of select="{xpath}"/>`). XSLT, `XsltTemplateRenderer.RenderAsync`'teki Saxon yoluyla render edilir; Saxon `document()`, `doc()`, `unparsed-text()`, `collection()` gibi fonksiyonları varsayılan olarak çalıştırır. Bu yüzden sıradan bir kimliği doğrulanmış kullanıcı:

1. binding XPath'i `document('http://169.254.169.254/latest/meta-data/...')` olan bir template oluşturur,
2. `GET /api/templates/{id}/download` ile XSLT'yi ürettirip depolatır,
3. `POST /api/render/{id}` ile Saxon'da çalıştırır → **SSRF / yerel dosya okuma** (`unparsed-text('file:///...')`).

## Korunma (fail-closed)
- **Generator:** `XsltGeneratorService.Validate` → `XsltSafety.FindThreat` ile üretilen XSLT'nin `select`/`test`/`match` attribute'larında yasak fonksiyon varsa üretimi reddeder (xslt null, error dolu).
- **Admin tema yükleme:** `AdminController.ValidateXsltFileAsync` ham `.xslt` içeriğini aynı taramadan geçirir (admin-trusted ama defense-in-depth).
- **Saxon motoru:** `XsltCompiler`/`DocumentBuilder` üzerinde `XmlResolver.ThrowingResolver` — harici `xsl:import/include` + giriş belgesi çözümlemesini reddeder.

## Test
- `XsltEvalTests.Case002_RejectsExternalResourceFunctions` (`[Trait("Category","Eval")]`) — `document()/doc()/unparsed-text()/collection()` binding'i reddedilir.
- `XsltEvalTests.Case002_AllowsBenignXPath` — meşru `//cbc:Note` engellenmemeli (regresyon).
- `XsltSafetyTests` — helper'ın doğrudan birim testleri (yorum/metin yanlış-pozitif üretmez).
