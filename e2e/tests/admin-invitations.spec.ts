import { test, expect } from '@playwright/test';

test.describe('Admin Invitation Management', () => {
  const testInvite = {
    email: `test-user-${Date.now()}@example.com`,
    provider: 'any',
  };

  test.beforeEach(async ({ page }) => {
    // Navigate to login page
    await page.goto('/login');
  });

  test('should show login page with Google OAuth option', async ({ page }) => {
    // Verify page title
    await expect(page).toHaveTitle(/Session Manager/);

    // Check for Google OAuth button
    const googleAuth = page.locator('button:has-text("Google")');
    await expect(googleAuth).toBeVisible();
  });

  test('should redirect to login when accessing protected routes', async ({ page }) => {
    // Try to access admin page without authentication
    await page.goto('/admin/invitations');

    // Should redirect to login
    await expect(page).toHaveURL(/\/login/);
  });

  test('should show dashboard navigation after authentication', async ({ page }) => {
    test.skip(true, 'Requires valid authentication session - TODO: implement login flow');

    await page.goto('/dashboard');

    // If authenticated, check navbar elements
    const navbar = page.locator('.navbar, nav');
    await expect(navbar).toBeVisible();
  });

  test('should load invitations page structure', async ({ page }) => {
    test.skip(true, 'Requires valid authentication session - TODO: implement login flow');

    await page.goto('/admin/invitations');

    // Check for create button
    const createButton = page.locator('button:has-text("Create Invitation")');
    await expect(createButton).toBeVisible();
  });
});

test.describe('Session Manager UI Structure', () => {
  test('should have proper routing configuration', async ({ page }) => {
    const routes = [
      '/login',
      '/dashboard',
      '/sessions',
      '/admin/users',
      '/admin/invitations',
      '/admin/applications',
    ];

    for (const route of routes) {
      await page.goto(route);
      // Should not return 404 - redirect to login if protected
      expect(page.url()).not.toContain('404');
    }
  });

  test('should load frontend JavaScript without errors', async ({ page }) => {
    const errors: string[] = [];

    page.on('pageerror', (error) => {
      errors.push(error.message);
    });

    await page.goto('/login');
    await page.waitForLoadState('networkidle');

    // Check for critical JavaScript errors
    const criticalErrors = errors.filter(e =>
      e.includes('Uncaught') ||
      e.includes('ReferenceError') ||
      e.includes('TypeError')
    );

    expect(criticalErrors.length).toBe(0);
  });

  test('should have proper meta tags and title', async ({ page }) => {
    await page.goto('/login');

    const title = await page.title();
    expect(title).toBeTruthy();
    expect(title.length).toBeGreaterThan(0);
  });
});

test.describe('API Endpoints Health Check', () => {
  test('should respond to auth providers endpoint', async ({ page }) => {
    const response = await page.request.get('https://session-manager.lab.josnelihurt.me/api/auth/providers');
    expect(response.status()).toBe(200);

    const data = await response.json();
    expect(Array.isArray(data)).toBeTruthy();
  });

  test('should return 401 for protected endpoint without auth', async ({ page }) => {
    const response = await page.request.get('https://session-manager.lab.josnelihurt.me/api/auth/me');
    expect(response.status()).toBe(401);
  });

  test('should return 404 for non-existent endpoint', async ({ page }) => {
    const response = await page.request.get('https://session-manager.lab.josnelihurt.me/api/non-existent');
    expect(response.status()).toBe(404);
  });
});
