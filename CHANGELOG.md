# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

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
