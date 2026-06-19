# Tech Stack (kod-gerçeği)

> Kaynak: README.md + kodun kendisi (DI kaydı, `appsettings`, `.csproj`). **HANDOFF.md'nin eski değerleri esas alınmaz.**

## Frontend
React 19 · TypeScript 5.9 · Vite 8 · Zustand · TailwindCSS 4 · @dnd-kit · React Router 7 · Monaco Editor · lucide-react.
- `frontend/xsltcraft-ui/src/` — `store/` (auth, editor, xml, toast), `services/` (Axios + NDJSON streaming), `components/`, `pages/`.

## Backend — Clean Architecture
.NET 10 · ASP.NET Core Web API. Proje adları (gerçek):
- `XsltCraft.Api` — controllers, rate limiting (klasör: `backend/XsltCraft/`, `.csproj` = `XsltCraft.Api`).
- `XsltCraft.Application` — servisler, XSLT generator (V1+V2), AI prompt pipeline (`Ai/`, `Prompts/`, `Preview/`).
- `XsltCraft.Domain` — entity'ler.
- `XsltCraft.Infrastructure` — EF Core, storage sağlayıcıları, AI provider orchestrator.
- **Testler:** `XsltCraft.Tests` (generator/preview), `XsltCraft.Application.Tests` (AI pipeline + Verify golden).

## XSLT motoru
Üretilen stylesheet **`version="2.0"`**; namespace'ler `n1`/`cbc`/`cac`/`ext` (`urn:oasis:names:specification:ubl:schema:xsd:...`). Önizleme render'ı **Saxon HE 10.9** (XSLT 2.0 / XPath 2.0). Generator çıktısı testlerde `XslCompiledTransform.Load` ile derlenip doğrulanır.

## AI (opsiyonel, feature-flagged)
- **Birincil:** Ollama `qwen2.5-coder:3b` (`Ai:Ollama`).
- **Yedek:** Gemini `gemini-2.5-flash` (`Ai:Gemini:Enabled`, varsayılan kapalı).
- **Anthropic/Claude sağlayıcısı KODDA YOK.** Kayıtlı sağlayıcılar: `OllamaAssistantProvider`, `GeminiAssistantProvider`; orchestrator `AiProviderOrchestrator`.
- NDJSON streaming (`PipeWriter` + `FlushAsync`), iki ayrı timeout (connect + first-token), per-user rate limit + günlük token bütçesi. Mid-stream fallback yapılmaz.
- Prompt'lar embedded markdown (`XsltCraft.Application/Prompts/`): Core/* + Patterns/* (8 UBL-TR pattern pack).

## Database & Storage
PostgreSQL 16 · EF Core · Npgsql. Storage: `IStorageService` — dev `LocalStorageService`, prod `S3StorageService`/MinIO (S3 uyumlu). `.xslt` ve görseller storage'a yazılır; DB yalnız `storagePath` tutar (istisna: `UserXsltTemplate` içeriği DB'de).

## Auth
JWT (15 dk access) + HttpOnly refresh-token rotation (30 gün) + Google OAuth 2.0. Inactive kullanıcı login/refresh'te bloklanır.

## CI/CD & Test
GitHub Actions: `ci.yml` (frontend lint/build/test + backend format/build/test + non-blocking `eval` job), `backend.yml`/`frontend.yml` (path-gated), `release.yml` (tag). **Dev `docker-compose.yml` = Postgres + MinIO + minio-init** (uygulama varsayılan storage'ı Local; MinIO S3 testleri için hazır). Prod stack: `docker-compose.prod.yml` + Nginx + MinIO.
