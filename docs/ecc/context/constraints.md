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

// XSLT yükleme — XslCompiledTransform'u ASLA doğrudan Load etme; tek giriş noktası:
var transform = SecureXslt.Compile(xslt); // XsltCraft.Application.Xslt
```

`SecureXslt.Compile` = DTD yasak + `document()`/script kapalı + **stylesheet resolver `null`**.
`transform.Load(..., new XmlUrlResolver())` KULLANMA: `xsl:include/import href` ile yerel dosya
okuma (`file:///`, `C:/`, `/etc/`), SSRF (`http://169.254.169.254/`), UNC ile NTLM sızıntısı
(`\\sunucu\pay`) açar; stylesheet string'den yüklendiği için göreli href bile sunucunun çalışma
dizinine göre çözülür. Parametresiz `Load(reader)` da kullanılmaz. Saxon yolunda
`XmlResolver.ThrowingResolver` kullanılır (bkz. `XsltTemplateRenderer.RenderAsync`).
İçerik taraması: `XsltSafety.FindThreat` (tüm `xsl:import/include` + tehlikeli XPath fonksiyonları).

İlgili saldırı yüzeyleri: **XXE**, **XSLT/script injection**, **XPath injection** (kullanıcı XPath'i), **prompt injection** (AI asistanı), **SSRF**, **path traversal** (asset/storage), **file-upload** (extension+MIME+boyut), **IDOR** (asset/template sahipliği), JWT/refresh-token akışı.
- Üretilen XSLT çıktısı `msxsl:script` / harici `document()` **içermemeli**.
- **Sunucuda saklanan görseller** (asset, tema thumbnail'i): yalnız PNG/JPG/JPEG, `ImageUpload.Validate` (magic byte = uzantı, boyut başlıktan); MIME/uzantı içerikten türetilir — istemci `Content-Type`'ı saklanmaz/servis edilmez. `assets/{id}/serve` MIME'ı uzantıdan alır + `nosniff` + sandbox CSP. SVG yalnız tarayıcı-içi data URI logolarda (sunucuya yüklenmez).
- Request boyut limitleri: `PreviewRaw` ~1MB, `ValidateXslt` ~512KB, `AiAssistant` 8MB (görseller dahil; reverse proxy `client_max_body_size` hizalanmalı).
- **AI ekran görüntüsü:** yalnız PNG/JPEG — magic byte beyan MIME ile eşleşmeli, APNG/SVG/GIF/WebP red; boyut başlıktan okunur (decode yok), ≤2048 px uzun kenar, ≤4.2 MP, görsel ≤1.5 MB, toplam ≤4 MB, mesaj başına plan limiti (Free 1 / Pro 3). Görseller **kalıcı değildir ve loglanmaz**; kota kontrolünden önce doğrulanır (`AiVisionGate`). Görselli feedback global örnek yapılamaz.

## Mimari değişmezler
- `.xslt` ve görsel dosyalar DB'ye yazılmaz (storage + `storagePath`). Preview tamamen in-memory. AI sohbet görselleri hiçbir yere yazılmaz (yalnız istek ömrü).
- AI asistanı kullanıcı onayı olmadan otomatik insert yapmaz; insert öncesi `/api/preview/validate-xslt` ile sözdizimi kontrolü.
