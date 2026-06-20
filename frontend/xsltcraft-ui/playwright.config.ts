import { defineConfig, devices } from '@playwright/test'

// XsltCraft E2E (Mode B). Canlı app'e karşı koşar: UI :5173, API :5000.
// Backend + Postgres ayrı ayağa kalkmalı (bkz. .claude/skills/playwright-e2e).
export default defineConfig({
  testDir: './e2e',
  fullyParallel: true,
  forbidOnly: !!process.env.CI,
  retries: process.env.CI ? 1 : 0,
  reporter: 'list',
  use: {
    baseURL: 'http://localhost:5173',
    trace: 'on-first-retry',
  },
  projects: [{ name: 'chromium', use: { ...devices['Desktop Chrome'] } }],
  // Lokalde çalışan dev sunucusunu yeniden kullan; CI'da kendisi başlatır.
  webServer: {
    command: 'npm run dev',
    url: 'http://localhost:5173',
    reuseExistingServer: !process.env.CI,
    timeout: 120_000,
  },
})
