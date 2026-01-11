import { useState, useEffect } from 'react'
import { AdminLayout } from '../../components/Layout/AdminLayout'
import { getAllApplications, createApplication, createApplicationRole, deleteApplicationRole } from '../../api'

export function ApplicationsPage() {
  const [applications, setApplications] = useState([])
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState('')
  const [showCreateModal, setShowCreateModal] = useState(false)
  const [showRoleModal, setShowRoleModal] = useState(false)
  const [selectedApp, setSelectedApp] = useState(null)

  const [url, setUrl] = useState('')
  const [name, setName] = useState('')
  const [description, setDescription] = useState('')
  const [roleName, setRoleName] = useState('')

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
                  <th>Actions</th>
                </tr>
              </thead>
              <tbody>
                {app.roles.map((role) => (
                  <tr key={role.id}>
                    <td>{role.name}</td>
                    <td>
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
    </AdminLayout>
  )
}
