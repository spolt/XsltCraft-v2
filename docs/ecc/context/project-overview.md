# Project Overview

**XsltCraft** — Türk e-Fatura (e-Fatura/e-Arşiv) ve e-İrsaliye için web tabanlı, **low-code XSLT şablon tasarımcısı**. Hedef: XSLT bilmeyen kullanıcının UBL-TR 2.1 XML belgelerine görsel olarak baskı şablonu tasarlayıp production-hazır `.xslt` üretmesi.

## Kim kullanıyor
- Muhasebe / e-dönüşüm operasyonu yapan, XSLT'ye hâkim olmayan kullanıcılar (birincil).
- XSLT'yi elle düzenlemek isteyen geliştiriciler (dev mode araçları).
- Admin: tema/snippet kütüphanesi küratörlüğü, kullanıcı/rol yönetimi, AI flag'leri.

## Temel akış
1. Kullanıcı A4 grid canvas'a blok yerleştirir (Table, ForEach, Conditional, Image, Text, DocumentInfo, LegalMonetaryTotal, QR/ETTN, GİB karekod, Supplier/Customer party…).
2. XML ağacı gezgininden node seçip blok alanına **XPath bağlar**.
3. Canlı önizleme HTML'i günceller (grid: süreç içi korumalı .NET motoru; ham XSLT: 1.0 → .NET, 2.0 → Saxon-HE 12.10, izole worker); UBL-TR iş kuralları (40+) doğrulanır.
4. Blok ağacı `version="2.0"` stylesheet'e derlenir (içerik iki motorda aynı sonucu veren XPath 1.0 altkümesi); görseller **her zaman** base64 gömülür, çıktıda açıklama yorumu olmaz → `.xslt` indirilir (QR kütüphanesi CDN'den yüklenir).

## Ürün hedefi
"XSLT yazmadan, UBL-TR'ye uygun, namespace'i bozulmayan, GİB'e gönderilebilir baskı şablonu." AI asistanı (opsiyonel) seçimi refactor eder ve Türkçe sohbet eder — ama **insan onayı olmadan otomatik insert yapmaz**.

## Kaynak
Detaylı ürün tanımı: kök `README.md` ve `XsltCraft_PRD_v2.md`. Bu özet README'den türetilmiştir; çelişki olursa **kod + README esastır** (eski `HANDOFF.md` değil).
