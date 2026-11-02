---
name: xss-xxe-checklist
description: XsltCraft güvenlik inceleme checklist'i — XXE, XSLT/script injection, XPath/fonksiyon injection (document/doc/unparsed-text → SSRF/dosya okuma), SSRF, path traversal, file-upload, IDOR, JWT/refresh, prompt injection. XML/XSLT işleyen, dosya yükleyen, auth/asset/storage veya AI girdisine dokunan değişikliklerde kullan. security-reviewer agent'ı bu checklist'i uygular.
---

# Güvenlik Checklist — XXE / Injection / SSRF (XsltCraft)

> Bağlam: `docs/ecc/context/constraints.md` (guard pattern) + `HANDOFF.md` hardening tablosu.
> Bu uygulama **kullanıcı XML/XSLT'si işler ve AI'a kullanıcı metni gönderir** → saldırı yüzeyi yüksek. Değişen kodu al (`git diff`) ve aşağıdaki maddeleri yeni parse/upload/auth/AI noktalarında uygula.

## Zorunlu guard pattern (her yeni parse noktası)
```csharp
// XML okuma
var settings = new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null };
using var reader = XmlReader.Create(new StringReader(content), settings);
// XSLT yükleme (.NET) — tek giriş noktası; XslCompiledTransform'u doğrudan Load etme
var transform = SecureXslt.Compile(xslt); // DTD yasak + document()/script kapalı + resolver null
```
> **Bulgu say:** `transform.Load(..., new XmlUrlResolver())` veya parametresiz `Load(reader)` →
> `xsl:include/import href` ile yerel dosya okuma / SSRF / UNC-NTLM sızıntısı (göreli href bile
> çalışma dizinine göre çözülür). `rg "new XmlUrlResolver|\.Load\(reader\)" backend` → 0 olmalı.
> .NET 10'da `XmlReader`/`XDocument` varsayılanı zaten güvenli (`DtdProcessing.Prohibit`) ama **açık yaz** — konvansiyon + framework değişimine karşı.

## 1. XXE
- [ ] Her `XmlReader.Create` / `XmlDocument.Load` / `XDocument.Parse` → `DtdProcessing.Prohibit` + `XmlResolver=null`.
- [ ] Özellikle **kullanıcı XML'i** parse eden yerlerde açık guard (örn. `UblTrBusinessRuleService`, `LimitedXmlReader`, controller'lar).

## 2. XSLT / script & dış fonksiyon injection (SSRF / dosya okuma)
- [ ] **.NET yolu:** `transform.Load` → `XsltSettings(enableDocumentFunction:false, enableScript:false)`.
- [ ] **Saxon yolu (12.x, ADR 008 §6):** `Processor` yalnız `SaxonProcessorFactory`'den (yeni `new Processor(...)` = bulgu). Saxon `doc()`/`unparsed-text()`/`collection` varsayılan AÇIK → SSRF/`file://` okuma, bu yüzden:
  - `ResourceResolver` (config + compiler + transformer), `UnparsedTextURIResolver`, `CollectionFinder`, `ResultDocumentHandler` **fırlatmalı** — `null` dönmek Saxon'un varsayılan çözümleyicisine düşer.
  - `ALLOWED_PROTOCOLS` geçersiz token (`"xsltcraft-none"`); **tuzak:** `""` = tüm protokollere izin.
  - `ALLOW_EXTERNAL_FUNCTIONS=false` (`system-property('java.*')` için tek katman), `DISABLE_XSL_EVALUATE=true`, boş env resolver, `XPATH_VERSION_FOR_XSLT=20`, `REGEX_BACKTRACKING_LIMIT`. `FEATURE_SECURE_PROCESSING`'e güvenme (12.10'da yalnız AEF'yi kapatır).
  - Collation: `RestrictedCollationResolver` şart — `saxon.sf.net/collation?class=X` AEF=false iken de sınıf yükler; dinamik URI (`concat(...)`) statik taramada görünmez.
  - Stylesheet derlemeden önce .NET ile ön okunmalı (`StylesheetSource.PreRead`: DOCTYPE/iç entity, derinlik, `use-when`/gölge `_*`/`static` reddi) — Saxon `disallow-doctype-decl` stylesheet'e uygulanmaz; girdi XML `XmlReaderTreeBridge` ile.
  - Kullanıcı XSLT'si (Raw/Validate/UserTemplate) yalnız `IXsltExecutionService` → izole worker; süreç içine (Grid/Theme) kullanıcı XSLT'si sızdıran yeni yol = bulgu. `transform()` için tek kapı `XsltSafety`.
- [ ] **Kullanıcı XPath'i stylesheet'e gömülüyorsa** `XsltSafety` ile fail-closed tara (çalıştırma: `Scan(..., Execute)`, saklama: `FindThreat`) ve grid bağlamasında `XPathPortability` maliyet kapısından geçir (`document`/`doc`/`unparsed-text`/`collection`/`system-property` + harici `xsl:import/include`). Generator ve admin tema yükleme bunu kullanır — yeni gömme noktası eklerken atlama.
- [ ] Üretilen/kabul edilen XSLT çıktısında `msxsl:script` / `xmlns:msxsl` yok.

## 3. XPath injection
- [ ] Kullanıcı XPath'i string birleştirmeyle sorguya gömülüyorsa doğrulama/escape var mı? `XmlAttr` yalnız XML-escape yapar — **anlamsal güvenlik sağlamaz** (bkz. `XsltSafety`).

## 4. SSRF
- [ ] Kullanıcı kaynaklı URL ile sunucu-taraflı istek (Ollama/Gemini base URL, asset fetch, `document()`) → allowlist / resolver kısıtı.

## 5. Path traversal
- [ ] Asset/storage yolunda `..`, mutlak yol, normalize eksikliği. `IStorageService` sınırında doğrula.

## 6. File-upload
- [ ] Extension **+** MIME **+** boyut limiti birlikte (örn. tema `.xslt` 2MB, thumbnail 1MB).
- [ ] Yüklenen `.xslt` içeriği `XsltSafety.FindThreat`'ten geçiyor.

## 7. IDOR / authorization
- [ ] Endpoint `[Authorize]` mı? Kaynak **sahipliği** doğrulanıyor mu?
      Desen: `!template.IsFreeTheme && template.OwnerId != userId && role != "Admin" → Forbid()`.
- [ ] Free theme (public) ile private kullanıcı template'i ayrımı doğru — public olanı bilerek serbest, private olanı sahibe/admine kısıtlı.
- [ ] Inactive kullanıcı login/refresh'te bloklanıyor.

## 8. JWT / refresh-token
- [ ] Refresh rotation, HttpOnly cookie, süre kontrolü; token sızıntısı yok.

## 9. Prompt injection (AI)
- [ ] Kullanıcı metni sistem talimatıyla karışmıyor; AI çıktısı **kullanıcı onayı olmadan** insert/exec edilmiyor; insert öncesi `/api/preview/validate-xslt`.

## 10. Bilgi sızıntısı
- [ ] Prod'da ham `ex.Message` dönülmüyor (generic mesaj + `LogError`).

## Çıktı
Bulgular **[Blocker]/[Yüksek]/[Orta]/[Düşük]** + `dosya:satır` + somut sömürü senaryosu + düzeltme. Yanlış-pozitifte mütevazı ol (önce sömürülebilirliği doğrula — örn. veri akışını izle) ama guard eksikliğini görmezden gelme.
