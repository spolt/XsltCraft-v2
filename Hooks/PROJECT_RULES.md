# XsltCraft — Proje Kuralları

**Referans:** `XsltCraft_PRD_v1_2.md` · **Son güncelleme:** 2026-03-18

Bu dosya hem geliştirici uyumu hem de otomatik hook kontrollerinin referansıdır.
Her kural karşısında hangi hook'un onu denetlediği belirtilmiştir.

---

## 1. Git Kuralları

### 1.1 Branch İsimlendirme

| Tip | Format | Örnek |
|-----|--------|-------|
| Faz görevi | `feature/faz-N-konu` | `feature/faz-2-storage-service` |
| Hata düzeltme | `fix/konu` | `fix/refresh-token-rotation` |
| Acil düzeltme | `hotfix/konu` | `hotfix/auth-bypass` |
| Altyapı | `chore/konu` | `chore/update-ef-core` |
| Dökümantasyon | `docs/konu` | `docs/api-endpoints` |
| Aktif geliştirme | `develop` | — |
| Production | `main` | — |

> **Kontrol:** `pre-commit` hook — branch ismi pattern'a uymuyorsa uyarı verir.

### 1.2 Commit Mesajı Formatı

```
<tip>: <kısa açıklama>

[opsiyonel detay]
```

| Tip | Ne zaman |
|-----|----------|
| `feat` | Yeni özellik |
| `fix` | Hata düzeltme |
| `refactor` | Kod yeniden yapılandırma |
| `test` | Test ekleme/düzenleme |
| `docs` | Dökümantasyon |
| `chore` | Bağımlılık güncelleme, config değişikliği |
| `ci` | CI/CD pipeline değişikliği |

**Örnekler:**
```
feat: POST /api/auth/google endpoint eklendi
fix: refresh token rotation null hatası giderildi
chore: EF Core 10.0.1 → 10.0.3
```

### 1.3 Push Kuralları

- `main` branch'ine **doğrudan push yapılamaz** → PR zorunlu
- Her faz sonu `develop → main` PR açılır, review sonrası merge edilir
- Feature branch'ler `develop`'a merge edilir, `main`'e asla doğrudan değil

> **Kontrol:** `pre-push` hook — main'e doğrudan push girişimi engellenir.

---

## 2. Güvenlik Kuralları

### 2.1 Hassas Dosyalar

Bu dosyalar **asla** commit edilmez:

| Dosya | Neden |
|-------|-------|
| `appsettings.Development.json` | JWT secret, DB şifresi, Google credentials |
| `secrets.json` | .NET user secrets |
| `.env`, `.env.*` | Frontend ortam değişkenleri |
| `*.pfx`, `*.p12` | SSL sertifikaları |

> `.gitignore`'a eklenmiş durumdalar. `pre-commit` hook yanlışlıkla stage edilirse yakalar.

### 2.2 Credential Yönetimi

- **Development:** `appsettings.Development.json` (gitignore'da) veya `dotnet user-secrets`
- **CI/CD:** GitHub Secrets (`VITE_GOOGLE_CLIENT_ID`, `JWT_SECRET_KEY`, vb.)
- **Production:** Environment variable veya Azure Key Vault / AWS Secrets Manager
- JWT `SecretKey` minimum 32 karakter olmalı
- Production'da `change-me-*` gibi placeholder değerler **kullanılamaz**

> **Kontrol:** `pre-commit` + `pre-push` hook — bilinen secret pattern'ları taranır.

### 2.3 Yasaklı Pattern'lar

Kaynak kodunda aşağıdakiler bulunmamalı:

```
GOCSPX-*              → Google OAuth secret
AKIA*                 → AWS Access Key
BEGIN PRIVATE KEY     → Private key dosyası
Password=<gerçek>     → Connection string şifresi
```

---

## 3. Mimari Kurallar (PRD §5, §17)

### 3.1 Storage Stratejisi

**Kural:** XSLT dosyaları ve görseller **veritabanına yazılmaz.** Yalnızca dosya yolları (`storagePath`) DB'ye kaydedilir.

```
✅ template.XsltStoragePath = "themes/abc123.xslt"   ← yol tutulur
❌ template.XsltContent = "<xsl:stylesheet>..."        ← içerik yazılmaz
```

**Kural:** Storage servisi **her zaman DI ile inject edilir,** `new` ile örneklenmez.

```csharp
✅ public AdminController(IStorageService storage) { ... }
❌ var storage = new LocalStorageService();
```

> **Kontrol:** `pre-push` hook — `new LocalStorageService()` ve `XsltContent` entity pattern'ı taranır.

### 3.2 Preview In-Memory Çalışır

`POST /api/preview` endpoint'i:
- XSLT'yi **in-memory** üretir
- XML'e uygular, HTML döndürür
- **Storage'a hiçbir şey yazmaz**

Storage'a yazma yalnızca `GET /api/templates/:id/download` (ödeme sonrası) endpoint'inde gerçekleşir.

> **Kontrol:** `pre-push` hook — PreviewController içinde `WriteAsync` çağrısı tespit edilirse hata verir.

### 3.3 Proje Referans Yapısı

```
XsltCraft.Api  →  XsltCraft.Application  →  XsltCraft.Domain
                  XsltCraft.Infrastructure  →  XsltCraft.Domain
```

- `Api`, `Infrastructure`'a **doğrudan bağımlı olamaz** (DI üzerinden gelir)
- `Domain`, diğer projelere **hiç bağımlı değildir**
- `Application`, `Infrastructure`'a **bağımlı olamaz**

### 3.4 Frontend API Erişimi

- Tüm API çağrıları `src/services/apiService.ts` üzerinden yapılır
- Bileşenler `axios` veya `fetch`'i doğrudan çağırmaz
- `localhost` URL'leri bileşen koduna hardcode edilmez — `VITE_API_URL` env değişkeni kullanılır

---

## 4. Kod Standartları

### 4.1 Backend (.NET)

- .NET 10 / C# 13 kullanılır
- Controller action'larında iş mantığı olmaz — servis katmanına taşınır
- `async/await` her zaman kullanılır (`.Result` veya `.Wait()` yasak)
- Repository pattern: `AppDbContext` doğrudan Controller'da kullanılmaz

### 4.2 Frontend (React / TypeScript)

- `any` tipi kullanılmaz — her zaman explicit tip tanımlanır
- State yönetimi Zustand ile yapılır (`useState` yalnızca local UI state için)
- `console.log` production koda girmez

> **Kontrol:** `pre-commit` hook — staged frontend dosyalarında `console.log` taranır.

---

## 5. .gitignore Uyumluluk Listesi

Aşağıdaki dosya/klasör türleri her zaman gitignore'da olmalıdır:

| Pattern | Neden |
|---------|-------|
| `backend/**/bin/`, `backend/**/obj/` | Build artefaktları |
| `**/appsettings.Development.json` | Yerel credentials |
| `frontend/**/node_modules/` | Bağımlılıklar |
| `frontend/**/dist/` | Build çıktısı |
| `storage/themes/*`, `storage/assets/*` | Yerel dosyalar (`.gitkeep` hariç) |
| `*.xslt` | XSLT dosyaları storage'a aittir |
| `*.log`, `*.tmp` | Geçici dosyalar |
| `.env`, `.env.*` | Ortam değişkenleri |

---

## 6. CI/CD Kuralları

- `main` ve `develop` branch'lerine her push'ta CI çalışır
- Backend CI: `dotnet build` + `dotnet test` (test projesi eklendiğinde)
- Frontend CI: `npm install` + `npm run build`
- **CI'dan geçmeyen branch `main`'e merge edilemez**
- `VITE_GOOGLE_CLIENT_ID` GitHub Secret olarak tanımlanmalıdır (frontend build için)

---

## 7. Hook Referansı

| Hook | Ne zaman | Ne kontrol eder |
|------|----------|-----------------|
| `pre-commit` | Her commit öncesi (~5 sn) | Hassas dosya, secret pattern, .xslt, console.log, hardcoded URL, branch ismi |
| `pre-push` | Her push öncesi (~60-90 sn) | main koruma, backend build, frontend build+lint, kapsamlı secret tarama, mimari kurallar |

**Kurulum:**
```bash
bash Hooks/setup-hooks.sh
```

**Hook'u geçici devre dışı bırakmak** (acil durum):
```bash
git push --no-verify   # ⚠ yalnızca zorunlu durumlarda
```
