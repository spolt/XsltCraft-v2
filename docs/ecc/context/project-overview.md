# Project Overview

**XsltCraft** — Türk e-Fatura (e-Fatura/e-Arşiv) ve e-İrsaliye için web tabanlı, **low-code XSLT şablon tasarımcısı**. Hedef: XSLT bilmeyen kullanıcının UBL-TR 2.1 XML belgelerine görsel olarak baskı şablonu tasarlayıp production-hazır `.xslt` üretmesi.

## Kim kullanıyor
- Muhasebe / e-dönüşüm operasyonu yapan, XSLT'ye hâkim olmayan kullanıcılar (birincil).
- XSLT'yi elle düzenlemek isteyen geliştiriciler (dev mode araçları).
- Admin: tema/snippet kütüphanesi küratörlüğü, kullanıcı/rol yönetimi, AI flag'leri.

## Temel akış
1. Kullanıcı A4 grid canvas'a blok yerleştirir (Table, ForEach, Conditional, Image, Text, DocumentInfo, LegalMonetaryTotal, QR/ETTN, GİB karekod, Supplier/Customer party…).
2. XML ağacı gezgininden node seçip blok alanına **XPath bağlar**.
3. Canlı önizleme (Saxon HE) HTML'i günceller; UBL-TR iş kuralları (40+) doğrulanır.
4. Blok ağacı **XSLT 2.0**'a derlenir; görseller base64 gömülür → kendi kendine yeten `.xslt` indirilir.

## Ürün hedefi
"XSLT yazmadan, UBL-TR'ye uygun, namespace'i bozulmayan, GİB'e gönderilebilir baskı şablonu." AI asistanı (opsiyonel) seçimi refactor eder ve Türkçe sohbet eder — ama **insan onayı olmadan otomatik insert yapmaz**.

## Kaynak
Detaylı ürün tanımı: kök `README.md` ve `XsltCraft_PRD_v2.md`. Bu özet README'den türetilmiştir; çelişki olursa **kod + README esastır** (eski `HANDOFF.md` değil).
