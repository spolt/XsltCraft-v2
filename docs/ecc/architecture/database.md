# Architecture — Database

> Kod-gerçeği. PostgreSQL 16 · EF Core · Npgsql. Bağlam: `docs/ecc/decisions/006-storage-abstraction.md`.

## Context
`Infrastructure/Persistence/AppDbContext.cs`. Migration'lar `Infrastructure/Persistence/Migrations/` altında, **açılışta otomatik uygulanır** (`db.Database.Migrate()` — `Program.cs`). Yeni migration:
```bash
cd backend && dotnet ef migrations add <Ad> --project XsltCraft.Infrastructure --startup-project XsltCraft
```

## Tablolar (DbSet'ler)
| Entity | Rol |
|--------|-----|
| `User` | Hesap (username + email, hash, rol Admin/Editor/User, `IsActive`, GoogleId) |
| `RefreshToken` | HttpOnly refresh-token rotation (30 gün) |
| `Template` | Tasarımcı şablonu — `OwnerId?` (→User), `IsFreeTheme`, `BlockTree` (JSON), `XsltStoragePath`, `DocumentType` |
| `Asset` | Yüklenen görsel (logo/imza) — `filePath`, `Template` ilişkisi |
| `UserXsltTemplate` | Ham XSLT şablonu — **içerik DB'de** (`text` kolon), storage'a yazılmaz |
| `UserXsltTemplateShare` | Ham şablon paylaşımı / eşzamanlı düzenleme kilidi |
| `UserSnippet` | Kişisel XSLT snippet'leri (admin public snippet'ler de burada) |
| `UserActivity` | Aktivite kaydı (Save/Download…) — `IUserActivityRecorder` |
| `FeatureFlag` | Runtime flag (örn. `ai.enabled`) + değer; `appsettings`'i override eder |
| `UserAiUsage` | Per-user günlük AI token bütçesi sayacı |

## Storage stratejisi (önemli değişmez)
- **`.xslt` ve görseller DB'ye yazılmaz** — object storage'a (`IStorageService`) yazılır, DB yalnız `XsltStoragePath`/`filePath` tutar.
- **İstisna:** `UserXsltTemplate` içeriği doğrudan DB'de (`text`). Boyut küçük tutulmalı; büyürse storage'a taşıma değerlendirilir.
- **Preview storage'a yazmaz** — `POST /api/preview` ve `/api/preview/raw` tamamen in-memory.

## İlişkiler & erişim
- `Template.OwnerId` → `User`. Sahiplik kontrolü deseni: `!IsFreeTheme && OwnerId != userId && role != "Admin" → Forbid()` (bkz. `TemplateController.GetById/Download`, `PreviewController` user-template).
- Free theme'ler (`IsFreeTheme=true`, `OwnerId=null`) public — admin küratörlü.
- `FeatureFlag` cache TTL ~15 sn (AI flag değişimi en geç 15 sn'de yayılır).

## Domain'de DB-dışı entity'ler
`CompiledTemplate`, `Layout`, `Slot`, `Block`, `TemplateMetadata` — DbSet **değil**; dosya-tabanlı template motorunun (`Infrastructure/Templates/`, `FileTemplateRepository`) domain nesneleri. DB tablosu yoktur.

## Seed
Açılışta `appsettings`'ten admin kullanıcı seed edilir (`Admin:Email`/`Username`; şifre user-secrets `Admin:Password`).
