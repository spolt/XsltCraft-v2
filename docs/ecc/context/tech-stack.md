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
- `XsltCraft.Infrastructure` — EF Core, storage sağlayıcıları, AI provider orchestrator, XSLT worker havuzu + süreç içi grid host'u.
- `XsltCraft.Xslt.Saxon` — Saxon-HE 12.10 (IKVM; tek IKVM/Maven projesi). `XsltCraft.XsltWorker` (izole worker exe) + `XsltCraft.XsltWorker.Contracts` (IPC).
- **Testler:** `XsltCraft.Tests` (generator/preview/XsltSafety), `XsltCraft.Application.Tests` (AI pipeline + Verify golden), `XsltCraft.Infrastructure.Tests`, `XsltCraft.Xslt.Saxon.Tests`, `XsltCraft.XsltWorker.Tests` (gerçek süreç, parite).

## XSLT motoru (2.0.0 — ADR 007/008)
Üretilen stylesheet **`version="2.0"`**; namespace'ler `n1`/`cbc`/`cac`/`ext` (`urn:oasis:names:specification:ubl:schema:xsd:...`). İki motor:
- **.NET `XslCompiledTransform`** (XSLT 1.0, yalnız `SecureXslt`) — grid önizlemesi (`Xslt:Engine:Grid = DotNet`, süreç içi korumalı), hazır tema hattı, 1.0 ham XSLT.
- **Saxon-HE 12.10** (`IKVM.Maven.Sdk 1.12.2`, Maven + SHA-256 + lock; XSLT 2.0 / XPath 2.0 tavanı) — `version="2.0"` ham XSLT ve `validate-xslt` (`Xslt:Engine:Raw = Auto`).
- Kullanıcı XSLT'si (Raw, Validate, UserTemplate) **izole worker havuzunda** (`XsltWorker:PoolSize` 2) çalışır. Generator çıktısı iki motorda aynı sonucu veren XPath 1.0 altkümesidir (`XPathPortability`); indirilen `.xslt` 1.0-taşınabilir.

## AI (opsiyonel, feature-flagged)
- **Birincil:** Ollama `qwen2.5-coder:3b` (`Ai:Ollama`).
- **Yedek:** Gemini `gemini-2.5-flash` (`Ai:Gemini:Enabled`, varsayılan kapalı).
- **Anthropic/Claude sağlayıcısı KODDA YOK.** Kayıtlı sağlayıcılar: `OllamaAssistantProvider`, `GeminiAssistantProvider`; orchestrator `AiProviderOrchestrator`.
- NDJSON streaming (`PipeWriter` + `FlushAsync`), iki ayrı timeout (connect + first-token), per-user rate limit + günlük token bütçesi. Mid-stream fallback yapılmaz.
- Prompt'lar embedded markdown (`XsltCraft.Application/Prompts/`): Core/* + Patterns/* (8 UBL-TR pattern pack).

## Database & Storage
PostgreSQL 16 · EF Core · Npgsql. Storage: `IStorageService` — dev `LocalStorageService`, prod `S3StorageService`/MinIO (S3 uyumlu). `.xslt` ve görseller storage'a yazılır; DB yalnız `storagePath` tutar (istisna: `UserXsltTemplate` içeriği DB'de).

## Auth
JWT (15 dk access, yalnız bellekte) + HttpOnly refresh-token rotation (DB'de SHA-256 özeti, `Path=/api/auth`; 2 sa hareketsizlik / 12 sa mutlak oturum, rotation reuse tespiti) + Google OAuth 2.0. Inactive kullanıcı login/refresh'te bloklanır.

## CI/CD & Test
GitHub Actions: `ci.yml` (frontend lint/build/test + backend format/build/test + non-blocking `eval` job), `backend.yml`/`frontend.yml` (path-gated), `release.yml` (tag). Backend: NuGet/Maven/IKVM önbelleği, `--locked-mode` restore, filtre `Category!=Eval&Category!=Measurement&Category!=Corpus`; worker entegrasyon testleri bloke eder. **Dev `docker-compose.yml` = Postgres + MinIO + minio-init** (uygulama varsayılan storage'ı Local; MinIO S3 testleri için hazır). Prod stack: `docker-compose.prod.yml` + Nginx + MinIO; backend imajı worker'ı `/app/publish/xsltworker`'a alır (~627 MB, `-r linux-x64`, `init`, `cap_drop: ALL`, `no-new-privileges`, `pids_limit` 512, `mem_limit` 5g).
