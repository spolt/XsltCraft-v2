import { readFile } from 'node:fs/promises'
import { test, expect, request, type APIResponse, type BrowserContext, type Download, type Page } from '@playwright/test'
import type { editor as MonacoEditor } from 'monaco-editor'

// Geliştirici Modu veri kaybı senaryoları: gerçek API, gerçek tema, gerçek compose ve önizleme.
// Editöre gerçek klavye ve fareyle yazılır; Monaco yalnız okumak ve satırı görünür kılmak için kullanılır.
// Kayıt ucu çağrılmaz, DB'ye şablon yazılmaz; indirme bildirimi taklit edilir.
const API = process.env.E2E_API_URL ?? 'http://localhost:5000'
const USER = { username: 'e2e_tester', email: 'e2e@test.local', password: 'E2ePass123', displayName: 'E2E Tester' }

const LEAVE_CONFIRM = 'Kaydedilmemiş değişiklikler var. Sayfadan ayrılmak istediğinize emin misiniz?'
const EDITED_NOTICE = "Geliştirici Modu'ndaki XSLT düzenlemeleri uygulanıyor."
const DIRTY_LABEL = 'Kaydedilmemiş değişiklik'
const STYLESHEET_END = '</xsl:stylesheet>'
const BANK_NAME = 'E2E Bankasi'

interface Theme { id: string; name: string; documentType: string; isPremium: boolean }
type ProbeWindow = typeof window & {
  monaco?: { editor: { getEditors(): MonacoEditor.ICodeEditor[] } }
  __xsltEditor: () => MonacoEditor.ICodeEditor
  __sameDocument?: boolean
}

// Monaco'nun yanında XML sekmesinin salt-okunur editörü de olabilir: XSLT editörü içeriğinden bulunur.
function installProbes() {
  const w = window as ProbeWindow
  w.__xsltEditor = () => {
    const found = w.monaco?.editor.getEditors().find((e) => e.getModel()?.getValue().includes('<xsl:stylesheet'))
    if (!found) throw new Error('XSLT editörü yok')
    return found
  }
}

test.describe.configure({ mode: 'default' }) // Sıralı (tek oturum), ama bir senaryo kırılınca diğerleri yine koşar.
test.setTimeout(120_000)

let context: BrowserContext
let theme: Theme
let page: Page
let dialogs: ReturnType<typeof trackDialogs>

// Login dakikada 10 istekle sınırlı: tek oturum, her senaryoda yeni sayfa ve boş taslak.
test.beforeAll(async ({ browser }) => {
  const api = await request.newContext({ baseURL: API })
  await api.post('/api/auth/register', { data: USER }) // 201 ya da 409: ikisi de kabul
  const themes: Theme[] = await (await api.get('/api/templates')).json()
  await api.dispose()
  const found = themes.find((t) => !t.isPremium && t.documentType === 'Invoice')
  if (!found) throw new Error('Veritabanında ücretsiz e-Fatura teması yok: bu senaryolar gerçek tema ister.')
  theme = found

  context = await browser.newContext()
  const login = await context.newPage()
  await login.goto('/auth/login')
  await login.locator('input[autocomplete="username"]').fill(USER.username)
  await login.locator('input[type="password"]').fill(USER.password)
  await login.getByRole('button', { name: 'Giriş Yap' }).click()
  await expect(login).toHaveURL(/\/dashboard/)
  await login.close()
})

test.afterAll(async () => { await context.close() })

test.beforeEach(async () => {
  page = await context.newPage()
  await page.addInitScript(installProbes)
  dialogs = trackDialogs(page)
  await page.route('**/api/activity/download', (route) => route.fulfill({
    status: 204,
    headers: { 'Access-Control-Allow-Origin': new URL(page.url()).origin, 'Access-Control-Allow-Credentials': 'true' },
  }))
})

test.afterEach(async () => { await page.close() })

// ─── Yardımcılar ────────────────────────────────────────────────────────────

function trackDialogs(target: Page) {
  const state = { seen: [] as string[], answer: 'dismiss' as 'accept' | 'dismiss' }
  target.on('dialog', async (dialog) => {
    state.seen.push(dialog.message())
    await (state.answer === 'accept' ? dialog.accept() : dialog.dismiss())
  })
  return state
}

const editorText = () => page.evaluate(() => {
  return (window as ProbeWindow).__xsltEditor().getModel()!.getValue()
}).catch(() => '')

async function waitForEditor() {
  await expect.poll(editorText, { timeout: 45_000 }).toContain(STYLESHEET_END)
  return editorText()
}

const cursor = () => page.evaluate(() => {
  const editor = (window as ProbeWindow).__xsltEditor()
  const position = editor.getPosition()!
  return {
    line: position.lineNumber,
    column: position.column,
    lineText: editor.getModel()!.getLineContent(position.lineNumber),
    focused: editor.hasTextFocus(),
  }
})

const lastTemplateLine = (text: string) => text.split(/\r?\n/).findLastIndex((l) => /^\s*<xsl:template\b/.test(l)) + 1

/** Satırı görünür kılar ve başına gerçek fareyle tıklar. Son şablon satırı, compose'un eklediği bloğun altındadır. */
async function clickLineStart(line: number) {
  const point = await page.evaluate(async (lineNumber) => {
    const editor = (window as ProbeWindow).__xsltEditor()
    editor.revealLineInCenter(lineNumber)
    await new Promise((done) => requestAnimationFrame(() => requestAnimationFrame(done)))
    const at = editor.getScrolledVisiblePosition({ lineNumber, column: 1 })!
    const box = editor.getDomNode()!.getBoundingClientRect()
    return { x: box.left + at.left + 1, y: box.top + at.top + at.height / 2 }
  }, line)
  await page.mouse.click(point.x, point.y)
  await expect.poll(cursor).toMatchObject({ line, column: 1, focused: true })
}

/** XML yorumu her ara durumda yazıma engel değil, bittiğinde metni well-formed bırakır. */
async function typeComment(token: string) {
  const comment = `<!--${token}-->`
  await page.keyboard.type(comment, { delay: 5 })
  await page.keyboard.press('Escape') // Açık kalan öneri kutusu sonraki adıma karışmasın.
  return comment
}

async function editLastTemplateLine(token: string) {
  const line = lastTemplateLine(await editorText())
  await clickLineStart(line)
  return typeComment(token)
}

const occurrences = (text: string, part: string) => text.split(part).length - 1

async function expectIntactXslt(text: string) {
  const wellFormed = await page.evaluate((xml) => (
    new DOMParser().parseFromString(xml, 'application/xml').getElementsByTagName('parsererror').length === 0
  ), text)
  expect(wellFormed, 'XSLT well-formed olmalı').toBe(true)
  expect(text.trimEnd().endsWith(STYLESHEET_END), `${STYLESHEET_END} sonrasına yazı gitmemeli`).toBe(true)
}

const bankSection = () => page.getByRole('button', { name: /^Banka \/ IBAN/ })
const ibanInput = () => page.getByLabel('IBAN', { exact: true })
const dirtyLabel = () => page.getByText(DIRTY_LABEL)
const editedNotice = () => page.getByRole('status').filter({ hasText: EDITED_NOTICE })
const downloadButton = () => page.getByRole('button', { name: 'XSLT İndir' })
const moduleLink = (name: string) => page.getByRole('navigation', { name: 'Modüller' }).getByRole('link', { name })

async function openBankSection() {
  if (await bankSection().getAttribute('aria-expanded') !== 'true') await bankSection().click()
  await expect(ibanInput()).toBeVisible()
}

async function enterIban(iban: string) {
  await openBankSection()
  await page.getByLabel('Banka adı', { exact: true }).fill(BANK_NAME)
  await ibanInput().fill(iban)
}

/** Geliştirici Modu'nu boş taslakla açar ve Düzenle panelini gösterir. */
async function openDevMode() {
  await page.goto(`/dev-mode/${theme.id}`)
  await waitForEditor()
  await page.getByRole('button', { name: 'Düzenle', exact: true }).click()
  await expect(bankSection()).toBeVisible()
}

interface HeldResponse {
  sentXslt: string
  response: APIResponse
  responseXslt: string
  release: () => void
}

/** Compose isteği gerçek sunucuya gider; yanıt `release` çağrılana kadar tarayıcıya verilmez. */
async function holdComposeResponses() {
  const held: HeldResponse[] = []
  await page.route('**/api/preview/compose', async (route) => {
    try {
      const response = await route.fetch()
      const { xslt } = await response.json()
      const released = new Promise<void>((release) => {
        held.push({ sentXslt: route.request().postDataJSON().xslt, response, responseXslt: xslt, release })
      })
      await released
      await route.fulfill({ response })
    } catch {
      // Tarayıcı isteği iptal ettiyse ya da sayfa kapandıysa yanıtı alacak kimse kalmamıştır.
    }
  })
  return held
}

async function downloadXslt() {
  await expect(downloadButton()).toBeEnabled({ timeout: 30_000 })
  const [download]: [Download, void] = await Promise.all([page.waitForEvent('download'), downloadButton().click()])
  return readFile(await download.path(), 'utf8')
}

// ─── Senaryolar ─────────────────────────────────────────────────────────────

test('çıkış koruması: kaydedilmemiş düzenlemede header gezinmesi sorar, iptalde metin ve sayfa durur, kabulde çıkar', async () => {
  await openDevMode()
  await expect(dirtyLabel()).toHaveCount(0)

  const comment = await editLastTemplateLine('E2E_LEAVE_GUARD')
  await expect(dirtyLabel()).toBeVisible()
  const devModeUrl = page.url()
  const editedText = await editorText()
  expect(editedText).toContain(comment)
  await page.locator('.monaco-editor').first().evaluate((el) => { (el as HTMLElement).dataset.probe = 'same-node' })

  dialogs.answer = 'dismiss'
  await moduleLink('Şablonlarım').click()
  expect(dialogs.seen).toEqual([LEAVE_CONFIRM])
  expect(page.url()).toBe(devModeUrl)
  expect(await editorText()).toBe(editedText)
  await expect(page.locator('.monaco-editor').first()).toHaveAttribute('data-probe', 'same-node')
  await expect(dirtyLabel()).toBeVisible()

  dialogs.answer = 'accept'
  await moduleLink('Şablonlarım').click()
  await expect(page).toHaveURL(/\/my-xslt-templates/)
  expect(dialogs.seen).toEqual([LEAVE_CONFIRM, LEAVE_CONFIRM])
})

test('geç compose yanıtı: yanıt beklerken yazılan metin ezilmez, IBAN nihai metne işlenir', async () => {
  const iban = 'TR11 0006 1005 1978 6457 8413 01'
  await openDevMode()
  const held = await holdComposeResponses()

  await enterIban(iban)
  await expect.poll(() => held.length, { timeout: 15_000 }).toBe(1)
  expect(held[0].response.status()).toBe(200)
  expect(held[0].responseXslt).toContain(iban) // Bekletilen yanıt gerçek ve metni ezmeye yeter.

  const originalLine = (await editorText()).split(/\r?\n/)[lastTemplateLine(await editorText()) - 1]
  const comment = await editLastTemplateLine('E2E_LATE_COMPOSE')
  held[0].release()

  // İkinci istek geldiğinde eski yanıtın yazma fırsatı geçmiştir; ikincisi de bekletildiğinden metin yalnız kullanıcınındır.
  await expect.poll(() => held.length, { timeout: 15_000 }).toBe(2)
  expect(held[1].response.status()).toBe(200)
  expect(held[1].sentXslt).toContain(comment)
  const whileWaiting = await editorText()
  expect(whileWaiting).toContain(comment)
  expect(whileWaiting).not.toContain(iban)

  held[1].release()
  await expect.poll(editorText, { timeout: 15_000 }).toContain(iban)

  const finalText = await editorText()
  expect(occurrences(finalText, comment)).toBe(1)
  expect(occurrences(finalText, iban)).toBe(1)
  expect(finalText).toContain(comment + originalLine)
  await expectIntactXslt(finalText)
  await expect(ibanInput()).toHaveValue(iban)
})

test('compose sonrası imleç: tıklanan satırda kalır, yazı oraya gider, metin well-formed kalır', async () => {
  const iban = 'TR22 0006 1005 1978 6457 8413 02'
  await openDevMode()
  const held = await holdComposeResponses()

  await enterIban(iban)
  const before = await editorText()
  const line = lastTemplateLine(before)
  const lineText = before.split(/\r?\n/)[line - 1]
  await clickLineStart(line)

  await expect.poll(() => held.length, { timeout: 15_000 }).toBe(1)
  held[0].release()
  await expect.poll(editorText, { timeout: 15_000 }).toContain(iban)

  // Blok tıklanan satırın üstüne eklenir: satır numarası kayar, imleç aynı içerikte kalır.
  const afterCompose = await cursor()
  expect(afterCompose.line).toBeGreaterThan(line)
  expect(afterCompose).toMatchObject({ column: 1, lineText, focused: true })

  const comment = await typeComment('E2E_CURSOR')
  expect(await cursor()).toMatchObject({
    line: afterCompose.line,
    column: comment.length + 1,
    lineText: comment + lineText,
  })

  const finalText = await editorText()
  expect(occurrences(finalText, comment)).toBe(1)
  expect(occurrences(finalText, iban)).toBe(1)
  await expectIntactXslt(finalText)
})

test('çift yönlü taslak: geçişlerde varlık ve elle düzenleme korunur, iner; kütüphaneden yeniden açılış boş başlar', async () => {
  const iban = 'TR33 0006 1005 1978 6457 8413 03'

  await test.step('Tema Kullan\'da girilen IBAN Geliştirici Modu\'nda panelde ve XSLT\'de', async () => {
    await page.goto(`/theme-use/${theme.id}`)
    await page.evaluate(() => { (window as ProbeWindow).__sameDocument = true })
    await enterIban(iban)
    await page.getByRole('button', { name: 'XSLT editörünü aç' }).click()
    await expect(page).toHaveURL(new RegExp(`/dev-mode/${theme.id}`))
    expect(await waitForEditor()).toContain(iban)
    await expect(bankSection()).toContainText('1 hesap')
    await openBankSection()
    await expect(ibanInput()).toHaveValue(iban)
  })

  const comment = await editLastTemplateLine('E2E_ROUND_TRIP')

  await test.step('Tema Kullan\'a dönüş sormaz, bilgi şeridi görünür, inen XSLT düzenlemeyi içerir', async () => {
    await page.getByRole('button', { name: "Tema Kullan'a dön" }).click()
    await expect(page).toHaveURL(new RegExp(`/theme-use/${theme.id}`))
    await expect(editedNotice()).toBeVisible()
    expect(dialogs.seen).toEqual([])
    await openBankSection()
    await expect(ibanInput()).toHaveValue(iban)

    const downloaded = await downloadXslt()
    expect(occurrences(downloaded, comment)).toBe(1)
    expect(occurrences(downloaded, iban)).toBe(1)
    await expectIntactXslt(downloaded)
  })

  await test.step('tarayıcı Geri ve İleri ile geçişte korunur', async () => {
    await page.goBack()
    await expect(page).toHaveURL(new RegExp(`/dev-mode/${theme.id}`))
    const text = await waitForEditor()
    expect(occurrences(text, comment)).toBe(1)
    expect(occurrences(text, iban)).toBe(1)
    await openBankSection()
    await expect(ibanInput()).toHaveValue(iban)

    await page.goForward()
    await expect(page).toHaveURL(new RegExp(`/theme-use/${theme.id}`))
    await expect(editedNotice()).toBeVisible()
    await openBankSection()
    await expect(ibanInput()).toHaveValue(iban)
    expect(occurrences(await downloadXslt(), comment)).toBe(1)
    expect(dialogs.seen).toEqual([])
  })

  await test.step('Tema Kütüphanesi\'ne çıkış sorar, yeniden açılan tema boş başlar', async () => {
    dialogs.answer = 'accept'
    await page.getByRole('navigation', { name: 'Konum' }).getByRole('link', { name: 'Tema Kütüphanesi' }).click()
    await expect(page).toHaveURL(/\/templates/)
    expect(dialogs.seen).toEqual([LEAVE_CONFIRM])

    await page.getByRole('article', { name: theme.name, exact: true }).getByRole('button', { name: 'Kullan', exact: true }).click()
    await expect(page).toHaveURL(new RegExp(`/theme-use/${theme.id}`))
    await expect(bankSection()).toContainText('Boş')
    await openBankSection()
    await expect(ibanInput()).toHaveValue('')
    await expect(editedNotice()).toHaveCount(0)

    await page.getByRole('button', { name: 'XSLT editörünü aç' }).click()
    await expect(page).toHaveURL(new RegExp(`/dev-mode/${theme.id}`))
    const text = await waitForEditor()
    expect(text).not.toContain(iban)
    expect(text).not.toContain(comment)
    await expect(dirtyLabel()).toHaveCount(0)
    await expect(page.getByRole('button', { name: 'Düzenle', exact: true })).toHaveAttribute('aria-pressed', 'false')

    // Sayfa yenilenseydi taslak zaten silinirdi: kanıt ancak aynı belgede geçerli.
    expect(await page.evaluate(() => (window as ProbeWindow).__sameDocument)).toBe(true)
    expect(dialogs.seen).toEqual([LEAVE_CONFIRM])
  })
})

// ─── Dalga 2 ────────────────────────────────────────────────────────────────

const CORS = () => ({
  'Access-Control-Allow-Origin': new URL(page.url()).origin,
  'Access-Control-Allow-Credentials': 'true',
  'Access-Control-Expose-Headers': 'Retry-After',
})
const composeReply = (xslt: string) => ({ xslt, html: null, generationTimeMs: 1, warnings: [], appliedSlots: {}, fixedNotesSupported: true })
const previewFrames = () => page.locator('iframe[title="Canlı Önizleme"]')
const tagMonaco = () => page.locator('.monaco-editor').first().evaluate((el) => { (el as HTMLElement).dataset.probe = 'same-node' })
const expectSameMonaco = () => expect(page.locator('.monaco-editor').first()).toHaveAttribute('data-probe', 'same-node')

/** Sohbet yanıtı taklit: model çıktısı deterministik değil, panelin davranışı sınanır. */
async function mockAi(reply: () => string) {
  await page.route('**/api/ai/status', (route) => route.fulfill({ json: { enabled: true, vision: false }, headers: CORS() }))
  await page.route('**/api/ai/assistant', (route) => route.fulfill({
    headers: CORS(),
    contentType: 'application/x-ndjson',
    body: `${JSON.stringify({ type: 'delta', text: reply() })}\n${JSON.stringify({ type: 'done', provider: 'taklit', model: 'taklit', ms: 1 })}\n`,
  }))
}

test('iki bölgeli yazım: aradaki imleç aynı içerikte kalır, tek geri-al adımıdır', async () => {
  await openDevMode()
  const before = await editorText()
  const lines = before.split(/\r?\n/)
  const line = Math.floor(lines.length / 2)
  let release = () => {}
  const held = new Promise<void>((done) => { release = done })
  await page.route('**/api/preview/compose', async (route) => {
    await held
    const edited = (route.request().postDataJSON().xslt as string).replace(/\r\n/g, '\n').split('\n')
    edited.splice(edited.length - 1, 0, '<!--E2E_SON-->')
    edited.splice(1, 0, '<!--E2E_BAS_1-->', '<!--E2E_BAS_2-->')
    await route.fulfill({ json: composeReply(edited.join('\n')), headers: CORS() })
  })

  await enterIban('TR44 0006 1005 1978 6457 8413 04')
  await clickLineStart(line)
  await page.keyboard.press('ArrowRight')
  await page.keyboard.press('ArrowRight')
  release()
  await expect.poll(editorText, { timeout: 15_000 }).toContain('E2E_SON')

  expect(await cursor()).toMatchObject({ line: line + 2, column: 3, lineText: lines[line - 1] })
  await page.keyboard.press('Control+z')
  expect(await editorText()).toBe(before)
})

test('sunucu meşgul: 429 hata şeridi göstermez, Retry-After beklenir, değişiklik uygulanır', async () => {
  const iban = 'TR55 0006 1005 1978 6457 8413 05'
  await openDevMode()
  const attempts: number[] = []
  await page.route('**/api/preview/compose', async (route) => {
    attempts.push(Date.now())
    if (attempts.length > 2) return route.continue()
    await route.fulfill({ status: 429, json: { error: 'meşgul' }, headers: { ...CORS(), 'Retry-After': '1' } })
  })

  await enterIban(iban)
  await expect.poll(editorText, { timeout: 15_000 }).toContain(iban)
  expect(attempts).toHaveLength(3)
  expect(attempts[1] - attempts[0]).toBeGreaterThanOrEqual(900)
  await expect(page.getByRole('complementary', { name: 'Tema düzenleme' }).getByRole('alert')).toHaveCount(0)
})

test('AI Asistan: sohbet sekme değişiminde kalır, uygulama geri alınır, AI sekmesinde iframe yok', async () => {
  const marker = '<!--E2E_AI-->'
  let before = ''
  await mockAi(() => `\`\`\`xml\n${before.replace(STYLESHEET_END, marker + STYLESHEET_END)}\n\`\`\``)
  await openDevMode()
  await tagMonaco()
  before = await editorText()

  const tabs = page.getByRole('tablist', { name: 'Önizleme bölmesi' })
  await tabs.getByRole('tab', { name: 'AI Asistan' }).click()
  await expect(previewFrames()).toHaveCount(0)
  await page.getByLabel('AI Asistana sor').fill('E2E_SORU')
  await page.getByRole('button', { name: 'Gönder' }).click()
  const apply = page.getByRole('button', { name: 'Editöre uygula' })
  await expect(apply).toBeVisible()

  await tabs.getByRole('tab', { name: 'Önizleme' }).click()
  await expect(previewFrames()).toHaveCount(1)
  await tabs.getByRole('tab', { name: 'AI Asistan' }).click()
  await expect(previewFrames()).toHaveCount(0)
  await expect(page.getByText('E2E_SORU')).toBeVisible()

  await apply.click()
  await page.getByRole('button', { name: 'Uygula', exact: true }).click()
  await expect.poll(editorText).toContain(marker)
  await expect(dirtyLabel()).toBeVisible()
  await expectSameMonaco()

  await page.evaluate(() => (window as ProbeWindow).__xsltEditor().focus())
  await page.keyboard.press('Control+z')
  await expect.poll(editorText).toBe(before)
})

test('AI uygulaması metinden düşürdüğü varlığı kaybettirmez: compose bloğu yeniden yazar', async () => {
  const png = Buffer.from('iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8DwHwAFBQIAX8jx0gAAAABJRU5ErkJggg==', 'base64')
  const marker = '<!--E2E_AI-->'
  const block = 'data-xc-overrides'
  let before = ''
  await mockAi(() => `\`\`\`xml\n${before.replace(STYLESHEET_END, marker + STYLESHEET_END)}\n\`\`\``)
  await openDevMode()
  before = await editorText()
  expect(before).not.toContain(block)

  await page.getByRole('tablist', { name: 'Önizleme bölmesi' }).getByRole('tab', { name: 'AI Asistan' }).click()
  await page.getByLabel('AI Asistana sor').fill('E2E_SORU')
  await page.getByRole('button', { name: 'Gönder' }).click()
  const apply = page.getByRole('button', { name: 'Editöre uygula' })
  await expect(apply).toBeVisible()

  const aside = page.getByRole('complementary', { name: 'Tema düzenleme' })
  await aside.locator('input[type="file"]').first().setInputFiles({ name: 'logo.png', mimeType: 'image/png', buffer: png })
  await expect.poll(editorText, { timeout: 15_000 }).toContain(block)

  await apply.click()
  await page.getByRole('button', { name: 'Uygula', exact: true }).click()
  await expect.poll(editorText).toContain(marker)
  await expect.poll(editorText, { timeout: 15_000 }).toContain(block)
  await expect.poll(editorText).toContain(marker)
})

test('Alanları göster: logo gerçek fareyle yuvaya bırakılır, XSLT güncellenir, tek iframe, Monaco aynı düğüm', async () => {
  const png = Buffer.from('iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8DwHwAFBQIAX8jx0gAAAABJRU5ErkJggg==', 'base64')
  await openDevMode()
  await tagMonaco()
  const aside = page.getByRole('complementary', { name: 'Tema düzenleme' })
  await aside.locator('input[type="file"]').first().setInputFiles({ name: 'logo.png', mimeType: 'image/png', buffer: png })
  await expect(page.getByRole('button', { name: /^Logo/ })).toContainText('Eklendi')

  const toggle = aside.getByRole('button', { name: 'Alanları göster' })
  await toggle.click()
  await expect(toggle).toHaveAttribute('aria-pressed', 'true')
  await expect(previewFrames()).toHaveCount(1)
  const frame = page.frameLocator('iframe[title="Canlı Önizleme"]')
  await expect.poll(() => frame.locator('[data-xc-slot]').count(), { timeout: 15_000 }).toBeGreaterThan(1)

  const slots = await frame.locator('[data-xc-slot]').evaluateAll((els) => els.map((el) => el.getAttribute('data-xc-slot')!))
  const slot = slots[slots.length - 1]
  const composed = page.waitForResponse((r) => r.url().endsWith('/api/preview/compose') && r.request().postDataJSON().assets[0]?.slot === slot)
  const chip = aside.getByRole('button', { name: /önizlemedeki bir konuma sürükleyin/ })
  const target = frame.locator(`[data-xc-slot="${slot}"]`).first()
  await target.scrollIntoViewIfNeeded()
  const from = (await chip.boundingBox())!
  const to = (await target.boundingBox())!
  await page.mouse.move(from.x + from.width / 2, from.y + from.height / 2)
  await page.mouse.down()
  await page.mouse.move(from.x + from.width / 2 + 12, from.y + from.height / 2 + 12, { steps: 3 })
  await page.mouse.move(to.x + to.width / 2, to.y + to.height / 2, { steps: 10 })
  await page.mouse.up()
  expect((await composed).status()).toBe(200)

  const placed = frame.locator(`[data-xc-slot="${slot}"] [data-xc-asset="logo"], [data-xc-slot="${slot}"][data-xc-asset="logo"]`)
  await expect(placed.first()).toBeAttached({ timeout: 15_000 })
  await expect(page.getByRole('region', { name: 'Alan yerleşimi' })).toBeVisible()
  await expect(previewFrames()).toHaveCount(1)
  await page.keyboard.press('Escape')
  await expect(toggle).toHaveAttribute('aria-pressed', 'false')
  await expect(toggle).toBeFocused()
  await expect(previewFrames()).toHaveCount(1)
  await expectSameMonaco()
})
