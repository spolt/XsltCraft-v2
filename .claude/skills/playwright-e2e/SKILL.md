---
name: playwright-e2e
description: XsltCraft uçtan-uca (E2E) senaryolarını yazma ve koşturma rehberi. Tarayıcı üzerinden gerçek kullanıcı akışlarını (XML yükle, XSLT yükle, blok düzenle, önizleme üret, çıktı doğrula) Playwright MCP ile sürmek ya da @playwright/test spec'leri olarak commit'lemek istendiğinde kullan. UI akışı değişen değişikliklerde (workflows.md new-feature) çalıştır.
---

# Playwright E2E (XsltCraft)

> Faz 2 dilimi. Önce uygulama ayağa kalkmalı; sonra ya **Playwright MCP** ile interaktif sür, ya da **@playwright/test** spec'i commit'le.

## Ön koşullar
```bash
docker compose up -d                                   # Postgres (+ MinIO)
cd backend && dotnet run --project XsltCraft/XsltCraft.Api.csproj   # API → http://localhost:5000
cd frontend/xsltcraft-ui && npm install && npm run dev # UI → http://localhost:5173
```
- **Test kullanıcısı:** seed admin (`appsettings`'teki `Admin:Username`, şifre user-secrets `Admin:Password`). E2E için ayrı bir test hesabı/seed tercih et (prod credential kullanma).
- **Playwright MCP:** `.mcp.json`'da `playwright` sunucusu tanımlı. Etkinleşmesi için **oturumu yeniden başlat**; ilk koşuda `npx playwright install chromium` gerekebilir.

## Rotalar (React Router — gerçek)
| Akış | Rota |
|------|------|
| Login | `/auth/login` |
| Dashboard | `/dashboard` |
| **V2 grid tasarımcı** | `/editor/new` (veya `/editor/:templateId`) |
| **XSLT editör (Monaco)** | `/xslt-editor` |
| Şablonlarım | `/my-xslt-templates` |

## Mod A — Playwright MCP (interaktif, roadmap'in önerdiği)
Tarayıcıyı MCP araçlarıyla sür: `browser_navigate`, `browser_snapshot` (DOM/erişilebilirlik ağacı), `browser_click`, `browser_type`, `browser_file_upload`, `browser_wait_for`. Her adımda `browser_snapshot` ile durumu doğrula. Selector için rol/metin/`data-testid` tercih et.

## 5 çekirdek senaryo (adımlar + assert)
**Login (ortak):** `/auth/login` → kullanıcı adı + şifre gir → submit → `/dashboard`'a yönlendi.

1. **XML yükle** — `/editor/new` → XML yükle (veya yerleşik varsayılan UBL fatura) → **assert:** `XmlTreeExplorer` ağacı dolu (`n1:Invoice` / `cbc:ID` node'ları görünür).
2. **XSLT yükle** — `/xslt-editor` → `.xslt` + `.xml` yükle → **assert:** Monaco içerik gösterir, `XsltEditorPreview` iframe render eder.
3. **Blok düzenle** — `/editor/new` → `BlockPalette`'ten bir bloğu `Canvas`'a sürükle → `PropertyPanel`'de XPath bağla (`XmlTreeExplorer`'dan node seç) → **assert:** blok canvas'ta, bağlı XPath görünür.
4. **Önizleme üret** — `/editor/new` blok ağacı + XML → **assert:** `EditorPreviewPanel` HTML önizlemesi bağlı değerleri gösterir (boş değil).
5. **Çıktı doğrula** — geçersiz/eksik alanlı XML ya da hatalı XSLT → **assert:** `ProblemsPanel` UBL-TR iş kuralı / XSLT hatalarını satır numarasıyla listeler.

## Mod B — commit'lenen spec (@playwright/test) — KURULU
Kurulum tamam: `frontend/xsltcraft-ui/playwright.config.ts` (baseURL :5173, dev sunucusunu reuse eder) + `e2e/smoke.spec.ts` (auth akışı + korumalı sayfa yükleme + redirect). İlk makinede tarayıcı: `npx playwright install chromium`.

Koşturma (app ayakta + backend :5000 iken):
```bash
cd frontend/xsltcraft-ui && npm run test:e2e
```
Test kullanıcısı public `/api/auth/register` ile **idempotent** oluşturulur (201/409). 5 senaryonun derin kısımları (blok sürükle-bırak, önizleme, problems paneli) için önce bileşenlere `data-testid` ekle (`XmlTreeExplorer`, `EditorPreviewPanel`, `ProblemsPanel`) — kırılgan selector yerine. Auth `xsltcraft-auth` localStorage anahtarında persist olur → tam-sayfa `goto` sonrası korunur.

## Keşfedilen gerçek yapı + @dnd-kit drag reçetesi (Mode A ile doğrulandı)
- **Login:** `input[autocomplete="username"]`, `input[type="password"]`, `button:has-text("Giriş Yap")` → `/dashboard`.
- **/editor/new:** açılışta **`varsayilan-fatura.xml` otomatik yüklü** (XML yüklemeden önizleme alınabilir). Toolbar: `+ XML`, `Önizle`, `↓ İndir`, `Kaydet`. Palet blokları role=button + etiket (`Satıcı Bilgileri`, `Fatura Başlığı`, `Fatura Satırları`, `Fatura Dip Toplamları`, `Tablo`, `Metin`…). Sağ panel sekmeleri: `Özellikler` / `XML Ağacı`.
- **Blok ekleme yalnız SÜRÜKLE** (tıklama paleti `[active]` yapar, eklemez). @dnd-kit droppable id = **`grid-canvas`**. **Basit `dragTo` bloğu YERLEŞTİRMEZ** (status "was dropped" der ama canvas boş kalır). Çalışan reçete — aşamalı manuel pointer:
  ```js
  const sb = await src.boundingBox(); const db = await page.locator('main').boundingBox()
  await page.mouse.move(sb.x+sb.width/2, sb.y+sb.height/2)
  await page.mouse.down()
  await page.mouse.move(sb.x+sb.width/2+15, sb.y+sb.height/2+15, { steps: 6 }) // aktivasyon mesafesi
  await page.mouse.move(db.x+db.width/2, db.y+db.height/2, { steps: 20 })
  await page.mouse.up()
  ```
  Başarıda status: `… dropped over droppable area grid-canvas`; sağ panel blok özelliklerini gösterir; `İndir`/`Kaydet` aktifleşir.
- **Önizleme:** `Önizle` → `Canlı Önizleme` paneli + **iframe** UBL verisini render eder. Assert: `page.frameLocator('iframe')` içinde beklenen alan (örn. `Fatura No` / `TST2026000000001`).

## Entegrasyon
- **Eval gibi non-blocking başla:** CI'da ayrı job + `continue-on-error: true`; olgunlaşınca blocking.
- `workflows.md` new-feature zincirinde UI akışı değiştiyse bu adımı koş.
- Selector kararlılığı için `data-testid` eklemek meşru bir frontend değişikliğidir (reviewer onayıyla).
