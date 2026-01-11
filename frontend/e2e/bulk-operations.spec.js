import { test, expect } from '@playwright/test'

const BASE_URL = process.env.BASE_URL || 'https://session-manager.lab.josnelihurt.me'

test.describe('Bulk Delete Operations', () => {
  test.beforeEach(async ({ page }) => {
    // Login as super admin
    await page.goto(`${BASE_URL}/login`)
    await page.fill('#username', 'jrb')
    await page.fill('#password', 's3ss10nM4n4g3rJ0sn3l1hurt')
    await page.click('button[type="submit"]')
    await page.waitForURL(/.*dashboard/)
  })

  test.describe('Invitations Bulk Delete', () => {
    test('should show checkboxes and select all functionality', async ({ page }) => {
      await page.goto(`${BASE_URL}/admin/invitations`)
      await page.waitForLoadState('networkidle')

      // Check that checkbox column exists
      await expect(page.locator('table th input[type="checkbox"]')).toBeVisible()

      // Check select all checkbox
      const selectAllCheckbox = page.locator('table th input[type="checkbox"]')
      await selectAllCheckbox.click()

      // After clicking select all, the delete button should be enabled if there are invitations
      const deleteButton = page.locator('.actions-bar button:has-text("Delete Selected")')
      const deleteButtonText = await deleteButton.textContent()
      expect(deleteButtonText).toMatch(/Delete Selected/)
    })

    test('should allow selecting individual invitations', async ({ page, request }) => {
      // Create some test invitations first
      const loginResponse = await request.post(`${BASE_URL}/api/auth/login`, {
        data: { username: 'jrb', password: 's3ss10nM4n4g3rJ0sn3l1hurt' }
      })
      const sessionCookie = loginResponse.headers()['set-cookie']?.match(/_session_manager=([^;]+)/)?.[1]

      // Create 3 invitations
      await Promise.all([
        request.post(`${BASE_URL}/api/invitations`, {
          headers: { 'Cookie': `_session_manager=${sessionCookie}` },
          data: { email: `bulk-test-1-${Date.now()}@example.com`, provider: 'local', expiresInHours: 24 }
        }),
        request.post(`${BASE_URL}/api/invitations`, {
          headers: { 'Cookie': `_session_manager=${sessionCookie}` },
          data: { email: `bulk-test-2-${Date.now()}@example.com`, provider: 'local', expiresInHours: 24 }
        }),
        request.post(`${BASE_URL}/api/invitations`, {
          headers: { 'Cookie': `_session_manager=${sessionCookie}` },
          data: { email: `bulk-test-3-${Date.now()}@example.com`, provider: 'local', expiresInHours: 24 }
        })
      ])

      await page.goto(`${BASE_URL}/admin/invitations`)
      await page.waitForLoadState('networkidle')

      // Get initial row count
      const initialRows = await page.locator('table tbody tr').count()

      // Click first checkbox
      const firstCheckbox = page.locator('table tbody tr:first-child td input[type="checkbox"]')
      await firstCheckbox.click()

      // Delete button should show (1)
      const deleteButton = page.locator('.actions-bar button:has-text("Delete Selected")')
      await expect(deleteButton).toBeEnabled()
    })

    test('should bulk delete selected invitations', async ({ page, request }) => {
      // Create test invitations with identifiable emails
      const timestamp = Date.now()
      const testEmail1 = `bulk-del-test-1-${timestamp}@example.com`
      const testEmail2 = `bulk-del-test-2-${timestamp}@example.com`

      const loginResponse = await request.post(`${BASE_URL}/api/auth/login`, {
        data: { username: 'jrb', password: 's3ss10nM4n4g3rJ0sn3l1hurt' }
      })
      const sessionCookie = loginResponse.headers()['set-cookie']?.match(/_session_manager=([^;]+)/)?.[1]

      // Create invitations
      await Promise.all([
        request.post(`${BASE_URL}/api/invitations`, {
          headers: { 'Cookie': `_session_manager=${sessionCookie}` },
          data: { email: testEmail1, provider: 'local', expiresInHours: 24 }
        }),
        request.post(`${BASE_URL}/api/invitations`, {
          headers: { 'Cookie': `_session_manager=${sessionCookie}` },
          data: { email: testEmail2, provider: 'local', expiresInHours: 24 }
        })
      ])

      await page.goto(`${BASE_URL}/admin/invitations`)
      await page.waitForLoadState('networkidle')

      // Get initial count
      const initialRows = await page.locator('table tbody tr').count()

      // Select rows with our test emails
      const rows = page.locator('table tbody tr')
      const row1 = rows.filter({ hasText: testEmail1 })
      const row2 = rows.filter({ hasText: testEmail2 })

      await row1.locator('td input[type="checkbox"]').click()
      await row2.locator('td input[type="checkbox"]').click()

      // Click delete selected
      page.on('dialog', dialog => dialog.accept())
      await page.click('.actions-bar button:has-text("Delete Selected")')

      // Wait for deletion to complete
      await page.waitForLoadState('networkidle')

      // Verify rows are gone
      await expect(rows.filter({ hasText: testEmail1 })).toHaveCount(0)
      await expect(rows.filter({ hasText: testEmail2 })).toHaveCount(0)
    })

    test('should show error when creating invitation with existing user email', async ({ page, request }) => {
      // Try to create an invitation with jrb's email (which already exists)
      const loginResponse = await request.post(`${BASE_URL}/api/auth/login`, {
        data: { username: 'jrb', password: 's3ss10nM4n4g3rJ0sn3l1hurt' }
      })
      const sessionCookie = loginResponse.headers()['set-cookie']?.match(/_session_manager=([^;]+)/)?.[1]

      // Get the super admin's email from the user list
      const usersResponse = await request.get(`${BASE_URL}/api/users`, {
        headers: { 'Cookie': `_session_manager=${sessionCookie}` }
      })
      const users = await usersResponse.json()
      const superAdminEmail = users.find(u => u.isSuperAdmin)?.email

      expect(superAdminEmail).toBeTruthy()

      // Try to create invitation with existing email
      const createResponse = await request.post(`${BASE_URL}/api/invitations`, {
        headers: { 'Cookie': `_session_manager=${sessionCookie}` },
        data: { email: superAdminEmail, provider: 'local', expiresInHours: 24 }
      })

      // Should get a 400 Bad Request with error message
      expect(createResponse.status()).toBe(400)
      const errorData = await createResponse.json()
      expect(errorData.error).toContain('already exists')
      expect(errorData.error).toContain(superAdminEmail)
    })

    test('should show error in UI when creating invitation with existing email', async ({ page }) => {
      // Get the super admin's email (jrb's email) from the users page first
      await page.goto(`${BASE_URL}/admin/users`)
      await page.waitForLoadState('networkidle')

      const superAdminRow = page.locator('table tbody tr').filter({ hasText: /jrb/ })
      // Email is in column 3 (after checkbox and username)
      const emailText = await superAdminRow.locator('td:nth-child(3)').textContent()

      // Now try to create an invitation with this email
      await page.goto(`${BASE_URL}/admin/invitations`)
      await page.waitForLoadState('networkidle')

      await page.locator('.actions-bar button:has-text("Create Invitation")').click()
      await expect(page.locator('h2')).toContainText('Create Invitation')

      // Fill in the form with existing user's email
      await page.fill('input[type="email"]', emailText)
      await page.selectOption('select', 'Any')

      // Submit the form
      await page.click('button[type="submit"]:has-text("Create Invitation")')

      // Should show error alert about existing email
      await expect(page.locator('.error-alert')).toBeVisible()
      await expect(page.locator('.error-alert')).toContainText('already exists')
      await expect(page.locator('.error-alert')).toContainText(emailText)

      // Verify we're still on the create form (not the success view)
      await expect(page.locator('h2:has-text("Invitation Created!")')).not.toBeVisible()
      await expect(page.locator('h2:has-text("Create Invitation")')).toBeVisible()
    })
  })

  test.describe('Users Bulk Delete', () => {
    test('should show checkboxes but super admin is not selectable', async ({ page }) => {
      await page.goto(`${BASE_URL}/admin/users`)
      await page.waitForLoadState('networkidle')

      // Check that checkbox column exists
      await expect(page.locator('table th input[type="checkbox"]')).toBeVisible()

      // Find super admin row (jrb)
      const superAdminRow = page.locator('table tbody tr').filter({ hasText: /jrb/ })
      const superAdminBadge = superAdminRow.locator('.badge:has-text("Super Admin")')
      await expect(superAdminBadge).toBeVisible()

      // Super admin checkbox should show a dash, not be clickable
      const checkboxCell = superAdminRow.locator('td:first-child')
      await expect(checkboxCell.locator('span[title="Super admin cannot be selected"]')).toBeVisible()
    })

    test('select all should only select non-super-admin users', async ({ page }) => {
      await page.goto(`${BASE_URL}/admin/users`)
      await page.waitForLoadState('networkidle')

      // Click select all
      await page.locator('table th input[type="checkbox"]').click()

      // Count selected checkboxes (exclude the dash in super admin row)
      const checkedCheckboxes = await page.locator('table tbody td input[type="checkbox"]:checked').count()
      const dashCount = await page.locator('table tbody td span[title="Super admin cannot be selected"]').count()

      // Select all should only select non-super-admin users
      expect(checkedCheckboxes).toBeGreaterThan(0)
      expect(dashCount).toBe(1) // Only jrb should have the dash
    })

    test('should allow selecting multiple users', async ({ page }) => {
      await page.goto(`${BASE_URL}/admin/users`)
      await page.waitForLoadState('networkidle')

      // Find selectable (non-super-admin) users
      const selectableUsers = page.locator('table tbody tr').filter({
        hasNot: page.locator('.badge:has-text("Super Admin")')
      })

      const count = await selectableUsers.count()

      if (count > 0) {
        // Select first two selectable users
        const user1 = selectableUsers.nth(0)
        const user2 = selectableUsers.nth(Math.min(1, count - 1))

        await user1.locator('td input[type="checkbox"]').click()

        if (count > 1) {
          await user2.locator('td input[type="checkbox"]').click()
        }

        // Verify delete button is enabled
        const deleteButton = page.locator('.actions-bar button:has-text("Delete Selected")')
        await expect(deleteButton).toBeEnabled()

        const buttonText = await deleteButton.textContent()
        const expectedCount = count > 1 ? 2 : 1
        expect(buttonText).toContain(`Delete Selected (${expectedCount})`)
      } else {
        // No selectable users available - verify delete button is disabled
        const deleteButton = page.locator('.actions-bar button:has-text("Delete Selected")')
        await expect(deleteButton).toBeDisabled()
      }
    })

    test('should disable delete button when nothing is selected', async ({ page }) => {
      await page.goto(`${BASE_URL}/admin/users`)
      await page.waitForLoadState('networkidle')

      const deleteButton = page.locator('.actions-bar button:has-text("Delete Selected")')
      await expect(deleteButton).toBeDisabled()

      // Select a user
      const firstSelectableRow = page.locator('table tbody tr').filter({
        hasText: /^(?!.*Super Admin).*$/
      }).first()
      await firstSelectableRow.locator('td input[type="checkbox"]').click()

      // Button should now be enabled
      await expect(deleteButton).toBeEnabled()
    })
  })
})
