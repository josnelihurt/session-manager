import { describe, it, expect, vi, beforeEach } from 'vitest'
import { render, screen } from '@testing-library/react'
import { LoginPage } from './pages/LoginPage'
import { AuthProvider } from './contexts/AuthContext'
import * as api from './api'

// Mock the api module
vi.mock('./api', () => ({
  api: {
    get: vi.fn(),
    post: vi.fn(),
    put: vi.fn(),
    delete: vi.fn(),
  },
  getSessions: vi.fn(),
  deleteSession: vi.fn(),
  deleteAllSessions: vi.fn(),
  getProviders: vi.fn(),
  login: vi.fn(),
  logout: vi.fn(),
  getCurrentUser: vi.fn(),
  getAllUsers: vi.fn(),
  getUserById: vi.fn(),
  assignUserRoles: vi.fn(),
  removeUserRole: vi.fn(),
  setUserActive: vi.fn(),
  getAllApplications: vi.fn(),
  getMyApplications: vi.fn(),
  createApplication: vi.fn(),
  updateApplication: vi.fn(),
  deleteApplication: vi.fn(),
  createApplicationRole: vi.fn(),
  deleteApplicationRole: vi.fn(),
  getAllInvitations: vi.fn(),
  createInvitation: vi.fn(),
  deleteInvitation: vi.fn(),
  validateInvitationToken: vi.fn(),
}))

// Mock config
vi.mock('./config', () => ({
  config: {
    api: { baseUrl: '/api' },
    ui: { refreshIntervalMs: 30000, messageTimeoutMs: 100 },
  },
}))

// Mock window.location
const mockLocation = {
  href: 'http://localhost:3000/',
  assign: vi.fn(),
}

Object.defineProperty(window, 'location', {
  value: mockLocation,
  writable: true,
  configurable: true,
})

function renderWithAuth(ui) {
  return render(<AuthProvider>{ui}</AuthProvider>)
}

describe('LoginPage Component', () => {
  beforeEach(() => {
    vi.clearAllMocks()
  })

  it('shows session manager title', () => {
    renderWithAuth(<LoginPage />)

    expect(screen.getByText('Session Manager')).toBeInTheDocument()
  })

  it('shows subtitle', () => {
    renderWithAuth(<LoginPage />)

    expect(screen.getByText('Sign in to access your applications')).toBeInTheDocument()
  })

  it('has username and password inputs', () => {
    renderWithAuth(<LoginPage />)

    expect(screen.getByLabelText('Username')).toBeInTheDocument()
    expect(screen.getByLabelText('Password')).toBeInTheDocument()
  })

  it('shows Google sign in button', () => {
    renderWithAuth(<LoginPage />)

    const googleButton = screen.getByRole('button', { name: /Sign in with Google/i })
    expect(googleButton).toBeInTheDocument()
  })

  it('shows local sign in button', () => {
    renderWithAuth(<LoginPage />)

    const signInButton = screen.getByRole('button', { name: 'Sign In' })
    expect(signInButton).toBeInTheDocument()
  })

  it('shows divider with "or" text', () => {
    renderWithAuth(<LoginPage />)

    expect(screen.getByText('or')).toBeInTheDocument()
  })

  it('renders without crashing', () => {
    const { container } = renderWithAuth(<LoginPage />)
    expect(container).toBeInTheDocument()
  })
})

describe('API Functions', () => {
  beforeEach(() => {
    vi.clearAllMocks()
  })

  it('getCurrentUser fetches from /auth/me endpoint', async () => {
    api.getCurrentUser.mockResolvedValue({
      id: '123',
      username: 'test',
      email: 'test@test.com',
    })

    await api.getCurrentUser()

    expect(api.getCurrentUser).toHaveBeenCalled()
  })

  it('logout posts to /auth/logout endpoint', async () => {
    api.logout.mockResolvedValue({ success: true })

    await api.logout()

    expect(api.logout).toHaveBeenCalled()
  })

  it('getSessions fetches from /sessions endpoint', async () => {
    const mockData = { data: [] }
    api.getSessions.mockResolvedValue(mockData)

    const result = await api.getSessions()

    expect(api.getSessions).toHaveBeenCalled()
    expect(result).toEqual(mockData)
  })

  it('deleteSession sends DELETE request with encoded key', async () => {
    api.deleteSession.mockResolvedValue({ success: true })

    await api.deleteSession('_oauth2_proxy_redis-abc123')

    expect(api.deleteSession).toHaveBeenCalledWith('_oauth2_proxy_redis-abc123')
  })

  it('getAllUsers fetches from /users endpoint', async () => {
    const mockUsers = []
    api.getAllUsers.mockResolvedValue(mockUsers)

    const result = await api.getAllUsers()

    expect(api.getAllUsers).toHaveBeenCalled()
    expect(result).toEqual(mockUsers)
  })

  it('getAllInvitations fetches from /invitations endpoint', async () => {
    const mockInvitations = []
    api.getAllInvitations.mockResolvedValue(mockInvitations)

    const result = await api.getAllInvitations()

    expect(api.getAllInvitations).toHaveBeenCalled()
    expect(result).toEqual(mockInvitations)
  })

  it('getAllApplications fetches from /applications/all endpoint', async () => {
    const mockApps = []
    api.getAllApplications.mockResolvedValue(mockApps)

    const result = await api.getAllApplications()

    expect(api.getAllApplications).toHaveBeenCalled()
    expect(result).toEqual(mockApps)
  })

  it('createInvitation posts to /invitations endpoint', async () => {
    api.createInvitation.mockResolvedValue({
      id: 'inv-1',
      email: 'test@test.com',
      token: 'token123',
    })

    await api.createInvitation('test@test.com', 'any', [])

    expect(api.createInvitation).toHaveBeenCalledWith('test@test.com', 'any', [])
  })

  it('deleteInvitation sends DELETE request', async () => {
    api.deleteInvitation.mockResolvedValue({ success: true })

    await api.deleteInvitation('inv-1')

    expect(api.deleteInvitation).toHaveBeenCalledWith('inv-1')
  })

  it('assignUserRoles puts to /users/:id/roles endpoint', async () => {
    api.assignUserRoles.mockResolvedValue({ success: true })

    await api.assignUserRoles('user-1', ['role-1', 'role-2'])

    expect(api.assignUserRoles).toHaveBeenCalledWith('user-1', ['role-1', 'role-2'])
  })

  it('deleteApplication sends DELETE request', async () => {
    api.deleteApplication.mockResolvedValue({ success: true })

    await api.deleteApplication('app-1')

    expect(api.deleteApplication).toHaveBeenCalledWith('app-1')
  })
})
