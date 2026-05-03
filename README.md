<div align="center">

# XsltCraft

**Low-code XSLT template designer for Turkish e-Invoice (e-Fatura) and e-Waybill (e-İrsaliye) — with AI assistance**

![Version](https://img.shields.io/badge/version-1.1.0-blue?style=flat)
![.NET](https://img.shields.io/badge/.NET-10-512BD4?style=flat&logo=dotnet&logoColor=white)
![React](https://img.shields.io/badge/React-19.2-61DAFB?style=flat&logo=react&logoColor=black)
![TypeScript](https://img.shields.io/badge/TypeScript-5.9-3178C6?style=flat&logo=typescript&logoColor=white)
![Vite](https://img.shields.io/badge/Vite-8-646CFF?style=flat&logo=vite&logoColor=white)
![Saxon HE](https://img.shields.io/badge/Saxon_HE-10.9-FF6F00?style=flat)
![Monaco Editor](https://img.shields.io/badge/Monaco_Editor-4.7-0078D4?style=flat&logo=visual-studio-code&logoColor=white)

![TailwindCSS](https://img.shields.io/badge/Tailwind-4-38B2AC?style=flat&logo=tailwindcss&logoColor=white)
![License](https://img.shields.io/badge/License-MIT-green?style=flat)
![Status](https://img.shields.io/badge/status-active-brightgreen?style=flat)

</div>

---

XsltCraft is a web-based platform that lets you visually design XSLT print templates for **UBL-TR 2.1** XML documents (e-Fatura, e-Arşiv, e-İrsaliye) — no XSLT knowledge required. Drag blocks onto an A4 grid canvas, bind XPath values from your XML tree, validate against UBL-TR business rules, and download a production-ready `.xslt` file. Optional AI assistant (Ollama / Gemini) can refactor selections and chat in Turkish about your transformation.

---

## Features

### Visual editor
- **V2 grid canvas** — A4 page with free X/Y placement (mm), pixel-perfect resize handles, keyboard nudge (1 mm / 5 mm with `Shift`), undo/redo (`Ctrl+Z` / `Ctrl+Y`), block duplication (`Ctrl+D`), z-index overlay layer.
- **Block library** — Table, ForEach, Conditional (If / Choose), Image, Text, Heading, Paragraph, Divider, Spacer, DocumentInfo, LegalMonetaryTotal, Notes, BankInfo, QR / ETTN, GİB karekod & logo, Supplier / Customer party blocks. Auto-height by default; per-block margins (mm).
- **Live preview** — upload an XML file (or use the built-in default UBL invoice) and see rendered HTML update as you edit. Zoom 50–200 %.
- **XPath binding** — click any node in the XML tree explorer to bind it to a block field; resolved values shown inline.
- **XSLT generation** — block tree compiles to a valid, namespace-correct **XSLT 2.0** stylesheet (Saxon HE 10.9; UBL-TR namespaces auto-included). Image assets embedded as base64 — output is fully self-contained.

### Developer tools
- **XSLT editor (dev mode)** — raw XSLT in a Monaco editor, live preview, auto-format, folding, problems panel.
- **XPath Console** — interactive XPath 1.0 evaluator with namespace awareness; results show node type, name, value.
- **Profiler** — transform-time measurement, slow-region detection, performance hints.
- **Problems panel** — XML parsing errors + XSLT validation errors + UBL-TR business-rule violations in a tabbed view with line-number anchors.
- **Snippet library** — personal XSLT snippets with `Ctrl+Space` autocomplete; admin-curated public snippets shared with all users.
- **Keyboard shortcuts** — `ShortcutsDialog` lists every editor binding.

### UBL-TR validation
- **40+ business rules** — `UblTrBusinessRuleService` enforces UBL-TR 2.1 mandatory fields, monetary consistency, tax checks. Violations listed with line numbers.

### AI assistant *(optional, feature-flagged)*
- **Chat panel** — UBL-TR–aware Turkish AI assistant docked to the editor; NDJSON streaming, cancel via `AbortController`, toast notifications.
- **Refactor dialog** — select an XSLT range, ask for a transformation, preview the diff, apply on approval.
- **Provider orchestrator** — primary: Ollama (`qwen2.5-coder:7b`); optional cloud fallback: Gemini. Separate timeouts for connect / first-token; per-user rate limit and daily token budget.
- **Prompt registry** — 8 UBL-TR pattern packs (`InvoiceNote`, `InvoiceLine`, `LegalMonetaryTotal`, `InvoiceHeader`, `SupplierPartyAddress`, `CustomerPartyAddress`, `SupplierPartyPerson`, `CustomerPartyPerson`) loaded from embedded markdown; trigger-based selection with Turkish character folding (max 4 patterns / request).
- **Admin AI page** — `/admin/ai` toggles the feature flag, picks the active provider, shows provider health and per-user daily usage.

### Templates & sharing
- **Personal drafts & templates** — save, version, preview (split-panel iframe) and edit; bulk multi-select / delete.
- **Free theme library** — admin-curated `.xslt` themes available to every user; one-click "use" flow.
- **Asset management** — upload logos / signatures (PNG, JPG, SVG); embedded as base64 in generated XSLT.

### Auth & admin
- **Authentication** — username + password (3–30 chars) or Google OAuth 2.0; JWT (15 min access) + HttpOnly refresh-token rotation (30 days). Inactive users blocked at login / refresh.
- **Admin panel** — `/admin/users` (roles: Admin / Editor / User, activation, password reset, activity stats), `/admin/themes` (theme curation), `/admin/snippets` (public snippet library), `/admin/ai` (AI flags & usage).

---

## Tech stack

| Layer | Technologies |
|-------|--------------|
| Frontend | React 19, TypeScript 5.9, Vite 8, Zustand, TailwindCSS 4, @dnd-kit, React Router 7, Monaco Editor, lucide-react |
| Backend | .NET 10, ASP.NET Core Web API, Clean Architecture (Api / Application / Domain / Infrastructure), Saxon HE 10.9 (XSLT 2.0 / XPath 2.0) |
| AI | Ollama (`qwen2.5-coder:7b`) primary, Gemini cloud fallback; embedded markdown prompts; NDJSON streaming |
| Database | PostgreSQL 16, EF Core, Npgsql |
| Storage | `IStorageService` abstraction — `LocalStorageService` (dev), `S3StorageService` / **MinIO** (prod, S3-compatible) |
| Auth | JWT + refresh-token rotation, Google OAuth 2.0 |
| Tests | xUnit 2.9, Verify.Xunit (golden snapshots), 26 prompt-pipeline tests |
| CI / CD | GitHub Actions (`ci.yml`, `release.yml`); production Docker stack (`docker-compose.prod.yml`, `nginx.conf`, `update.sh`) |

---

## Project structure

```
XsltCraft/
├── backend/
│   └── XsltCraft.slnx
│       ├── XsltCraft.Api               # ASP.NET Core Web API, controllers, rate limiting
│       ├── XsltCraft.Application       # Services, XSLT generator (V1 + V2), AI prompt pipeline
│       │   ├── Ai/                     # PromptRegistry, PatternSelector, AiMode, BuildMessages
│       │   └── Prompts/                # Embedded markdown: Core/* + Patterns/*
│       ├── XsltCraft.Application.Tests # xUnit + Verify golden snapshots
│       ├── XsltCraft.Domain            # Entities (User, Template, FeatureFlag, UserAiUsage…)
│       └── XsltCraft.Infrastructure    # EF Core, storage providers, AI provider orchestrator
├── frontend/
│   └── xsltcraft-ui/                   # Vite + React 19 app
│       └── src/
│           ├── store/                  # Zustand stores (auth, editor, xml, toast)
│           ├── services/               # API service layer (Axios + NDJSON streaming)
│           ├── components/             # Editor, AI panel, admin, layout
│           └── pages/                  # Route-level components (Dashboard, Editor, Admin, About…)
├── storage/                            # Local dev: themes/, assets/
├── .github/workflows/                  # ci.yml, release.yml
├── docker-compose.yml                  # Dev: Postgres
├── docker-compose.prod.yml             # Prod: Postgres + MinIO + Nginx + API + UI
├── CHANGELOG.md
├── ROADMAP.md
└── XsltCraft_PRD_v2.md
```

---

## Getting started

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- [Node.js 20+](https://nodejs.org)
- [Docker Desktop](https://www.docker.com/products/docker-desktop)
- *(Optional)* [Ollama](https://ollama.ai) running `qwen2.5-coder:7b` for the AI assistant

### 1 — Start the database

```bash
docker compose up -d
```

PostgreSQL 16 on port `5432`; EF Core migrations applied automatically on first run.

### 2 — Start the backend

```bash
cd backend
dotnet run --project XsltCraft.Api
```

API at `https://localhost:7001`.

### 3 — Start the frontend

```bash
cd frontend/xsltcraft-ui
npm install
npm run dev
```

App at `http://localhost:5173`.

### 4 — Seed admin user

On first run, the backend seeds an admin from `appsettings.Development.json`:

```json
"Admin": {
  "Email": "admin@example.com",
  "Username": "admin"
}
```

Set the password via user-secrets:

```bash
cd backend/XsltCraft.Api
dotnet user-secrets set "Admin:Password" "YourSecurePassword123"
```

### 5 — *(Optional)* Enable the AI assistant

```bash
ollama pull qwen2.5-coder:7b
ollama serve
```

Then in `appsettings.Development.json` set the `Ai` section (Ollama base URL, model, `NumCtx: 8192`) and toggle the `Ai` feature flag from `/admin/ai`. Add a Gemini API key under `Ai:Gemini` for cloud fallback.

### 6 — Run tests

```bash
cd backend
dotnet test                          # 26/26 (prompt registry + pattern selector + golden snapshots)
```

---

## Environment variables

Key settings in `appsettings.Development.json`:

| Key | Description |
|-----|-------------|
| `ConnectionStrings:DefaultConnection` | PostgreSQL connection string |
| `Jwt:SecretKey` / `Jwt:Issuer` / `Jwt:Audience` | JWT signing config |
| `Google:ClientId` / `Google:ClientSecret` | Google OAuth (user-secrets) |
| `Storage:Provider` | `"Local"` (dev) or `"S3"` / `"MinIO"` (prod) |
| `Storage:LocalBasePath` | Base path for local file storage |
| `Ai:Provider` | `"Ollama"` (primary) or `"Gemini"` (cloud fallback) |
| `Ai:Ollama:BaseUrl` / `Ai:Ollama:Model` / `Ai:Ollama:NumCtx` | Ollama config (default `NumCtx: 8192`) |
| `Ai:Gemini:ApiKey` / `Ai:Gemini:Model` | Gemini config |
| `Ai:ConnectTimeout` / `Ai:FirstTokenTimeout` | Streaming timeouts |

---

## API overview

### Auth
| Method | Path | Description |
|--------|------|-------------|
| `POST` | `/api/auth/register` | Register (username + email + password) |
| `POST` | `/api/auth/login` | Login by **username**, returns JWT + refresh cookie |
| `POST` | `/api/auth/refresh` | Rotate refresh token |
| `POST` | `/api/auth/google` | Google OAuth login |

### Templates & preview
| Method | Path | Description |
|--------|------|-------------|
| `GET` | `/api/templates` | List free themes (public) |
| `GET` | `/api/templates/my` | List current user's templates |
| `POST` | `/api/templates` | Create new template |
| `PUT` | `/api/templates/:id` | Update block tree |
| `GET` | `/api/templates/:id/download` | Download generated `.xslt` |
| `POST` | `/api/preview` | Generate HTML preview from block tree + XML |
| `POST` | `/api/preview/raw` | Preview from raw XSLT + XML (dev mode) |
| `POST` | `/api/preview/user-template/:id` | Preview a saved V1 / V2 user template |
| `POST` | `/api/assets/upload` | Upload image asset |

### Validation & tooling
| Method | Path | Description |
|--------|------|-------------|
| `POST` | `/api/ubltr/validate` | UBL-TR 2.1 business-rule validation |
| `POST` | `/api/xpath/evaluate` | XPath 1.0 evaluator (namespace-aware) |
| `GET\|POST\|PUT\|DELETE` | `/api/user-snippets` | Personal snippet CRUD |

### AI *(feature-flagged)*
| Method | Path | Description |
|--------|------|-------------|
| `GET` | `/api/ai/status` | Is AI enabled (anonymous) |
| `POST` | `/api/ai/assistant` | Chat (NDJSON streaming, rate-limited) |
| `POST` | `/api/ai/refactor-selection` | Refactor a selected XSLT range |

### Admin *(role: Admin)*
| Method | Path | Description |
|--------|------|-------------|
| `*` | `/api/admin/themes` | Theme library |
| `*` | `/api/admin/snippets` | Public snippet library |
| `*` | `/api/admin/users` | User management (roles, activation, reset password) |
| `*` | `/api/admin/feature-flags/ai` | AI feature flag, provider, health, daily usage |

---

## Documentation

- [CHANGELOG.md](./CHANGELOG.md) — full release history
- [ROADMAP.md](./ROADMAP.md) — upcoming work
- [SETUP_GUIDE.md](./SETUP_GUIDE.md) — detailed environment setup
- [CONTRIBUTING.md](./CONTRIBUTING.md) — contribution guide
- [VERSION_STRATEGY.md](./VERSION_STRATEGY.md) — semver policy
- [HANDOFF.md](./HANDOFF.md) — engineering handoff notes
- [XsltCraft_PRD_v2.md](./XsltCraft_PRD_v2.md) — product requirements

---

## License

MIT — see [LICENSE](./LICENSE) for details.

## Author

Developed by **Semih Polat**
