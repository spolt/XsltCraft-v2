# Constraints

Bu kısıtlar XsltCraft'a özeldir; agent/eval'ler bunları **ihlal eden değişikliği reddetmeli**.

## UBL-TR 2.1 / XSLT
- **Namespace bütünlüğü kutsaldır.** Üretilen stylesheet şu prefix'leri korumalı:
  - `xsl` = `http://www.w3.org/1999/XSL/Transform`
  - `n1`  = `urn:oasis:names:specification:ubl:schema:xsd:Invoice-2`
  - `cbc` = `urn:oasis:names:specification:ubl:schema:xsd:CommonBasicComponents-2`
  - `cac` = `urn:oasis:names:specification:ubl:schema:xsd:CommonAggregateComponents-2`
  - `ext` = `urn:oasis:names:specification:ubl:schema:xsd:CommonExtensionComponents-2`
  - `exclude-result-prefixes="n1 cbc cac ext"`, `version="2.0"`.
- Bir prefix'i silmek, URI'sini değiştirmek veya `version`'ı düşürmek **regresyondur**.
- `xsl:for-each` / `xsl:variable` / `xsl:template` yapısını "kısaltma" amacıyla kırmak yasak — loop ve binding bütünlüğü korunmalı.

## GİB
- Çıktı **UTF-8**. Makul boyut hedefi (~250KB); görseller base64 gömülür ama gereksiz şişirme yapma.
- QR / ETTN (UUID) blokları üretilen XSLT'de bozulmadan kalmalı.

## Güvenlik — her XML/XSLT okuma noktasında zorunlu guard
Yeni eklenen her parse noktasında **bu pattern kullanılır** (mevcut hardening, bkz. `HANDOFF.md` + `decisions/001`):

```csharp
// XML okuma
var settings = new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null };
using var reader = XmlReader.Create(new StringReader(content), settings);

// XSLT yükleme
var xsltSettings = new XsltSettings(enableDocumentFunction: false, enableScript: false);
transform.Load(reader, xsltSettings, new XmlUrlResolver());
```

İlgili saldırı yüzeyleri: **XXE**, **XSLT/script injection**, **XPath injection** (kullanıcı XPath'i), **prompt injection** (AI asistanı), **SSRF**, **path traversal** (asset/storage), **file-upload** (extension+MIME+boyut), **IDOR** (asset/template sahipliği), JWT/refresh-token akışı.
- Üretilen XSLT çıktısı `msxsl:script` / harici `document()` **içermemeli**.
- Request boyut limitleri: `PreviewRaw` ~1MB, `ValidateXslt` ~512KB.

## Mimari değişmezler
- `.xslt` ve görsel dosyalar DB'ye yazılmaz (storage + `storagePath`). Preview tamamen in-memory.
- AI asistanı kullanıcı onayı olmadan otomatik insert yapmaz; insert öncesi `/api/preview/validate-xslt` ile sözdizimi kontrolü.
