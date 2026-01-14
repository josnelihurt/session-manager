import { useState, useEffect } from 'react'
import { AdminLayout } from '../../components/Layout/AdminLayout'
import { RoleSelector } from '../../components/RoleSelector'
import { ImpersonateModal } from '../../components/ImpersonateModal'
import { useAuth } from '../../contexts/AuthContext'
import { getAllApplications, assignUserRoles, removeUserRole, setUserActive, deleteUser, getActiveImpersonationSessions, forceEndImpersonationSession } from '../../api'

export function UsersPage() {
  const [users, setUsers] = useState([])
  const [applications, setApplications] = useState([])
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState('')
  const [selectedUser, setSelectedUser] = useState(null)
  const [showRoleModal, setShowRoleModal] = useState(false)
  const [showImpersonateModal, setShowImpersonateModal] = useState(false)
  const [impersonateUser, setImpersonateUser] = useState(null)
  const [selectedUsers, setSelectedUsers] = useState(new Set())
  const [deleting, setDeleting] = useState(false)
  const [activeImpersonations, setActiveImpersonations] = useState([])
  const [loadingImpersonations, setLoadingImpersonations] = useState(false)
  const { isSuperAdmin, canImpersonate: hasImpersonatePermission } = useAuth()

  useEffect(() => {
    loadData()
  }, [])

  const loadData = async () => {
    try {
      const [usersData, appsData] = await Promise.all([
        fetch('/api/users').then(r => r.json()),
        getAllApplications()
      ])
      setUsers(usersData)
      setApplications(appsData)

      // Load active impersonations for users with impersonate permission
      if (hasImpersonatePermission) {
        loadImpersonations()
      }
    } catch (err) {
      setError('Failed to load data')
    } finally {
      setLoading(false)
    }
  }

  const loadImpersonations = async () => {
    setLoadingImpersonations(true)
    try {
      const sessions = await getActiveImpersonationSessions()
      setActiveImpersonations(sessions.data || sessions || [])
    } catch (err) {
      console.error('Failed to load impersonations:', err)
    } finally {
      setLoadingImpersonations(false)
    }
  }

  const handleToggleActive = async (user) => {
    try {
      await setUserActive(user.id, !user.isActive)
      loadData()
    } catch (err) {
      setError('Failed to update user')
    }
  }

  const handleEditRoles = (user) => {
    setSelectedUser(user)
    setShowRoleModal(true)
  }

  const handleImpersonate = (user) => {
    setImpersonateUser(user)
    setShowImpersonateModal(true)
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
      const updatedUsers = await fetch('/api/users').then(r => r.json())
      setUsers(updatedUsers)
      const updatedUser = updatedUsers.find(u => u.id === selectedUser.id)
      setSelectedUser(updatedUser)
    } catch (err) {
      setError('Failed to update roles')
    }
  }

  const handleSelectUser = (userId) => {
    const newSelected = new Set(selectedUsers)
    if (newSelected.has(userId)) {
      newSelected.delete(userId)
    } else {
      newSelected.add(userId)
    }
    setSelectedUsers(newSelected)
  }

  const handleSelectAll = (e) => {
    if (e.target.checked) {
      // Only select non-super-admin users
      setSelectedUsers(new Set(users.filter(u => !u.isSuperAdmin).map(u => u.id)))
    } else {
      setSelectedUsers(new Set())
    }
  }

  const handleBulkDelete = async () => {
    if (selectedUsers.size === 0) return
    if (!confirm(`Delete ${selectedUsers.size} user(s)? This action cannot be undone.`)) return

    setDeleting(true)
    setError('')

    try {
      await Promise.all(
        Array.from(selectedUsers).map(id => deleteUser(id))
      )
      setSelectedUsers(new Set())
      loadData()
    } catch (err) {
      setError('Failed to delete some users')
    } finally {
      setDeleting(false)
    }
  }

  const handleForceEndImpersonation = async (sessionId, targetUsername) => {
    if (!confirm(`End impersonation of ${targetUsername}? This will terminate their impersonated session.`)) return

    try {
      await forceEndImpersonationSession(sessionId)
      await loadImpersonations()
    } catch (err) {
      setError(err.response?.data?.error || 'Failed to end impersonation')
    }
  }

  const canImpersonate = (user) => {
    if (!hasImpersonatePermission) return false
    if (user.isSuperAdmin) return false
    if (!user.isActive) return false
    // Check if user is already being impersonated
    if (isUserBeingImpersonated(user.id)) return false
    return true
  }

  // Only Super Admins can manage users (edit roles, delete)
  const canManageUsers = isSuperAdmin

  const isUserBeingImpersonated = (userId) => {
    return activeImpersonations.some(imp => imp.targetUsername && users.find(u => u.id === userId)?.username === imp.targetUsername)
  }

  const getUserImpersonation = (userId) => {
    const user = users.find(u => u.id === userId)
    return activeImpersonations.find(imp => imp.targetUsername === user?.username)
  }

  const nonSuperAdminUsers = users.filter(u => !u.isSuperAdmin)
  const isAllSelected = nonSuperAdminUsers.length > 0 && selectedUsers.size === nonSuperAdminUsers.length
  const isSomeSelected = selectedUsers.size > 0 && !isAllSelected

  if (loading) return <AdminLayout title="Users"><p>Loading...</p></AdminLayout>

  return (
    <AdminLayout title="Users">
      {error && (
        <div className="error-alert">
          {error}
          <button onClick={() => setError('')} className="alert-close">×</button>
        </div>
      )}

      <div className="actions-bar">
        {canManageUsers && (
          <button
            onClick={handleBulkDelete}
            className="btn btn-danger"
            disabled={selectedUsers.size === 0 || deleting}
          >
            {deleting ? 'Deleting...' : `Delete Selected (${selectedUsers.size})`}
          </button>
        )}
      </div>

      <table className="data-table">
        <thead>
          <tr>
            <th style={{ width: '40px' }}>
              <input
                type="checkbox"
                checked={isAllSelected}
                ref={input => {
                  if (input) {
                    input.indeterminate = isSomeSelected
                  }
                }}
                onChange={handleSelectAll}
              />
            </th>
            <th>Username</th>
            <th>Email</th>
            <th>Provider</th>
            <th>Status</th>
            <th>Roles</th>
            <th>Last Login</th>
            <th>Actions</th>
          </tr>
        </thead>
        <tbody>
          {users.map((user) => {
            const isSelected = selectedUsers.has(user.id)
            const isSelectable = !user.isSuperAdmin
            const userCanImpersonate = canImpersonate(user)
            const impersonation = getUserImpersonation(user.id)
            const isBeingImpersonated = !!impersonation

            return (
              <tr key={user.id} className={`${isSelected ? 'selected' : ''} ${isBeingImpersonated ? 'impersonated-row' : ''}`}>
                <td>
                  {isSelectable ? (
                    <input
                      type="checkbox"
                      checked={isSelected}
                      onChange={() => handleSelectUser(user.id)}
                    />
                  ) : (
                    <span style={{ color: '#999' }} title="Super admin cannot be selected">—</span>
                  )}
                </td>
                <td>
                  {user.username}
                  {user.isSuperAdmin && <span className="badge">Super Admin</span>}
                  {isBeingImpersonated && (
                    <div className="impersonation-badge">
                      👤 Impersonated by {impersonation.impersonatorUsername}
                    </div>
                  )}
                </td>
                <td>{user.email}</td>
                <td>{user.provider}</td>
                <td>
                  <span className={`status ${user.isActive ? 'active' : 'inactive'}`}>
                    {user.isActive ? 'Active' : 'Inactive'}
                  </span>
                </td>
                <td>
                  {user.roles.length > 0 ? (
                    <ul className="role-list">
                      {user.roles.map((role, idx) => (
                        <li key={idx}>
                          {role.applicationName}: {role.roleName}
                        </li>
                      ))}
                    </ul>
                  ) : (
                    <span className="no-roles">No roles</span>
                  )}
                </td>
                <td>
                  {user.lastLoginAt
                    ? new Date(user.lastLoginAt).toLocaleDateString()
                    : 'Never'}
                  {isBeingImpersonated && (
                    <div className="impersonation-time">
                      Expires: {impersonation.remainingMinutes}m
                    </div>
                  )}
                </td>
                <td className="actions">
                  {!user.isSuperAdmin && (
                    <>
                      {canManageUsers && (
                        <>
                          <button
                            onClick={() => handleEditRoles(user)}
                            className="btn btn-small"
                          >
                            Edit Roles
                          </button>
                          <button
                            onClick={() => handleToggleActive(user)}
                            className={`btn btn-small ${user.isActive ? 'btn-danger' : 'btn-success'}`}
                          >
                            {user.isActive ? 'Disable' : 'Enable'}
                          </button>
                        </>
                      )}
                      {isBeingImpersonated && impersonation && (
                        <button
                          onClick={() => handleForceEndImpersonation(impersonation.id, user.username)}
                          className="btn btn-small btn-warning"
                          title={`End impersonation by ${impersonation.impersonatorUsername}`}
                        >
                          End Impersonation
                        </button>
                      )}
                      {userCanImpersonate && (
                        <button
                          onClick={() => handleImpersonate(user)}
                          className="btn btn-small btn-primary"
                          title={`View system as ${user.username}`}
                        >
                          Impersonate
                        </button>
                      )}
                    </>
                  )}
                </td>
              </tr>
            )
          })}
        </tbody>
      </table>

      {showRoleModal && selectedUser && (
        <div className="modal-overlay" onClick={() => setShowRoleModal(false)}>
          <div className="modal" onClick={(e) => e.stopPropagation()}>
            <h2>Edit Roles for {selectedUser.username}</h2>

            <RoleSelector
              applications={applications}
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

      {showImpersonateModal && impersonateUser && (
        <ImpersonateModal
          user={impersonateUser}
          onClose={() => {
            setShowImpersonateModal(false)
            setImpersonateUser(null)
          }}
        />
      )}
    </AdminLayout>
  )
}
