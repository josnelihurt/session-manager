import { describe, it, expect, vi, beforeEach } from 'vitest'
import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import App from './App'
import * as api from './api'

// Mock the api module
vi.mock('./api')

// Mock config to use shorter intervals for tests
vi.mock('./config', () => ({
  config: {
    api: { baseUrl: '/api' },
    ui: { refreshIntervalMs: 30000, messageTimeoutMs: 100 },
  },
}))

describe('App', () => {
  beforeEach(() => {
    vi.clearAllMocks()
  })

  it('renders loading state initially', () => {
    api.getSessions.mockReturnValue(new Promise(() => {})) // Never resolves
    render(<App />)
    expect(screen.getByText('Loading sessions...')).toBeInTheDocument()
  })

  it('renders sessions when loaded', async () => {
    const mockSessions = {
      success: true,
      data: [
        {
          sessionId: 'abc123',
          cookiePrefix: '_oauth2_proxy_redis',
          ttl: 3600000,
          expiresAt: '2026-01-10T12:00:00Z',
          remaining: '1h 0m',
          fullKey: '_oauth2_proxy_redis-abc123'
        }
      ],
      count: 1
    }
    api.getSessions.mockResolvedValue(mockSessions)

    render(<App />)

    await waitFor(() => {
      expect(screen.getByText('abc123')).toBeInTheDocument()
    })

    expect(screen.getByText('_oauth2_proxy_redis')).toBeInTheDocument()
    expect(screen.getByText('1h 0m')).toBeInTheDocument()
    expect(screen.getByText(/Total sessions:/)).toBeInTheDocument()
  })

  it('renders empty state when no sessions', async () => {
    api.getSessions.mockResolvedValue({ success: true, data: [], count: 0 })

    render(<App />)

    await waitFor(() => {
      expect(screen.getByText('No active sessions found')).toBeInTheDocument()
    })
  })

  it('displays error message when API fails', async () => {
    api.getSessions.mockRejectedValue(new Error('Network error'))

    render(<App />)

    await waitFor(() => {
      expect(screen.getByText('Failed to load sessions')).toBeInTheDocument()
    })
  })

  it('calls deleteSession when delete button clicked', async () => {
    const user = userEvent.setup()
    const mockSessions = {
      success: true,
      data: [
        {
          sessionId: 'abc123',
          cookiePrefix: '_oauth2_proxy_redis',
          ttl: 3600000,
          expiresAt: '2026-01-10T12:00:00Z',
          remaining: '1h 0m',
          fullKey: '_oauth2_proxy_redis-abc123'
        }
      ],
      count: 1
    }
    api.getSessions.mockResolvedValue(mockSessions)
    api.deleteSession.mockResolvedValue({ success: true, message: 'Session deleted' })

    render(<App />)

    await waitFor(() => {
      expect(screen.getByText('abc123')).toBeInTheDocument()
    })

    const deleteButton = screen.getByRole('button', { name: 'Delete' })
    await user.click(deleteButton)

    expect(api.deleteSession).toHaveBeenCalledWith('_oauth2_proxy_redis-abc123')
  })

  it('refreshes sessions when refresh button clicked', async () => {
    const user = userEvent.setup()
    api.getSessions.mockResolvedValue({ success: true, data: [], count: 0 })

    render(<App />)

    await waitFor(() => {
      expect(screen.getByText('No active sessions found')).toBeInTheDocument()
    })

    const refreshButton = screen.getByRole('button', { name: 'Refresh' })
    await user.click(refreshButton)

    expect(api.getSessions).toHaveBeenCalledTimes(2) // Initial + refresh
  })

  it('shows Delete All button only when sessions exist', async () => {
    api.getSessions.mockResolvedValue({ success: true, data: [], count: 0 })

    render(<App />)

    await waitFor(() => {
      expect(screen.getByText('No active sessions found')).toBeInTheDocument()
    })

    expect(screen.queryByRole('button', { name: /Delete All/ })).not.toBeInTheDocument()
  })
})
