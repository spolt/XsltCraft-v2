# CLAUDE.md — XsltCraft

> Bu dosya her oturumda otomatik yüklenir. Kısa tutulur; **tek doğru kaynak** budur.
> Derin bağlam: `docs/ecc/context/*` · Mimari: `docs/ecc/architecture/*` · Kararlar: `docs/ecc/decisions/*` · Standartlar: `docs/ecc/standards.md`

## Proje
Türk e-Fatura / e-İrsaliye (UBL-TR 2.1) için **low-code XSLT şablon tasarımcısı** + opsiyonel AI asistanı. Kullanıcı A4 grid canvas'a blok sürükler, XML ağacından XPath bağlar, UBL-TR kurallarına göre doğrular ve production-hazır `.xslt` indirir.

## Tech stack (kod-gerçeği)
- **Frontend:** React 19, TypeScript 5.9, Vite 8, Zustand, TailwindCSS 4, @dnd-kit, React Router 7, Monaco Editor, lucide-react.
- **Backend:** .NET 10, ASP.NET Core Web API, **Clean Architecture** — `XsltCraft.Api` / `XsltCraft.Application` / `XsltCraft.Domain` / `XsltCraft.Infrastructure`. XSLT üretimi XSLT 2.0 hedefler; önizleme **Saxon HE 10.9** ile render edilir.
- **AI:** Birincil **Ollama `qwen2.5-coder:3b`**; yedek **Gemini `gemini-2.5-flash`** (`Ai:Gemini:Enabled`). NDJSON streaming, embedded markdown prompt'lar. **Anthropic sağlayıcısı YOKTUR** — kodda yalnız `OllamaAssistantProvider` + `GeminiAssistantProvider` kayıtlı (`AiProviderOrchestrator`).
- **DB:** PostgreSQL 16, EF Core, Npgsql. **Storage:** `IStorageService` soyutlaması — `LocalStorageService` (dev), `S3StorageService`/MinIO (prod).
- **Auth:** JWT (15 dk) + HttpOnly refresh-token rotation (30 gün) + Google OAuth 2.0.
- **Test:** xUnit 2.9, Verify.Xunit golden snapshot. `XsltCraft.Tests` → generator testleri; `XsltCraft.Application.Tests` → AI pipeline + golden.

## Kritik kısıtlar
- UBL-TR 2.1 namespace'lerini **asla bozma** (`n1`/`cbc`/`cac`/`ext`, `urn:oasis:names:specification:...`). Üretilen stylesheet `version="2.0"`.
- GİB: çıktı UTF-8, makul boyut (~250KB hedef), QR/ETTN bütünlüğü.
- **XXE/XSLT-injection guard zorunlu** her XML/XSLT okuma noktasında (`DtdProcessing.Prohibit`, `XmlResolver=null`, `XsltSettings(enableDocumentFunction:false, enableScript:false)`). Detay: `docs/ecc/context/constraints.md`.

## Standartlar (özet)
Application katmanı Infrastructure'a **bağımlı olmaz**; interface tercih et; static service'ten kaçın; feature-based klasör; dosyaları küçük tut. Tamamı: `docs/ecc/standards.md`.

## Agent / eval
- `.claude/agents/`: `reviewer`, `security-reviewer`, `test-engineer`.
- XSLT eval harness: `backend/XsltCraft.Tests/Eval/` (`[Trait("Category","Eval")]`), case'ler `docs/ecc/evaluations/`. Lokal: `dotnet test --filter "Category=Eval"`. CI'da non-blocking.
