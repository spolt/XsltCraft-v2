import { test, expect, request, type APIRequestContext } from '@playwright/test'

// Hazır tema logo/imza konumları: sunucu birleştirmesi (POST /api/preview/compose) + XSLT'ye gömülen
// çalışma betiğinin gerçek tarayıcıda görseli doğru yuvaya yerleştirmesi. Admin hesabı gerektirmez
// (xslt modu, analiz edilmiş bir temanın işaretlerini taşıyan küçük bir şablonla).
const API = process.env.E2E_API_URL ?? 'http://localhost:5000'
const USER = { username: 'e2e_tester', email: 'e2e@test.local', password: 'E2ePass123', displayName: 'E2E Tester' }
const PNG = 'data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNkYPhfDwAChwGA60e6kgAAAABJRU5ErkJggg=='

const MARKED_XSLT = `<?xml version="1.0" encoding="UTF-8"?>
<xsl:stylesheet version="1.0" xmlns:xsl="http://www.w3.org/1999/XSL/Transform">
  <xsl:output method="html" encoding="UTF-8"/>
  <xsl:template match="/">
    <html><body>
      <table><tbody><tr>
        <td><div data-xc-slot="seller-top" data-xc-layout="block" data-xc-inserted="1"></div>Satıcı</td>
        <td><div id="qrcode" style="float:right"></div><div data-xc-slot="qr-below" data-xc-layout="block-clear" data-xc-inserted="1"></div></td>
      </tr></tbody></table>
      <div data-xc-slot="docinfo-top" id="company_logo"></div>
    </body></html>
  </xsl:template>
</xsl:stylesheet>`

const LEGACY_XSLT = `<?xml version="1.0" encoding="UTF-8"?>
<xsl:stylesheet version="1.0" xmlns:xsl="http://www.w3.org/1999/XSL/Transform">
  <xsl:output method="html" encoding="UTF-8"/>
  <xsl:template match="/"><html><body><div id="company_logo"></div><div id="imza"></div></body></html></xsl:template>
</xsl:stylesheet>`

// Temadaki statik görseller yuva olarak işaretlenmiş (admin "logo" / "imza" seçmiş).
const THEME_IMG = 'data:image/gif;base64,R0lGODlhAQABAIAAAP///wAAACH5BAEAAAAALAAAAAABAAEAAAICRAEAOw=='
const PLACEHOLDER_XSLT = `<?xml version="1.0" encoding="UTF-8"?>
<xsl:stylesheet version="1.0" xmlns:xsl="http://www.w3.org/1999/XSL/Transform">
  <xsl:output method="html" encoding="UTF-8"/>
  <xsl:template match="/">
    <html><body>
      <table><tbody><tr>
        <td>Satıcı</td>
        <td><div id="qrcode" style="float:right"></div><img data-xc-slot="existing-logo" data-xc-placeholder="logo" data-xc-removable="1" style="float:right;width:120px;height:60px" src="${THEME_IMG}" alt="company_logo"/></td>
      </tr></tbody></table>
      <img data-xc-slot="existing-signature" data-xc-placeholder="signature" src="${THEME_IMG}" style="width:80px;height:80px"/>
      <div data-xc-slot="totals-left" data-xc-layout="float-left" data-xc-inserted="1"></div>
    </body></html>
  </xsl:template>
</xsl:stylesheet>`

// Analiz edilmemiş eski tema: yedek id (#company_logo) bir görsel — içine bir şey eklenemez.
const LEGACY_IMG_XSLT = `<?xml version="1.0" encoding="UTF-8"?>
<xsl:stylesheet version="1.0" xmlns:xsl="http://www.w3.org/1999/XSL/Transform">
  <xsl:output method="html" encoding="UTF-8"/>
  <xsl:template match="/"><html><body><img id="company_logo" src="${THEME_IMG}" style="width:120px;height:60px"/></body></html></xsl:template>
</xsl:stylesheet>`

const XML = '<?xml version="1.0" encoding="UTF-8"?><Invoice xmlns="urn:oasis:names:specification:ubl:schema:xsd:Invoice-2"/>'

let api: APIRequestContext
let token = ''

test.describe.configure({ mode: 'serial' })

test.beforeAll(async () => {
  api = await request.newContext({ baseURL: API })
  await api.post('/api/auth/register', { data: USER }) // 201 ya da 409 — ikisi de kabul
  const login = await api.post('/api/auth/login', { data: { username: USER.username, password: USER.password } })
  expect(login.ok()).toBeTruthy()
  token = (await login.json()).accessToken
})

test.afterAll(async () => { await api.dispose() })

function compose(body: Record<string, unknown>) {
  return api.post('/api/preview/compose', { data: body, headers: { Authorization: `Bearer ${token}` } })
}

test('görseller seçilen yuvalara yerleşir; aynı yuvada iki görsel alt alta', async ({ page }) => {
  const res = await compose({
    xslt: MARKED_XSLT,
    xmlContent: XML,
    assets: [
      { kind: 'logo', slot: 'qr-below', dataUrl: PNG, width: 40, height: 20, alignment: 'right' },
      { kind: 'signature', slot: 'qr-below', dataUrl: PNG, width: null, height: null, alignment: 'left' },
    ],
    bankInfo: [],
    includeXslt: true,
    includeHtml: true,
  })
  expect(res.status()).toBe(200)
  const body = await res.json()
  expect(body.appliedSlots).toEqual({ logo: 'qr-below', signature: 'qr-below' })
  expect(body.xslt).toContain('data-xc-overrides="2"')

  await page.setContent(body.html)
  const slot = page.locator('[data-xc-slot="qr-below"]')
  await expect(slot.locator('[data-xc-asset]')).toHaveCount(2)
  await expect(slot.locator('[data-xc-asset="logo"]')).toHaveCSS('justify-content', 'flex-end')
  await expect(slot.locator('[data-xc-asset="logo"]')).toHaveCSS('clear', 'both')
  await expect(page.locator('#company_logo img')).toHaveCount(0)
})

test('konumu olmayan eski temada görsel eski #company_logo alanına düşer', async ({ page }) => {
  const res = await compose({
    xslt: LEGACY_XSLT,
    xmlContent: XML,
    assets: [{ kind: 'logo', slot: 'qr-below', dataUrl: PNG, width: null, height: null, alignment: 'left' }],
    bankInfo: [],
    includeXslt: false,
    includeHtml: true,
  })
  expect(res.status()).toBe(200)
  const body = await res.json()
  expect(body.warnings.map((w: { code: string }) => w.code)).toContain('legacy-fallback')

  await page.setContent(body.html)
  await expect(page.locator('#company_logo img')).toHaveAttribute('src', PNG)
})

test('temadaki logo görseli kullanıcının logosuyla yerinde değişir; başka yere konan imzanın tema görseli gizlenir', async ({ page }) => {
  const res = await compose({
    xslt: PLACEHOLDER_XSLT,
    xmlContent: XML,
    assets: [
      { kind: 'logo', slot: 'existing-logo', dataUrl: PNG, width: null, height: null, alignment: 'right' },
      { kind: 'signature', slot: 'totals-left', dataUrl: PNG, width: null, height: null, alignment: 'left' },
    ],
    bankInfo: [],
    includeXslt: false,
    includeHtml: true,
  })
  expect(res.status()).toBe(200)
  await page.setContent((await res.json()).html)

  const logo = page.locator('img[data-xc-slot="existing-logo"]')
  await expect(logo).toHaveAttribute('src', PNG)
  await expect(logo).toHaveAttribute('data-xc-asset', 'logo')
  await expect(logo).toHaveCSS('width', '120px') // temadaki kutu korunur
  await expect(logo).toHaveCSS('float', 'right')
  await expect(logo).toHaveCSS('object-position', '100% 50%')

  const stamp = page.locator('img[data-xc-slot="existing-signature"]')
  await expect(stamp).toHaveAttribute('src', THEME_IMG)
  await expect(stamp).toHaveCSS('visibility', 'hidden') // yeri korunur, tasarım kaymaz
  await expect(page.locator('[data-xc-slot="totals-left"] [data-xc-asset="signature"] img')).toHaveAttribute('src', PNG)
})

test('kullanıcı görsel eklemezse temadaki görseller olduğu gibi kalır; boyut verilirse uygulanır', async ({ page }) => {
  const res = await compose({
    xslt: PLACEHOLDER_XSLT,
    xmlContent: XML,
    assets: [{ kind: 'logo', slot: 'existing-logo', dataUrl: PNG, width: 50, height: null, alignment: 'left' }],
    bankInfo: [],
    includeXslt: false,
    includeHtml: true,
  })
  expect(res.status()).toBe(200)
  await page.setContent((await res.json()).html)

  await expect(page.locator('img[data-xc-slot="existing-logo"]')).toHaveCSS('width', '50px')
  const stamp = page.locator('img[data-xc-slot="existing-signature"]')
  await expect(stamp).toHaveAttribute('src', THEME_IMG)
  await expect(stamp).toHaveCSS('visibility', 'visible')
})

test('görsel eklemeden kaldırma yalnız admin izin verdiyse uygulanır', async ({ page }) => {
  const res = await compose({
    xslt: PLACEHOLDER_XSLT,
    xmlContent: XML,
    assets: [],
    bankInfo: [],
    hideThemeImages: ['logo', 'signature'],
    includeXslt: false,
    includeHtml: true,
  })
  expect(res.status()).toBe(200)
  await page.setContent((await res.json()).html)

  await expect(page.locator('img[data-xc-slot="existing-logo"]')).toHaveCSS('visibility', 'hidden') // izinli
  await expect(page.locator('img[data-xc-slot="existing-signature"]')).toHaveCSS('visibility', 'visible') // izinsiz
})

test('logo hiçbir yere yerleşemezse temadaki logo gizlenmez', async ({ page }) => {
  // notes-side bu temada yok; xslt modunda katalog varsayılanı, temada eski id de yok → betik hedef bulamaz.
  const res = await compose({
    xslt: PLACEHOLDER_XSLT,
    xmlContent: XML,
    assets: [{ kind: 'logo', slot: 'notes-side', dataUrl: PNG, width: null, height: null, alignment: 'left' }],
    bankInfo: [],
    includeXslt: false,
    includeHtml: true,
  })
  expect(res.status()).toBe(200)
  const body = await res.json()
  expect(body.appliedSlots).toEqual({ logo: null })
  await page.setContent(body.html)

  const logo = page.locator('img[data-xc-slot="existing-logo"]')
  await expect(logo).toHaveAttribute('src', THEME_IMG)
  await expect(logo).toHaveCSS('visibility', 'visible')
  await expect(page.locator('[data-xc-asset]')).toHaveCount(0)
})

test('eski temada yedek id bir görselse kullanıcının logosu onun yerini alır', async ({ page }) => {
  const res = await compose({
    xslt: LEGACY_IMG_XSLT,
    xmlContent: XML,
    assets: [{ kind: 'logo', slot: null, dataUrl: PNG, width: null, height: null, alignment: 'left' }],
    bankInfo: [],
    includeXslt: false,
    includeHtml: true,
  })
  expect(res.status()).toBe(200)
  await page.setContent((await res.json()).html)

  const logo = page.locator('img#company_logo')
  await expect(logo).toHaveAttribute('src', PNG)
  await expect(logo).toHaveAttribute('data-xc-asset', 'logo')
  await expect(logo).toHaveCSS('width', '120px') // temadaki kutu korunur
  await expect(page.locator('#company_logo *')).toHaveCount(0) // img'in içine bir şey eklenmez
})

test('banka bilgisi kaçışlanır, betik çalışmaz', async ({ page }) => {
  const res = await compose({
    xslt: LEGACY_XSLT,
    xmlContent: XML,
    assets: [],
    bankInfo: [{ bankName: '<img src=x onerror="window.__xss=1">', iban: 'TR00' }],
    includeXslt: false,
    includeHtml: true,
  })
  expect(res.status()).toBe(200)
  await page.setContent((await res.json()).html)
  await expect(page.getByText('<img src=x onerror="window.__xss=1">')).toBeVisible()
  expect(await page.evaluate(() => (window as unknown as { __xss?: number }).__xss)).toBeUndefined()
})

test('SVG ve bozuk görseller reddedilir', async () => {
  for (const dataUrl of ['data:image/svg+xml;base64,PHN2Zy8+', "data:image/png;base64,iVBOR'</script>"]) {
    const res = await compose({
      xslt: LEGACY_XSLT, assets: [{ kind: 'logo', slot: null, dataUrl, width: null, height: null, alignment: 'left' }],
      bankInfo: [], includeXslt: true, includeHtml: false,
    })
    expect(res.status()).toBe(400)
  }
})

test('oturumsuz istek reddedilir', async () => {
  const anon = await request.newContext({ baseURL: API })
  const res = await anon.post('/api/preview/compose', { data: { xslt: LEGACY_XSLT, assets: [], bankInfo: [], includeXslt: true } })
  expect(res.status()).toBe(401)
  await anon.dispose()
})
