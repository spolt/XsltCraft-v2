import { test, expect, type Page } from '@playwright/test'

// XSLT Editör tam ekran modu + yüzen mini önizleme penceresi.
// Değişmezler: Monaco remount olmaz, sayfada aynı anda en fazla bir önizleme iframe'i bulunur.
const USER = { username: 'e2e_tester', password: 'E2ePass123' }

const STYLESHEET = `<?xml version="1.0" encoding="UTF-8"?>
<xsl:stylesheet version="1.0" xmlns:xsl="http://www.w3.org/1999/XSL/Transform">
  <xsl:template match="/"><html><body><p>MINI_ONIZLEME_OK</p></body></html></xsl:template>
</xsl:stylesheet>`

const INVOICE = `<?xml version="1.0" encoding="UTF-8"?>
<Invoice xmlns="urn:oasis:names:specification:ubl:schema:xsd:Invoice-2"><ID>X1</ID></Invoice>`

const PREVIEW_FRAME = 'iframe[title="Canlı Önizleme"]'
const STORAGE_KEY = 'xsltcraft-mini-preview'

const file = (name: string, content: string) => ({ name, mimeType: 'application/xml', buffer: Buffer.from(content) })

// Login global rate-limit'e (10/dk) takılmamak için tek oturum, testler sırayla aynı sayfada.
test.describe.configure({ mode: 'serial' })
let page: Page

const frames = () => page.locator(PREVIEW_FRAME)
const mini = () => page.getByRole('region', { name: 'Mini önizleme' })
const monaco = () => page.locator('.monaco-editor').first()
const xmlTab = () => page.getByRole('button', { name: 'XML Kaynak' })
const previewText = () => page.frameLocator(PREVIEW_FRAME).getByText('MINI_ONIZLEME_OK')

test.beforeAll(async ({ browser }) => {
  page = await browser.newPage()
  await page.goto('/auth/login')
  await page.locator('input[autocomplete="username"]').fill(USER.username)
  await page.locator('input[type="password"]').fill(USER.password)
  await page.getByRole('button', { name: 'Giriş Yap' }).click()
  await expect(page).toHaveURL(/\/dashboard/)

  await page.goto('/xslt-editor')
  await page.locator('input[type="file"][accept=".xsl,.xslt"]').setInputFiles(file('sablon.xslt', STYLESHEET))
  await page.locator('input[type="file"][accept=".xml"]').setInputFiles(file('fatura.xml', INVOICE))
  await expect(previewText()).toBeVisible()
  await monaco().evaluate((el) => { (el as HTMLElement).dataset.probe = 'same-node' })
})

test.afterAll(async () => { await page.close() })

test('tam ekrana girince editör alanı kaplar, önizleme mini pencereye taşınır', async () => {
  const widthBefore = (await monaco().boundingBox())!.width

  await page.getByRole('button', { name: 'Tam ekran editör', exact: true }).click()

  await expect(mini()).toBeVisible()
  await expect(previewText()).toBeVisible()
  await expect(frames()).toHaveCount(1)
  await expect(mini().locator(PREVIEW_FRAME)).toHaveCount(1)
  await expect(xmlTab()).toBeHidden()
  await expect(monaco()).toHaveAttribute('data-probe', 'same-node') // remount yok

  // Editör panel grubunun tamamını kaplamalı (panel kütüphanesi yükseltme bekçisi).
  const group = (await page.getByTestId('xslt-editor-panels').boundingBox())!
  const editor = (await monaco().boundingBox())!
  expect(Math.abs(editor.width - group.width)).toBeLessThan(2)
  expect(editor.width).toBeGreaterThan(widthBefore * 1.5)
})

test('mini pencere sürüklenir ve konumu kalıcıdır', async () => {
  const before = (await mini().boundingBox())!
  const handle = (await mini().getByTitle(/Sürükleyerek taşı/).boundingBox())!
  const startX = handle.x + 60
  const startY = handle.y + handle.height / 2

  await page.mouse.move(startX, startY)
  await page.mouse.down()
  await page.mouse.move(startX - 100, startY + 150, { steps: 10 }) // önizleme iframe'inin üzerinden geç
  await page.mouse.move(startX - 200, startY + 40, { steps: 10 })
  await page.mouse.up()

  const after = (await mini().boundingBox())!
  expect(Math.abs(after.x - (before.x - 200))).toBeLessThanOrEqual(1)
  expect(Math.abs(after.y - (before.y + 40))).toBeLessThanOrEqual(1)

  // Kayıt, pencerenin kapsayıcısının sağ-üst köşesine göre konumudur.
  const host = (await mini().locator('xpath=..').boundingBox())!
  const stored = await page.evaluate((key) => JSON.parse(localStorage.getItem(key)!), STORAGE_KEY)
  expect(stored.open).toBe(true)
  expect(Math.abs(stored.rect.right - (host.x + host.width - after.x - after.width))).toBeLessThanOrEqual(1)
  expect(Math.abs(stored.rect.top - (after.y - host.y))).toBeLessThanOrEqual(1)
})

test('mini pencere kapatılıp araç çubuğundan geri açılır', async () => {
  await mini().getByRole('button', { name: 'Mini önizlemeyi kapat' }).click()
  await expect(frames()).toHaveCount(0)

  await page.getByRole('button', { name: 'Mini önizlemeyi göster' }).click()
  await expect(frames()).toHaveCount(1)
  await expect(mini()).toBeVisible()
})

test('Ctrl+Shift+F Monaco odaktayken modu kapatır; önizleme sağ panele döner', async () => {
  await monaco().click({ position: { x: 200, y: 200 } })
  await page.keyboard.press('Control+Shift+F')

  await expect(xmlTab()).toBeVisible()
  await expect(mini()).toHaveCount(0)
  await expect(frames()).toHaveCount(1)
  await expect(monaco()).toHaveAttribute('data-probe', 'same-node')
})

test('Önizleme sekmesindeki buton tam ekran + mini pencereyi açar', async () => {
  await page.getByRole('button', { name: 'Önizlemeyi mini pencerede göster' }).click()

  await expect(mini()).toBeVisible()
  await expect(frames()).toHaveCount(1)

  await mini().getByRole('button', { name: /Sağ panele geri al/ }).click()
  await expect(xmlTab()).toBeVisible()
  await expect(frames()).toHaveCount(1)
})
