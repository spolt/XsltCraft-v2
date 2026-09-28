import { test, expect, type Page } from '@playwright/test'

// XSLT Editör yükleme doğrulaması: XML alanı yalnız veri belgesi, XSLT alanı yalnız stylesheet kabul eder.
// `accept` yalnız ipucu olduğu için dosyalar doğrudan input'a verilir (seçici atlanmış gibi).
const USER = { username: 'e2e_tester', password: 'E2ePass123' }

const STYLESHEET = `<?xml version="1.0" encoding="UTF-8"?>
<xsl:stylesheet version="1.0" xmlns:xsl="http://www.w3.org/1999/XSL/Transform">
  <xsl:template match="/"><html><body>ok</body></html></xsl:template>
</xsl:stylesheet>`

const INVOICE = `<?xml version="1.0" encoding="UTF-8"?>
<Invoice xmlns="urn:oasis:names:specification:ubl:schema:xsd:Invoice-2"><ID>X1</ID></Invoice>`

const file = (name: string, content: string) => ({ name, mimeType: 'application/xml', buffer: Buffer.from(content) })

// Login global rate-limit'e (10/dk) takılmamak için tek oturum, testler sırayla aynı sayfada.
test.describe.configure({ mode: 'serial' })
let page: Page

test.beforeAll(async ({ browser }) => {
  page = await browser.newPage()
  await page.goto('/auth/login')
  await page.locator('input[autocomplete="username"]').fill(USER.username)
  await page.locator('input[type="password"]').fill(USER.password)
  await page.getByRole('button', { name: 'Giriş Yap' }).click()
  await expect(page).toHaveURL(/\/dashboard/)
})

test.afterAll(async () => { await page.close() })

// Her test boş yükleme ekranından başlar (tam sayfa yükleme editör state'ini sıfırlar).
async function openEditor() {
  await page.goto('/xslt-editor')
  await expect(page.getByRole('heading', { name: 'XSLT Editör' })).toBeVisible()
}

const xmlInput = () => page.locator('input[type="file"][accept=".xml"]')
const xsltInput = () => page.locator('input[type="file"][accept=".xsl,.xslt"]')

test('XML alanı .xslt uzantılı dosyayı reddeder', async () => {
  await openEditor()
  await xmlInput().setInputFiles(file('sablon.xslt', STYLESHEET))
  await expect(page.getByText('bir XML dosyası değil')).toBeVisible()
  await expect(page.getByText('dosyası yüklendi')).toHaveCount(0) // hiçbir şey yüklenmedi
})

test('XML alanı .xml uzantısıyla gizlenmiş XSLT\'yi reddeder', async () => {
  await openEditor()
  await xmlInput().setInputFiles(file('fatura.xml', STYLESHEET))
  await expect(page.getByText('Bu dosya bir XSLT şablonu.')).toBeVisible()
  await expect(page.getByText('dosyası yüklendi')).toHaveCount(0)
})

test('XSLT alanı stylesheet olmayan belgeyi reddeder', async () => {
  await openEditor()
  await xsltInput().setInputFiles(file('sablon.xslt', INVOICE))
  await expect(page.getByText('XSLT şablonu değil')).toBeVisible()
  await expect(page.getByText('dosyası yüklendi')).toHaveCount(0)
})

test('geçerli XML yüklenir', async () => {
  await openEditor()
  await xmlInput().setInputFiles(file('fatura.xml', INVOICE))
  await expect(page.getByText('XML dosyası yüklendi')).toBeVisible()
})
