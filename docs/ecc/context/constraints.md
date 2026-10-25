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
- Çıktı **UTF-8**. Makul boyut hedefi (~250KB); gereksiz şişirme yapma.
- **Görseller her zaman base64 gömülür** (`PreviewAssetEmbedder`; URL/`/serve` yedeği yok — yetkisiz/silinmiş görsel → boş kutu). Yetki: kendi asset'i ∨ Editor/Admin ∨ erişilebilir yayınlanmış temanın BlockTree'sindeki birebir id (tema sahibi = asset sahibi ya da sahipsiz).
- **Üretilen çıktıya açıklama yorumu yazılmaz** (CSS, section, blok yorumları dahil). İzinli işaretçiler yalnız `DEV-MODE-OVERRIDES`, `xc:fixed-note` ve metinsiz `xsl:comment`. QR kütüphanesi CDN'den (`cdnjs`) yüklenir (sahip kararı).
- QR / ETTN (UUID) blokları üretilen XSLT'de bozulmadan kalmalı.

## Güvenlik — her XML/XSLT okuma noktasında zorunlu guard
Yeni eklenen her parse noktasında **bu pattern kullanılır** (mevcut hardening, bkz. `HANDOFF.md` + `decisions/001`):

```csharp
// XML okuma
var settings = new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null };
using var reader = XmlReader.Create(new StringReader(content), settings);

// XSLT 1.0 (.NET) — XslCompiledTransform'u ASLA doğrudan Load etme; tek giriş noktası:
var transform = SecureXslt.Compile(xslt); // XsltCraft.Application.Xslt

// XSLT 2.0 (Saxon 12.x) — Processor'ı ASLA elle kurma; sandbox'ın tek kaynağı SaxonProcessorFactory:
var engine = new SaxonXsltEngine(SaxonEngineOptions.Isolated); // XsltCraft.Xslt.Saxon (yalnız worker / süreç içi grid host'u)
```

`SecureXslt.Compile` = DTD yasak + `document()`/script kapalı + **stylesheet resolver `null`**.
`transform.Load(..., new XmlUrlResolver())` KULLANMA: `xsl:include/import href` ile yerel dosya
okuma (`file:///`, `C:/`, `/etc/`), SSRF (`http://169.254.169.254/`), UNC ile NTLM sızıntısı
(`\\sunucu\pay`) açar; stylesheet string'den yüklendiği için göreli href bile sunucunun çalışma
dizinine göre çözülür. Parametresiz `Load(reader)` da kullanılmaz.
İçerik taraması: `XsltSafety.Scan(xslt, Execute)` çalıştırmadan önce, `FindThreat` (Store) saklamadan önce.

**Saxon deseni (12.x, `SaxonProcessorFactory`):** `ALLOWED_PROTOCOLS="xsltcraft-none"` (**`""` = hepsine izin**); `ResourceResolver`/`UnparsedTextURIResolver`/`CollectionFinder`/`ResultDocumentHandler` **fırlatır** (`null` dönmek varsayılana düşer); boş env resolver; `ALLOW_EXTERNAL_FUNCTIONS=false`, `DISABLE_XSL_EVALUATE`, `XPATH_VERSION_FOR_XSLT=20`, `REGEX_BACKTRACKING_LIMIT`; `RestrictedCollationResolver` (AEF `?class=` collation sınıf yüklemesini engellemez); stylesheet derlemeden önce .NET ile ön okunur (`StylesheetSource.PreRead`: DOCTYPE, derinlik, `use-when`/gölge/`static` reddi — Saxon'un `disallow-doctype-decl`'ı stylesheet'e uygulanmaz). `FEATURE_SECURE_PROCESSING` sandbox katmanı değildir. Ayrıntı: ADR 008 §6.

**Worker değişmezleri (ADR 008):** kullanıcı XSLT'si (Raw, Validate, UserTemplate) Api sürecinde çalışmaz (tek istisna yerel `XsltWorker:Enabled=false`: yalnız 1.0, .NET); worker Infrastructure'a referans vermez, appsettings okumaz, ortamı beyaz listeyle temizlenir (secret geçmez), CWD boş dizin; stdout yalnız protokol; `--test-ops` havuzdan geçmez. Worker DoS sınırıdır, gizlilik sınırı değil (aynı uid) — gizliliği sandbox sağlar.

İlgili saldırı yüzeyleri: **XXE**, **XSLT/script injection**, **XPath injection** (kullanıcı XPath'i), **prompt injection** (AI asistanı), **SSRF**, **path traversal** (asset/storage), **file-upload** (extension+MIME+boyut), **IDOR** (asset/template sahipliği), JWT/refresh-token akışı.
- Üretilen XSLT çıktısı `msxsl:script` / harici `document()` **içermemeli**.
- **Sunucuda saklanan görseller** (asset, tema thumbnail'i): yalnız PNG/JPG/JPEG, `ImageUpload.Validate` (magic byte = uzantı, boyut başlıktan); MIME/uzantı içerikten türetilir — istemci `Content-Type`'ı saklanmaz/servis edilmez. `assets/{id}/serve` MIME'ı uzantıdan alır + `nosniff` + sandbox CSP. SVG yalnız tarayıcı-içi data URI logolarda (sunucuya yüklenmez).
- Request/içerik sınırları (kod-gerçeği; reverse proxy `client_max_body_size` hizalanmalı):

| Uç | HTTP gövde | İçerik (`Xslt:Limits`) |
|---|---|---|
| `preview/raw` | 12 MB | XSLT 5M kr / 5 MiB + XML 5 MiB, 10 sn |
| `preview/user-template/{id}` | 12 MB | XSLT 2 MiB + XML 5 MiB, 8 sn |
| `preview` (grid) | 10 MB | XSLT 6 MiB (base64), XML 5 MiB / derinlik 64 / 200k düğüm, 8 sn |
| `preview/theme/{id}` | 6 MB | grid sınırları |
| `preview/validate-xslt` | 1 MB | XSLT 1 MiB, 5 sn |
| `ai/assistant` | 8 MB | görseller dahil |

  Çıktı ≤ 8M karakter, stylesheet derinliği ≤ 256. Grid bağlama XPath'i: uzunluk ≤ 512, parantez ≤ 16, iç içe yüklem ≤ 3 + maliyet kuralları (`XPathPortability`, ADR 007 §5).
- **AI ekran görüntüsü:** yalnız PNG/JPEG — magic byte beyan MIME ile eşleşmeli, APNG/SVG/GIF/WebP red; boyut başlıktan okunur (decode yok), ≤2048 px uzun kenar, ≤4.2 MP, görsel ≤1.5 MB, toplam ≤4 MB, mesaj başına plan limiti (Free 1 / Pro 3). Görseller **kalıcı değildir ve loglanmaz**; kota kontrolünden önce doğrulanır (`AiVisionGate`). Görselli feedback global örnek yapılamaz.

## Mimari değişmezler
- `.xslt` ve görsel dosyalar DB'ye yazılmaz (storage + `storagePath`). Preview tamamen in-memory. AI sohbet görselleri hiçbir yere yazılmaz (yalnız istek ömrü).
- AI asistanı kullanıcı onayı olmadan otomatik insert yapmaz; insert öncesi `/api/preview/validate-xslt` ile sözdizimi kontrolü.
