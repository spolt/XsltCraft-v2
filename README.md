<div align="center">

# XsltCraft

**Low-code XSLT template designer for Turkish e-Invoice (e-Fatura) and e-Waybill (e-İrsaliye)**

![.NET](https://img.shields.io/badge/.NET-10-512BD4?style=flat&logo=dotnet&logoColor=white)
![React](https://img.shields.io/badge/React-19.2-61DAFB?style=flat&logo=react&logoColor=black)
![TypeScript](https://img.shields.io/badge/TypeScript-5.9-3178C6?style=flat&logo=typescript&logoColor=white)
![Vite](https://img.shields.io/badge/Vite-8-646CFF?style=flat&logo=vite&logoColor=white)
![Monaco Editor](https://img.shields.io/badge/Monaco_Editor-4.7-0078D4?style=flat&logo=visual-studio-code&logoColor=white)

![FlexLayout](https://img.shields.io/badge/FlexLayout-0.8-555555?style=flat)
![License](https://img.shields.io/badge/License-MIT-green?style=flat)
![Status](https://img.shields.io/badge/status-active-brightgreen?style=flat)

</div>

---

XsltCraft is a web-based platform that lets you visually design XSLT print templates for UBL 2.1 XML documents — no XSLT knowledge required. Drag blocks onto a canvas, bind XPath values from your XML tree, and download a production-ready `.xslt` file.

---

## Features

- **Visual block editor** — 14 block types: Table, For-Each, If/Choose, Image, Text, Heading, Paragraph, Divider, Spacer, Document Info, Totals, Notes, Bank Info, QR/ETTN, Party Info
- **Live preview** — upload an XML file, see the rendered HTML in real time as you edit
- **XPath binding** — click any node in the XML tree explorer to bind it to a block field; resolved values shown inline
- **XSLT generation** — block tree compiles to a valid, namespace-correct XSLT 1.0 stylesheet (UBL 2.1 namespaces auto-included)
- **Developer mode** — edit the raw XSLT in a Monaco editor with live preview
- **Free themes** — admin-curated `.xslt` templates available to all users at no cost
- **Asset management** — upload logos and signatures (PNG, JPG, SVG); embedded in the generated XSLT as base64
- **Auth** — email/password + Google OAuth, JWT + HttpOnly refresh tokens

---

## Tech stack

| Layer | Technologies |
|-------|-------------|
| Frontend | React 19, TypeScript, Vite, Zustand, TailwindCSS 4, @dnd-kit, Axios |
| Backend | .NET 10, ASP.NET Core Web API, Clean Architecture (Api / Application / Domain / Infrastructure) |
| Database | PostgreSQL 16, EF Core, Npgsql |
| Storage | `IStorageService` abstraction — `LocalStorageService` (dev), `S3StorageService` (prod) |
| Auth | JWT (15 min access token), refresh token rotation (30 days), Google OAuth 2.0 |
| CI | GitHub Actions — `dotnet build` + `dotnet test` / `npm install` + `npm run build` |

---

## Project structure

```
XsltCraft-v2/
├── backend/
│   └── XsltCraft.sln
│       ├── XsltCraft.Api            # ASP.NET Core Web API, controllers
│       ├── XsltCraft.Application    # Services, XSLT generator, use cases
│       ├── XsltCraft.Domain         # Entities, interfaces
│       └── XsltCraft.Infrastructure # EF Core, storage services, DB context
├── frontend/
│   └── xsltcraft-ui/               # Vite + React app
│       └── src/
│           ├── store/               # Zustand stores (auth, editor, xml)
│           ├── services/            # API service layer (Axios)
│           ├── types/               # Block and template TypeScript types
│           └── pages/               # Route-level components
├── storage/
│   ├── themes/                      # Local XSLT theme files (dev)
│   └── assets/                      # Local uploaded assets (dev)
├── .github/workflows/               # CI pipelines
├── docker-compose.yml
├── ROADMAP.md
├── HANDOFF.md
└── XsltCraft_PRD_v1_2.md
```

---

## Getting started

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- [Node.js 20+](https://nodejs.org)
- [Docker Desktop](https://www.docker.com/products/docker-desktop)

### 1 — Start the database

```bash
docker compose up -d
```

This starts PostgreSQL 16 on port `5432` and applies EF Core migrations automatically on first run.

### 2 — Start the backend

```bash
cd backend
dotnet run --project XsltCraft.Api
```

API will be available at `https://localhost:7001`.

### 3 — Start the frontend

```bash
cd frontend/xsltcraft-ui
npm install
npm run dev
```

App will be available at `http://localhost:5173`.

### 4 — Seed admin user

On first run, the backend seeds an admin user from `appsettings.Development.json`:

```json
"Admin": {
  "Email": "admin@example.com"
}
```

Set the password via dotnet user-secrets:

```bash
cd backend/XsltCraft.Api
dotnet user-secrets set "Admin:Password" "YourSecurePassword123"
```

---

## Environment variables

Key settings in `appsettings.Development.json`:

| Key | Description |
|-----|-------------|
| `ConnectionStrings:DefaultConnection` | PostgreSQL connection string |
| `Jwt:SecretKey` | JWT signing key |
| `Jwt:Issuer` / `Jwt:Audience` | JWT issuer / audience |
| `Google:ClientId` / `Google:ClientSecret` | Google OAuth credentials (user-secrets) |
| `Storage:Provider` | `"Local"` (dev) or `"S3"` (prod) |
| `Storage:LocalBasePath` | Base path for local file storage |

---

## Block types

| Block | Description |
|-------|-------------|
| `Table` | Tabular data with configurable columns |
| `ForEach` | Loop container over an XPath collection |
| `Conditional` | If / choose logic (all comparison operators) |
| `Image` | Asset or base64-embedded image |
| `Text` | Inline text with XPath binding |
| `Heading` | Section heading |
| `Paragraph` | Multi-line text block |
| `Divider` | Horizontal rule |
| `Spacer` | Vertical whitespace |
| `DocumentInfo` | Invoice date, number, type fields |
| `Totals` | Tax subtotals and payable amount |
| `Notes` | Free-text note lines |
| `BankInfo` | Supplier bank account details |
| `QR / ETTN` | QR code and ETTN display |
| `PartyInfo` | Seller / buyer party details (3 label styles) |

---

## API overview

| Method | Path | Description |
|--------|------|-------------|
| `POST` | `/api/auth/register` | Register new user |
| `POST` | `/api/auth/login` | Login, returns JWT + sets refresh cookie |
| `POST` | `/api/auth/refresh` | Rotate refresh token |
| `POST` | `/api/auth/google` | Google OAuth login |
| `GET` | `/api/templates` | List free themes (public) |
| `GET` | `/api/templates/my` | List current user's templates |
| `POST` | `/api/templates` | Create new template |
| `PUT` | `/api/templates/:id` | Update block tree |
| `GET` | `/api/templates/:id/download` | Download generated `.xslt` |
| `POST` | `/api/preview` | Generate HTML preview from block tree + XML |
| `POST` | `/api/preview/raw` | Preview from raw XSLT + XML (dev mode) |
| `POST` | `/api/assets/upload` | Upload image asset |
| `POST` | `/api/admin/themes` | Upload free theme (admin only) |

---

## Roadmap

| Phase | Title | Status |
|-------|-------|--------|
| 1 | Monorepo infrastructure & auth | ✅ Done |
| 2 | Storage layer & free theme management | ✅ Done |
| 3 | Block editor & XSLT generation engine | ✅ Done |
| 4 | XML binding & live preview | ✅ Done |
| 5 | Template management, assets & payments | ✅ Done (payment deferred) |
| 6 | Production readiness & S3 migration | 🔄 In progress |

See [ROADMAP.md](./ROADMAP.md) for detailed task breakdowns.

---


## License

MIT — see [LICENSE](./LICENSE) for details.

## Author

Developed by **Semih Polat**
