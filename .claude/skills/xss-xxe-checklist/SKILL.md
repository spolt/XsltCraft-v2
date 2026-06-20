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
// XSLT yükleme (.NET)
var xsltSettings = new XsltSettings(enableDocumentFunction: false, enableScript: false);
transform.Load(reader, xsltSettings, new XmlUrlResolver());
```
> .NET 10'da `XmlReader`/`XDocument` varsayılanı zaten güvenli (`DtdProcessing.Prohibit`) ama **açık yaz** — konvansiyon + framework değişimine karşı.

## 1. XXE
- [ ] Her `XmlReader.Create` / `XmlDocument.Load` / `XDocument.Parse` → `DtdProcessing.Prohibit` + `XmlResolver=null`.
- [ ] Özellikle **kullanıcı XML'i** parse eden yerlerde açık guard (örn. `UblTrBusinessRuleService`, `RenderService`, controller'lar).

## 2. XSLT / script & dış fonksiyon injection (SSRF / dosya okuma)
- [ ] **.NET yolu:** `transform.Load` → `XsltSettings(enableDocumentFunction:false, enableScript:false)`.
- [ ] **Saxon yolu:** `XsltCompiler.XmlResolver` ve `DocumentBuilder.XmlResolver` = `XmlResolver.ThrowingResolver` (Saxon `document()`/`doc()`/`unparsed-text()` varsayılan AÇIK → SSRF/`file://` okuma).
- [ ] **Kullanıcı XPath'i stylesheet'e gömülüyorsa** `XsltSafety.FindThreat` ile fail-closed tara (`document`/`doc`/`unparsed-text`/`collection`/`system-property` + harici `xsl:import/include`). Generator ve admin tema yükleme bunu kullanır — yeni gömme noktası eklerken atlama.
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
