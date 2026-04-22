# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

---

## [0.6.0] - 2026-04-22

### Added
- **Blok kenar boşlukları**: `GridBlockLayout`'a `marginTop`, `marginBottom`, `marginLeft`, `marginRight` (mm) alanları eklendi; PropertyPanel'de "Kenar Boşlukları" bölümünden düzenlenebilir. Değerler önizleme çıktısında `padding + box-sizing:border-box` olarak uygulanır — canvas konumunu ve sweep algoritmasını etkilemez.
- **Z-index overlay katmanı**: `zIndex > 0` olan bloklar sweep akışından ayrılır; `.page` üzerine `position:absolute` ile render edilir. Birden fazla overlay bloğu CSS `z-index` sırasıyla üst üste binebilir; canvas konumlarıyla birebir örtüşür.

### Fixed
- **Divider/Spacer band-spanning**: `Divider` ve `Spacer` tipleri genişliklerinden bağımsız olarak her zaman sweep band'ini kapatır. Geniş ama full-width eşiğini aşmayan Divider'ların (ör. 155 mm) sonraki dar blokları kendi kolonuna emmesi önlendi; yan yana dizilim doğru çalışıyor.
- **İlk blok kolon hizalaması**: Kolon içindeki ilk bloğun `margin-top` değeri band tepesinden (Y=0) değil, bloğun kendi Y koordinatından hesaplanıyor (`bgl.Y - bandTop`). Farklı Y'lerdeki bloklar artık canvas'taki konumlarıyla hizalanıyor.
- **Full-width blok sonrası yan yana dizilim**: Full-width bir bloktan (ör. Fatura Satırları) sonra gelen dar bloklar kendi band'lerinde yan yana yerleşebiliyor; önceki band sweep'i tarafından emilme sorunu giderildi.

---

## [0.5.1] - 2026-04-21

### Fixed
- **V2 grid-canvas preview taşma**: `editorStore.addBlock` ve `updateBlockGridLayout` artık `clampToPage` ile A4 sınırlarına kilitleniyor (drag / resize / PropertyPanel input tek noktadan).
- **V2 blok üst üste binme**: `XsltGeneratorService.BuildBodyV2` şerit (band) + sütun akış algoritmasıyla yeniden yazıldı. Bloklar Y'ye göre taranır, X-kesişimine göre sütunlara eklenir; birden çok sütunla kesişen geniş blok (ör. Fatura Satırları) yeni bir şerit başlatır. `autoHeight` blok büyüdüğünde altındaki bloklar ve sonraki şeritler doğal olarak aşağı kayar — üst üste binmez, WYSIWYG korunur.
- **.page CSS**: `position:relative` + `overflow:hidden` eklendi; A4 dışı taşmalar kırpılır.

### Removed
- `GroupIntoRows` / `RenderV2Row` ölü kodu (~80 satır) temizlendi.

---

## [0.5.0] - 2026-04-19

### Added
- **UBL-TR İş Kuralları Doğrulama**: 40+ UBL-TR 2.1 iş kuralı motoru (`UblTrBusinessRuleService`); zorunlu alan, tutar tutarlılığı ve vergi doğrulaması; ihlaller satır numarasıyla listeleniyor
- **Sekmeli Problemler Paneli**: XML ayrıştırma hataları, XSLT doğrulama hataları ve UBL-TR iş kuralı ihlalleri tek panelde; sekme başlıklarında badge sayacı
- **XPath Konsolu**: Monaco editörlü interaktif XPath 1.0 sorgu paneli; namespace-aware; sonuçlar node tipi, adı ve değeriyle gösteriliyor
- **XML Kaynak Sekmesi**: Editörde XSLT ↔ XML kaynak görünümü arasında hızlı geçiş
- **XSLT Profiler**: Transform süresi ölçümü, yavaş bölge tespiti ve performans önerileri
- **Snippet Kütüphanesi**: Kullanıcıya özel XSLT snippet oluşturma/düzenleme/silme; `Ctrl+Space` ile editörde autocomplete; tam CRUD API (`/api/user-snippets`)
- **Önizleme Zoom Kontrolü**: %50–%200 arası 7 kademe zoom (ZoomIn / ZoomOut / %100 reset); önizleme paneli başlığında kompakt kontrol çubuğu
- **Seçili Satırları Biçimlendirme**: Sağ tık menüsünden seçili satırları XML-fragment farkındalıklı biçimlendirme; geçersiz fragmentleri geçici root ile sarar
- **Klavye Kısayolları Diyaloğu**: Tüm editör kısayollarını listeleyen `ShortcutsDialog`; toolbar'dan erişilebilir
- **About Sayfası**: Uygulama versiyonu, teknoloji stack'i ve bağlantılar
- `UblTrController`: `POST /api/ubltr/validate` endpoint'i
- `XPathController`: `POST /api/xpath/evaluate` endpoint'i
- `UserSnippetsController`: Snippet CRUD endpoint'leri
- `XPathEvaluator`: Namespace-aware XPath 1.0 değerlendirici
- `AddUserSnippet` migration: `UserSnippets` tablosu
- `docker-compose.prod.yml`, `Dockerfile`'lar, `nginx.conf`, `update.sh` — production altyapısı
- `vite-env.d.ts`: Vite ortam değişkeni tip tanımları

### Fixed
- Pre-commit hook false-positive: `vite-env.d.ts` artık `.env` kalıbıyla eşleşmiyor
- Pre-push hook false-positive: `${POSTGRES_PASSWORD}` env var referansı artık secret olarak işaretlenmiyor
- `dotnet format` whitespace hataları: `XPathModels.cs`, `XPathEvaluator.cs`, `XsltGeneratorService.cs`

---

## [0.4.0] - 2026-04-16

### Added
- **V2 Grid Canvas**: Bloklar artık A4 sayfası üzerinde serbest X/Y koordinatlarıyla (mm) yerleştiriliyor; eski bölüm tabanlı (V1) editörün yerini aldı
- **Satır-gruplu akış render**: Önizleme XSLT'sinde `z-index=0` bloklar Y-aralığı örtüşme tabanlı satır gruplama algoritmasıyla `flex-row` olarak render ediliyor; `autoHeight` bloklar altındakileri aşağı itiyor
- **Overlay katmanı**: `z-index>0` olan bloklar `position:absolute` ile üst üste bindirilebilir; yüksek z-index öndedir
- **ResizeHandles**: Seçili bloğu 8 yönde pixel-perfect yeniden boyutlandırma
- **Klavye navigasyonu**: Seçili blok yön tuşlarıyla 1 mm, `Shift` ile 5 mm hareket ettirilebilir
- **Undo/Redo**: `Ctrl+Z` / `Ctrl+Y` ile 20 adım geri/ileri
- **Blok kopyalama**: `Ctrl+D` / `duplicateBlock` ile seçili blok +5 mm offset ile kopyalanır
- **Z-index kontrolü**: Property panel'den blok öne/arkaya gönderilir; negatif değer engellendi
- **PartyInfo bölünmesi**: Palette'te "Satıcı Bilgileri" ve "Alıcı Bilgileri" olarak iki ayrı hazır blok; `configOverride` pipeline ile önceden yapılandırılmış gelir
- **Türkçe blok etiketleri**: Canvas üzerindeki tüm blok adları Türkçeleştirildi
- **MinIO depolama**: Faz 6 — MinIO kurulumu ve secret yönetimi; dosya depolama S3-uyumlu MinIO'ya taşındı
- **XSLT editör iyileştirmeleri**: Auto-format, folding range provider ve UI geliştirmeleri
- **XML dosya boyutu doğrulaması**: Yüklenen XML dosyaları için boyut sınırı ve istek limiti artırıldı
- `gridSnap.ts`: `snapToGrid`, `clampToPage`, `pxToMm` yardımcı fonksiyonları
- `treeMigration.ts`: V1 (bölüm tabanlı) → V2 (grid tabanlı) otomatik migrasyon yardımcısı
- `canvasRefs.ts`: `canvasPageRef`, `canvasScaleRef`, `CANVAS_DROPPABLE_ID` paylaşılan modül ref'leri

### Changed
- Tüm blok tipleri için varsayılan `autoHeight: true`; yalnızca Divider, Spacer, GibKarekod, GibLogo sabit yükseklik
- Divider varsayılan yüksekliği 3 mm → 10 mm
- V2 sayfa stili: `height: auto; min-height: 297mm; overflow: hidden` — içerik uzadığında sayfa büyür, print'te doğal sayfalama
- Palette VERİ kategorisinden "KDV Özeti" ve "Toplamlar" kaldırıldı
- `addBlock` aksiyonu isteğe bağlı `configOverride` parametresi kabul ediyor

### Fixed
- Önizlemede auto-height blokların altındaki blokların üst üste binmesi giderildi
- Yön tuşlarının ilk hareketten sonra yanıt vermemesi (stale closure) `useRef` pattern ile düzeltildi
- Negatif `z-index` değerinin bloğu canvas'tan gizlemesi engellendi
- TypeScript build hataları: `config` cast, `ResizeHandles` çift `cursor`, kullanılmayan import
- V1 `SectionComponent.tsx` kaldırıldı (V2'de kullanılmıyor)
- `canvasRefs.ts` ayrı dosyaya alınarak ESLint `react-refresh/only-export-components` uyarısı giderildi

---

## [0.3.1] - 2026-03-29

### Fixed
- `SectionComponent.tsx` `toggleLayout` fonksiyonunda `width` property'sinin nesne spread ile çakışmasından kaynaklanan TypeScript derleme hatası giderildi (`TS2783: 'width' is specified more than once`)

---

## [0.3.0] - 2026-03-29

### Added
- **Toplu seçim/silme**: Şablonlarım ve Taslaklarım sayfalarında çoklu satır seçimi ve toplu silme işlevi eklendi
- **Çok sütunlu blok gruplama**: Editörde 3 sütunlu bölümlerde her bloğa sütun ataması yapılabiliyor (`col` property); aynı sütundaki bloklar dikey sıralanıyor
- **Sütunlar arası drag-drop**: `ColDropZone` bileşeni ile bloklar sürüklenerek sütunlar arasında taşınabiliyor
- **3 sütun genişlik oranı**: Eşit %33.33 yerine Sütun1=%40, Sütun2=%30, Sütun3=%30 olarak ayarlandı (`2/5` ve `3/10` genişlik değerleri eklendi)
- **Self-contained XSLT**: Image bloğu asset'leri önizleme ve XSLT indirme sırasında `base64 data URI` olarak gömülüyor; indirilen XSLT harici sunucu bağımlılığı olmadan çalışıyor
- **GİB Karekod formatı**: QR içeriği lowercase alan adları, YOLCUBERABERFATURA koşulları ve quoted değerlerle güncellendi
- `BlockLayout.col` (frontend) ve `BlockLayoutDto.Col` (backend) eklendi; sütun bazlı blok konumlandırması destekleniyor
- `BlockWidth` tipine `2/5` ve `3/10` değerleri eklendi

### Changed
- `xsl:stylesheet version="1.0"` → `version="2.0"` olarak güncellendi
- `unitCode` xsl:when değerleri okunabilirlik için alt alta yazılıyor (`AppendLine`)
- `RenderColGrouped` backend metodu: aynı sütundaki bloklar `display:block;width:100%` wrapper ile dikey diziliyor

### Fixed
- Aynı sütuna eklenen blokların yan yana değil, üst üste görünmesi sağlandı
- Çok sütunlu bölümlerde layout değiştirildiğinde mevcut blokların sütun genişliği doğru güncelleniyor

---

## [0.2.0] - 2026-03-28

### Added
- Varsayılan UBL 2.1 e-Fatura XML örneği eklendi (`default-invoice.xml`)
- ThemeUsePage: Kullanıcıdan XML beklenmeden doğrudan önizleme açılıyor
- EditorPage (Yeni Şablon): Sayfa açılışında XML store varsayılan fatura ile önyükleniyor
- CI pipeline: `ci.yml` ve `release.yml` workflow'ları eklendi

### Changed
- ThemeUsePage artık Phase-1 XML yükleme ekranını atlar; "XML Değiştir" butonu toolbar'da kalır
- EditorPage yeni şablon modunda XML store her açılışta sıfırlanır ve varsayılan XML yüklenir

### Fixed
- Frontend lint hataları giderildi (24 error → 0)
- CI'da frontend `working-directory` yolu düzeltildi
- `dotnet format` whitespace uyumsuzlukları düzeltildi

### Security
- pre-commit hook: `.github/workflows/` dizini secret taramasından muaf tutuldu

---

## [0.1.0] - 2024-02-15

### Added
- Initial XsltCraft monorepo structure
- React 19 + TypeScript frontend with TailwindCSS
- C# .NET 10 backend with PostgreSQL
- XSLT template editor with real-time validation
- UBL 2.1 e-Fatura standard support
- E-İrsaliye (shipping invoice) support
- JWT + Google OAuth authentication
- Local storage (dev) and S3 storage (prod) integration
- Template saving and versioning
- Block-based template designer

### Changed
- Migrated to new GitHub repository (XsltCraft-v2)
- Restructured monorepo with frontend/backend separation

### Fixed
- TypeScript strict mode compliance
- EditorStore state management
- XSLT namespace resolution

### Security
- JWT token validation
- API endpoint authentication

---

## Format Guide

Use these sections in your entries:

- **Added** — New features
- **Changed** — Changes in existing functionality
- **Deprecated** — Soon-to-be removed features
- **Removed** — Removed features
- **Fixed** — Bug fixes
- **Security** — Security fixes and improvements

Example entry:

```markdown
## [1.2.0] - 2024-03-01

### Added
- Support for XSD schema validation
- Batch template export

### Fixed
- Auth token expiration handling
- XSLT variable scope resolution
```

---

**Maintainer**: @spolt
