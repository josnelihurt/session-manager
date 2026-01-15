import { useState, useEffect } from 'react'
import { AdminLayout } from '../../components/Layout/AdminLayout'
import { getAllApplications, createApplication, createApplicationRole, deleteApplicationRole, updateApplicationRole } from '../../api'

export function ApplicationsPage() {
  const [applications, setApplications] = useState([])
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState('')
  const [showCreateModal, setShowCreateModal] = useState(false)
  const [showRoleModal, setShowRoleModal] = useState(false)
  const [showEditRoleModal, setShowEditRoleModal] = useState(false)
  const [selectedApp, setSelectedApp] = useState(null)
  const [selectedRole, setSelectedRole] = useState(null)

  const [url, setUrl] = useState('')
  const [name, setName] = useState('')
  const [description, setDescription] = useState('')
  const [roleName, setRoleName] = useState('')
  const [editRoleName, setEditRoleName] = useState('')
  const [editPermissions, setEditPermissions] = useState({})

  useEffect(() => {
    loadData()
  }, [])

  const loadData = async () => {
    try {
      const data = await getAllApplications()
      setApplications(data)
    } catch (err) {
      setError('Failed to load applications')
    } finally {
      setLoading(false)
    }
  }

  const handleCreateApp = async (e) => {
    e.preventDefault()
    try {
      await createApplication(url, name, description)
      loadData()
      setShowCreateModal(false)
      setUrl('')
      setName('')
      setDescription('')
    } catch (err) {
      setError(err.response?.data?.error || 'Failed to create application')
    }
  }

  const handleCreateRole = async (e) => {
    e.preventDefault()
    if (!selectedApp) return

    try {
      await createApplicationRole(selectedApp.id, roleName, {})
      loadData()
      setRoleName('')
      const updated = await getAllApplications()
      setSelectedApp(updated.find(a => a.id === selectedApp.id) || null)
    } catch (err) {
      setError(err.response?.data?.error || 'Failed to create role')
    }
  }

  const handleDeleteRole = async (roleId) => {
    if (!confirm('Delete this role?')) return

    try {
      await deleteApplicationRole(roleId)
      loadData()
      if (selectedApp) {
        const updated = await getAllApplications()
        setSelectedApp(updated.find(a => a.id === selectedApp.id) || null)
      }
    } catch (err) {
      setError('Failed to delete role')
    }
  }

  const openEditRoleModal = (role) => {
    setSelectedRole(role)
    setEditRoleName(role.name)
    setEditPermissions(role.permissions || {})
    setShowEditRoleModal(true)
  }

  const handleUpdateRole = async (e) => {
    e.preventDefault()
    if (!selectedRole) return

    try {
      await updateApplicationRole(selectedRole.id, editRoleName, editPermissions)
      setShowEditRoleModal(false)
      setSelectedRole(null)
      setEditRoleName('')
      setEditPermissions({})
      loadData()
      if (selectedApp) {
        const updated = await getAllApplications()
        setSelectedApp(updated.find(a => a.id === selectedApp.id) || null)
      }
    } catch (err) {
      setError(err.response?.data?.error || 'Failed to update role')
    }
  }

  const togglePermission = (key) => {
    setEditPermissions(prev => ({
      ...prev,
      [key]: !prev[key]
    }))
  }

  const formatPermissions = (permissions) => {
    if (!permissions || Object.keys(permissions).length === 0) {
      return <span className="permissions-empty">No permissions</span>
    }
    return Object.entries(permissions)
      .filter(([_, value]) => value)
      .map(([key, _]) => (
        <span key={key} className="permission-tag">{key}</span>
      ))
  }

  if (loading) return <AdminLayout title="Applications"><p>Loading...</p></AdminLayout>

  return (
    <AdminLayout title="Applications">
      {error && (
        <div className="error-alert">
          {error}
          <button onClick={() => setError('')} className="alert-close">×</button>
        </div>
      )}

      <div className="actions-bar">
        <button
          onClick={() => setShowCreateModal(true)}
          className="btn btn-primary"
        >
          Add Application
        </button>
      </div>

      <div className="applications-grid">
        {applications.map((app) => (
          <div key={app.id} className="app-card">
            <div className="app-header">
              <h3>{app.name}</h3>
              <span className={`status ${app.isActive ? 'active' : 'inactive'}`}>
                {app.isActive ? 'Active' : 'Inactive'}
              </span>
            </div>
            <p className="app-url">{app.url}</p>
            {app.description && <p className="app-desc">{app.description}</p>}

            <table className="app-roles-table">
              <thead>
                <tr>
                  <th>Role</th>
                  <th>Permissions</th>
                  <th>Actions</th>
                </tr>
              </thead>
              <tbody>
                {app.roles.map((role) => (
                  <tr key={role.id}>
                    <td>{role.name}</td>
                    <td className="permissions-cell">{formatPermissions(role.permissions)}</td>
                    <td>
                      <button
                        onClick={() => openEditRoleModal(role)}
                        className="btn-icon"
                        title="Edit role permissions"
                      >
                        ✎
                      </button>
                      <button
                        onClick={() => handleDeleteRole(role.id)}
                        className="btn-icon btn-danger"
                        title="Delete role"
                      >
                        ×
                      </button>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
            <button
              onClick={() => {
                setSelectedApp(app)
                setShowRoleModal(true)
              }}
              className="btn btn-small"
            >
              Add Role
            </button>
          </div>
        ))}
      </div>

      {showCreateModal && (
        <div className="modal-overlay" onClick={() => setShowCreateModal(false)}>
          <div className="modal" onClick={(e) => e.stopPropagation()}>
            <h2>Add Application</h2>
            <form onSubmit={handleCreateApp}>
              <div className="form-group">
                <label>URL (hostname)</label>
                <input
                  type="text"
                  value={url}
                  onChange={(e) => setUrl(e.target.value)}
                  placeholder="app.lab.josnelihurt.me"
                  required
                />
              </div>
              <div className="form-group">
                <label>Name</label>
                <input
                  type="text"
                  value={name}
                  onChange={(e) => setName(e.target.value)}
                  placeholder="My Application"
                  required
                />
              </div>
              <div className="form-group">
                <label>Description (optional)</label>
                <textarea
                  value={description}
                  onChange={(e) => setDescription(e.target.value)}
                  placeholder="What does this application do?"
                />
              </div>
              <div className="modal-actions">
                <button type="button" onClick={() => setShowCreateModal(false)} className="btn">
                  Cancel
                </button>
                <button type="submit" className="btn btn-primary">
                  Create
                </button>
              </div>
            </form>
          </div>
        </div>
      )}

      {showRoleModal && selectedApp && (
        <div className="modal-overlay" onClick={() => setShowRoleModal(false)}>
          <div className="modal" onClick={(e) => e.stopPropagation()}>
            <h2>Add Role to {selectedApp.name}</h2>
            <form onSubmit={handleCreateRole}>
              <div className="form-group">
                <label>Role Name</label>
                <input
                  type="text"
                  value={roleName}
                  onChange={(e) => setRoleName(e.target.value)}
                  placeholder="e.g., editor, contributor"
                  required
                />
              </div>
              <div className="modal-actions">
                <button type="button" onClick={() => setShowRoleModal(false)} className="btn">
                  Cancel
                </button>
                <button type="submit" className="btn btn-primary">
                  Create Role
                </button>
              </div>
            </form>
          </div>
        </div>
      )}

      {showEditRoleModal && selectedRole && (
        <div className="modal-overlay" onClick={() => setShowEditRoleModal(false)}>
          <div className="modal" onClick={(e) => e.stopPropagation()}>
            <h2>Edit Role: {selectedRole.name}</h2>
            <form onSubmit={handleUpdateRole}>
              <div className="form-group">
                <label>Role Name</label>
                <input
                  type="text"
                  value={editRoleName}
                  onChange={(e) => setEditRoleName(e.target.value)}
                  required
                />
              </div>
              <div className="form-group">
                <label>Permissions</label>
                <div className="permissions-editor">
                  <label className="permission-checkbox">
                    <input
                      type="checkbox"
                      checked={editPermissions.impersonate || false}
                      onChange={() => togglePermission('impersonate')}
                    />
                    <span>Can Impersonate Users</span>
                  </label>
                  <label className="permission-checkbox">
                    <input
                      type="checkbox"
                      checked={editPermissions.read || false}
                      onChange={() => togglePermission('read')}
                    />
                    <span>Read</span>
                  </label>
                  <label className="permission-checkbox">
                    <input
                      type="checkbox"
                      checked={editPermissions.write || false}
                      onChange={() => togglePermission('write')}
                    />
                    <span>Write</span>
                  </label>
                  <label className="permission-checkbox">
                    <input
                      type="checkbox"
                      checked={editPermissions.delete || false}
                      onChange={() => togglePermission('delete')}
                    />
                    <span>Delete</span>
                  </label>
                  <label className="permission-checkbox">
                    <input
                      type="checkbox"
                      checked={editPermissions.admin || false}
                      onChange={() => togglePermission('admin')}
                    />
                    <span>Admin</span>
                  </label>
                </div>
              </div>
              <div className="modal-actions">
                <button type="button" onClick={() => setShowEditRoleModal(false)} className="btn">
                  Cancel
                </button>
                <button type="submit" className="btn btn-primary">
                  Update Role
                </button>
              </div>
            </form>
          </div>
        </div>
      )}
    </AdminLayout>
  )
}
