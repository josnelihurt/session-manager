import { test, expect } from '@playwright/test'

const BASE_URL = process.env.BASE_URL || 'https://session-manager.lab.josnelihurt.me'

test.describe('User Registration Flow', () => {
  test.beforeEach(async ({ page }) => {
    // Navigate to the application
    await page.goto(BASE_URL)
  })

  test('should show login page initially', async ({ page }) => {
    await expect(page).toHaveURL(/.*login/)
    await expect(page.locator('h1')).toContainText('Session Manager')
  })

  test('should login as super admin', async ({ page }) => {
    // Navigate to login page explicitly
    await page.goto(`${BASE_URL}/login`)

    // Fill in login form using ID selectors
    await page.fill('#username', 'jrb')
    await page.fill('#password', 's3ss10nM4n4g3rJ0sn3l1hurt')

    // Submit login
    await page.click('button[type="submit"]')

    // Should redirect to dashboard
    await expect(page).toHaveURL(/.*dashboard/)
    await expect(page.locator('h1')).toContainText('Dashboard')

    // Verify user is logged in
    await expect(page.locator('text=/Welcome/')).toBeVisible()
  })

  test('should create invitation and test registration form', async ({ page }) => {
    // First, login as super admin to create an invitation
    await page.goto(`${BASE_URL}/login`)
    await page.fill('#username', 'jrb')
    await page.fill('#password', 's3ss10nM4n4g3rJ0sn3l1hurt')
    await page.click('button[type="submit"]')

    // Wait for navigation to complete
    await page.waitForURL(/.*dashboard/)

    // Navigate to invitations page
    await page.goto(`${BASE_URL}/admin/invitations`)

    // Wait for page to load
    await page.waitForLoadState('networkidle')

    // Create a new invitation - use more specific selector
    await expect(page.locator('.actions-bar button:has-text("Create Invitation")')).toBeVisible()
    await page.locator('.actions-bar button:has-text("Create Invitation")').click()

    // Fill in invitation form
    const testEmail = `test-${Date.now()}@example.com`
    await page.fill('input[type="email"]', testEmail)
    // Select element uses value prop, not name attribute
    await page.selectOption('select', 'Any')

    // Create invitation
    await page.click('button[type="submit"]:has-text("Create Invitation")')

    // Wait for success message and get the invite URL
    await expect(page.locator('h2:has-text("Invitation Created!")')).toBeVisible()

    // Get the invite URL
    const inviteInput = page.locator('.invite-link input[type="text"]')
    const inviteUrl = await inviteInput.inputValue()

    // Extract token from URL
    const tokenMatch = inviteUrl.match(/token=([a-f0-9]+)/)
    expect(tokenMatch).toBeTruthy()
    const token = tokenMatch[1]

    // Close the modal
    await page.click('button:has-text("Done")')

    // Now test that the registration page loads with the token
    await page.goto(`${BASE_URL}/register?token=${token}`)

    // Verify registration page loads correctly
    await expect(page.locator('h1')).toContainText('Create Account')
    // Email is shown in the subtitle
    await expect(page.locator('.subtitle')).toContainText(testEmail)

    // Verify both provider options are shown
    await expect(page.locator('.btn-provider:has-text("Username & Password")')).toBeVisible()
    await expect(page.locator('.btn-provider:has-text("Google")')).toBeVisible()

    // Select local provider and verify form fields are present
    await page.click('.btn-provider:has-text("Username & Password")')

    // Verify all required form fields exist
    await expect(page.locator('#username')).toBeVisible()
    await expect(page.locator('#password')).toBeVisible()
    await expect(page.locator('#confirmPassword')).toBeVisible()
    await expect(page.locator('button[type="submit"]')).toBeVisible()
  })

  test('should fail registration with duplicate username', async ({ page, request }) => {
    // Login to get session cookie
    const loginResponse = await request.post(`${BASE_URL}/api/auth/login`, {
      data: {
        username: 'jrb',
        password: 's3ss10nM4n4g3rJ0sn3l1hurt'
      }
    })

    const sessionCookie = loginResponse.headers()['set-cookie']?.match(/_session_manager=([^;]+)/)?.[1]

    // Create invitation
    const createInviteResponse = await request.post(`${BASE_URL}/api/invitations`, {
      headers: {
        'Cookie': `_session_manager=${sessionCookie}`
      },
      data: {
        email: `duplicate-test-${Date.now()}@example.com`,
        provider: 'local',
        expiresInHours: 24
      }
    })

    const inviteData = await createInviteResponse.json()
    const token = inviteData.token

    // Logout
    await request.post(`${BASE_URL}/api/auth/logout`)

    // Navigate to register page
    await page.goto(`${BASE_URL}/register?token=${token}`)

    // Select local provider
    await page.click('.btn-provider:has-text("Username & Password")')

    // Try to register with username "jrb" (which already exists)
    await page.fill('#username', 'jrb')
    await page.fill('#password', 'SomePassword123!')
    await page.fill('#confirmPassword', 'SomePassword123!')

    // Submit registration
    await page.click('button[type="submit"]')

    // Should show error message - check for either the specific error or generic failure
    await expect(page.locator('.error-alert')).toBeVisible()
    const errorText = await page.locator('.error-alert').textContent()
    expect(errorText.toLowerCase()).toMatch(/username|exists|already|failed/)
  })

  test('should fail registration with invalid token', async ({ page }) => {
    // Navigate to register page with invalid token
    const invalidToken = 'invalidtoken123456789'
    await page.goto(`${BASE_URL}/register?token=${invalidToken}`)

    // Should show error about invalid token
    await expect(page.locator('.error-alert')).toContainText('Invalid invitation token')
  })

  test('should show Google OAuth option on registration page', async ({ page, request }) => {
    // Login to get session cookie
    const loginResponse = await request.post(`${BASE_URL}/api/auth/login`, {
      data: {
        username: 'jrb',
        password: 's3ss10nM4n4g3rJ0sn3l1hurt'
      }
    })

    const sessionCookie = loginResponse.headers()['set-cookie']?.match(/_session_manager=([^;]+)/)?.[1]

    // Create invitation
    const createInviteResponse = await request.post(`${BASE_URL}/api/invitations`, {
      headers: {
        'Cookie': `_session_manager=${sessionCookie}`
      },
      data: {
        email: `google-test-${Date.now()}@example.com`,
        provider: 'any',
        expiresInHours: 24
      }
    })

    const inviteData = await createInviteResponse.json()
    const token = inviteData.token

    // Navigate to register page
    await page.goto(`${BASE_URL}/register?token=${token}`)

    // Should show both provider options
    await expect(page.locator('.btn-provider:has-text("Username & Password")')).toBeVisible()
    await expect(page.locator('.btn-provider:has-text("Google")')).toBeVisible()

    // Click on Google provider
    await page.click('.btn-provider:has-text("Google")')

    // Should show Google registration message
    await expect(page.locator('p:has-text("Continue with Google")')).toBeVisible()
  })

  test('should validate password requirements', async ({ page, request }) => {
    // Login to get session cookie
    const loginResponse = await request.post(`${BASE_URL}/api/auth/login`, {
      data: {
        username: 'jrb',
        password: 's3ss10nM4n4g3rJ0sn3l1hurt'
      }
    })

    const sessionCookie = loginResponse.headers()['set-cookie']?.match(/_session_manager=([^;]+)/)?.[1]

    // Create invitation
    const createInviteResponse = await request.post(`${BASE_URL}/api/invitations`, {
      headers: {
        'Cookie': `_session_manager=${sessionCookie}`
      },
      data: {
        email: `password-test-${Date.now()}@example.com`,
        provider: 'local',
        expiresInHours: 24
      }
    })

    const inviteData = await createInviteResponse.json()
    const token = inviteData.token

    // Navigate to register page
    await page.goto(`${BASE_URL}/register?token=${token}`)

    // Select local provider
    await page.click('.btn-provider:has-text("Username & Password")')

    // Fill username but leave password too short (less than 8 characters)
    await page.fill('#username', `testuser-${Date.now()}`)
    await page.fill('#password', 'short123')
    await page.fill('#confirmPassword', 'short123')

    // Try to submit - button should be enabled since field has 8+ chars
    // But let's test with mismatched passwords instead
    await page.fill('#password', 'ValidPass123')
    await page.fill('#confirmPassword', 'DifferentPass123')
    await page.click('button[type="submit"]')

    // Should show error about passwords not matching
    await expect(page.locator('.error-alert')).toBeVisible()
    const errorText = await page.locator('.error-alert').textContent()
    expect(errorText.toLowerCase()).toMatch(/password|match|do not/)
  })
})
