import { test, expect, request } from '@playwright/test'

// XsltCraft E2E smoke — auth akışı + korumalı sayfaların yüklenmesi.
// Test kullanıcısı public /api/auth/register ile oluşturulur (idempotent: 201 veya 409).
const API = 'http://localhost:5000'
const USER = {
  username: 'e2e_tester',
  email: 'e2e@test.local',
  password: 'E2ePass123',
  displayName: 'E2E Tester',
}

test.beforeAll(async () => {
  const ctx = await request.newContext()
  // 201 (yeni) ya da 409 (zaten var) — ikisi de kabul; register şifre formatı zorlamaz.
  await ctx.post(`${API}/api/auth/register`, {
    data: {
      username: USER.username,
      email: USER.email,
      password: USER.password,
      displayName: USER.displayName,
    },
  })
  await ctx.dispose()
})

async function login(page: import('@playwright/test').Page) {
  await page.goto('/auth/login')
  await page.locator('input[autocomplete="username"]').fill(USER.username)
  await page.locator('input[type="password"]').fill(USER.password)
  await page.getByRole('button', { name: 'Giriş Yap' }).click()
  await expect(page).toHaveURL(/\/dashboard/)
}

test('login → dashboard', async ({ page }) => {
  await login(page)
})

test('korumalı sayfalar auth ile yüklenir (login\'e atılmaz)', async ({ page }) => {
  await login(page)

  // authStore localStorage'a persist eder → tam-sayfa goto sonrası PrivateRoute geçer.
  await page.goto('/editor/new')
  await expect(page).toHaveURL(/\/editor\/new/)

  await page.goto('/xslt-editor')
  await expect(page).toHaveURL(/\/xslt-editor/)
})

test('auth olmadan korumalı sayfa login\'e yönlendirir', async ({ page }) => {
  // Temiz context (login yok) → PrivateRoute /auth/login'e atmalı.
  await page.goto('/editor/new')
  await expect(page).toHaveURL(/\/auth\/login/)
})
