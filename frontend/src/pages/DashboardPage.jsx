import { useState, useEffect } from 'react'
import { Link } from 'react-router-dom'
import { AdminLayout } from '../components/Layout/AdminLayout'
import { useAuth } from '../contexts/AuthContext'
import { RoleSelector } from '../components/RoleSelector'
import { getMyApplications, getAllApplications, getSessions, deleteSession, deleteAllSessions, getAllUsers, assignUserRoles, removeUserRole, setUserActive } from '../api'
import './DashboardPage.css'

export function DashboardPage() {
  const { user, isSuperAdmin } = useAuth()
  const [applications, setApplications] = useState([])
  const [loading, setLoading] = useState(true)

  // Sessions state
  const [sessions, setSessions] = useState([])
  const [sessionsLoading, setSessionsLoading] = useState(true)
  const [sessionsError, setSessionsError] = useState(null)
  const [actionLoading, setActionLoading] = useState(false)
  const [sessionsMessage, setSessionsMessage] = useState(null)

  // User management state (super admin only)
  const [users, setUsers] = useState([])
  const [usersLoading, setUsersLoading] = useState(true)
  const [usersError, setUsersError] = useState(null)
  const [allApplications, setAllApplications] = useState([])
  const [selectedUser, setSelectedUser] = useState(null)
  const [showRoleModal, setShowRoleModal] = useState(false)

  // Load applications
  useEffect(() => {
    const loadApps = async () => {
      try {
        const apps = await getMyApplications()
        setApplications(apps)
      } catch (err) {
        console.error('Failed to load apps:', err)
      } finally {
        setLoading(false)
      }
    }

    loadApps()
  }, [])

  // Load sessions
  useEffect(() => {
    loadSessions()
    const interval = setInterval(loadSessions, 30000) // Refresh every 30s
    return () => clearInterval(interval)
  }, [])

  // Load users (super admin only)
  useEffect(() => {
    if (isSuperAdmin) {
      loadUsersAndApps()
    }
  }, [isSuperAdmin])

  const loadSessions = async () => {
    setSessionsLoading(true)
    setSessionsError(null)
    try {
      const response = await getSessions()
      setSessions(response.data || [])
    } catch (err) {
      setSessionsError('Failed to load sessions')
      console.error(err)
    } finally {
      setSessionsLoading(false)
    }
  }

  const loadUsersAndApps = async () => {
    setUsersLoading(true)
    setUsersError(null)
    try {
      const [usersData, appsData] = await Promise.all([
        getAllUsers(),
        getAllApplications()
      ])
      setUsers(usersData)
      setAllApplications(appsData)
    } catch (err) {
      setUsersError('Failed to load users')
      console.error(err)
    } finally {
      setUsersLoading(false)
    }
  }

  // Session actions
  const handleDeleteSession = async (fullKey) => {
    setActionLoading(true)
    try {
      await deleteSession(fullKey)
      await loadSessions()
      showSessionsMessage('Session deleted successfully')
    } catch (err) {
      setSessionsError('Failed to delete session')
      console.error(err)
    } finally {
      setActionLoading(false)
    }
  }

  const handleDeleteAllSessions = async () => {
    if (!confirm('Are you sure you want to delete ALL sessions? This will log out all users.')) {
      return
    }

    setActionLoading(true)
    try {
      const response = await deleteAllSessions()
      await loadSessions()
      showSessionsMessage(response.message || `Deleted ${response.count} session(s)`)
    } catch (err) {
      setSessionsError('Failed to delete all sessions')
      console.error(err)
    } finally {
      setActionLoading(false)
    }
  }

  const showSessionsMessage = (msg) => {
    setSessionsMessage(msg)
    setTimeout(() => setSessionsMessage(null), 5000)
  }

  // User management actions
  const handleToggleActive = async (user) => {
    try {
      await setUserActive(user.id, !user.isActive)
      loadUsersAndApps()
    } catch (err) {
      setUsersError('Failed to update user')
    }
  }

  const handleEditRoles = (user) => {
    setSelectedUser(user)
    setShowRoleModal(true)
  }

  const handleRoleChange = async (roleId, checked) => {
    if (!selectedUser) return

    try {
      if (checked) {
        const currentRoleIds = selectedUser.roles.map(r => r.roleId)
        await assignUserRoles(selectedUser.id, [...currentRoleIds, roleId])
      } else {
        await removeUserRole(selectedUser.id, roleId)
      }
      const updatedUsers = await getAllUsers()
      setUsers(updatedUsers)
      const updatedUser = updatedUsers.find(u => u.id === selectedUser.id)
      setSelectedUser(updatedUser)
    } catch (err) {
      setUsersError('Failed to update roles')
    }
  }

  return (
    <AdminLayout title="Dashboard">
      <div className="dashboard">
        {/* User Info Section */}
        <section className="user-info-section">
          <h2>Welcome, {user?.username}!</h2>
          <p>Email: {user?.email}</p>
          <p>Account type: {isSuperAdmin ? 'Super Admin' : 'User'}</p>
        </section>

        {/* OAuth2 Sessions Management */}
        <section className="sessions-section">
          <div className="section-header">
            <h2>Active Sessions</h2>
            <p className="subtitle">Manage OAuth2 Proxy sessions stored in Redis</p>
          </div>

          {sessionsError && (
            <div className="alert alert-error">
              {sessionsError}
              <button onClick={() => setSessionsError(null)} className="alert-close">×</button>
            </div>
          )}

          {sessionsMessage && (
            <div className="alert alert-success">
              {sessionsMessage}
              <button onClick={() => setSessionsMessage(null)} className="alert-close">×</button>
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
                onClick={handleDeleteAllSessions}
                className="btn btn-danger"
                disabled={actionLoading}
              >
                {actionLoading ? 'Deleting...' : `Delete All (${sessions.length})`}
              </button>
            )}
          </div>

          {sessionsLoading ? (
            <div className="loading">Loading sessions...</div>
          ) : sessions.length === 0 ? (
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
        </section>

        {/* User Permission Management (Super Admin Only) */}
        {isSuperAdmin && (
          <section className="users-section">
            <div className="section-header">
              <h2>User Permission Management</h2>
              <p className="subtitle">Quick access to manage user roles and permissions</p>
            </div>

            {usersError && (
              <div className="alert alert-error">
                {usersError}
                <button onClick={() => setUsersError(null)} className="alert-close">×</button>
              </div>
            )}

            {usersLoading ? (
              <div className="loading">Loading users...</div>
            ) : (
              <div className="table-container">
                <table className="users-table">
                  <thead>
                    <tr>
                      <th>Username</th>
                      <th>Email</th>
                      <th>Provider</th>
                      <th>Status</th>
                      <th>Roles</th>
                      <th>Actions</th>
                    </tr>
                  </thead>
                  <tbody>
                    {users.map((userItem) => (
                      <tr key={userItem.id}>
                        <td>
                          {userItem.username}
                          {userItem.isSuperAdmin && <span className="badge">Super Admin</span>}
                        </td>
                        <td>{userItem.email}</td>
                        <td>{userItem.provider}</td>
                        <td>
                          <span className={`status ${userItem.isActive ? 'active' : 'inactive'}`}>
                            {userItem.isActive ? 'Active' : 'Inactive'}
                          </span>
                        </td>
                        <td>
                          {userItem.roles.length > 0 ? (
                            <ul className="role-list">
                              {userItem.roles.map((role, idx) => (
                                <li key={idx}>
                                  {role.applicationName}: {role.roleName}
                                </li>
                              ))}
                            </ul>
                          ) : (
                            <span className="no-roles">No roles</span>
                          )}
                        </td>
                        <td className="actions">
                          {!userItem.isSuperAdmin && (
                            <>
                              <button
                                onClick={() => handleEditRoles(userItem)}
                                className="btn btn-small"
                              >
                                Edit Roles
                              </button>
                              <button
                                onClick={() => handleToggleActive(userItem)}
                                className={`btn btn-small ${userItem.isActive ? 'btn-danger' : 'btn-success'}`}
                              >
                                {userItem.isActive ? 'Disable' : 'Enable'}
                              </button>
                            </>
                          )}
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            )}

            {showRoleModal && selectedUser && (
              <div className="modal-overlay" onClick={() => setShowRoleModal(false)}>
                <div className="modal" onClick={(e) => e.stopPropagation()}>
                  <h2>Edit Roles for {selectedUser.username}</h2>

                  <RoleSelector
                    applications={allApplications}
                    isRoleSelected={(roleId) => selectedUser.roles.some(r => r.roleId === roleId)}
                    onRoleToggle={(roleId, checked) => handleRoleChange(roleId, checked)}
                    label="Application Roles"
                  />

                  <button
                    onClick={() => setShowRoleModal(false)}
                    className="btn btn-primary"
                  >
                    Done
                  </button>
                </div>
              </div>
            )}
          </section>
        )}

        {/* Applications Section */}
        <section className="apps-section">
          <h2>Your Applications</h2>
          {loading ? (
            <p>Loading...</p>
          ) : applications.length > 0 ? (
            <div className="apps-grid">
              {applications.map((app) => (
                <a
                  key={app.id}
                  href={`https://${app.url}`}
                  target="_blank"
                  rel="noopener noreferrer"
                  className="app-link"
                >
                  <h3>{app.name}</h3>
                  <p>{app.url}</p>
                </a>
              ))}
            </div>
          ) : (
            <p>No applications available. Contact an admin to get access.</p>
          )}
        </section>

        {/* Quick Links */}
        <section className="quick-links">
          <h2>Quick Links</h2>
          <div className="links-grid">
            <Link to="/sessions" className="quick-link">
              Dedicated Sessions Page
            </Link>
            {isSuperAdmin && (
              <>
                <Link to="/admin/users" className="quick-link">
                  User Management (Detailed)
                </Link>
                <Link to="/admin/invitations" className="quick-link">
                  Invitations
                </Link>
                <Link to="/admin/applications" className="quick-link">
                  Applications Management
                </Link>
              </>
            )}
          </div>
        </section>
      </div>

      {/* CSS for Dashboard Page */}
      <style>{`
        .dashboard {
          display: flex;
          flex-direction: column;
          gap: 2rem;
        }

        .dashboard section {
          background: #fff;
          border-radius: 8px;
          padding: 1.5rem;
          box-shadow: 0 1px 3px rgba(0,0,0,0.1);
        }

        .dashboard .section-header {
          margin-bottom: 1rem;
        }

        .dashboard .section-header h2 {
          margin: 0 0 0.5rem 0;
          font-size: 1.5rem;
        }

        .dashboard .subtitle {
          margin: 0;
          color: #666;
          font-size: 0.9rem;
        }

        .dashboard .actions-bar {
          display: flex;
          gap: 0.5rem;
          margin-bottom: 1rem;
        }

        .dashboard .table-container {
          overflow-x: auto;
        }

        .dashboard table {
          width: 100%;
          border-collapse: collapse;
        }

        .dashboard th,
        .dashboard td {
          padding: 0.75rem;
          text-align: left;
          border-bottom: 1px solid #eee;
        }

        .dashboard th {
          background: #f8f9fa;
          font-weight: 600;
        }

        .dashboard .session-id code {
          background: #f4f4f4;
          padding: 0.25rem 0.5rem;
          border-radius: 4px;
          font-size: 0.85rem;
        }

        .dashboard .role-list {
          margin: 0;
          padding-left: 1.25rem;
          font-size: 0.9rem;
        }

        .dashboard .role-list li {
          margin-bottom: 0.25rem;
        }

        .dashboard .no-roles {
          color: #999;
          font-style: italic;
        }

        .dashboard .status {
          padding: 0.25rem 0.75rem;
          border-radius: 12px;
          font-size: 0.85rem;
          font-weight: 500;
        }

        .dashboard .status.active {
          background: #d4edda;
          color: #155724;
        }

        .dashboard .status.inactive {
          background: #f8d7da;
          color: #721c24;
        }

        .dashboard .badge {
          background: #007bff;
          color: white;
          padding: 0.125rem 0.5rem;
          border-radius: 4px;
          font-size: 0.75rem;
          margin-left: 0.5rem;
        }

        .dashboard .actions {
          display: flex;
          gap: 0.5rem;
        }

        .dashboard .btn {
          padding: 0.5rem 1rem;
          border: none;
          border-radius: 4px;
          cursor: pointer;
          font-size: 0.9rem;
        }

        .dashboard .btn-sm {
          padding: 0.25rem 0.5rem;
          font-size: 0.85rem;
        }

        .dashboard .btn-primary {
          background: #007bff;
          color: white;
        }

        .dashboard .btn-secondary {
          background: #6c757d;
          color: white;
        }

        .dashboard .btn-danger {
          background: #dc3545;
          color: white;
        }

        .dashboard .btn-success {
          background: #28a745;
          color: white;
        }

        .dashboard .alert {
          padding: 0.75rem 1rem;
          border-radius: 4px;
          margin-bottom: 1rem;
          display: flex;
          justify-content: space-between;
          align-items: center;
        }

        .dashboard .alert-error {
          background: #f8d7da;
          color: #721c24;
        }

        .dashboard .alert-success {
          background: #d4edda;
          color: #155724;
        }

        .dashboard .alert-close {
          background: none;
          border: none;
          font-size: 1.25rem;
          cursor: pointer;
          padding: 0;
          line-height: 1;
        }

        .dashboard .empty-state {
          text-align: center;
          padding: 2rem;
          color: #999;
        }

        .dashboard .loading {
          text-align: center;
          padding: 2rem;
          color: #666;
        }

        .dashboard .apps-grid {
          display: grid;
          grid-template-columns: repeat(auto-fill, minmax(200px, 1fr));
          gap: 1rem;
        }

        .dashboard .app-link {
          display: block;
          padding: 1rem;
          border: 1px solid #ddd;
          border-radius: 8px;
          text-decoration: none;
          color: #333;
          transition: box-shadow 0.2s;
        }

        .dashboard .app-link:hover {
          box-shadow: 0 2px 8px rgba(0,0,0,0.15);
        }

        .dashboard .app-link h3 {
          margin: 0 0 0.5rem 0;
          font-size: 1.1rem;
        }

        .dashboard .app-link p {
          margin: 0;
          font-size: 0.9rem;
          color: #666;
        }

        .dashboard .links-grid {
          display: grid;
          grid-template-columns: repeat(auto-fill, minmax(200px, 1fr));
          gap: 0.5rem;
        }

        .dashboard .quick-link {
          display: block;
          padding: 0.75rem 1rem;
          background: #f8f9fa;
          border: 1px solid #ddd;
          border-radius: 4px;
          text-decoration: none;
          color: #333;
          text-align: center;
        }

        .dashboard .quick-link:hover {
          background: #e9ecef;
        }

        .dashboard .modal-overlay {
          position: fixed;
          top: 0;
          left: 0;
          right: 0;
          bottom: 0;
          background: rgba(0,0,0,0.5);
          display: flex;
          align-items: center;
          justify-content: center;
          z-index: 1000;
        }

        .dashboard .modal {
          background: white;
          border-radius: 8px;
          padding: 2rem;
          max-width: 600px;
          width: 90%;
          max-height: 80vh;
          overflow-y: auto;
        }

        .dashboard .modal h2 {
          margin-top: 0;
        }
      `}</style>
    </AdminLayout>
  )
}
