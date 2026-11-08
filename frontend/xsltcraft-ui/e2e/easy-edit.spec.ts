import { test, expect, type Page, type Route } from '@playwright/test'

// Kolay düzenle: Dashboard'dan yüklenen XSLT'nin Kolay düzenle'de açılması, kaydedilmesi ve Şablonlarım'dan yeniden açılması.
// API tamamen taklit edilir (page.route): Pro/Free planı sunucu verisine bağlı kalmadan seçilir ve kayıt DB'ye yazılmaz.
// Dev sunucusu playwright.config'teki webServer ile kalkar; canlı API gerekmez.
const API = process.env.E2E_API_URL ?? 'http://localhost:5000'
const UI_ORIGIN = 'http://localhost:5173'
const USER = { username: 'e2e_tester', password: 'E2ePass123' }
const PNG = 'data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNkYPhfDwAChwGA60e6kgAAAABJRU5ErkJggg=='

const UPLOADED_XSLT = `<?xml version="1.0" encoding="UTF-8"?>
<xsl:stylesheet version="2.0" xmlns:xsl="http://www.w3.org/1999/XSL/Transform"
  xmlns:n1="urn:oasis:names:specification:ubl:schema:xsd:Invoice-2"
  xmlns:cbc="urn:oasis:names:specification:ubl:schema:xsd:CommonBasicComponents-2" exclude-result-prefixes="n1 cbc">
  <xsl:output method="html" encoding="UTF-8"/>
  <xsl:template match="/">
    <html><body>
      <table><tr><td><xsl:value-of select="//cbc:ID"/></td><td><img id="firma" style="width:120px" src="${PNG}"/></td></tr></table>
      <div id="notes"></div>
    </body></html>
  </xsl:template>
</xsl:stylesheet>`

// Analizin döndürdüğü işaretli metin (yuva işaretleri eklenmiş).
const MARKED_XSLT = UPLOADED_XSLT
  .replace('<img id="firma"', '<img data-xc-slot="existing-logo" data-xc-placeholder="logo" id="firma"')
  .replace('<div id="notes">', '<div id="notes" data-xc-slot="notes-box">')

const SLOTS = [
  { id: 'existing-logo', label: 'Logo yeri', group: 'Üst bilgi', defaultFor: 'logo', order: 0, removable: false },
  { id: 'notes-box', label: 'Notlar', group: 'Alt bilgi', defaultFor: null, order: 1, removable: false },
  { id: 'docinfo-top', label: 'Belge bilgisinin üstü', group: 'Üst bilgi', defaultFor: null, order: 2, removable: false },
]

function analysis(source: 'suggested' | 'marker', documentBank: unknown = null) {
  return {
    verdict: 'suitable',
    reasonCode: null,
    message: null,
    documentType: 'Invoice',
    slots: SLOTS,
    conditionalSlots: [],
    images: [{
      index: 0, role: 'logo', source, reason: 'Sağ üstte, firma bilgisinin yanında.', line: 7,
      previewDataUrl: PNG, movable: true, blocker: null, slotId: 'existing-logo',
    }],
    previous: null,
    features: [{ group: 'Üst bilgi', count: 1 }, { group: 'Alt bilgi', count: 1 }],
    warnings: [],
    notes: [],
    notesSupported: true,
    documentBank,
    xslt: MARKED_XSLT,
    error: null,
  }
}

function entitlements(plan: 'Free' | 'Pro') {
  const pro = plan === 'Pro'
  return {
    role: 'User', plan, isPrivileged: false,
    canDownloadGridXslt: pro, canSaveRawXslt: pro, canAccessRawXslt: pro, canUsePremiumThemes: pro,
    dailyAiRequestLimit: 0, dailyAiTokenLimit: 0, dailyTemplateExportLimit: 0,
    usage: { aiRequestCount: 0, aiTokensUsed: 0, templateExportCount: 0 },
  }
}

interface SavedTemplate {
  id: string
  name: string
  xsltContent: string
  xmlContent: string | null
  lastEditMode: 'editor' | 'easyEdit'
  createdAt: string
  updatedAt: string
}

interface MockApi {
  analyzeCalls: unknown[]
  templates: SavedTemplate[]
  unhandled: string[]
}

const CORS = {
  'Access-Control-Allow-Origin': UI_ORIGIN,
  'Access-Control-Allow-Credentials': 'true',
  'Access-Control-Allow-Headers': 'Authorization, Content-Type',
  'Access-Control-Allow-Methods': 'GET, POST, PUT, PATCH, DELETE, OPTIONS',
  'Access-Control-Expose-Headers': 'Retry-After',
}

const STAMP = '2026-11-07T10:00:00Z'

function summary(t: SavedTemplate) {
  return {
    id: t.id, name: t.name, createdAt: t.createdAt, updatedAt: t.updatedAt, isOwner: true, isShared: false,
    ownerId: 'u-1', ownerName: 'E2E Tester', folderId: null, isFavorite: false, lastEditMode: t.lastEditMode,
  }
}

/** Uygulamanın çağırdığı API uçlarının taklidi; bilinmeyen uç `unhandled`'a yazılır ve boş yanıt alır. */
async function mockApi(page: Page, plan: 'Free' | 'Pro', documentBank: unknown = null): Promise<MockApi> {
  const state: MockApi = { analyzeCalls: [], templates: [], unhandled: [] }
  const json = (route: Route, body: unknown, status = 200) => route.fulfill({ status, json: body, headers: CORS })

  await page.route(`${API}/**`, async (route) => {
    const req = route.request()
    const method = req.method()
    if (method === 'OPTIONS') return route.fulfill({ status: 204, headers: CORS })
    const path = new URL(req.url()).pathname

    if (path === '/api/auth/login') return json(route, { accessToken: 'e2e-token' })
    if (path === '/api/auth/me') return json(route, { id: 'u-1', email: 'e2e@test.local', displayName: 'E2E Tester', role: 'User' })
    if (path === '/api/auth/refresh') return json(route, { accessToken: 'e2e-token' })
    if (path === '/api/me/entitlements') return json(route, entitlements(plan))

    if (path === '/api/easy-edit/analyze') {
      state.analyzeCalls.push(req.postDataJSON())
      if (plan !== 'Pro') return json(route, { error: 'Pro gerekir', upgrade: true }, 402)
      // Kayıtlı (işaretli) metin yeniden analizde rolü işaretten okur.
      const body = req.postDataJSON() as { xslt: string }
      return json(route, analysis(body.xslt.includes('data-xc-placeholder') ? 'marker' : 'suggested', documentBank))
    }

    if (path === '/api/preview/compose') {
      const body = req.postDataJSON() as { xslt?: string }
      return json(route, {
        xslt: body.xslt ?? MARKED_XSLT,
        html: '<html><body><p>ONIZLEME_OK</p></body></html>',
        generationTimeMs: 1,
        warnings: [],
        appliedSlots: {},
        fixedNotesSupported: true,
      })
    }

    if (path === '/api/user-xslt-templates' && method === 'GET') return json(route, state.templates.map(summary))
    if (path === '/api/user-xslt-templates' && method === 'POST') {
      const body = req.postDataJSON() as { name: string; xsltContent: string; xmlContent?: string; lastEditMode?: 'editor' | 'easyEdit' }
      const t: SavedTemplate = {
        id: `00000000-0000-4000-8000-${String(state.templates.length + 1).padStart(12, '0')}`,
        name: body.name, xsltContent: body.xsltContent, xmlContent: body.xmlContent ?? null,
        lastEditMode: body.lastEditMode ?? 'editor', createdAt: STAMP, updatedAt: STAMP,
      }
      state.templates.push(t)
      return json(route, { ...summary(t), xsltContent: t.xsltContent, xmlContent: t.xmlContent }, 201)
    }
    const detail = /^\/api\/user-xslt-templates\/([^/]+)$/.exec(path)
    if (detail && method === 'GET') {
      const t = state.templates.find((x) => x.id === detail[1])
      return t ? json(route, { ...summary(t), xsltContent: t.xsltContent, xmlContent: t.xmlContent }) : json(route, { error: 'yok' }, 404)
    }
    if (/^\/api\/user-xslt-templates\/[^/]+\/(editing|lock)/.test(path)) {
      return json(route, { lockedByOther: false, editingUserId: null, editingUserName: null, updatedAt: STAMP })
    }

    state.unhandled.push(`${method} ${path}`)
    return method === 'GET' ? json(route, []) : route.fulfill({ status: 204, headers: CORS })
  })
  return state
}

async function login(page: Page) {
  await page.goto('/auth/login')
  await page.locator('input[autocomplete="username"]').fill(USER.username)
  await page.locator('input[type="password"]').fill(USER.password)
  await page.getByRole('button', { name: 'Giriş Yap' }).click()
  await expect(page).toHaveURL(/\/dashboard/)
}

const xsltFile = () => ({ name: 'firma-sablonu.xslt', mimeType: 'application/xml', buffer: Buffer.from(UPLOADED_XSLT) })
const xsltSlotInput = (page: Page) => page.locator('section[aria-labelledby="dash-xslt-title"] input[type="file"]').first()

test('Pro kullanıcı yüklediği XSLT\'yi Kolay düzenle ile açar, kaydeder ve Şablonlarım\'dan soru sorulmadan yeniden açar', async ({ page }) => {
  const api = await mockApi(page, 'Pro')
  await login(page)

  await xsltSlotInput(page).setInputFiles(xsltFile())
  const open = page.getByRole('button', { name: 'Kolay düzenle ile aç' })
  await expect(open).toHaveAttribute('aria-disabled', 'false')
  expect(api.analyzeCalls).toHaveLength(1)
  await open.click()

  await expect(page).toHaveURL(/\/easy-edit\/[0-9a-f-]{36}$/)
  // İşaretsiz (önerilen) görsel var: tek soru modalı açılır.
  const dialog = page.getByTestId('image-roles-dialog')
  await expect(dialog).toBeVisible()
  await expect(dialog.getByTestId('image-role-0')).toBeVisible()
  await dialog.getByTestId('image-roles-submit').click()
  await expect(dialog).toBeHidden()

  await page.getByRole('button', { name: 'Şablonlarıma kaydet' }).click()
  await expect(page).toHaveURL(/\/my-xslt-templates/)
  expect(api.templates).toHaveLength(1)
  const saved = api.templates[0]
  expect(saved.lastEditMode).toBe('easyEdit')
  expect(saved.name).toBe('firma-sablonu')

  await page.getByRole('button', { name: saved.name, exact: true }).first().click()
  await expect(page).toHaveURL(new RegExp(`/easy-edit/template/${saved.id}$`))
  // Çalışma alanı analizle yüklendi (rol değiştirme bağlantısı görünür); modal kendiliğinden açılmaz.
  await expect(page.getByTestId('change-image-roles')).toBeVisible()
  await expect(page.getByTestId('image-roles-dialog')).toHaveCount(0)
  expect(api.unhandled.filter((u) => u.includes('easy-edit') || u.includes('user-xslt'))).toEqual([])
})

test('Free kullanıcıda Kolay düzenle kartı kilitli görünür ve analiz isteği gönderilmez', async ({ page }) => {
  const api = await mockApi(page, 'Free')
  await login(page)

  await xsltSlotInput(page).setInputFiles(xsltFile())
  const card = page.getByLabel('Kolay düzenle', { exact: true })
  await expect(card.getByText('Kolay düzenle XsltCraft Pro ile kullanılabilir.')).toBeVisible()
  await expect(page.getByRole('button', { name: "Pro'ya geç" })).toBeVisible()
  await expect(page.getByRole('button', { name: 'Kolay düzenle ile aç' })).toHaveCount(0)
  await expect(page.getByRole('button', { name: 'Editörde aç' })).toBeVisible()
  expect(api.analyzeCalls).toHaveLength(0)
})

test('Belgedeki IBAN satırı panelde görünür; düzenlenince compose isteğine documentBankEdits olarak gider', async ({ page }) => {
  const documentBank = {
    found: true,
    canAdd: false,
    rows: [{ index: 0, bankName: 'Örnek Bank', iban: 'TR33 0006 1005 1978 6457 8413 26', bankNameEditable: false, deletable: true, line: 8 }],
  }
  await mockApi(page, 'Pro', documentBank)
  await login(page)

  await xsltSlotInput(page).setInputFiles(xsltFile())
  await page.getByRole('button', { name: 'Kolay düzenle ile aç' }).click()
  const dialog = page.getByTestId('image-roles-dialog')
  await dialog.getByTestId('image-roles-submit').click()
  await expect(dialog).toBeHidden()

  await page.getByRole('button', { name: /Banka \/ IBAN/ }).click()
  const name = page.getByLabel('Banka adı')
  await expect(name).toHaveValue('Örnek Bank')
  await expect(name).toHaveAttribute('readonly', '')
  await expect(page.getByRole('button', { name: 'Banka hesabı ekle' })).toHaveCount(0)

  const NEW_IBAN = 'TR12 0001 0000 0000 0000 0000 01'
  const composed = page.waitForRequest((req) => {
    if (!req.url().endsWith('/api/preview/compose') || req.method() !== 'POST') return false
    const body = req.postDataJSON() as { documentBankEdits?: { index: number | null; iban: string }[] }
    return body.documentBankEdits?.some((e) => e.index === 0 && e.iban === NEW_IBAN) ?? false
  })
  await page.getByLabel('IBAN', { exact: true }).fill(NEW_IBAN)
  const body = (await composed).postDataJSON() as { bankInfo: unknown[]; documentBankEdits: unknown[] }
  expect(body.documentBankEdits).toEqual([{ index: 0, bankName: 'Örnek Bank', iban: NEW_IBAN, remove: false }])
  expect(body.bankInfo).toEqual([])
})

interface ComposeBody {
  xslt?: string
  staticImages?: { index: number; slot: string | null; remove: boolean }[]
  freeImages?: { slot: string }[]
}

/** Koşulu sağlayan sonraki compose isteğinin gövdesi. */
const nextCompose = (page: Page, match: (body: ComposeBody) => boolean) =>
  page.waitForRequest((req) => req.url().endsWith('/api/preview/compose') && req.method() === 'POST'
    && match(req.postDataJSON() as ComposeBody)).then((req) => req.postDataJSON() as ComposeBody)

test('Dosyadaki logo iki kez taşınır: index ve temel metin aynı kalır; boşalan eski yer serbest görsele açılır', async ({ page }) => {
  await mockApi(page, 'Pro')
  await login(page)
  await xsltSlotInput(page).setInputFiles(xsltFile())
  await page.getByRole('button', { name: 'Kolay düzenle ile aç' }).click()
  const dialog = page.getByTestId('image-roles-dialog')
  await dialog.getByTestId('image-roles-submit').click()
  await expect(dialog).toBeHidden()

  const section = (title: RegExp) => page.locator('section').filter({ has: page.getByRole('button', { name: title }) })
  const logoSlot = section(/^Logo/).getByRole('combobox', { name: 'Konum' })

  let body = nextCompose(page, (b) => b.staticImages?.[0]?.slot === 'notes-box')
  await logoSlot.selectOption('notes-box')
  expect((await body).staticImages).toEqual([{ index: 0, remove: false, slot: 'notes-box' }])

  body = nextCompose(page, (b) => b.staticImages?.[0]?.slot === 'docinfo-top')
  await logoSlot.selectOption('docinfo-top')
  const second = await body
  expect(second.staticImages).toEqual([{ index: 0, remove: false, slot: 'docinfo-top' }])
  expect(second.xslt).toBe(MARKED_XSLT)

  // Logo yerinden ayrıldı: "Logo yeri" serbest görsel için hedef olur.
  await section(/Serbest görsel/).getByRole('button', { name: /Serbest görsel/ }).click()
  const free = section(/Serbest görsel/)
  await free.locator('input[type="file"]').setInputFiles({ name: 'ek.png', mimeType: 'image/png', buffer: Buffer.from(PNG.split(',')[1], 'base64') })
  const freeSlot = free.getByRole('combobox', { name: 'Konum' })
  await expect(freeSlot.locator('option[value="existing-logo"]')).toHaveCount(1)
  body = nextCompose(page, (b) => b.freeImages?.[0]?.slot === 'existing-logo')
  await freeSlot.selectOption('existing-logo')
  expect((await body).staticImages).toEqual([{ index: 0, remove: false, slot: 'docinfo-top' }])

  // Logo kendi yerine dönünce eski yer dolar: serbest görsel gönderilmez, konum listesinden çıkar.
  body = nextCompose(page, (b) => (b.staticImages?.length ?? 0) === 0)
  await logoSlot.selectOption('existing-logo')
  expect((await body).freeImages ?? []).toEqual([])
  await expect(freeSlot.locator('option[value="existing-logo"]')).toHaveCount(0)
})
