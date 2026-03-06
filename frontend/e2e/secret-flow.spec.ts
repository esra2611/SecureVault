import { test, expect } from '@playwright/test'

test.describe('Mobile viewport smoke', () => {
  test('home loads and create flow is visible on mobile', async ({ page }, testInfo) => {
    test.skip(testInfo.project.name !== 'mobile', 'Smoke runs only on mobile project')
    await page.goto('/')
    await expect(page.getByRole('heading', { name: /SecureVault/i })).toBeVisible()
    await expect(page.getByLabel(/your secret/i)).toBeVisible()
    await expect(page.getByLabel(/expiry/i)).toBeVisible()
    await expect(page.getByRole('button', { name: /Create link/i })).toBeVisible()
  })
})

test.describe('Secret flow', () => {
  test('create secret and reveal once', async ({ page }) => {
    await page.goto('/')
    await expect(page.getByRole('heading', { name: /SecureVault/i })).toBeVisible()

    await page.getByLabel(/your secret/i).fill('E2E test secret')
    await page.getByLabel(/expiry/i).selectOption('burn')
    await page.getByRole('button', { name: /Create link/i }).click()

    await expect(page.getByText(/Your secret link/i)).toBeVisible()
    const link = page.locator('a[href*="/s/"]')
    await expect(link).toBeVisible()
    const href = await link.getAttribute('href')
    expect(href).toContain('/s/')

    const firstReveal = await page.goto(href!)
    expect(firstReveal?.status()).toBe(200)
    await expect(page.getByText(/E2E test secret/)).toBeVisible()

    const secondReveal = await page.goto(href!)
    expect(secondReveal?.status()).toBe(404)
    await expect(page.getByText(/expired or has already been viewed/)).toBeVisible()
  })

  test('empty secret shows UI error and button is disabled', async ({ page }) => {
    await page.goto('/')
    await expect(page.getByRole('heading', { name: /SecureVault/i })).toBeVisible()
    await expect(page.getByRole('button', { name: /Create link/i })).toBeDisabled()
    await expect(page.getByText(/0\/1000/)).toBeVisible()
    await page.getByLabel(/your secret/i).fill('x')
    await page.getByLabel(/expiry/i).selectOption('1h')
    await page.getByLabel(/your secret/i).fill('')
    await page.evaluate(() => {
      document.querySelector('form')?.requestSubmit()
    })
    await expect(page.getByText(/Secret cannot be empty/i)).toBeVisible()
    await expect(page.getByText(/Your secret link/i)).not.toBeVisible()
  })

  test('whitespace-only secret treated as empty', async ({ page }) => {
    await page.goto('/')
    await page.getByLabel(/your secret/i).fill('   ')
    await page.getByLabel(/expiry/i).selectOption('1h')
    await page.getByRole('button', { name: /Create link/i }).click()
    await expect(page.getByText(/Secret cannot be empty/i)).toBeVisible()
  })

  test('1001 chars shows UI error', async ({ page }) => {
    await page.goto('/')
    const textarea = page.getByLabel(/your secret/i)
    await textarea.fill('')
    await page.evaluate(() => {
      const el = document.querySelector('textarea')
      if (el) {
        el.removeAttribute('maxlength')
        el.value = 'x'.repeat(1001)
        el.dispatchEvent(new Event('input', { bubbles: true }))
      }
    })
    await page.getByLabel(/expiry/i).selectOption('24h')
    await page.getByRole('button', { name: /Create link/i }).click()
    await expect(page.getByText(/at most 1000 characters/i)).toBeVisible()
  })

  test('no expiry selected shows UI error', async ({ page }) => {
    await page.goto('/')
    await page.getByLabel(/your secret/i).fill('a secret')
    await expect(page.locator('#expiry-select')).toHaveValue('')
    await page.evaluate(() => document.querySelector('form')?.requestSubmit())
    await expect(page.getByText(/Please select an expiry/i)).toBeVisible()
  })

  test('valid 1000 chars and expiry submits successfully', async ({ page }) => {
    await page.goto('/')
    await page.getByLabel(/your secret/i).fill('a'.repeat(1000))
    await page.getByLabel(/expiry/i).selectOption('7d')
    await page.getByRole('button', { name: /Create link/i }).click()
    await expect(page.getByText(/Your secret link/i)).toBeVisible()
    await expect(page.locator('a[href*="/s/"]')).toBeVisible()
  })

  test('password-protected: create with password, wrong password fails, correct reveals, second open gone', async ({ page }) => {
    await page.goto('/')
    await expect(page.getByRole('heading', { name: /SecureVault/i })).toBeVisible()

    await page.getByLabel(/your secret/i).fill('E2E password secret')
    await page.getByLabel(/optional password/i).fill('correct-password')
    await page.getByLabel(/expiry/i).selectOption('burn')
    await page.getByRole('button', { name: /Create link/i }).click()

    await expect(page.getByText(/Your secret link/i)).toBeVisible()
    const link = page.locator('a[href*="/s/"]')
    await expect(link).toBeVisible()
    const href = await link.getAttribute('href')
    expect(href).toContain('/s/')
    expect(href).toContain('#p')

    await page.goto(href!)
    await expect(page.getByText(/This link may be password-protected/i)).toBeVisible()
    await expect(page.getByPlaceholder('Password')).toBeVisible()

    await page.getByPlaceholder('Password').fill('wrong-password')
    await page.getByRole('button', { name: /Reveal secret/i }).click()
    await expect(page.getByText(/Wrong password\./i)).toBeVisible()
    await expect(page.getByText(/E2E password secret/)).not.toBeVisible()

    await page.getByPlaceholder('Password').fill('correct-password')
    await page.getByRole('button', { name: /Reveal secret/i }).click()
    await expect(page.getByText(/E2E password secret/)).toBeVisible()
    await expect(page.getByText(/Wrong password\./i)).not.toBeVisible()

    const secondOpen = await page.goto(href!)
    expect(secondOpen?.status()).toBe(404)
    await expect(page.getByText(/expired or has already been viewed/)).toBeVisible()
  })

  test('invalid or malformed token shows expired screen', async ({ page }) => {
    const res = await page.goto('/s/invalid-token')
    expect(res?.status()).toBe(200)
    await expect(page.getByText(/expired or has already been viewed/i)).toBeVisible()
  })

  test('API failure on reveal shows user-friendly message', async ({ page }) => {
    await page.route('**/s/*', (route) => {
      if (route.request().resourceType() === 'fetch') {
        return route.fulfill({ status: 500, body: '{}' })
      }
      return route.continue()
    })

    await page.goto('/s/aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa') // 32 'a' base64url-ish token
    await expect(page.getByText(/Something went wrong|try again later/i)).toBeVisible()
  })
})
