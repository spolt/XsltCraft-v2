# XsltCraft — Geliştirme Yol Haritası

**Son güncelleme:** 2026-03-17  
**Referans döküman:** `XsltCraft_PRD_v1_2.md`  
**Faz sayısı:** 6  
**Toplam tahmini süre:** 10–14 hafta

Her faz sırayla tamamlanmalıdır. Bir sonraki faza geçiş için önceki fazın tüm tamamlanma kriterleri karşılanmış olmalıdır.

---

## Faz özeti

| Faz | Başlık | Tahmini süre | Önkoşul |
|-----|--------|-------------|---------|
| 1 | Monorepo altyapı & kimlik doğrulama | 5–8 gün | — |
| 2 | Storage katmanı & free theme yönetimi | 4–6 gün | Faz 1 |
| 3 | Block editor & XSLT üretim motoru | 10–15 gün | Faz 2 |
| 4 | XML binding & canlı önizleme | 5–7 gün | Faz 3 |
| 5 | Template kayıt, asset yönetimi & ödeme | 7–10 gün | Faz 4 |
| 6 | Prod hazırlık & S3 geçişi | 3–5 gün | Faz 5 |

---

## Faz 1 — Monorepo altyapı & kimlik doğrulama

**Tahmini süre:** 5–8 gün  
**Faz sonu çıktısı:** Çalışan auth API + React login/register ekranı. Postman'den token alınabilir, frontend üzerinden giriş yapılabilir.

### Görev grubu 1 — Monorepo iskeleti

- [x] `xsltcraft/` kök dizininde repo oluştur, `README.md` ve `.gitignore` ekle (Node, .NET, `storage/` klasörü ignore'a)
- [x] `apps/frontend/` ve `apps/backend/` klasörlerini oluştur
- [x] `apps/frontend/` altında Vite + React 19 + TypeScript projesi başlat
- [x] TailwindCSS 4, Zustand, Axios kurulumlarını yap
- [x] `apps/backend/` altında .NET 10 solution oluştur, 4 proje ekle:
  - [x] `XsltCraft.Api` (ASP.NET Core Web API)
  - [x] `XsltCraft.Application`
  - [x] `XsltCraft.Domain`
  - [x] `XsltCraft.Infrastructure`
- [x] Proje referanslarını bağla: Api → Application → Domain, Infrastructure → Domain
- [x] `docker-compose.yml` yaz: PostgreSQL 16 servisi, volume, env değişkenleri
- [x] `appsettings.Development.json`'a connection string ekle
- [x] `storage/themes/` ve `storage/assets/` klasörlerini oluştur (`.gitkeep` ile)

### Görev grubu 2 — Veritabanı & EF Core

- [x] `XsltCraft.Infrastructure` projesine EF Core + Npgsql provider kur
- [x] `AppDbContext` sınıfını yaz
- [x] `User` entity'sini `XsltCraft.Domain/Entities/` altında tanımla (PRD §16.1)
- [x] `RefreshToken` entity'sini tanımla (PRD §16.5)
- [x] İlk migration oluştur: `dotnet ef migrations add InitialCreate`
- [x] `Program.cs`'te uygulama ayağa kalktığında migration otomatik uygulansın

### Görev grubu 3 — JWT auth servisi & endpoint'ler

- [x] `JwtService` sınıfını yaz: access token üretimi (15 dk) + refresh token üretimi (30 gün)
- [x] `appsettings`'e JWT konfigürasyonu ekle: `SecretKey`, `Issuer`, `Audience`, `AccessTokenExpiryMinutes`
- [x] `AuthController`'ı yaz, aşağıdaki endpoint'leri implemente et:
  - [x] `POST /api/auth/register` — kayıt, email kontrolü, `passwordHash` ile kayıt
  - [x] `POST /api/auth/login` — doğrulama, access token + `HttpOnly` cookie refresh token
  - [x] `POST /api/auth/refresh` — cookie'den refresh token oku, rotate et, yenisini döndür
  - [x] `POST /api/auth/logout` — refresh token'ı `revokedAt` ile iptal et
  - [x] `GET /api/auth/me` — `[Authorize]`, mevcut kullanıcı bilgilerini döndür
- [x] Refresh token rotation mantığını yaz
- [x] JWT middleware'ini `Program.cs`'e kaydet

### Görev grubu 4 — Google OAuth

- [x] Google Cloud Console'da OAuth 2.0 client ID oluştur, redirect URI'ları ekle
- [x] `appsettings`'e `Google:ClientId` ve `Google:ClientSecret` ekle (secrets.json ile)
- [x] ASP.NET Core Google Auth middleware'ini kur
- [x] `POST /api/auth/google` endpoint'ini yaz: token doğrula → kullanıcı yoksa kaydet → JWT döndür

### Görev grubu 5 — React frontend (auth ekranları)

- [x] `src/store/authStore.ts` — Zustand store: `user`, `accessToken`, `login()`, `logout()`, `refreshToken()`
- [x] `src/services/apiService.ts` — Axios instance, Bearer header interceptor, 401'de otomatik refresh
- [x] Login sayfası (`/auth/login`): form, submit → `POST /api/auth/login`, hata yönetimi
- [x] Register sayfası (`/auth/register`): validasyon (min 8 karakter, 1 büyük harf, 1 rakam)
- [x] `PrivateRoute` bileşeni: token yoksa `/auth/login`'e yönlendir
- [x] Boş `/dashboard` sayfası ekle (Faz 2'de doldurulacak)
- [x] Google ile giriş butonu ekle

### Görev grubu 6 — CI pipeline

- [x] `.github/workflows/backend.yml`: `dotnet build` + `dotnet test`
- [x] `.github/workflows/frontend.yml`: `npm install` + `npm run build`
- [x] Her iki workflow `main` ve `develop` branch push'larında tetiklensin

### Faz 1 tamamlanma kriterleri

| Kriter | Nasıl doğrulanır |
|--------|-----------------|
| Kullanıcı kayıt olabilir | `POST /api/auth/register` → 201, DB'de kayıt görünür |
| Kullanıcı giriş yapabilir | `POST /api/auth/login` → access token + `Set-Cookie` |
| Token yenileme çalışır | `POST /api/auth/refresh` → yeni token, eskisi iptal |
| `/dashboard` korumalı | Token olmadan erişilince login'e yönlendirir |
| Docker Compose ayağa kalkar | `docker compose up` ile PostgreSQL + API birlikte çalışır |
| CI geçiyor | `main`'e push'ta her iki workflow yeşil |

---

## Faz 2 — Storage katmanı & free theme yönetimi

**Tahmini süre:** 4–6 gün  
**Faz sonu çıktısı:** Admin `.xslt` dosyası yükleyebilir, dosya `storage/themes/` altına yazılır, DB'de path tutulur.

### Görev grubu 1 — IStorageService arayüzü

- [x] `IStorageService` interface'ini `XsltCraft.Infrastructure/Storage/` altında tanımla:
  - [x] `Task<string> WriteAsync(Stream content, string relativePath, string contentType)`
  - [x] `Task<Stream> ReadAsync(string relativePath)`
  - [x] `Task DeleteAsync(string relativePath)`
  - [x] `Task<bool> ExistsAsync(string relativePath)`
- [x] `LocalStorageService` implementasyonunu yaz (fiziksel dosya sistemi, dev ortamı)
- [x] `appsettings.Development.json`'a `Storage:Provider: "Local"` ve `Storage:LocalBasePath` ekle
- [x] `Program.cs`'te DI kaydını yap: `Storage:Provider` değerine göre doğru implementasyonu enjekte et

### Görev grubu 2 — Template & Asset data modelleri

- [x] `Template` entity'sini `XsltCraft.Domain/Entities/` altında tanımla (PRD §16.2 — `xsltStoragePath` dahil)
- [x] `Asset` entity'sini tanımla (PRD §16.3)
- [x] `AppDbContext`'e `Templates` ve `Assets` `DbSet`'lerini ekle
- [x] Migration oluştur: `dotnet ef migrations add AddTemplateAndAsset`

### Görev grubu 3 — Admin free theme endpoint'leri

- [x] `AdminController`'ı yaz, `[Authorize(Roles = "admin")]` ile koru
- [x] `POST /api/admin/themes` endpoint'i:
  - [x] `multipart/form-data` ile `.xslt` dosyası + metadata al
  - [x] `IStorageService.WriteAsync` ile `themes/{themeId}.xslt` yoluna yaz
  - [x] `Template` kaydını DB'ye ekle: `isFreeTheme = true`, `xsltStoragePath` set et
- [x] `GET /api/templates` endpoint'i: tüm free theme'leri listele (public, auth gerektirmez)
- [x] `PUT /api/admin/themes/:id` endpoint'i: eski dosyayı değiştir, DB path'i güncelle
- [x] `DELETE /api/admin/themes/:id` endpoint'i: storage'dan sil + DB kaydını kaldır
- [x] `.xslt` dosya tipi ve boyut validasyonu ekle (max 2MB, yalnızca `.xslt`)

### Görev grubu 4 — Free theme download endpoint'i

- [x] `GET /api/templates/:id/download` endpoint'ini yaz (free theme için):
  - [x] DB'den `xsltStoragePath` oku
  - [x] `IStorageService.ReadAsync` ile dosyayı stream olarak aç
  - [x] `Content-Type: application/xslt+xml`, `Content-Disposition: attachment` header'larıyla döndür

### Görev grubu 5 — Frontend: Template Library sayfası (iskelet)

- [x] `GET /api/templates` çağrısı yapan `templateService.ts` fonksiyonu yaz
- [x] `/templates` sayfasını oluştur: free theme kartlarını listele (ad, döküman tipi)
- [x] Her kartta "Bu temayı kullan" butonu ekle (Faz 3'te editor'a bağlanacak)

### Faz 2 tamamlanma kriterleri

| Kriter | Nasıl doğrulanır |
|--------|-----------------|
| Admin `.xslt` yükleyebilir | `POST /api/admin/themes` → dosya `storage/themes/` altında görünür |
| DB'de yalnızca path tutuluyor | `templates` tablosunda `xslt_storage_path` dolu, binary yok |
| Free theme indirilebilir | `GET /api/templates/:id/download` → `.xslt` dosyası tarayıcıya iner |
| Storage servisi DI ile geliyor | `LocalStorageService` doğrudan `new` ile değil inject ile kullanılıyor |

---

## Faz 3 — Block editor & XSLT üretim motoru

**Tahmini süre:** 10–15 gün  
**Faz sonu çıktısı:** Kullanıcı blok ekleyip block tree oluşturabilir; backend bu tree'den geçerli bir XSLT üretir.

### Görev grubu 1 — Block tree veri modeli (frontend)

- [ ] `src/types/blocks.ts` dosyasını oluştur: tüm 14 block tipi için TypeScript interface'leri (PRD §9.2)
- [ ] `src/types/template.ts`: `Section`, `BlockTree` tipleri
- [ ] `src/store/editorStore.ts` — Zustand store:
  - [ ] `sections`, `selectedBlockId`, `isDirty` state'leri
  - [ ] `addBlock`, `removeBlock`, `moveBlock`, `updateBlockConfig` aksiyonları
  - [ ] Undo/redo için `past[]` ve `future[]` stack'leri (min 20 adım)

### Görev grubu 2 — Block editor UI bileşenleri

- [ ] `BlockPalette` bileşeni: 14 block tipini kategorilere göre listele (Layout, Data, Media, Decoration)
- [ ] `Canvas` bileşeni: section'ları ve içindeki block'ları render et
- [ ] `Section` bileşeni: başlık, block listesi, "+" butonu
- [ ] `BlockCard` bileşeni: block'un adını ve tipini göster, seçili olunca highlight
- [ ] `PropertyPanel` bileşeni: seçili block'un `config`'ini düzenleyen form alanları (her block tipi için ayrı sub-component)
- [ ] `dnd-kit` entegrasyonu:
  - [ ] Palette'ten Canvas'a sürükle-bırak (yeni block oluştur)
  - [ ] Section içinde block sıralamasını değiştir
  - [ ] Block'u section'lar arası taşı
  - [ ] Drop zone highlight efekti
- [ ] Undo/redo: `Ctrl+Z` / `Ctrl+Y` keyboard shortcut'ları

### Görev grubu 3 — XSLT üretim motoru (backend)

- [ ] `XsltGeneratorService` sınıfını `XsltCraft.Application/Preview/` altında yaz
- [ ] Her block tipi için XSLT snippet üretici metot yaz (PRD §9.2'deki XSLT Output örneklerini baz al):
  - [ ] `Text`, `Heading`, `Paragraph`
  - [ ] `Table` (for-each loop dahil)
  - [ ] `ForEach` (container)
  - [ ] `Conditional` (if/choose, tüm operatörler)
  - [ ] `Image` (base64 embed)
  - [ ] `DocumentInfo`, `Totals`, `Notes`, `BankInfo`, `ETTN`
  - [ ] `Divider`, `Spacer`
- [ ] Snippet'leri birleştirip tam ve geçerli XSLT stylesheet'i oluştur (namespace declarations, `xsl:stylesheet` wrapper dahil)
- [ ] UBL 2.1 namespace'lerini otomatik ekle (PRD §10.5)
- [ ] Üretilen XSLT'yi `System.Xml.Xsl.XslCompiledTransform` ile doğrula (syntax hatası varsa anlamlı hata mesajı döndür)

### Görev grubu 4 — Editor sayfası & route'lar

- [ ] `/editor/new` route'u: boş block tree ile editor aç
- [ ] `/editor/:templateId` route'u: mevcut template'i yükle, block tree'yi doldur
- [ ] Editor toolbar'ı: template adı (inline edit), Save butonu, Download butonu (Faz 5'te aktif)
- [ ] `PUT /api/templates/:id` çağrısıyla otomatik kaydetme (30 sn debounce + manuel save butonu)
- [ ] `POST /api/templates` ile yeni template oluşturma

### Faz 3 tamamlanma kriterleri

| Kriter | Nasıl doğrulanır |
|--------|-----------------|
| Tüm 14 block tipi eklenebilir | Palette'ten canvas'a sürüklenebilir, property panel açılır |
| Undo/redo çalışır | 20 adım geri/ileri gidilebilir |
| XSLT üretimi doğru | Her block tipi için üretilen XSLT XSD'ye göre valid |
| Block tree DB'ye kaydediliyor | `PUT /api/templates/:id` → DB'de `block_tree` JSONB güncellenir |

---

## Faz 4 — XML binding & canlı önizleme

**Tahmini süre:** 5–7 gün  
**Faz sonu çıktısı:** Kullanıcı XML yükler, tree'den XPath seçer, iframe'de anlık HTML önizleme görür.

### Görev grubu 1 — Preview endpoint'i

- [ ] `POST /api/preview` endpoint'ini yaz:
  - [ ] Request: `{ sections, xmlContent, assets }`
  - [ ] Backend: block tree → XSLT üret (in-memory, storage'a yazma)
  - [ ] `XslCompiledTransform` ile XSLT'yi XML'e uygula → HTML üret
  - [ ] Response: `{ html, generationTimeMs }`
- [ ] `POST /api/preview/theme/:themeId` endpoint'ini yaz:
  - [ ] DB'den `xsltStoragePath` oku
  - [ ] `IStorageService.ReadAsync` ile `.xslt` dosyasını yükle
  - [ ] XML'e uygula → HTML döndür
- [ ] Hata yönetimi: geçersiz XML veya XSLT'de kullanıcıya anlamlı hata mesajı

### Görev grubu 2 — XML Tree Explorer (frontend)

- [ ] XML yükleme: toolbar'a "XML Yükle" butonu, `DOMParser` ile client-side parse
- [ ] `XmlTreeExplorer` bileşeni:
  - [ ] Collapsible tree render'ı
  - [ ] Namespace prefix'i göster, tam XPath'i internal olarak sakla
  - [ ] Yaprak node'larda resolved değeri inline göster
  - [ ] Tekrarlayan node'ları `[1]`, `[2]` indexiyle göster
  - [ ] Node'a tıklayınca seçili block'un aktif binding alanına XPath yaz
- [ ] `src/store/xmlStore.ts`: yüklü XML dosyaları listesi, aktif XML seçimi

### Görev grubu 3 — XPath binding UI

- [ ] `PropertyPanel`'deki her bind edilebilir alana şunları ekle:
  - [ ] XPath input alanı (elle düzenlenebilir)
  - [ ] "Tree'den seç" butonu: XML tree'yi aktif hale getirir
  - [ ] Resolved value gösterimi: yüklü XML'de XPath'in döndürdüğü değer
  - [ ] Fallback alanı: XPath sonuç döndürmezse kullanılacak değer

### Görev grubu 4 — Canlı önizleme paneli

- [ ] `PreviewPanel` bileşeni: sandboxed `<iframe>`, `srcdoc` ile HTML inject
- [ ] Editor'da block tree değişince 1500ms debounce sonrası `POST /api/preview` çağır
- [ ] Toolbar'a XML seçici dropdown ekle: birden fazla XML arasında geçiş
- [ ] Yükleniyor göstergesi: preview yenilenirken spinner

### Faz 4 tamamlanma kriterleri

| Kriter | Nasıl doğrulanır |
|--------|-----------------|
| XML yüklenebilir | Tree explorer XML'i doğru parse eder ve gösterir |
| XPath binding çalışır | Node'a tıklanınca property panel'e XPath yazılır |
| Preview render süresi ≤ 2 sn | 50 satırlı e-Fatura XML ile ölçülür |
| Preview in-memory | Storage'da preview için yeni dosya oluşmaz |
| Free theme preview | `POST /api/preview/theme/:id` storage'dan okur, HTML döndürür |

---

## Faz 5 — Template kayıt, asset yönetimi & ödeme

**Tahmini süre:** 7–10 gün  
**Faz sonu çıktısı:** Kullanıcı logo/imza yükleyebilir, ödeme yapabilir, `.xslt` dosyasını indirebilir.

### Görev grubu 1 — Asset upload

- [ ] `POST /api/assets/upload` endpoint'i:
  - [ ] `IFormFile` al, tip/boyut validasyonu yap (PNG, JPG, SVG — max 5MB)
  - [ ] `IStorageService.WriteAsync` ile `assets/{userId}/{assetId}.{ext}` yoluna yaz
  - [ ] `Asset` kaydını DB'ye ekle (`filePath` = storage yolu)
  - [ ] Response: `{ id, url, storagePath, type, mimeType, sizeBytes }`
- [ ] `GET /api/assets/:id/serve` endpoint'i: storage'dan dosyayı stream et
- [ ] `DELETE /api/assets/:id` endpoint'i: storage'dan sil + DB kaydını kaldır
- [ ] Frontend'de image block property panel'ine upload UI ekle

### Görev grubu 2 — Template CRUD tamamlama

- [ ] `GET /api/templates/my` endpoint'i: kullanıcının kendi template'lerini listele
- [ ] `POST /api/templates/:id/clone` endpoint'i: free theme'i klonla, yeni User Template oluştur
- [ ] `DELETE /api/templates/:id` endpoint'i: owner kontrolü, DB'den sil
- [ ] Frontend'de "My Templates" sayfasını yap: liste, rename, duplicate, delete

### Görev grubu 3 — Ödeme entegrasyonu

- [ ] Ödeme sağlayıcısını seç ve entegre et (iyzico veya Stripe)
- [ ] `Payment` entity'sini `AppDbContext`'e ekle (PRD §16.4), migration çalıştır
- [ ] `POST /api/payments/initiate` endpoint'i: ödeme session'ı başlat, provider URL'ini döndür
- [ ] `POST /api/payments/webhook` endpoint'i:
  - [ ] Provider imzasını doğrula
  - [ ] `Payment` kaydını `completed` olarak güncelle
  - [ ] Onay e-postası gönder (yeniden indirme linki dahil)
- [ ] `GET /api/payments/history` endpoint'i: kullanıcının ödeme geçmişi

### Görev grubu 4 — Ödeme sonrası XSLT üretimi & indirme

- [ ] `GET /api/templates/:id/download` endpoint'ini tamamla:
  - [ ] Ödeme kontrolü: free theme veya `Payment.status = completed` ise devam
  - [ ] Block tree'den XSLT üret
  - [ ] `IStorageService.WriteAsync` ile `templates/{templateId}/{timestamp}.xslt` yoluna yaz
  - [ ] DB'de `Template.xsltStoragePath`'i güncelle
  - [ ] Dosyayı stream olarak döndür (`Content-Disposition: attachment; filename="...xslt"`)
  - [ ] Yeniden indirme: `xsltStoragePath` doluysa storage'dan doğrudan oku, yeniden üretme
- [ ] Frontend'de Download butonu + ödeme modal'ı: fiyat göster, ödeme formu, success/error state

### Görev grubu 5 — Profil sayfası

- [ ] `/profile` sayfası: display name düzenle, e-posta güncelle
- [ ] "Satın alınan template'ler" listesi: her biri için yeniden indirme butonu
- [ ] Hesap silme akışı (onay modal'ı ile)

### Faz 5 tamamlanma kriterleri

| Kriter | Nasıl doğrulanır |
|--------|-----------------|
| Asset upload çalışır | Görsel `storage/assets/` altında görünür, DB'de path var |
| Ödeme akışı tamamlanır | Webhook gelince `Payment.status = completed` olur |
| XSLT indirilebilir | Ödeme sonrası `.xslt` dosyası tarayıcıya iner |
| Yeniden indirme çalışır | İkinci indirmede storage'dan okur, yeniden üretmez |
| Free theme görseli ücretsiz | Görsel yüklenmiş free theme ödemesiz indirilebilir |

---

## Faz 6 — Prod hazırlık & S3 geçişi

**Tahmini süre:** 3–5 gün  
**Faz sonu çıktısı:** Production deploy — S3 üzerinde çalışan, KVKK uyumlu, ölçeklenebilir sistem.

### Görev grubu 1 — S3StorageService

- [ ] `S3StorageService` implementasyonunu yaz (`AWSSDK.S3` veya `Minio` paketi ile)
- [ ] `appsettings.Production.json`'a `Storage:Provider: "S3"`, `S3BucketName`, `S3Region` ekle
- [ ] TR bölge datacenter'ında S3-uyumlu depo oluştur (KVKK gereği)
- [ ] Tüm Faz 2–5 storage işlemlerini `S3StorageService` ile test et

### Görev grubu 2 — Güvenlik hardening

- [ ] Storage URL'leri frontend'e doğrudan açılmıyor — tüm dosya erişimi backend üzerinden
- [ ] `.xslt` upload için MIME type + içerik validasyonu (sadece geçerli XSLT XML)
- [ ] Rate limiting ekle: auth endpoint'leri için (brute force koruması)
- [ ] CORS konfigürasyonunu production domain'e göre kısıtla
- [ ] Tüm endpoint'lerde `[Authorize]` attribute eksiklerini gözden geçir
- [ ] Payment webhook imza doğrulamasını production key'leriyle test et

### Görev grubu 3 — NFR testleri & performans

- [ ] Preview endpoint'i ≤ 2 sn: 50 satırlı e-Fatura XML ile yük testi
- [ ] Sayfa yükleme ≤ 3 sn: editor sayfasını Lighthouse ile ölç
- [ ] Storage okuma gecikmesi ölç: S3 için ≤ 500ms hedefi
- [ ] Frontend bundle boyutunu kontrol et, gerekirse code splitting ekle

### Görev grubu 4 — Production altyapısı

- [ ] Environment variable'ları production ortamına set et (JWT secret, DB URL, S3 credentials, OAuth keys)
- [ ] Database backup stratejisi belirle
- [ ] Health check endpoint'i ekle: `GET /health`
- [ ] Loglama konfigürasyonu: hata seviyesi logları toplanacak yer belirlenir (Seq, Serilog, CloudWatch vb.)
- [ ] `docker-compose.prod.yml` veya Kubernetes manifest hazırla

### Görev grubu 5 — Son kontroller

- [ ] WCAG 2.1 AA: auth sayfaları ve dashboard için erişilebilirlik kontrolü
- [ ] Browser uyumluluğu: Chrome 120+, Firefox 120+, Edge 120+, Safari 17+
- [ ] KVKK: tüm kullanıcı verilerinin TR sunucusunda tutulduğunu doğrula
- [ ] Tüm API endpoint'lerini Postman collection ile belgele

### Faz 6 tamamlanma kriterleri

| Kriter | Nasıl doğrulanır |
|--------|-----------------|
| S3 entegrasyonu çalışır | Prod'da dosyalar S3 bucket'ında görünür |
| Tüm NFR'ler karşılanır | Preview ≤ 2 sn, yükleme ≤ 3 sn |
| Güvenlik açığı yok | Yetkisiz storage erişimi 401/403 döner |
| Health check yeşil | `GET /health` → 200 |

---

## Genel notlar

- Her fazın sonunda bir PR açılır, review yapılır, `main`'e merge edilir.
- `develop` branch'i aktif geliştirme için kullanılır; feature'lar `feature/faz-N-konu` formatında branch açılır.
- Faz 1–5 süresince storage provider olarak `LocalStorageService` kullanılır; Faz 6'da `S3StorageService`'e geçilir.
- PRD'de belirtilen "Out of Scope" maddeleri (PRD §19) bu roadmap'e dahil değildir.
