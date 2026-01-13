import { useState, useEffect } from 'react'
import { Navbar } from '../components/Layout/Navbar'
import { getSessions, deleteSession, deleteAllSessions } from '../api'
import './SessionsPage.css'

export function SessionsPage() {
  const [sessions, setSessions] = useState([])
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState(null)
  const [actionLoading, setActionLoading] = useState(false)
  const [message, setMessage] = useState(null)

  const loadSessions = async () => {
    setLoading(true)
    setError(null)
    try {
      const response = await getSessions()
      setSessions(response.data || [])
    } catch (err) {
      setError('Failed to load sessions')
      console.error(err)
    } finally {
      setLoading(false)
    }
  }

  const handleDeleteSession = async (fullKey) => {
    setActionLoading(true)
    try {
      await deleteSession(fullKey)
      await loadSessions()
      showMessage('Session deleted successfully')
    } catch (err) {
      setError('Failed to delete session')
      console.error(err)
    } finally {
      setActionLoading(false)
    }
  }

  const handleDeleteAll = async () => {
    if (!confirm('Are you sure you want to delete ALL sessions? This will log out all users.')) {
      return
    }

    setActionLoading(true)
    try {
      const response = await deleteAllSessions()
      await loadSessions()
      showMessage(response.message || `Deleted ${response.count} session(s)`)
    } catch (err) {
      setError('Failed to delete all sessions')
      console.error(err)
    } finally {
      setActionLoading(false)
    }
  }

  const showMessage = (msg) => {
    setMessage(msg)
    setTimeout(() => setMessage(null), 5000)
  }

  useEffect(() => {
    loadSessions()
    const interval = setInterval(loadSessions, 30000) // Refresh every 30s
    return () => clearInterval(interval)
  }, [])

  if (loading) {
    return (
      <div className="sessions-page">
        <Navbar />
        <div className="container">
          <div className="header">
            <h1>Active Sessions</h1>
          </div>
          <div className="loading">Loading sessions...</div>
        </div>
      </div>
    )
  }

  return (
    <div className="sessions-page">
      <Navbar />
      <div className="container">
        <div className="header">
          <h1>Active Sessions</h1>
          <p className="subtitle">Manage OAuth2 Proxy sessions stored in Redis</p>
        </div>

        {error && (
          <div className="alert alert-error">
            {error}
            <button onClick={() => setError(null)} className="alert-close">×</button>
          </div>
        )}

        {message && (
          <div className="alert alert-success">
            {message}
            <button onClick={() => setMessage(null)} className="alert-close">×</button>
          </div>
        )}

        <div className="actions-bar">
          <button
            onClick={loadSessions}
            className="btn btn-secondary"
            disabled={actionLoading}
          >
            Refresh
          </button>
          {sessions.length > 0 && (
            <button
              onClick={handleDeleteAll}
              className="btn btn-danger"
              disabled={actionLoading}
            >
              {actionLoading ? 'Deleting...' : `Delete All (${sessions.length})`}
            </button>
          )}
        </div>

        {sessions.length === 0 ? (
          <div className="empty-state">
            <p>No active sessions found</p>
          </div>
        ) : (
          <div className="table-container">
            <table className="sessions-table">
              <thead>
                <tr>
                  <th>Session ID</th>
                  <th>Cookie Prefix</th>
                  <th>User</th>
                  <th>Email</th>
                  <th>Expires At</th>
                  <th>Remaining</th>
                  <th>Actions</th>
                </tr>
              </thead>
              <tbody>
                {sessions.map((session, index) => (
                  <tr key={index}>
                    <td className="session-id">
                      <code>{session.sessionId}</code>
                    </td>
                    <td>{session.cookiePrefix}</td>
                    <td className="user-info">
                      {session.username || (
                        <span className="no-user">Unknown/Dangling</span>
                      )}
                    </td>
                    <td className="user-email">
                      {session.email || (
                        <span className="no-email">N/A</span>
                      )}
                    </td>
                    <td className="expires-at">
                      {session.expiresAt
                        ? new Date(session.expiresAt).toLocaleString()
                        : 'N/A'}
                    </td>
                    <td className={session.ttl < 0 ? 'expired' : 'remaining'}>
                      {session.remaining}
                    </td>
                    <td className="actions">
                      <button
                        onClick={() => handleDeleteSession(session.fullKey)}
                        className="btn btn-sm btn-danger"
                        disabled={actionLoading}
                      >
                        Delete
                      </button>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}

        <div className="footer">
          <p>Total sessions: <strong>{sessions.length}</strong></p>
        </div>
      </div>
    </div>
  )
}
