# XsltCraft Low-Code Platform — Product Requirements Document

**Version:** 1.2  
**Date:** 2026-03-17  
**Author:** Semih Polat  
**Status:** Draft — Ready for Development

### Changelog

| Version | Date | Changes |
|---------|------|---------|
| 1.0 | 2026-03-16 | Initial draft |
| 1.1 | 2026-03-17 | Free themes now support user logo & signature image uploads; image upload is free (not part of paid download) |
| 1.2 | 2026-03-17 | XSLT şablonları filesystem/S3 storage'da tutulur, DB yalnızca path saklar; backend dosyayı storage'dan okuyarak render eder; upload API storage'a yazar + DB'ye path kaydeder; monorepo yapısına geçildi (apps/frontend + apps/backend klasör ayrımı) |

---

## Table of Contents

1. [Executive Summary](#1-executive-summary)
2. [Goals & Success Metrics](#2-goals--success-metrics)
3. [User Personas](#3-user-personas)
4. [User Stories](#4-user-stories)
5. [Application Architecture Overview](#5-application-architecture-overview)
6. [Tech Stack](#6-tech-stack)
7. [Monorepo & Klasör Yapısı](#7-monorepo--klasör-yapısı)
8. [Screen & Page Flows](#8-screen--page-flows)
9. [Block System — Core Specification](#9-block-system--core-specification)
10. [XML Binding & Tree Explorer](#10-xml-binding--tree-explorer)
11. [Template Management](#11-template-management)
12. [Live Preview](#12-live-preview)
13. [Auth & User Management](#13-auth--user-management)
14. [Paid Download Flow](#14-paid-download-flow)
15. [Backend API Contracts](#15-backend-api-contracts)
16. [Data Models](#16-data-models)
17. [Storage Stratejisi](#17-storage-stratejisi)
18. [Non-Functional Requirements](#18-non-functional-requirements)
19. [Out of Scope](#19-out-of-scope)
20. [Glossary](#20-glossary)

---

## 1. Executive Summary

XsltCraft is being evolved from a developer-focused XSLT playground into a **low-code XSLT template design platform**. The new version allows users — including non-developers — to design fully customized XSLT-based document templates (e-Invoice / e-Despatch in Turkish GIB standard) through a **visual block editor**, without writing raw XSLT code.

Users can:
- Start from a ready-made theme (free) or design a new template from scratch using drag-and-drop blocks.
- Upload their own logo and signature images onto ready-made themes (free — no payment required for image uploads).
- Bind XML fields to blocks visually via an XML Tree Explorer.
- Preview the rendered output in real time.
- Download their custom-designed templates (paid — one-time per template).

The platform is fully compatible with the **Turkish GIB e-Fatura (UBL 2.1)** and **e-İrsaliye** XML standards.

**v1.2 Temel Mimari Değişikliği:** XSLT şablon dosyaları artık veritabanında değil, ayrı bir **dosya storage** katmanında (yerel filesystem veya S3-uyumlu nesne deposu) saklanır. Veritabanı yalnızca bu dosyalara ait **storage path** bilgisini tutar. Backend, önizleme ve indirme sırasında dosyayı storage'dan okuyarak işler.

---

## 2. Goals & Success Metrics

### Goals

| # | Goal |
|---|------|
| G1 | Enable non-developer users to create XSLT templates without coding |
| G2 | Reduce template creation time from hours to under 30 minutes |
| G3 | Generate revenue through paid template downloads |
| G4 | Support Turkish e-Fatura and e-İrsaliye XML standards fully |
| G5 | Maintain real-time preview for immediate feedback |
| G6 | XSLT ve asset dosyalarını DB dışında storage'da saklayarak DB boyutunu kontrol altında tutmak |

### Success Metrics

| Metric | Target |
|--------|--------|
| Template creation completion rate | ≥ 70% of started sessions |
| Average time to first preview | ≤ 5 minutes |
| Paid download conversion rate | ≥ 15% of free users |
| Preview render time | ≤ 2 seconds |
| System uptime | ≥ 99.5% |

---

## 3. User Personas

### Persona 1 — Muhasebe Yazılımcısı (ERP Developer)
- **Background:** Develops or maintains ERP/accounting software that generates GIB-compliant invoices.
- **Goal:** Quickly create and customize invoice/despatch print templates for their clients.
- **Pain Point:** Writing XSLT from scratch is time-consuming and error-prone.
- **Tech Level:** Medium-high. Understands XML but prefers a visual tool.

### Persona 2 — KOBİ Sahibi / Muhasebeci
- **Background:** Small business owner or accountant who needs to produce branded e-Fatura printouts.
- **Goal:** Add company logo, signature, and custom layout to a ready-made theme without hiring a developer.
- **Pain Point:** Has no XSLT knowledge. Currently using a default ugly printout.
- **Tech Level:** Low. Comfortable with drag-and-drop tools (e.g., Canva, Word).

### Persona 3 — Yazılım Evi / Entegratör
- **Background:** Software house that builds GIB-integrated systems for multiple clients.
- **Goal:** Build a library of template variants per client and export XSLT files.
- **Pain Point:** Managing multiple template versions manually.
- **Tech Level:** High. Wants efficiency and bulk template management.

---

## 4. User Stories

### Authentication

| ID | Story |
|----|-------|
| US-01 | As a new user, I want to register with email and password so I can save my templates. |
| US-02 | As a returning user, I want to log in and see my previously created templates. |
| US-03 | As a user, I want to reset my password via email. |
| US-04 | As a user, I want to log in with Google (OAuth) for convenience. |

### Template Selection

| ID | Story |
|----|-------|
| US-05 | As a user, I want to browse a library of free ready-made themes and select one as a starting point. |
| US-06 | As a user, I want to preview a theme before selecting it. |
| US-06a | As a user, I want to upload my own logo and signature images onto a free theme so I can personalize it without creating a custom template. |
| US-07 | As a user, I want to create a new blank template from scratch. |
| US-08 | As a user, I want to duplicate an existing template and modify it. |

### Block Editor

| ID | Story |
|----|-------|
| US-09 | As a user, I want to drag blocks from a sidebar panel onto a canvas organized in sections. |
| US-10 | As a user, I want to reorder blocks within a section by dragging them up or down. |
| US-11 | As a user, I want to add a new section (e.g., Header, Body, Footer) to organize my layout. |
| US-12 | As a user, I want to click a block to open its property panel on the right side. |
| US-13 | As a user, I want to delete a block from the canvas with a single action. |
| US-14 | As a user, I want to duplicate a block within the same section. |

### XML Binding

| ID | Story |
|----|-------|
| US-15 | As a user, I want to upload an XML file (e-Fatura or e-İrsaliye) and see its tree structure. |
| US-16 | As a user, I want to click a node in the XML tree to bind it to the selected block's field. |
| US-17 | As a user, I want to type or edit an XPath expression manually in the binding field. |
| US-18 | As a user, I want to see the resolved value of a bound XPath expression in the property panel. |

### Block Configuration

| ID | Story |
|----|-------|
| US-19 | As a user, I want to type static text in a text block or bind it to an XML field. |
| US-20 | As a user, I want to configure the columns of a table block and bind each column to an XML field. |
| US-21 | As a user, I want to upload a logo or signature image and place it in an image block (free — no payment required for image uploads, including on free themes). |
| US-22 | As a user, I want to configure an if/choose block's condition by selecting an XML field and a comparison value. |
| US-23 | As a user, I want to configure a for-each block to iterate over a repeating XML node (e.g., InvoiceLine). |
| US-24 | As a user, I want to style a block (font size, bold, color, alignment, border) from the property panel. |
| US-25 | As a user, I want to add a divider or spacer block between other blocks for visual separation. |

### Preview

| ID | Story |
|----|-------|
| US-26 | As a user, I want to see a live HTML preview of my template rendered with the uploaded XML data. |
| US-27 | As a user, I want the preview to refresh automatically when I make changes to a block. |
| US-28 | As a user, I want to switch the preview XML between different uploaded XML files. |

### Download & Payment

| ID | Story |
|----|-------|
| US-29 | As a user, I want to download a free theme as an XSLT file at no cost, even if I have uploaded my own logo and/or signature images onto it. |
| US-30 | As a user, I want to download my custom-designed template after completing a one-time payment. |
| US-31 | As a user, I want to see the price clearly before initiating payment. |
| US-32 | As a user, I want to receive a payment confirmation email and a download link. |
| US-33 | As a user, I want to re-download a template I have already paid for from my profile. |

---

## 5. Application Architecture Overview

```
┌─────────────────────────────────────────────────────────────┐
│                        Frontend (React 19)                  │
│                                                             │
│  ┌──────────────┐  ┌──────────────────┐  ┌──────────────┐  │
│  │ Template     │  │  Block Editor    │  │  Preview     │  │
│  │ Library      │  │  (Canvas +       │  │  Panel       │  │
│  │ Panel        │  │   Sections)      │  │  (iframe)    │  │
│  └──────────────┘  └──────────────────┘  └──────────────┘  │
│                           │                                 │
│  ┌──────────────┐  ┌──────────────────┐                     │
│  │ XML Tree     │  │  Block Property  │                     │
│  │ Explorer     │  │  Panel (Right)   │                     │
│  └──────────────┘  └──────────────────┘                     │
│                           │                                 │
│              transformService.ts / apiService.ts            │
└───────────────────────────┬─────────────────────────────────┘
                            │ REST / JSON
┌───────────────────────────▼─────────────────────────────────┐
│                    Backend (.NET 10 API)                     │
│                                                             │
│  ┌─────────────────┐  ┌──────────────┐  ┌───────────────┐  │
│  │ XSLT Transform  │  │ Template     │  │ Auth / User   │  │
│  │ Engine          │  │ CRUD         │  │ Management    │  │
│  └─────────────────┘  └──────────────┘  └───────────────┘  │
│                                                             │
│  ┌─────────────────┐  ┌──────────────┐                      │
│  │ Payment         │  │ Storage      │                      │
│  │ Integration     │  │ Service      │                      │
│  └─────────────────┘  └──────────────┘                      │
│              │                  │                           │
│    DB: path  │          storage │ (filesystem / S3)         │
└──────────────┼──────────────────┼──────────────────────────-┘
               │                  │
   ┌───────────▼──────┐  ┌────────▼──────────────────────┐
   │    PostgreSQL     │  │    File Storage               │
   │  (metadata +      │  │  (XSLT dosyaları,             │
   │   path referansı) │  │   asset görselleri)           │
   └───────────────────┘  └───────────────────────────────┘
```

### Key Design Decisions

- **Block editor state** is maintained in React state as a JSON document (the "block tree"). This JSON is serialized and sent to the backend for XSLT generation.
- **XSLT is never shown to the user** unless they explicitly request "Advanced Mode."
- **Live preview** is achieved by sending the current block tree + uploaded XML to the backend, which generates XSLT on the fly (in-memory), applies it to the XML, and returns rendered HTML. *Free theme preview için backend storage'daki .xslt dosyasını okur.*
- The backend **generates valid XSLT** from the block tree JSON at preview time and at download time.
- **XSLT dosyaları** (hem hazır free theme şablonları hem de kullanıcıların indirdiği custom şablonlar) storage'da `.xslt` uzantılı dosya olarak saklanır; veritabanında yalnızca `storagePath` kolonu tutulur.
- **Asset dosyaları** (logo, imza, kaşe görselleri) da aynı storage katmanında saklanır; `Asset` tablosu yalnızca `filePath`'i tutar.

---

## 6. Tech Stack

### Frontend

| Layer | Technology | Version |
|-------|-----------|---------|
| Framework | React | 19 |
| Language | TypeScript | 5.9 |
| Build Tool | Vite | 8 |
| Block Editor Drag & Drop | `@dnd-kit/core` + `@dnd-kit/sortable` | latest |
| Layout | flexlayout-react | 0.8 |
| Code Editor (Advanced Mode) | Monaco Editor | 4.7 |
| XML Tree Rendering | Custom TreeView component | — |
| Styling | TailwindCSS | 4 |
| State Management | Zustand | latest |
| HTTP Client | Axios | latest |

### Backend

| Layer | Technology | Version |
|-------|-----------|---------|
| Runtime | .NET | 10 |
| XSLT Engine | `System.Xml.Xsl.XslCompiledTransform` | built-in |
| Auth | ASP.NET Identity + JWT | built-in |
| ORM | Entity Framework Core | latest |
| Database | PostgreSQL | 16 |
| File Storage | Yerel filesystem (dev) / S3-uyumlu nesne deposu (prod) | — |
| Storage Abstraction | `IStorageService` arayüzü (LocalStorageService / S3StorageService) | custom |
| Payment | TBD (iyzico / Stripe) | — |

---

## 7. Monorepo & Klasör Yapısı

Proje, hem frontend hem backend kodunu tek bir Git deposunda barındıran **monorepo** yapısıyla geliştirilir.

```
xsltcraft/                          ← repo kökü
├── apps/
│   ├── frontend/                   ← React 19 / Vite uygulaması
│   │   ├── public/
│   │   ├── src/
│   │   │   ├── assets/
│   │   │   ├── components/
│   │   │   │   ├── editor/         ← block canvas, palette, property panel
│   │   │   │   ├── preview/        ← iframe preview panel
│   │   │   │   ├── xml-tree/       ← XML Tree Explorer
│   │   │   │   └── shared/         ← ortak UI bileşenleri
│   │   │   ├── hooks/
│   │   │   ├── pages/              ← route-level sayfalar
│   │   │   ├── services/
│   │   │   │   ├── apiService.ts
│   │   │   │   └── transformService.ts
│   │   │   ├── store/              ← Zustand store dosyaları
│   │   │   ├── types/
│   │   │   └── main.tsx
│   │   ├── index.html
│   │   ├── vite.config.ts
│   │   ├── tsconfig.json
│   │   └── package.json
│   │
│   └── backend/                    ← .NET 10 Web API
│       ├── XsltCraft.Api/          ← ASP.NET Core başlangıç projesi
│       │   ├── Controllers/
│       │   │   ├── AuthController.cs
│       │   │   ├── TemplatesController.cs
│       │   │   ├── PreviewController.cs
│       │   │   ├── AssetsController.cs
│       │   │   └── PaymentsController.cs
│       │   ├── Program.cs
│       │   └── appsettings.json
│       ├── XsltCraft.Application/  ← use case / servis katmanı
│       │   ├── Templates/
│       │   ├── Preview/
│       │   ├── Assets/
│       │   └── Payments/
│       ├── XsltCraft.Domain/       ← entity'ler, domain modelleri
│       │   ├── Entities/
│       │   └── Enums/
│       ├── XsltCraft.Infrastructure/ ← EF Core, storage, e-posta
│       │   ├── Persistence/
│       │   │   ├── AppDbContext.cs
│       │   │   └── Migrations/
│       │   ├── Storage/
│       │   │   ├── IStorageService.cs
│       │   │   ├── LocalStorageService.cs  ← dev ortamı
│       │   │   └── S3StorageService.cs     ← prod ortamı
│       │   └── Email/
│       └── XsltCraft.sln
│
├── storage/                        ← yerel geliştirme storage kökü (gitignore'da)
│   ├── themes/                     ← hazır free theme .xslt dosyaları
│   └── assets/                     ← kullanıcı görselleri (logo, imza vb.)
│
├── .github/
│   └── workflows/
│       ├── frontend.yml
│       └── backend.yml
├── docker-compose.yml              ← PostgreSQL + API + Frontend birlikte ayağa kaldırır
└── README.md
```

### Klasör Kuralları

| Kural | Açıklama |
|-------|----------|
| `apps/frontend` ve `apps/backend` birbirinden bağımsız çalışır | Her birinin kendi `package.json` / `.csproj` dosyası vardır. |
| Ortak tip tanımları | `apps/frontend/src/types/` altındaki modeller backend DTO'larıyla senkronize tutulur. |
| `storage/` yerel dosya deposu | Geliştirme ortamında `storage/themes/` ve `storage/assets/` klasörleri kullanılır. Prod'da bu klasöre yazan/okuyan kod `S3StorageService` ile değiştirilir. |
| `docker-compose.yml` | Tek komutla (`docker compose up`) tüm servisleri ayağa kaldırır: PostgreSQL 16, .NET API, React dev server. |

---

## 8. Screen & Page Flows

### 8.1 Overall Navigation Flow

```
Landing Page (/)
    │
    ├── Login / Register (/auth)
    │       │
    │       └── Dashboard (/dashboard)
    │               │
    │               ├── Template Library (/templates)
    │               │       └── Select Theme → Editor
    │               │
    │               ├── New Template (/editor/new)
    │               │
    │               └── My Templates (/my-templates)
    │                       └── Open → Editor
    │
    └── Editor (/editor/:templateId)
            │
            ├── Block Canvas (center)
            ├── Block Palette (left)
            ├── XML Tree Explorer (left, tab)
            ├── Block Property Panel (right)
            └── Preview Panel (right, tab or split)
                    │
                    └── Download → Payment Flow
```

### 8.2 Editor Layout

```
┌─────────────────────────────────────────────────────────────────┐
│  [XsltCraft]   [Template Name ✎]   [Preview XML ▾]   [Save] [↓Download]  │
├──────────────┬──────────────────────────────┬────────────────────┤
│              │                              │                    │
│  BLOCK       │   CANVAS                     │  PROPERTY PANEL    │
│  PALETTE     │                              │  (Block Settings)  │
│              │  ┌─ SECTION: Header ───────┐ │                    │
│  ─────────   │  │  [Logo Block]           │ │  [Selected Block   │
│  Layout      │  │  [Seller Info Block]    │ │   Properties Here] │
│  ─────────   │  └─────────────────────────┘ │                    │
│  [Header]    │                              │  ─────────────     │
│  [Text]      │  ┌─ SECTION: Buyer ────────┐ │  XML BINDING       │
│  [Paragraph] │  │  [Buyer Info Block]     │ │                    │
│  ─────────   │  └─────────────────────────┘ │  XPath: _______    │
│  Data        │                              │  Value: [resolved] │
│  ─────────   │  ┌─ SECTION: Table ────────┐ │                    │
│  [Table]     │  │  [Invoice Line Table]   │ │                    │
│  [Loop]      │  └─────────────────────────┘ │                    │
│  [If/Choose] │                              │                    │
│  ─────────   │  ┌─ SECTION: Summary ──────┐ │  [XML TREE TAB]    │
│  Media       │  │  [Totals Block]         │ │                    │
│  ─────────   │  └─────────────────────────┘ │  ▶ Invoice         │
│  [Logo]      │                              │    ▶ Supplier      │
│  [Signature] │  ┌─ SECTION: Footer ───────┐ │      ▷ Name       │
│  ─────────   │  │  [Notes Block]          │ │      ▷ TaxID      │
│  Decoration  │  │  [Bank Info Block]      │ │    ▶ Lines         │
│  ─────────   │  └─────────────────────────┘ │      ▷ Line[1]    │
│  [Divider]   │                              │                    │
│  [Spacer]    │  [+ Add Section]             │                    │
│              │                              │                    │
└──────────────┴──────────────────────────────┴────────────────────┘
│                        PREVIEW PANEL (below or tab)              │
│  ┌──────────────────────────────────────────────────────────┐    │
│  │  [Live rendered HTML preview — iframe]                   │    │
│  └──────────────────────────────────────────────────────────┘    │
└─────────────────────────────────────────────────────────────────┘
```

### 8.3 Payment & Download Flow

```
User clicks [↓ Download]
        │
        ▼
Is template a free theme?
   YES → Backend storage'dan .xslt dosyasını okur → Download immediately (free)
   NO (custom) → Has user already paid for this template?
                    YES → Backend storage'dan .xslt dosyasını okur → Re-download
                    NO  → Show Payment Modal
                              │
                              ▼
                          Price shown clearly
                          [Confirm & Pay]
                              │
                              ▼
                          Payment Gateway (TBD)
                              │
                         ┌────┴────┐
                       Success   Failure
                         │          │
                    Record payment  Show error
                    Send email      Allow retry
                    Enable download
                         │
                         ▼
                    Backend: block tree → XSLT oluşturur
                    Storage'a yazar → DB'ye path kaydeder
                    Trigger browser download
```

---

## 9. Block System — Core Specification

### 9.1 Block Architecture

Each block in the editor is represented as a **JSON node** in the template's block tree. The backend reads this JSON to generate the corresponding XSLT snippet for each block, then assembles all snippets into a complete, valid XSLT stylesheet.

**Block JSON Structure (base):**

```json
{
  "id": "uuid",
  "type": "BlockType",
  "sectionId": "uuid",
  "order": 1,
  "styles": {
    "fontSize": "12px",
    "fontWeight": "normal",
    "color": "#000000",
    "textAlign": "left",
    "borderTop": false,
    "borderBottom": false,
    "marginTop": "0px",
    "marginBottom": "8px",
    "padding": "4px"
  },
  "config": { /* block-specific config */ }
}
```

---

### 9.2 Block Types

#### BLOCK-01: Text Block

Renders a single line or paragraph of text. Can be static or bound to an XML field.

**Config:**
```json
{
  "type": "Text",
  "config": {
    "content": "Static text here",
    "binding": {
      "xpath": "//cac:AccountingSupplierParty/cac:Party/cac:PartyName/cbc:Name",
      "fallback": "—"
    },
    "isStatic": false
  }
}
```

**XSLT Output:**
```xml
<xsl:value-of select="//cac:AccountingSupplierParty/cac:Party/cac:PartyName/cbc:Name"/>
```

---

#### BLOCK-02: Heading Block

Same as Text Block but with heading-level styling (H1–H4).

**Config:**
```json
{
  "type": "Heading",
  "config": {
    "level": "H2",
    "content": "SAYIN",
    "isStatic": true
  }
}
```

---

#### BLOCK-03: Paragraph Block

Multi-line text block. Supports mixed static text and XML-bound inline values.

**Config:**
```json
{
  "type": "Paragraph",
  "config": {
    "lines": [
      { "isStatic": true, "content": "Tel: " },
      { "isStatic": false, "xpath": "//cbc:Telephone" },
      { "isStatic": true, "content": " Fax: " },
      { "isStatic": false, "xpath": "//cbc:Telefax" }
    ]
  }
}
```

---

#### BLOCK-04: Table Block

Renders a dynamic table driven by a for-each loop over a repeating XML node (e.g., `InvoiceLine`). Each column maps to an XPath expression relative to the loop context node.

**Config:**
```json
{
  "type": "Table",
  "config": {
    "iterateOver": "//cac:InvoiceLine",
    "columns": [
      { "header": "Sıra No", "xpath": "cbc:ID", "width": "5%" },
      { "header": "Ürün Kodu", "xpath": "cac:Item/cac:SellersItemIdentification/cbc:ID", "width": "10%" },
      { "header": "Mal Hizmet", "xpath": "cac:Item/cbc:Name", "width": "25%" },
      { "header": "Miktar", "xpath": "cbc:InvoicedQuantity", "width": "8%" },
      { "header": "Birim Fiyat", "xpath": "cac:Price/cbc:PriceAmount", "width": "12%" },
      { "header": "KDV Oranı", "xpath": "cac:TaxTotal/cac:TaxSubtotal/cac:TaxCategory/cbc:Percent", "width": "8%" },
      { "header": "KDV Tutarı", "xpath": "cac:TaxTotal/cbc:TaxAmount", "width": "12%" },
      { "header": "Mal Hizmet Tutarı", "xpath": "cbc:LineExtensionAmount", "width": "12%" }
    ],
    "showHeader": true,
    "alternateRowColor": "#F9F9F9",
    "headerBackgroundColor": "#E0E0E0"
  }
}
```

**XSLT Output:**
```xml
<table>
  <thead>
    <tr>
      <th>Sıra No</th>
      <!-- ... other headers -->
    </tr>
  </thead>
  <tbody>
    <xsl:for-each select="//cac:InvoiceLine">
      <tr>
        <td><xsl:value-of select="cbc:ID"/></td>
        <!-- ... other cells -->
      </tr>
    </xsl:for-each>
  </tbody>
</table>
```

---

#### BLOCK-05: For-Each Block (Loop Container)

A container block that wraps child blocks and repeats them for each matching XML node.

**Config:**
```json
{
  "type": "ForEach",
  "config": {
    "iterateOver": "//cac:InvoiceLine",
    "children": [ /* child block IDs */ ]
  }
}
```

---

#### BLOCK-06: If / Choose Block (Conditional)

Renders its content only when a condition is true. Supports simple comparisons and optional else branch.

**Config:**
```json
{
  "type": "Conditional",
  "config": {
    "condition": {
      "xpath": "//cbc:InvoiceTypeCode",
      "operator": "equals",
      "value": "TEMELFATURA"
    },
    "thenBlockIds": ["block-uuid-1"],
    "elseBlockIds": ["block-uuid-2"]
  }
}
```

**Supported Operators:** `equals`, `notEquals`, `contains`, `greaterThan`, `lessThan`, `exists`, `notExists`

**XSLT Output:**
```xml
<xsl:choose>
  <xsl:when test="//cbc:InvoiceTypeCode = 'TEMELFATURA'">
    <!-- then content -->
  </xsl:when>
  <xsl:otherwise>
    <!-- else content -->
  </xsl:otherwise>
</xsl:choose>
```

---

#### BLOCK-07: Image Block (Logo / Signature)

Embeds a base64-encoded image asset or references an asset by ID. On free themes, designated image blocks are user-editable. Image uploads are always free.

**Config:**
```json
{
  "type": "Image",
  "config": {
    "assetId": "uuid",
    "assetType": "logo",
    "altText": "Company Logo",
    "width": "150px",
    "height": "80px",
    "alignment": "center",
    "editableOnFreeTheme": true
  }
}
```

> **Free Theme Behavior:** When `editableOnFreeTheme` is `true`, the image block remains editable even on read-only free themes. Admin sets this flag when creating free themes.

---

#### BLOCK-08: Document Info Block

Displays structured document metadata in a two-column key-value table layout.

**Config:**
```json
{
  "type": "DocumentInfo",
  "config": {
    "rows": [
      { "label": "Özelleştirme No", "xpath": "//cbc:CustomizationID" },
      { "label": "Senaryo", "xpath": "//cbc:ProfileID" },
      { "label": "Fatura Tipi", "xpath": "//cbc:InvoiceTypeCode" },
      { "label": "Fatura No", "xpath": "//cbc:ID" },
      { "label": "Fatura Tarihi", "xpath": "//cbc:IssueDate" },
      { "label": "İrsaliye No", "xpath": "//cac:DespatchDocumentReference/cbc:ID" },
      { "label": "İrsaliye Tarihi", "xpath": "//cac:DespatchDocumentReference/cbc:IssueDate" }
    ]
  }
}
```

---

#### BLOCK-09: Totals / Summary Block

Displays summary financial rows in a right-aligned summary table.

**Config:**
```json
{
  "type": "Totals",
  "config": {
    "rows": [
      { "label": "Mal Hizmet Toplam Tutarı", "xpath": "//cac:LegalMonetaryTotal/cbc:LineExtensionAmount" },
      { "label": "Toplam İskonto", "xpath": "//cac:LegalMonetaryTotal/cbc:AllowanceTotalAmount" },
      { "label": "Hesaplanan (%1)", "xpath": "//cac:TaxTotal/cbc:TaxAmount" },
      { "label": "Vergiler Dahil Toplam Tutar", "xpath": "//cac:LegalMonetaryTotal/cbc:TaxInclusiveAmount" },
      { "label": "Ödenecek Tutar", "xpath": "//cac:LegalMonetaryTotal/cbc:PayableAmount", "highlight": true }
    ],
    "alignment": "right"
  }
}
```

---

#### BLOCK-10: Notes Block

Renders a list of note lines from the XML.

**Config:**
```json
{
  "type": "Notes",
  "config": {
    "iterateOver": "//cbc:Note",
    "prefix": "Not: ",
    "staticLines": []
  }
}
```

---

#### BLOCK-11: Bank / Payment Block

Renders payment terms and bank account information.

**Config:**
```json
{
  "type": "BankInfo",
  "config": {
    "bankNameXpath": "//cac:PaymentMeans/cac:PayeeFinancialAccount/cac:FinancialInstitutionBranch/cac:FinancialInstitution/cbc:Name",
    "ibanXpath": "//cac:PaymentMeans/cac:PayeeFinancialAccount/cbc:ID",
    "paymentTermsXpath": "//cac:PaymentTerms/cbc:Note"
  }
}
```

---

#### BLOCK-12: QR Code / ETTN Block

Renders the ETTN as text and optionally displays a QR code image placeholder.

**Config:**
```json
{
  "type": "ETTN",
  "config": {
    "ettnXpath": "//cbc:UUID",
    "showQR": true,
    "qrAssetId": null
  }
}
```

---

#### BLOCK-13: Divider Block

```json
{
  "type": "Divider",
  "config": {
    "style": "solid",
    "color": "#CCCCCC",
    "thickness": "1px",
    "marginTop": "8px",
    "marginBottom": "8px"
  }
}
```

---

#### BLOCK-14: Spacer Block

```json
{
  "type": "Spacer",
  "config": {
    "height": "24px"
  }
}
```

---

### 9.3 Section System

**Section JSON:**
```json
{
  "id": "uuid",
  "name": "Header",
  "order": 1,
  "layout": "single-column",
  "blocks": [ /* ordered block IDs */ ]
}
```

**Default Sections for e-Fatura:**

| Order | Section Name | Default Blocks |
|-------|-------------|----------------|
| 1 | Header | Logo, QR/ETTN |
| 2 | Seller Info | Seller Info Block |
| 3 | Buyer Info | Buyer Info Block |
| 4 | Document Info | Document Info Block |
| 5 | Invoice Lines | Table Block |
| 6 | Summary | Totals Block |
| 7 | Notes | Notes Block |
| 8 | Footer | Bank Info Block, Divider |

Users can rename, reorder, add, or remove sections.

---

### 9.4 Block Drag & Drop Behavior

- **From Palette → Canvas:** Dragging a block type from the left palette drops a new instance of that block into the target section at the drop position.
- **Within Section:** Blocks can be reordered by drag within the same section.
- **Between Sections:** Blocks can be moved between sections by drag.
- **Visual Indicators:** Drop zones highlight when a block is dragged over them.
- **Undo/Redo:** All block operations (add, move, delete, configure) are undoable (Ctrl+Z / Cmd+Z).

**Library:** `@dnd-kit/core` + `@dnd-kit/sortable`

---

## 10. XML Binding & Tree Explorer

### 10.1 Overview

The XML Tree Explorer is a collapsible panel (accessible as a tab in the left sidebar) that displays the structure of the user's uploaded XML file. Users can click any node to bind it to the currently selected block's active field.

### 10.2 XML Upload Flow

1. User clicks **"Upload XML"** in the toolbar.
2. File picker opens, accepts `.xml` files only.
3. File is parsed client-side using the browser's `DOMParser`.
4. Tree is rendered in the XML Tree Explorer panel.
5. Resolved XPath values become visible in the property panel when a field is bound.

### 10.3 XML Tree Explorer Component

```
▶ Invoice                                         (root)
  ▶ cbc:CustomizationID                          TR1.2
  ▶ cbc:ProfileID                                TEMELFATURA
  ▶ cbc:ID                                       SDF2026000002652
  ▶ cbc:IssueDate                                12-02-2026
  ▶ cac:AccountingSupplierParty
    ▶ cac:Party
      ▶ cac:PartyName
        ▷ cbc:Name                               SPOLT San. ve Tic. Ltd. Şti.
      ▶ cac:PostalAddress
        ▷ cbc:StreetName                         Yarış Çıkmazı Sok.
  ▶ cac:AccountingCustomerParty
    ▶ cac:Party
      ...
  ▶ cac:InvoiceLine [1]
    ▷ cbc:ID                                     1
    ▷ cbc:InvoicedQuantity                       25
    ▷ cbc:LineExtensionAmount                    25,188.00
  ▶ cac:InvoiceLine [2]
    ...
```

### 10.4 XPath Binding Field

In the Block Property Panel, every bindable field has:
- An **XPath input field** (editable).
- A **"Pick from tree" button** that activates the XML tree for selection.
- A **"Resolved value" display** showing what the XPath evaluates to in the uploaded XML.
- A **"Fallback" field** for when the XPath returns no result.

### 10.5 Namespace Handling

| Prefix | Namespace URI |
|--------|--------------|
| `cbc` | `urn:oasis:names:specification:ubl:schema:xsd:CommonBasicComponents-2` |
| `cac` | `urn:oasis:names:specification:ubl:schema:xsd:CommonAggregateComponents-2` |
| `ext` | `urn:oasis:names:specification:ubl:schema:xsd:CommonExtensionComponents-2` |

---

## 11. Template Management

### 11.1 Template Types

| Type | Description | Download Cost |
|------|-------------|--------------|
| **Free Theme** | Pre-built XSLT şablonları. Storage'da `.xslt` dosyası olarak saklanır; DB yalnızca `storagePath`'i tutar. Layout ve bloklar read-only'dir, ancak kullanıcılar logo ve imza yükleyebilir. | Ücretsiz (görsel yüklemeler dahil) |
| **User Template** | Kullanıcının block editor ile oluşturduğu şablon. Ödeme sonrası backend XSLT oluşturur, storage'a yazar, DB'ye path kaydeder. | Ücretli (tek seferlik) |
| **Cloned Theme** | Free Theme'in kullanıcı tarafından klonlanmış hali. User Template gibi işlenir. | Ücretli (tek seferlik) |

### 11.2 Template Library Page

- Displays all available Free Themes in a card grid.
- Each card shows: name, preview thumbnail, document type (e-Fatura / e-İrsaliye), and a "Use This Theme" button.
- A search and filter bar allows filtering by document type, style category.

### 11.3 My Templates Page

- Lists all templates belonging to the authenticated user.
- Each entry shows: template name, last modified date, document type, download status (paid / not paid).
- Actions: Open Editor, Rename, Duplicate, Delete.

### 11.4 Template Save

- Auto-save triggers every 30 seconds if there are unsaved changes.
- Manual save via toolbar button.
- Template name is editable in the toolbar (inline edit).

---

## 12. Live Preview

### 12.1 Behavior

- The preview panel renders the HTML output of the current block tree applied to the uploaded XML.
- Preview refreshes **on demand** (user clicks Refresh) or automatically after a 1.5-second debounce following any block change.
- The preview is rendered inside a sandboxed `<iframe>`.

### 12.2 Preview Flow

```
Block tree changed
      │
      ▼ (debounce 1500ms)
POST /api/preview
  Body: { blockTree: [...], xmlContent: "..." }
      │
      ▼
Backend: block tree → XSLT oluşturur (in-memory, storage'a yazılmaz)
Backend: XSLT'yi XML'e uygular → HTML üretir
Returns { html: "..." }
      │
      ▼
Frontend injects HTML into iframe srcdoc
```

> **Not:** Önizleme sırasında oluşturulan XSLT geçici olup storage'a yazılmaz. Storage'a yazma işlemi yalnızca **ödeme tamamlandıktan sonra** (User Template) veya **admin free theme yüklediğinde** gerçekleşir.

### 12.3 Free Theme Preview

Free theme için kullanıcı "Preview" istediğinde backend:
1. DB'den `storagePath` değerini okur.
2. Storage'dan `.xslt` dosyasını yükler (`IStorageService.ReadAsync`).
3. Yüklenen XSLT'yi kullanıcının XML'ine uygulayarak HTML döndürür.

### 12.4 Preview XML Switcher

The toolbar contains a dropdown showing all uploaded XML files for the current session.

---

## 13. Auth & User Management

### 13.1 Authentication Methods

| Method | Notes |
|--------|-------|
| Email + Password | Standard registration and login |
| Google OAuth 2.0 | One-click sign-in |

### 13.2 Registration Flow

1. User fills in: Name, Email, Password (min 8 chars, 1 uppercase, 1 number).
2. Email verification link sent.
3. User verifies email → account activated.
4. Redirect to Dashboard.

### 13.3 JWT Token Strategy

- **Access Token:** Short-lived (15 minutes). Sent in `Authorization: Bearer` header.
- **Refresh Token:** Long-lived (30 days). Stored in `HttpOnly` cookie.
- Refresh token rotation on each use.

### 13.4 User Roles

| Role | Permissions |
|------|------------|
| `user` | Create/edit own templates, upload logo/signature images (free), download free themes (with uploaded images), pay & download own custom templates |
| `admin` | All of the above + manage free themes (upload .xslt to storage + DB path), view all users, manage payments |

### 13.5 Profile Page

Users can view and update: display name, email address, purchased templates (with re-download links), account deletion.

---

## 14. Paid Download Flow

### 14.1 Pricing

- **Free Themes:** Always free to download as XSLT, including any user-uploaded logo and signature images.
- **Image Uploads (Logo / Signature):** Always free.
- **User-Created / Cloned Templates:** Fixed price per download (e.g., ₺99.00 per template).

### 14.2 Payment Integration

Payment provider is **TBD** (iyzico or Stripe recommended for Turkish market).

### 14.3 Download Flow Detail

```
1. User clicks [↓ Download] on a user template
2. System checks: has user paid for this templateId?
   → YES: go to step 7
   → NO: continue
3. Show Payment Modal:
   - Template name
   - Price (e.g., ₺99.00 + KDV)
   - Payment method selector
4. User enters payment info (or uses saved card)
5. POST /api/payments/initiate { templateId }
   → Returns payment session / redirect URL
6. User completes payment on payment provider UI
7. Payment provider calls POST /api/payments/webhook
   → Backend records payment, marks template as purchasedBy user
8. Backend sends confirmation email with re-download link
9. Frontend receives payment success callback
10. POST /api/templates/:id/download
    → Backend: block tree'den XSLT oluşturur
    → Storage'a yazar: "templates/{templateId}/{timestamp}.xslt"
    → DB'ye storagePath kaydeder (Template.xsltStoragePath)
    → Dosyayı stream olarak response'a yazar
11. Browser triggers file download: "template-name.xslt"
```

### 14.4 Re-download

- Users can re-download any previously purchased template from their Profile.
- Backend `Template.xsltStoragePath` değerini okur → storage'dan dosyayı döndürür.
- No additional payment required.

---

## 15. Backend API Contracts

### 15.1 Authentication Endpoints

| Method | Endpoint | Description |
|--------|----------|-------------|
| POST | `/api/auth/register` | Register new user |
| POST | `/api/auth/login` | Login with email/password |
| POST | `/api/auth/refresh` | Refresh access token |
| POST | `/api/auth/logout` | Invalidate refresh token |
| GET | `/api/auth/me` | Get current user info |
| POST | `/api/auth/google` | Google OAuth callback |

---

### 15.2 Template Endpoints

| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/api/templates` | List all free themes (public) |
| GET | `/api/templates/my` | List current user's templates |
| GET | `/api/templates/:id` | Get template by ID |
| POST | `/api/templates` | Create new template |
| PUT | `/api/templates/:id` | Update template (block tree, name, etc.) |
| DELETE | `/api/templates/:id` | Delete template (owner only) |
| POST | `/api/templates/:id/clone` | Clone a template |
| GET | `/api/templates/:id/download` | Download template as XSLT (auth + payment check) — storage'dan stream eder |

**POST /api/templates — Request Body:**
```json
{
  "name": "My Invoice Template",
  "documentType": "invoice",
  "sections": [
    {
      "id": "uuid",
      "name": "Header",
      "order": 1,
      "blocks": [ /* block JSON array */ ]
    }
  ],
  "assets": [ /* asset IDs */ ]
}
```

---

### 15.3 Preview Endpoint

| Method | Endpoint | Description |
|--------|----------|-------------|
| POST | `/api/preview` | Generate HTML preview from block tree + XML (in-memory, storage'a yazılmaz) |
| POST | `/api/preview/theme/:themeId` | Free theme önizlemesi — storage'dan .xslt okur, XML'e uygular |

**POST /api/preview — Request Body:**
```json
{
  "sections": [ /* current block tree */ ],
  "xmlContent": "<Invoice>...</Invoice>",
  "assets": [ /* base64 assets for preview */ ]
}
```

**Response:**
```json
{
  "html": "<html>...</html>",
  "generationTimeMs": 145
}
```

---

### 15.4 Asset Endpoints

> **Note:** Image upload (logo, signature) is a free feature. No payment is required.

| Method | Endpoint | Description |
|--------|----------|-------------|
| POST | `/api/assets/upload` | Görsel dosyayı storage'a yazar, DB'ye path kaydeder — always free |
| GET | `/api/assets/:id` | Get asset metadata by ID |
| DELETE | `/api/assets/:id` | Delete asset (storage'dan siler + DB kaydını kaldırır) |

**POST /api/assets/upload — Request:** `multipart/form-data`
- `file`: image file (PNG, JPG, SVG)
- `type`: `logo` | `signature` | `stamp` | `custom`
- `templateId` (optional): associates the asset with a specific template

**İşlem Akışı (backend):**
1. `IStorageService.WriteAsync(file, "assets/{userId}/{uuid}.{ext}")` çağrısı yapılır.
2. Dönen path, `Asset.filePath` kolonuna kaydedilir.
3. Response olarak asset metadata döndürülür.

**Response:**
```json
{
  "id": "uuid",
  "url": "/api/assets/uuid/serve",
  "storagePath": "assets/userId/uuid.png",
  "type": "logo",
  "mimeType": "image/png",
  "sizeBytes": 45230
}
```

---

### 15.5 Payment Endpoints

| Method | Endpoint | Description |
|--------|----------|-------------|
| POST | `/api/payments/initiate` | Start payment session for a template |
| POST | `/api/payments/webhook` | Payment provider webhook (server-to-server) |
| GET | `/api/payments/history` | List user's payment history |

---

### 15.6 Admin Endpoints

| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/api/admin/users` | List all users |
| POST | `/api/admin/themes` | Yeni free theme yükler: .xslt dosyasını storage'a yazar, DB'ye path + metadata kaydeder |
| PUT | `/api/admin/themes/:id` | Free theme günceller: eski dosyayı değiştirir, DB path'i günceller |
| DELETE | `/api/admin/themes/:id` | Free theme siler: storage'dan dosyayı kaldırır, DB kaydını siler |
| GET | `/api/admin/payments` | List all payment transactions |

**POST /api/admin/themes — Request:** `multipart/form-data`
- `file`: `.xslt` dosyası
- `name`: tema adı
- `documentType`: `invoice` | `despatch`
- `thumbnailFile` (optional): önizleme görseli

**İşlem Akışı (backend):**
1. `.xslt` dosyası `IStorageService.WriteAsync(file, "themes/{uuid}.xslt")` ile storage'a yazılır.
2. Dönen path, `Template.xsltStoragePath` kolonuna kaydedilir.
3. `Template.isFreeTheme = true` olarak işaretlenir.

---

## 16. Data Models

### 16.1 User

```
User {
  id            UUID          PK
  email         VARCHAR(255)  UNIQUE NOT NULL
  passwordHash  VARCHAR(255)
  displayName   VARCHAR(100)
  role          ENUM(user, admin)  DEFAULT user
  emailVerified BOOLEAN       DEFAULT false
  createdAt     TIMESTAMP
  updatedAt     TIMESTAMP
}
```

### 16.2 Template

```
Template {
  id               UUID          PK
  name             VARCHAR(255)  NOT NULL
  ownerId          UUID          FK → User (nullable for free themes)
  documentType     ENUM(invoice, despatch)
  isFreeTheme      BOOLEAN       DEFAULT false
  blockTree        JSONB         NOT NULL  -- full sections + blocks JSON (custom templates için)
  xsltStoragePath  VARCHAR(1000) NULLABLE  -- storage'daki .xslt dosyasının yolu
                                           -- free theme'ler için admin upload'unda set edilir
                                           -- custom template'ler için ödeme sonrası set edilir
  thumbnailUrl     VARCHAR(500)
  createdAt        TIMESTAMP
  updatedAt        TIMESTAMP
}
```

> **Önemli:** `xsltStoragePath` kolonu veritabanında tutulur; `.xslt` dosyasının içeriği veritabanına yazılmaz. Backend dosyayı her zaman bu path üzerinden storage'dan okur.

### 16.3 Asset

```
Asset {
  id         UUID        PK
  ownerId    UUID        FK → User
  type       ENUM(logo, signature, stamp, custom)
  filePath   VARCHAR(1000)  -- storage'daki dosya yolu (örn: "assets/userId/uuid.png")
  mimeType   VARCHAR(100)
  sizeBytes  INTEGER
  createdAt  TIMESTAMP
}
```

> **Önemli:** `filePath` storage içindeki göreli yolu ifade eder. Dosyanın binary içeriği veritabanında tutulmaz.

### 16.4 Payment

```
Payment {
  id           UUID          PK
  userId       UUID          FK → User
  templateId   UUID          FK → Template
  amount       DECIMAL(10,2)
  currency     VARCHAR(3)    DEFAULT 'TRY'
  status       ENUM(pending, completed, failed, refunded)
  providerRef  VARCHAR(255)  -- payment provider transaction ID
  createdAt    TIMESTAMP
  updatedAt    TIMESTAMP
}
```

### 16.5 RefreshToken

```
RefreshToken {
  id         UUID       PK
  userId     UUID       FK → User
  token      VARCHAR    UNIQUE
  expiresAt  TIMESTAMP
  revokedAt  TIMESTAMP  nullable
  createdAt  TIMESTAMP
}
```

---

## 17. Storage Stratejisi

### 17.1 Genel İlkeler

| İlke | Açıklama |
|------|----------|
| Dosya içeriği DB'de tutulmaz | XSLT ve görsel dosyaların binary içeriği veritabanına yazılmaz. |
| DB yalnızca path tutar | `Template.xsltStoragePath` ve `Asset.filePath` kolonları storage'daki göreli yolu saklar. |
| Storage erişimi servis üzerinden | Tüm okuma/yazma işlemleri `IStorageService` arayüzü üzerinden yapılır; controller/servis katmanı doğrudan dosya sistemine erişmez. |
| Ortam bağımsızlığı | Geliştirme ortamında `LocalStorageService`, üretim ortamında `S3StorageService` kullanılır. DI aracılığıyla değiştirilebilir. |

### 17.2 IStorageService Arayüzü

```csharp
public interface IStorageService
{
    /// <summary>Dosyayı storage'a yazar, göreli path döndürür.</summary>
    Task<string> WriteAsync(Stream content, string relativePath, string contentType);

    /// <summary>Verilen path'teki dosyayı stream olarak açar.</summary>
    Task<Stream> ReadAsync(string relativePath);

    /// <summary>Verilen path'teki dosyayı siler.</summary>
    Task DeleteAsync(string relativePath);

    /// <summary>Dosyanın var olup olmadığını kontrol eder.</summary>
    Task<bool> ExistsAsync(string relativePath);
}
```

### 17.3 Storage Klasör Yapısı

```
storage/                         ← kök (LocalStorageService için fiziksel klasör)
│                                   (S3StorageService için bucket prefix)
├── themes/
│   ├── {themeId}.xslt           ← admin tarafından yüklenen free theme şablonları
│   └── {themeId}_thumb.png      ← opsiyonel thumbnail
│
├── assets/
│   └── {userId}/
│       └── {assetId}.{ext}      ← kullanıcıya ait logo, imza vb. görseller
│
└── templates/
    └── {templateId}/
        └── {timestamp}.xslt     ← ödeme sonrası oluşturulan kullanıcı şablonları
```

### 17.4 Yazma Akışı — Upload API

```
HTTP POST /api/assets/upload
        │
        ▼ (Controller)
IFormFile alınır
        │
        ▼ (Application Layer)
IStorageService.WriteAsync(
    stream    = file.OpenReadStream(),
    path      = $"assets/{userId}/{assetId}.{ext}",
    mediaType = file.ContentType
)
        │
        ▼ dönen path
Asset { filePath = "assets/userId/assetId.png", ... }
DbContext.Assets.Add(asset)
await DbContext.SaveChangesAsync()
        │
        ▼
Response: { id, storagePath, url, ... }
```

### 17.5 Okuma Akışı — Download / Preview

```
GET /api/templates/:id/download
        │
        ▼
Template kaydı DB'den çekilir
        │
        ▼ template.xsltStoragePath
IStorageService.ReadAsync(storagePath)
        │
        ▼ Stream
Response.ContentType = "application/xslt+xml"
stream.CopyToAsync(Response.Body)
```

### 17.6 Free Theme Admin Upload Akışı

```
POST /api/admin/themes  (multipart: xsltFile + metadata)
        │
        ▼
themeId = Guid.NewGuid()
storagePath = $"themes/{themeId}.xslt"
        │
        ▼
IStorageService.WriteAsync(xsltFile.OpenReadStream(), storagePath, "application/xslt+xml")
        │
        ▼
Template {
  id              = themeId,
  isFreeTheme     = true,
  blockTree       = {},          ← free theme'lerde blockTree boş veya read-only metadata
  xsltStoragePath = storagePath
}
DbContext.Templates.Add(template)
await DbContext.SaveChangesAsync()
```

### 17.7 Ortam Konfigürasyonu

**appsettings.Development.json:**
```json
{
  "Storage": {
    "Provider": "Local",
    "LocalBasePath": "../../../storage"
  }
}
```

**appsettings.Production.json:**
```json
{
  "Storage": {
    "Provider": "S3",
    "S3BucketName": "xsltcraft-storage",
    "S3Region": "eu-central-1"
  }
}
```

**DI Kaydı (Program.cs):**
```csharp
var storageProvider = builder.Configuration["Storage:Provider"];
if (storageProvider == "S3")
    builder.Services.AddSingleton<IStorageService, S3StorageService>();
else
    builder.Services.AddSingleton<IStorageService, LocalStorageService>();
```

---

## 18. Non-Functional Requirements

| Category | Requirement |
|----------|------------|
| Performance | Preview generation ≤ 2 seconds for documents with ≤ 50 invoice lines |
| Performance | Page load (editor) ≤ 3 seconds on standard broadband |
| Performance | Storage okuma gecikmesi ≤ 200ms (yerel filesystem), ≤ 500ms (S3, aynı bölge) |
| Security | All API endpoints require JWT except public theme listing and auth endpoints |
| Security | File uploads validated for type and size (max 5MB per asset, max 2MB per .xslt) |
| Security | `.xslt` dosyaları yalnızca backend tarafından okunur; storage URL'leri frontend'e doğrudan açılmaz |
| Security | Payment webhook endpoint validates provider signature |
| Security | XSS prevention: generated HTML is rendered only in sandboxed iframe |
| Usability | Editor supports undo/redo with ≥ 20 history steps |
| Usability | All block operations respond within 200ms (local state update) |
| Scalability | Backend stateless, horizontally scalable |
| Scalability | Storage katmanı S3 uyumlu olduğundan yatay ölçekleme desteklenir |
| Compatibility | Frontend: Chrome 120+, Firefox 120+, Edge 120+, Safari 17+ |
| Accessibility | WCAG 2.1 AA for auth pages and dashboard |
| Data | User data stored in TR-based servers (for KVKK compliance) |
| Data | Storage dosyaları da TR bölge sunucusunda barındırılır |

---

## 19. Out of Scope (v1.0)

| # | Feature |
|---|---------|
| 1 | Advanced XSLT editor mode (Monaco) — available as read-only view only |
| 2 | Multi-page / paginated PDF output |
| 3 | Team / organization accounts and shared templates |
| 4 | Version history and branching for templates |
| 5 | API access for programmatic template management |
| 6 | White-label / embedded widget for third-party apps |
| 7 | Template marketplace (user-to-user template selling) |
| 8 | Subscription billing model |
| 9 | Mobile-responsive editor UI |
| 10 | E-Arşiv fatura support (beyond current e-Fatura / e-İrsaliye) |
| 11 | Bulk XML batch preview |
| 12 | Digital signature embedding in XSLT output |
| 13 | CDN entegrasyonu storage dosyaları için |

---

## 20. Glossary

| Term | Definition |
|------|-----------|
| **XSLT** | Extensible Stylesheet Language Transformations. A language for transforming XML documents into other formats (HTML, PDF, text, etc.). |
| **XPath** | XML Path Language. A query language for selecting nodes in an XML document. |
| **e-Fatura** | Turkish electronic invoice standard defined by GIB (Gelir İdaresi Başkanlığı), based on UBL 2.1. |
| **e-İrsaliye** | Turkish electronic despatch advice standard defined by GIB, based on UBL 2.1. |
| **GIB** | Gelir İdaresi Başkanlığı — Turkish Revenue Administration. |
| **UBL 2.1** | Universal Business Language 2.1. The XML standard used by GIB for e-documents. |
| **Block Tree** | The JSON representation of all sections and blocks in a template, as stored in the database and exchanged between frontend and backend. |
| **Section** | A named logical container within the editor canvas that holds one or more blocks. |
| **Block** | The atomic unit of the visual editor. Each block type maps to a specific XSLT pattern. |
| **Free Theme** | Pre-built, admin-created XSLT template. `.xslt` dosyası storage'da saklanır, DB yalnızca `xsltStoragePath`'i tutar. Users can upload their own logo and signature images into designated image blocks without cloning. |
| **User Template** | A template created or customized by a user. Ödeme sonrası XSLT oluşturulur, storage'a yazılır, `xsltStoragePath` DB'ye kaydedilir. |
| **Asset** | An uploaded image file (logo, signature, stamp) associated with a user's account. Dosya storage'da, path DB'de tutulur. |
| **Storage** | Dosya depolama katmanı. Geliştirmede yerel filesystem, üretimde S3-uyumlu nesne deposu. |
| **IStorageService** | Backend'de storage erişimini soyutlayan C# arayüzü. `LocalStorageService` ve `S3StorageService` implementasyonları vardır. |
| **xsltStoragePath** | `Template` tablosunda `.xslt` dosyasının storage içindeki göreli yolunu tutan kolon. |
| **Monorepo** | Tek bir Git deposunda hem frontend (`apps/frontend`) hem backend (`apps/backend`) kodunun barındırıldığı proje yapısı. |
| **XML Tree Explorer** | The UI component that displays the hierarchical structure of an uploaded XML file and enables click-to-bind XPath selection. |
| **ETTN** | Elektronik Ticaret Takip Numarası — the UUID-format unique identifier of a GIB e-document (stored in `cbc:UUID`). |
| **KVKK** | Kişisel Verilerin Korunması Kanunu — Turkish Personal Data Protection Law. |

---

*End of Document — XsltCraft Low-Code Platform PRD v1.2*
