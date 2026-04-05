# XsltCraft — Geliştirici El Teslimi

**Proje:** XsltCraft Low-Code XSLT Platform
**Tarih:** 2026-03-27
**Hazırlayan:** Semih Polat

Bu döküman, projeyi teslim alan geliştiricinin ilk 30 dakikada ortamı ayağa kaldırıp ilk göreve başlayabilmesi için yazılmıştır.

---

## Projeyi anlamak için oku

| Döküman | Ne anlatıyor |
|---------|-------------|
| `XsltCraft_PRD_v1_2.md` | Ürünün tamamı — mimari, data modelleri, API kontratları, storage stratejisi |
| `ROADMAP.md` (bu repo) | 6 fazlı geliştirme planı, checkbox'lı görev listeleri, tamamlanma kriterleri |

PRD'yi en az bir kere baştan sona oku. Özellikle şu bölümlere dikkat et:

- **§5** — Mimari genel bakış ve key design decisions
- **§7** — Monorepo klasör yapısı
- **§17** — Storage stratejisi (`IStorageService`, klasör yapısı, DI konfigürasyonu)
- **§16** — Data modelleri (`Template.xsltStoragePath`, `Asset.filePath`)

---

## Mevcut durum (2026-03-27)

Faz 1–5 büyük ölçüde tamamlandı. Son olarak aşağıdaki özellikler ve güvenlik düzeltmeleri eklendi:

### Son eklenen özellikler
- **XSLT Editör** (`/xslt-editor`) — Kullanıcı kendi XSLT + XML dosyalarını yükler, Monaco Editor (dark tema) ile düzenler, canlı önizleme görür. Debounce'lu XSLT/XML doğrulama, önizleme → editör navigasyonu, Monaco sağ tık "Resim Ekle", otomatik XML tag kapatma.
- **Şablonlarım** (`/my-xslt-templates`) — Kullanıcı kendi XSLT şablonlarını isimle kaydeder, listeler, yeniden açar, siler. Backend: `UserXsltTemplate` entity + CRUD API (`/api/user-xslt-templates`).
- **Dashboard güncellemesi** — "XSLT Editör" ve "Şablonlarım" kartları eklendi.

### Güvenlik hardening (Faz 6 — Güvenlik)
Aşağıdaki açıklar kapatıldı ve commit'lendi:

| # | Açık | Çözüm |
|---|------|-------|
| 1 | XXE Injection | Tüm `XmlReader.Create` ve `XmlDocument.Load` çağrılarına `DtdProcessing.Prohibit` + `XmlResolver=null` |
| 2 | XSLT Injection | Tüm `transform.Load` çağrılarına `XsltSettings(enableScript:false, enableDocumentFunction:false)` |
| 3 | Eksik Authorization | `RenderController` + `DocumentController` — `[Authorize]` class seviyesinde eklendi |
| 4 | Asset IDOR | `AssetsController.Serve` — authenticated kullanıcı başkasının asset'ine erişirse `403` |
| 5 | Thumbnail doğrulama eksik | `AdminController` — extension + MIME + 1 MB boyut kontrolü |
| 6 | PostMessage origin | `XsltEditorPreview.tsx` — `e.origin !== window.location.origin` kontrolü |
| 7 | Request boyut limiti yok | `PreviewRaw` → `[RequestSizeLimit(1 MB)]`, `ValidateXslt` → `512 KB` |
| 8 | Exception sızıntısı | Generic `catch` bloklarında prod → generic mesaj + `LogError`, dev → `ex.Message` |
| 9 | Dev config secret | `appsettings.Development.json` zaten `.gitignore`'da — tracked değil |

### Açık kalan Faz 5 görevleri
- `PartyInfo` block tipi (ROADMAP.md Faz 5 Görev grubu 6) — başlanmadı
- Ödeme entegrasyonu ertelenmiş durumda

---

## Klasör yapısı

```
xsltcraft/
├── backend/
│   ├── XsltCraft/              ← ASP.NET Core Web API
│   ├── XsltCraft.Application/  ← Servisler, DTO'lar, interface'ler
│   ├── XsltCraft.Domain/       ← Entity'ler
│   ├── XsltCraft.Infrastructure/ ← EF Core, Storage, XSLT engine
│   ├── XsltCraft.Tests/
│   └── XsltCraft.slnx
├── frontend/
│   └── xsltcraft-ui/           ← React 19 + TypeScript + Vite
├── storage/
│   ├── themes/
│   └── assets/
├── docker-compose.yml
├── ROADMAP.md
└── XsltCraft_PRD_v1_2.md
```

---

## Ön gereksinimler

| Araç | Versiyon | Kontrol |
|------|----------|---------|
| .NET SDK | 10 | `dotnet --version` |
| Node.js | 20+ | `node --version` |
| Docker Desktop | güncel | `docker --version` |
| Git | güncel | `git --version` |

---

## İlk kurulum

```bash
# 1. Repoyu klonla
git clone <repo-url>
cd xsltcraft

# 2. Docker ortam değişkenlerini oluştur
cp .env.example .env
# .env içindeki şifreleri değiştir (varsayılanlar dev için yeterli, prod'da mutlaka değiştir)

# 3. PostgreSQL + MinIO'yu ayağa kaldır
docker compose up -d

# 4. Backend config dosyasını oluştur
cp backend/XsltCraft/appsettings.Development.example.json \
   backend/XsltCraft/appsettings.Development.json
# appsettings.Development.json'ı kendi değerlerinle doldur (aşağıya bak)

# 5. Backend'i çalıştır (migration otomatik uygulanır)
cd backend
dotnet restore
dotnet run --project XsltCraft

# 6. Frontend'i çalıştır (yeni terminal)
cd frontend/xsltcraft-ui
npm install
npm run dev
```

Başarılıysa:
- API: `http://localhost:5000`
- Frontend: `http://localhost:5173`
- PostgreSQL: `localhost:5432`
- MinIO S3 API: `http://localhost:9000`
- MinIO Web Console: `http://localhost:9001` (kullanıcı adı/şifre: `.env` dosyasındaki değerler)

---

## Ortam değişkenleri

`backend/XsltCraft/appsettings.Development.json` dosyası `.gitignore`'da — repodan gelmez. Örnek dosyadan oluştur:

```bash
cp backend/XsltCraft/appsettings.Development.example.json \
   backend/XsltCraft/appsettings.Development.json
```

Doldurulması gereken alanlar:

```json
{
  "ConnectionStrings": {
    "Default": "Host=localhost;Port=5432;Database=xsltcraft;Username=...;Password=..."
  },
  "Jwt": {
    "SecretKey": "<en az 32 karakter rastgele string>",
    "Issuer": "xsltcraft",
    "Audience": "xsltcraft-client",
    "AccessTokenExpiryMinutes": 15
  },
  "Google": {
    "ClientId": "<Google Cloud Console'dan>",
    "ClientSecret": "<Google Cloud Console'dan>"
  },
  "Storage": {
    "Provider": "Local",
    "LocalBasePath": "../../../storage"
  },
  "AllowedOrigins": [
    "http://localhost:5173"
  ]
}
```

**MinIO ile storage test etmek istiyorsan** `Storage` bloğunu şununla değiştir (önce `docker compose up -d`):

```json
"Storage": {
  "Provider": "S3",
  "S3BucketName": "xsltcraft",
  "S3Region": "us-east-1",
  "S3ServiceUrl": "http://localhost:9000",
  "S3AccessKey": "<.env'deki MINIO_ROOT_USER>",
  "S3SecretKey": "<.env'deki MINIO_ROOT_PASSWORD>"
}
```

> Google OAuth şimdilik gerekli değil — `ClientId` olmadan çalışır, sadece Google ile giriş butonu çalışmaz.

### Docker ortam değişkenleri (.env)

`.env` dosyası `.gitignore`'da — repodan gelmez. `.env.example`'dan oluştur:

```bash
cp .env.example .env
```

`.env` içeriği:

```
POSTGRES_PASSWORD=xsltcraft_dev_password
MINIO_ROOT_USER=minioadmin
MINIO_ROOT_PASSWORD=minioadmin123
```

> **Dev ortamı için** varsayılan değerler yeterli. **Production'da** tüm şifreleri güçlü değerlerle değiştir.

---

## EF Core migration

Yeni migration eklemek gerekirse:

```bash
cd backend
dotnet ef migrations add <MigrationAdi> \
  --project XsltCraft.Infrastructure \
  --startup-project XsltCraft
```

Migration'lar uygulama başlarken `Program.cs` tarafından otomatik uygulanır.

---

## Nereden başlıyorum?

**Şu an Faz 5 sonu / Faz 6 başındayız.**

Öncelikli açık görevler:

1. **PartyInfo block tipi** — ROADMAP.md, Faz 5 Görev grubu 6 altındaki tüm checkbox'lar boş. Frontend + backend + XSLT generator birlikte tamamlanması gerekiyor.
2. **Faz 6 — S3/MinIO geçişi** — ✅ `S3StorageService` tamamlandı ve MinIO üzerinde uçtan uca test edildi. Kalan: production ortamı için TR bölge MinIO/S3 kurulumu (KVKK gereği).
3. **Rate limiting** — ✅ Auth endpoint'lerinde uygulandı (ROADMAP Faz 6 Görev grubu 2).
4. **Production altyapısı** — ROADMAP Faz 6 Görev grubu 4.

---

## Branch stratejisi

```
main        ← production-ready, her faz sonunda merge edilir
develop     ← aktif geliştirme branch'i (şu an kullanılmıyor, main üzerinde çalışılıyor)
feature/*   ← bireysel görevler için açılır
```

---

## Pre-commit hook

`Hooks/pre-commit` dosyası aktif. Her commit'te şunları kontrol eder:
1. Hassas dosya yok (`.env`, credentials vb.)
2. Secret pattern yok
3. XSLT dosyası commit'lenmiyor
4. `console.log` yok (frontend)
5. Hardcoded URL yok
6. Branch ismi geçerli

Hook hata verirse nedeni çöz, `--no-verify` kullanma.

---

## Sık karşılaşılan sorunlar

**`dotnet ef` komutu bulunamıyor:**
```bash
dotnet tool install --global dotnet-ef
```

**PostgreSQL bağlantısı reddediliyor:**
`docker compose up -d` çalıştırıldıktan sonra birkaç saniye bekle.

**`.env` dosyası bulunamadı / değişkenler boş:**
`cp .env.example .env` yaptığından emin ol. `docker compose config` komutu ile değişkenlerin doğru çözümlendiğini doğrulayabilirsin.

**MinIO bucket yok hatası:**
`minio-init` container'ı bir kere çalışıp çıkar. `docker compose up -d` sonrası `docker logs xsltcraft_minio_init` ile bucket oluşturulduğunu doğrula. "Bucket hazır." satırı görünmüyorsa `docker compose up minio-init` ile yeniden çalıştır.

**`storage/` klasörü yok hatası:**
```bash
mkdir -p storage/themes storage/assets
```

**CORS hatası (frontend → API):**
`appsettings.Development.json`'da `AllowedOrigins` içinde `http://localhost:5173` olduğundan emin ol.

**Saxon XSLT (Free theme render):**
`XsltTemplateRenderer.RenderAsync` free theme render'ı için Saxon HE kullanır. Paket `XsltCraft.Infrastructure.csproj`'ta kayıtlı, `dotnet restore` ile gelir.

---

## Önemli mimari kararlar

**XSLT dosyaları veritabanına yazılmaz.**
Tüm `.xslt` ve görsel dosyalar `storage/` klasörüne yazılır. DB yalnızca `storagePath` tutar.

**Preview storage'a yazmaz.**
`POST /api/preview` ve `POST /api/preview/raw` tamamen in-memory çalışır.

**Storage DI ile soyutlanmıştır.**
`IStorageService` kullan; `LocalStorageService` veya `S3StorageService`'i doğrudan `new` ile örnekleme.

**UserXsltTemplate içeriği DB'ye yazılır.**
Bu entity XSLT + XML içeriğini doğrudan DB'de tutar (`text` kolon) — storage'a yazılmaz. Boyut küçük tutulmalı; büyük dosyalar için storage'a geçiş değerlendirilebilir.

**Security hardening tamamlandı.**
Tüm XML/XSLT parsing noktaları XXE ve injection korumasıyla güncellendi. Yeni eklenen her XML/XSLT okuma noktasında aynı pattern'i kullan:
```csharp
// XML okuma
var settings = new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null };
using var reader = XmlReader.Create(new StringReader(content), settings);

// XSLT yükleme
var xsltSettings = new XsltSettings(enableDocumentFunction: false, enableScript: false);
transform.Load(reader, xsltSettings, new XmlUrlResolver());
```

---

## Soru için

Bir şey anlamadıysan ya da PRD'de belirsiz bir nokta gördüysen önce ilgili PRD bölümünü tekrar oku. Hâlâ netleşmediyse Semih'e sor.

---

*İyi kodlamalar.*
