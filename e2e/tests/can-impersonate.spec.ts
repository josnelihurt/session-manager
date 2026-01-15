import { test, expect } from '@playwright/test';

const BASE_URL = process.env.BASE_URL || 'https://session-manager.lab.josnelihurt.me';
const ADMIN_USERNAME = process.env.E2E_ADMIN_USERNAME || 'jrb';
const ADMIN_PASSWORD = process.env.E2E_ADMIN_PASSWORD || 'changeme';

// Helper function to create a test user via test API
async function createTestUser(request: any, username: string, email: string, password: string) {
  const response = await request.post(`${BASE_URL}/api/test/create-user`, {
    data: { username, email, password, isSuperAdmin: false, canImpersonate: false }
  });
  return response;
}

// Helper function to perform test login and get session cookie
async function testLogin(request: any, username: string, password: string) {
  const response = await request.post(`${BASE_URL}/api/test/login`, {
    data: { username, password }
  });

  if (response.status() !== 200) {
    throw new Error(`Login failed: ${await response.text()}`);
  }

  const data = await response.json();
  return data.sessionKey;
}

// Helper function to set session cookie
async function setSessionCookie(page: any, sessionKey: string) {
  const cookie = {
    name: '_session_manager',
    value: sessionKey,
    domain: '.lab.josnelihurt.me',
    path: '/',
    httpOnly: true,
    secure: true
  };
  await page.context().addCookies([cookie]);
}

// Helper function to delete a test user
async function deleteTestUser(request: any, userId: string, sessionKey: string) {
  await request.delete(`${BASE_URL}/api/users/${userId}`, {
    headers: {
      'Cookie': `_session_manager=${sessionKey}`
    }
  });
}

test.describe('Can Impersonate Permission - E2E', () => {
  let testUserId: string | null = null;
  let adminSessionKey: string | null = null;
  const testUsername = `e2e-impersonate-${Date.now()}`;
  const testEmail = `e2e-impersonate-${Date.now()}@example.com`;
  const testPassword = 'TestPassword123!';

  test.beforeAll(async ({ request }) => {
    // Login as admin to get session
    adminSessionKey = await testLogin(request, ADMIN_USERNAME, ADMIN_PASSWORD);

    // Create a test user via test API
    const createUserResponse = await createTestUser(request, testUsername, testEmail, testPassword);
    expect(createUserResponse.ok()).toBeTruthy();

    const userData = await createUserResponse.json();
    testUserId = userData.id;
  });

  test.afterAll(async ({ request }) => {
    // Cleanup: Delete the test user
    if (testUserId && adminSessionKey) {
      await deleteTestUser(request, testUserId, adminSessionKey);

      // Also cleanup via test API
      await request.delete(`${BASE_URL}/api/test/cleanup`);
    }
  });

  test('should show Can Impersonate checkbox for non-super-admin users', async ({ page }) => {
    // Set admin session cookie
    await setSessionCookie(page, adminSessionKey!);

    // Navigate to Users page
    await page.goto('/admin/users');

    // Wait for users table to load
    await page.waitForSelector('table.data-table', { timeout: 5000 });

    // Find our test user in the list
    const testUserRow = page.locator(`table.data-table tbody tr:has-text("${testUsername}")`);
    await expect(testUserRow).toBeVisible({ timeout: 5000 });

    // Verify the user is not a super admin
    const superAdminBadge = testUserRow.locator('.badge:has-text("Super Admin")');
    await expect(superAdminBadge).not.toBeVisible();

    // Click Edit Roles button for the test user
    const editButton = testUserRow.locator('button:has-text("Edit Roles")');
    await expect(editButton).toBeVisible();
    await editButton.click();

    // Wait for modal to appear
    const modal = page.locator('.modal');
    await expect(modal).toBeVisible({ timeout: 3000 });

    // Verify the modal title contains the username
    await expect(modal.locator('h2')).toContainText('Edit Roles for');
    await expect(modal.locator('h2')).toContainText(testUsername);

    // Verify Application Roles section is visible
    await expect(modal.locator('text=Application Roles')).toBeVisible();

    // Verify the "Can Impersonate Users" checkbox is visible
    const canImpersonateLabel = modal.locator('.checkbox-label:has-text("Can Impersonate Users")');
    await expect(canImpersonateLabel).toBeVisible();

    const canImpersonateCheckbox = canImpersonateLabel.locator('input[type="checkbox"]');
    await expect(canImpersonateCheckbox).toBeVisible();

    // Verify the checkbox is initially unchecked (we created user with canImpersonate: false)
    const initialState = await canImpersonateCheckbox.isChecked();
    expect(initialState).toBe(false);

    // Toggle the checkbox - this triggers an API call
    await canImpersonateCheckbox.click();

    // Wait for the API call to complete and state to update
    // The checkbox update is asynchronous, so we need to wait for the change to reflect
    // Wait for the checkbox to become checked
    await expect(async () => {
      const checked = await canImpersonateCheckbox.isChecked();
      expect(checked).toBe(true);
    }).toPass({ timeout: 5000 });

    // Verify the checkbox is now checked
    const newState = await canImpersonateCheckbox.isChecked();
    expect(newState).toBe(true);

    // Close the modal by clicking Done
    await modal.locator('button:has-text("Done")').click();

    // Verify modal is closed
    await expect(modal).not.toBeVisible({ timeout: 2000 });

    // Reopen the modal to verify the change persisted
    await testUserRow.locator('button:has-text("Edit Roles")').click();
    await expect(modal).toBeVisible({ timeout: 3000 });

    // Verify the checkbox is still checked
    const persistedState = await canImpersonateCheckbox.isChecked();
    expect(persistedState).toBe(true);

    // Close modal
    await modal.locator('button:has-text("Done")').click();
    await expect(modal).not.toBeVisible({ timeout: 2000 });
  });

  test('should update canImpersonate and verify user can impersonate', async ({ page, request }) => {
    // Set admin session cookie
    await setSessionCookie(page, adminSessionKey!);

    // Enable canImpersonate for test user via API
    const updateResponse = await request.put(`${BASE_URL}/api/users/${testUserId}/can-impersonate`, {
      data: true,
      headers: {
        'Cookie': `_session_manager=${adminSessionKey}`
      }
    });
    expect(updateResponse.ok()).toBeTruthy();

    // Navigate to Users page
    await page.goto('/admin/users');

    // Find test user and verify impersonate button is visible
    const testUserRow = page.locator(`table.data-table tbody tr:has-text("${testUsername}")`);
    await expect(testUserRow).toBeVisible({ timeout: 5000 });

    // The impersonate button should be visible
    const impersonateButton = testUserRow.locator('button:has-text("Impersonate")');
    await expect(impersonateButton).toBeVisible();

    // Verify via API that the user has canImpersonate
    const userResponse = await request.get(`${BASE_URL}/api/users`, {
      headers: {
        'Cookie': `_session_manager=${adminSessionKey}`
      }
    });
    expect(userResponse.ok()).toBeTruthy();

    const users = await userResponse.json();
    const testUser = users.find((u: any) => u.username === testUsername);
    expect(testUser).toBeDefined();
    expect(testUser.canImpersonate).toBe(true);
  });

  test('should NOT show Can Impersonate checkbox for super admin users', async ({ page }) => {
    // Set admin session cookie
    await setSessionCookie(page, adminSessionKey!);

    // Navigate to Users page
    await page.goto('/admin/users');

    // Wait for users table to load
    await page.waitForSelector('table.data-table', { timeout: 5000 });

    // Find the super admin user (jrb)
    const adminRow = page.locator(`table.data-table tbody tr:has-text("${ADMIN_USERNAME}")`);
    await expect(adminRow).toBeVisible({ timeout: 5000 });

    // Verify this user has Super Admin badge
    const superAdminBadge = adminRow.locator('.badge:has-text("Super Admin")');
    await expect(superAdminBadge).toBeVisible();

    // Click Edit Roles for the super admin (if button exists)
    const editButton = adminRow.locator('button:has-text("Edit Roles")');

    if (await editButton.count() > 0) {
      await editButton.click();

      // Wait for modal
      const modal = page.locator('.modal');
      await expect(modal).toBeVisible({ timeout: 3000 });

      // Verify the "Can Impersonate Users" checkbox is NOT visible for super admin
      const canImpersonateLabel = modal.locator('.checkbox-label:has-text("Can Impersonate Users")');
      await expect(canImpersonateLabel).not.toBeVisible();

      // Close modal
      await modal.locator('button:has-text("Done")').click();
      await expect(modal).not.toBeVisible({ timeout: 2000 });
    }
  });
});

test.describe('Can Impersonate - User Login Verification', () => {
  let testUserId: string | null = null;
  let adminSessionKey: string | null = null;
  let testUserSessionKey: string | null = null;
  const testUsername = `e2e-impersonate-login-${Date.now()}`;
  const testEmail = `e2e-impersonate-login-${Date.now()}@example.com`;
  const testPassword = 'TestPassword123!';

  test.beforeAll(async ({ request }) => {
    // Login as admin
    adminSessionKey = await testLogin(request, ADMIN_USERNAME, ADMIN_PASSWORD);

    // Create a test user with canImpersonate enabled
    const createUserResponse = await request.post(`${BASE_URL}/api/test/create-user`, {
      data: { username: testUsername, email: testEmail, password: testPassword, isSuperAdmin: false, canImpersonate: true }
    });
    const userData = await createUserResponse.json();
    testUserId = userData.id;

    // Login as the test user
    testUserSessionKey = await testLogin(request, testUsername, testPassword);
  });

  test.afterAll(async ({ request }) => {
    // Cleanup
    if (testUserId && adminSessionKey) {
      await deleteTestUser(request, testUserId, adminSessionKey);
      await request.delete(`${BASE_URL}/api/test/cleanup`);
    }
  });

  test('user with canImpersonate should see Users page and impersonation controls', async ({ page }) => {
    // Set test user session cookie
    await setSessionCookie(page, testUserSessionKey!);

    // Navigate to Users page - should be accessible because user has canImpersonate
    await page.goto('/admin/users');

    // Wait for users table to load
    await page.waitForSelector('table.data-table', { timeout: 5000 });

    // Verify the Users page is visible
    await expect(page.locator('h1')).toContainText('Users');

    // The user should NOT see "Delete Selected" button (only super admins can delete)
    const deleteButton = page.locator('button:has-text("Delete Selected")');
    await expect(deleteButton).not.toBeVisible();

    // Verify canImpersonate via /api/auth/me
    const meResponse = await page.request.get(`${BASE_URL}/api/auth/me`);
    expect(meResponse.ok()).toBeTruthy();

    const meData = await meResponse.json();
    expect(meData.canImpersonate).toBe(true);
    expect(meData.isSuperAdmin).toBe(false);
  });

  test('user without canImpersonate should not see Users page', async ({ page, request }) => {
    // Create another user WITHOUT canImpersonate
    const regularUsername = `e2e-regular-${Date.now()}`;
    const regularEmail = `e2e-regular-${Date.now()}@example.com`;

    const createUserResponse = await request.post(`${BASE_URL}/api/test/create-user`, {
      data: { username: regularUsername, email: regularEmail, password: testPassword, isSuperAdmin: false, canImpersonate: false }
    });
    const userData = await createUserResponse.json();

    // Login as the regular user
    const regularSessionKey = await testLogin(request, regularUsername, testPassword);

    // Set session cookie
    await setSessionCookie(page, regularSessionKey);

    // Try to access Users page - should redirect to login or show unauthorized
    await page.goto('/admin/users');

    // Should NOT see the users table (redirected or unauthorized)
    const usersTable = page.locator('table.data-table');
    await expect(usersTable).not.toBeVisible({ timeout: 3000 });

    // Verify canImpersonate is false via /api/auth/me
    const meResponse = await page.request.get(`${BASE_URL}/api/auth/me`);
    const meData = await meResponse.json();
    expect(meData.canImpersonate).toBe(false);

    // Cleanup regular user
    await deleteTestUser(request, userData.id, adminSessionKey!);
  });
});

test.describe('Can Impersonate - API Endpoints', () => {
  let adminSessionKey: string | null = null;
  let testUserId: string | null = null;

  test.beforeAll(async ({ request }) => {
    adminSessionKey = await testLogin(request, ADMIN_USERNAME, ADMIN_PASSWORD);
  });

  test.afterAll(async ({ request }) => {
    if (testUserId && adminSessionKey) {
      await deleteTestUser(request, testUserId, adminSessionKey);
      await request.delete(`${BASE_URL}/api/test/cleanup`);
    }
  });

  test('should allow setting canImpersonate to true', async ({ request }) => {
    // Create a test user
    const testUsername = `e2e-api-${Date.now()}`;
    const createUserResponse = await request.post(`${BASE_URL}/api/test/create-user`, {
      data: { username: testUsername, email: `e2e-api-${Date.now()}@example.com`, password: 'Test123!', isSuperAdmin: false, canImpersonate: false }
    });
    const userData = await createUserResponse.json();
    testUserId = userData.id;

    // Set canImpersonate to true
    const setResponse = await request.put(`${BASE_URL}/api/users/${testUserId}/can-impersonate`, {
      data: true,
      headers: {
        'Cookie': `_session_manager=${adminSessionKey}`
      }
    });

    expect(setResponse.ok()).toBeTruthy();

    const setResult = await setResponse.json();
    expect(setResult.message).toContain('impersonate permission enabled');

    // Verify by getting user list
    const usersResponse = await request.get(`${BASE_URL}/api/users`, {
      headers: {
        'Cookie': `_session_manager=${adminSessionKey}`
      }
    });

    const users = await usersResponse.json();
    const testUser = users.find((u: any) => u.username === testUsername);
    expect(testUser.canImpersonate).toBe(true);
  });

  test('should allow setting canImpersonate to false', async ({ request }) => {
    // Create a test user with canImpersonate already true
    const testUsername = `e2e-api-false-${Date.now()}`;
    const createUserResponse = await request.post(`${BASE_URL}/api/test/create-user`, {
      data: { username: testUsername, email: `e2e-api-false-${Date.now()}@example.com`, password: 'Test123!', isSuperAdmin: false, canImpersonate: true }
    });
    const userData = await createUserResponse.json();
    testUserId = userData.id;

    // Set canImpersonate to false
    const setResponse = await request.put(`${BASE_URL}/api/users/${testUserId}/can-impersonate`, {
      data: false,
      headers: {
        'Cookie': `_session_manager=${adminSessionKey}`
      }
    });

    expect(setResponse.ok()).toBeTruthy();

    const setResult = await setResponse.json();
    expect(setResult.message).toContain('impersonate permission disabled');

    // Verify by getting user list
    const usersResponse = await request.get(`${BASE_URL}/api/users`, {
      headers: {
        'Cookie': `_session_manager=${adminSessionKey}`
      }
    });

    const users = await usersResponse.json();
    const testUser = users.find((u: any) => u.username === testUsername);
    expect(testUser.canImpersonate).toBe(false);
  });

  test('should not allow setting canImpersonate for super admin', async ({ request }) => {
    // Try to set canImpersonate for the super admin (jrb)
    // First get jrb's user ID
    const usersResponse = await request.get(`${BASE_URL}/api/users`, {
      headers: {
        'Cookie': `_session_manager=${adminSessionKey}`
      }
    });

    const users = await usersResponse.json();
    const superAdmin = users.find((u: any) => u.isSuperAdmin);

    if (superAdmin) {
      const setResponse = await request.put(`${BASE_URL}/api/users/${superAdmin.id}/can-impersonate`, {
        data: true,
        headers: {
          'Cookie': `_session_manager=${adminSessionKey}`
        }
      });

      // Should return 400 Bad Request
      expect(setResponse.status()).toBe(400);

      const setError = await setResponse.json();
      expect(setError.error).toContain('super admin');
    }
  });
});
