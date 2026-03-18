# XsltCraft — Geliştirici El Teslimi

**Proje:** XsltCraft Low-Code XSLT Platform  
**Tarih:** 2026-03-17  
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

## Ön gereksinimler

Başlamadan önce şunların kurulu olduğundan emin ol:

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

# 2. PostgreSQL'i ayağa kaldır
docker compose up -d

# 3. Backend'i çalıştır
cd apps/backend
dotnet restore
dotnet run --project XsltCraft.Api

# 4. Frontend'i çalıştır (yeni terminal)
cd apps/frontend
npm install
npm run dev
```

Başarılıysa:
- API: `http://localhost:5000`
- Frontend: `http://localhost:5173`
- PostgreSQL: `localhost:5432`

---

## Ortam değişkenleri

Backend `apps/backend/XsltCraft.Api/appsettings.Development.json` dosyasını kullanır. Repodan gelen örnek dosyayı kopyala ve doldur:

```bash
cp apps/backend/XsltCraft.Api/appsettings.Development.example.json \
   apps/backend/XsltCraft.Api/appsettings.Development.json
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
  }
}
```

> Google OAuth şimdilik gerekli değil. Faz 1'in ilk görev gruplarını `ClientId` olmadan tamamlayabilirsin; OAuth'u sonraya bırak.

---

## Branch stratejisi

```
main        ← production-ready, her faz sonunda merge edilir
develop     ← aktif geliştirme branch'i
feature/*   ← bireysel görevler için açılır
```

Yeni bir göreve başlarken:

```bash
git checkout develop
git pull
git checkout -b feature/faz-1-jwt-service
# ... çalış ...
git push origin feature/faz-1-jwt-service
# PR aç: feature → develop
```

Faz tamamlanınca `develop → main` PR'ı açılır ve review edilir.

---

## Nereden başlıyorum?

**Şu an Faz 1'deyiz.**

ROADMAP.md'yi aç, "Faz 1 — Görev grubu 1" altındaki ilk checkbox'tan başla:

> ☐ `xsltcraft/` kök dizininde repo oluştur, `README.md` ve `.gitignore` ekle

Görevleri tamamladıkça checkbox'ları işaretle ve commit'le.

---

## Önemli mimari kararlar (PRD'den özet)

**XSLT dosyaları veritabanına yazılmaz.**  
Tüm `.xslt` ve görsel dosyalar `storage/` klasörüne (ya da prod'da S3'e) yazılır. Veritabanı yalnızca bu dosyaların yolunu (`storagePath`) tutar. Dosya içeriğini hiçbir zaman bir DB kolonu olarak sakla.

**Preview storage'a yazmaz.**  
`POST /api/preview` endpoint'i XSLT'yi in-memory üretir, XML'e uygular, HTML döndürür. Storage'a yazma yalnızca ödeme tamamlandıktan sonra (`/download` endpoint'inde) gerçekleşir.

**Storage DI ile soyutlanmıştır.**  
`IStorageService` arayüzünü kullan; `LocalStorageService` veya `S3StorageService`'i doğrudan `new` ile örnekleme. Servis, `Program.cs`'te konfigürasyona göre inject edilir.

**Monorepo, iki bağımsız proje.**  
`apps/frontend` ve `apps/backend` birbirinden bağımsız build ve run edilir. Ortak tip tanımları `apps/frontend/src/types/` altındaki TypeScript interface'leri ile backend DTO'ları arasında manuel olarak senkronize tutulur.

---

## Sık karşılaşılan sorunlar

**`dotnet ef` komutu bulunamıyor:**
```bash
dotnet tool install --global dotnet-ef
```

**PostgreSQL bağlantısı reddediliyor:**  
`docker compose up -d` çalıştırıldıktan sonra birkaç saniye bekle, container tam ayağa kalkmadan önce bağlantı kurulamaz.

**`storage/` klasörü yok hatası:**  
Repo kökünde `storage/themes/` ve `storage/assets/` klasörlerini oluştur:
```bash
mkdir -p storage/themes storage/assets
```

**CORS hatası (frontend → API):**  
`appsettings.Development.json`'da CORS origin'inin `http://localhost:5173` olarak ayarlandığından emin ol.

---

## Soru için

Bir şey anlamadıysan ya da PRD'de belirsiz bir nokta gördüysen önce ilgili PRD bölümünü tekrar oku. Hâlâ netleşmediyse Semih'e sor — belirsizlikleri açık bırakma, tahminle ilerleme.

---

*İyi kodlamalar.*
