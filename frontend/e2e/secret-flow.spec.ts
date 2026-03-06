/**
 * Assignment-aligned E2E: main secret flow only.
 * Avoids brittle HTTP/UI assertions; asserts durable user-visible outcomes.
 */
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
  test('create secret and reveal once; second open shows expired state', async ({ page }) => {
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

    await page.goto(href!)
    await expect(page.getByRole('region', { name: 'Secret content' })).toContainText('E2E test secret')

    await page.goto(href!)
    await expect(page.getByRole('status', { name: 'Secret expired' })).toBeVisible()
  })

  test('invalid or malformed token shows expired state', async ({ page }) => {
    await page.goto('/s/invalid-token')
    await expect(page.getByRole('status', { name: 'Secret expired' })).toBeVisible()
  })

  test('password-protected: create with password, wrong password does not reveal, correct reveals, second open shows expired', async ({ page }) => {
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

    await page.goto(href!)
    await expect(page.getByText(/password-protected/i)).toBeVisible()
    await expect(page.getByPlaceholder('Password')).toBeVisible()

    await page.getByPlaceholder('Password').fill('wrong-password')
    await page.getByRole('button', { name: /Reveal secret/i }).click()
    await expect(page.getByText(/wrong password/i)).toBeVisible()
    await expect(page.getByRole('region', { name: 'Secret content' })).not.toBeVisible()

    await page.getByPlaceholder('Password').fill('correct-password')
    await page.getByRole('button', { name: /Reveal secret/i }).click()
    await expect(page.getByRole('region', { name: 'Secret content' })).toContainText('E2E password secret')

    await page.goto(href!)
    await expect(page.getByRole('status', { name: 'Secret expired' })).toBeVisible()
  })
})
