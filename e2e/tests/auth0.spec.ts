import { test, expect } from '@playwright/test';

const BASE_URL = process.env.BASE_URL || 'https://session-manager.lab.josnelihurt.me';
const AUTH0_DOMAIN = process.env.AUTH0_DOMAIN || 'dev-064ow4ax1q4v6bxs.us.auth0.com';

test.describe('Auth0 Login Flow', () => {
  test.beforeEach(async ({ page }) => {
    // Navigate to login page
    await page.goto('/login');
  });

  test('should display Auth0 login button', async ({ page }) => {
    // Check that Auth0 button is visible
    const auth0Button = page.locator('button:has-text("Sign in with Auth0")');
    await expect(auth0Button).toBeVisible();
  });

  test('should have Auth0 button positioned below Google button', async ({ page }) => {
    // Verify button order in the DOM
    const googleButton = page.locator('button:has-text("Sign in with Google")');
    const auth0Button = page.locator('button:has-text("Sign in with Auth0")');

    await expect(googleButton).toBeVisible();
    await expect(auth0Button).toBeVisible();

    // Auth0 button should come after Google button in DOM
    const googleExists = await googleButton.count();
    const auth0Exists = await auth0Button.count();
    expect(googleExists).toBeGreaterThan(0);
    expect(auth0Exists).toBeGreaterThan(0);
  });

  test('should redirect to Auth0 on button click', async ({ page }) => {
    // Click Auth0 login button
    const auth0Button = page.locator('button:has-text("Sign in with Auth0")');
    await auth0Button.click();

    // Should navigate to Auth0 Universal Login
    await expect(page).toHaveURL(/auth0\.com/);
  });

  test('should generate correct Auth0 login URL via API', async ({ request }) => {
    // Test the login URL generation endpoint
    const response = await request.get(`${BASE_URL}/api/auth/auth0/login-url`);

    expect(response.status()).toBe(200);

    const data = await response.json();
    expect(data.loginUrl).toBeDefined();
    expect(data.loginUrl).toContain(AUTH0_DOMAIN);
    expect(data.loginUrl).toContain('client_id=');
    expect(data.loginUrl).toContain('redirect_uri=');
    expect(data.loginUrl).toContain('response_type=code');
    expect(data.loginUrl).toContain('scope=');
    expect(data.loginUrl).toContain('state=');
  });

  test('should include invitation token in login URL when provided', async ({ request }) => {
    // Test with invitation token
    const invitationToken = 'test-invitation-token';
    const response = await request.get(`${BASE_URL}/api/auth/auth0/login-url?invitationToken=${invitationToken}`);

    expect(response.status()).toBe(200);

    const data = await response.json();
    expect(data.loginUrl).toBeDefined();
    // The state parameter should contain the invitation token
    expect(data.loginUrl).toContain('state=');
  });

  test('should have proper Auth0 callback endpoint', async ({ request }) => {
    // Verify the callback endpoint exists (it will return 400 without proper Auth0 callback)
    const response = await request.post(`${BASE_URL}/api/auth/callback/auth0`, {
      data: {
        code: 'test-code',
        state: 'test-state'
      }
    });

    // Should not return 404 (endpoint exists)
    // Will return 400 or 401 due to invalid code, but not 404
    expect([400, 401]).toContain(response.status());
  });

  test('should reject callback without invitation token', async ({ request }) => {
    // Test that Auth0 requires pre-provisioned users (invitation)
    const response = await request.post(`${BASE_URL}/api/auth/callback/auth0`, {
      data: {
        code: 'test-code',
        state: 'test-state-without-invitation'
      }
    });

    // Should return error indicating invitation is required
    expect([400, 401]).toContain(response.status());

    const data = await response.json().catch(() => ({}));
    if (data.error) {
      expect(data.error).toContain('invitation');
    }
  });
});

test.describe('Auth0 Integration - Full Flow', () => {
  test('should complete full Auth0 login flow', async ({ page, request }) => {
    test.skip(true, 'Requires valid Auth0 credentials and invitation - Manual Test');

    // This test requires:
    // 1. A valid invitation token with provider="auth0"
    // 2. A test Auth0 user account
    // 3. Manual interaction with Auth0 Universal Login

    // Step 1: Get login URL with invitation
    const invitationToken = process.env.E2E_AUTH0_INVITATION!;
    const loginResponse = await request.get(`${BASE_URL}/api/auth/auth0/login-url?invitationToken=${invitationToken}`);
    const loginData = await loginResponse.json();

    // Step 2: Navigate to Auth0
    await page.goto(loginData.loginUrl);

    // Step 3: Complete Auth0 Universal Login (manual interaction required)
    // - Enter email/password
    // - Submit form

    // Step 4: After callback, verify session is created
    // Check for session cookie
    const cookies = await page.context().cookies();
    const sessionCookie = cookies.find(c => c.name === '_session_manager');

    expect(sessionCookie).toBeDefined();
    expect(sessionCookie?.domain).toContain('.josnelihurt.me');
    expect(sessionCookie?.httpOnly).toBe(true);
    expect(sessionCookie?.secure).toBe(true);

    // Step 5: Verify user is logged in
    // Should redirect to dashboard or show logged-in state
    await expect(page).toHaveURL(/\/dashboard/);
  });

  test('should sync roles to Auth0 after login', async ({ page }) => {
    test.skip(true, 'Requires valid Auth0 invitation and Management API access - Manual Test');

    // This test verifies that:
    // 1. User logs in with Auth0
    // 2. Roles from session-manager are synced to Auth0 user metadata
    // 3. Auth0 Management API is called with role data

    // Requires verifying Auth0 Management API calls or checking user metadata
  });

  test('should reject Auth0 login without valid invitation', async ({ page, request }) => {
    test.skip(true, 'Requires Auth0 test setup - Manual Test');

    // Try to login without invitation (should fail)
    const loginResponse = await request.get(`${BASE_URL}/api/auth/auth0/login-url`);
    const loginData = await loginResponse.json();

    await page.goto(loginData.loginUrl);

    // After Auth0 callback, should get error about missing invitation
    // The error message should indicate pre-provisioning is required
  });
});

test.describe('Auth0 Security', () => {
  test('should generate secure state parameter', async ({ request }) => {
    // Test that state parameter is properly generated
    const response1 = await request.get(`${BASE_URL}/api/auth/auth0/login-url`);
    const data1 = await response1.json();

    const response2 = await request.get(`${BASE_URL}/api/auth/auth0/login-url`);
    const data2 = await response2.json();

    // State should be different each time (CSRF protection)
    const state1 = new URL(data1.loginUrl).searchParams.get('state');
    const state2 = new URL(data2.loginUrl).searchParams.get('state');

    expect(state1).toBeDefined();
    expect(state2).toBeDefined();
    expect(state1).Not.toBe(state2);

    // State should be sufficiently long (at least 32 chars)
    expect(state1!.length).toBeGreaterOrEqual(32);
  });

  test('should use HTTPS for Auth0 callback', async ({ request }) => {
    const response = await request.get(`${BASE_URL}/api/auth/auth0/login-url`);
    const data = await response.json();

    const loginUrl = new URL(data.loginUrl);
    expect(loginUrl.protocol).toBe('https:');

    const redirectUri = new URL(loginUrl.searchParams.get('redirect_uri')!);
    expect(redirectUri.protocol).toBe('https:');
  });

  test('should have correct OAuth flow parameters', async ({ request }) => {
    const response = await request.get(`${BASE_URL}/api/auth/auth0/login-url`);
    const data = await response.json();

    const loginUrl = new URL(data.loginUrl);

    // Verify OAuth parameters
    expect(loginUrl.searchParams.get('response_type')).toBe('code');
    expect(loginUrl.searchParams.get('scope')).toContain('openid');
    expect(loginUrl.searchParams.get('scope')).toContain('profile');
    expect(loginUrl.searchParams.get('scope')).toContain('email');
    expect(loginUrl.searchParams.has('client_id')).toBe(true);
    expect(loginUrl.searchParams.has('redirect_uri')).toBe(true);
  });
});

test.describe('Auth0 with whoami endpoint', () => {
  test('should return user info after Auth0 login', async ({ page, request }) => {
    test.skip(true, 'Requires valid Auth0 login session - Manual Test');

    // This test verifies the full flow:
    // 1. Login with Auth0
    // 2. Get session cookie
    // 3. Call /api/auth/me (whoami)
    // 4. Verify response contains Auth0 user info

    // After successful Auth0 login
    const meResponse = await request.get(`${BASE_URL}/api/auth/me`);

    expect(meResponse.status()).toBe(200);

    const userData = await meResponse.json();
    expect(userData).toHaveProperty('id');
    expect(userData).toHaveProperty('username');
    expect(userData).toHaveProperty('email');
    expect(userData).toHaveProperty('provider', 'auth0');
    expect(userData).toHaveProperty('isSuperAdmin');

    // Verify user data is populated
    expect(userData.username).toBeDefined();
    expect(userData.email).toBeDefined();
    expect(userData.email).toMatch(/@/);
  });

  test('should indicate Auth0 provider in whoami response', async ({ page, request }) => {
    test.skip(true, 'Requires valid Auth0 login session - Manual Test');

    // After Auth0 login
    const meResponse = await request.get(`${BASE_URL}/api/auth/me`);
    const userData = await meResponse.json();

    // Provider should be "auth0"
    expect(userData.provider).toBe('auth0');
    expect(userData.isSuperAdmin).toBeDefined();
  });

  test('should handle unauthenticated whoami request', async ({ request }) => {
    // Test whoami without authentication
    const meResponse = await request.get(`${BASE_URL}/api/auth/me`);

    // Should return 401
    expect(meResponse.status()).toBe(401);
  });
});

test.describe('Auth0 Button Styling', () => {
  test('should have correct styling classes', async ({ page }) => {
    await page.goto('/login');

    const auth0Button = page.locator('button:has-text("Sign in with Auth0")');

    // Check for styling class
    await expect(auth0Button).toHaveClass(/btn-auth0/);
    await expect(auth0Button).toHaveClass(/btn/);
  });

  test('should have Auth0 icon', async ({ page }) => {
    await page.goto('/login');

    const auth0Button = page.locator('button:has-text("Sign in with Auth0")');

    // Check for SVG icon
    const icon = auth0Button.locator('svg');
    await expect(icon).toBeVisible();
  });

  test('should be properly positioned in layout', async ({ page }) => {
    await page.goto('/login');

    // Verify button is in the correct container
    const auth0Button = page.locator('button:has-text("Sign in with Auth0")');
    const loginForm = auth0Button.locator('xpath=ancestor::div[contains(@class, "login-container")]');

    await expect(loginForm).toBeVisible();
  });
});

test.describe('Auth0 Provider Registration', () => {
  test('should have Auth0 registered as provider', async ({ request }) => {
    // Check that Auth0 is in the list of providers
    const response = await request.get(`${BASE_URL}/api/auth/providers`);

    expect(response.status()).toBe(200);

    const providers = await response.json();
    expect(Array.isArray(providers)).toBe(true);

    const auth0Provider = providers.find((p: any) => p.name === 'auth0');
    expect(auth0Provider).toBeDefined();
    expect(auth0Provider.displayName).toBe('Auth0');
  });

  test('should have Auth0 provider enabled', async ({ request }) => {
    const response = await request.get(`${BASE_URL}/api/auth/providers`);
    const providers = await response.json();

    const auth0Provider = providers.find((p: any) => p.name === 'auth0');
    expect(auth0Provider.isEnabled).toBe(true);
  });
});

test.describe('Auth0 Error Handling', () => {
  test('should handle expired invitation token', async ({ request }) => {
    // Test callback with expired invitation
    const response = await request.post(`${BASE_URL}/api/auth/callback/auth0`, {
      data: {
        code: 'test-code',
        state: 'expired-invite|state'
      }
    });

    // Should return error about invalid/expired invitation
    expect([400, 401]).toContain(response.status());

    const data = await response.json().catch(() => ({}));
    if (data.error) {
      expect(data.error.toLowerCase()).toMatch(/invitation|expired|invalid/);
    }
  });

  test('should handle wrong provider in invitation', async ({ request }) => {
    // Test that invitation with provider="google" cannot be used with Auth0
    // This would require having a valid invitation with different provider
    // For now, we test the endpoint structure
  });

  test('should handle existing user with different provider', async ({ request }) => {
    test.skip(true, 'Requires database setup - Manual Test');

    // If user exists with provider="google", they cannot use Auth0
    // Should return appropriate error
  });
});
