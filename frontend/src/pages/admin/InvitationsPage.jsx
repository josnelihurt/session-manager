import { useState, useEffect } from 'react'
import { AdminLayout } from '../../components/Layout/AdminLayout'
import { RoleSelector } from '../../components/RoleSelector'
import { getAllApplications, createInvitation, deleteInvitation, resendInvitationEmail } from '../../api'

export function InvitationsPage() {
  const [invitations, setInvitations] = useState([])
  const [applications, setApplications] = useState([])
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState('')
  const [showCreateModal, setShowCreateModal] = useState(false)
  const [selectedInvitations, setSelectedInvitations] = useState(new Set())

  const [email, setEmail] = useState('')
  const [provider, setProvider] = useState('local')
  const [selectedRoles, setSelectedRoles] = useState([])
  const [sendEmail, setSendEmail] = useState(true)
  const [creating, setCreating] = useState(false)
  const [createdInvite, setCreatedInvite] = useState(null)
  const [deleting, setDeleting] = useState(false)

  useEffect(() => {
    loadData()
  }, [])

  const loadData = async () => {
    try {
      const [invData, appsData] = await Promise.all([
        fetch('/api/invitations').then(r => r.json()),
        getAllApplications()
      ])
      setInvitations(invData)
      setApplications(appsData)
    } catch (err) {
      setError('Failed to load data')
    } finally {
      setLoading(false)
    }
  }

  const handleCreate = async (e) => {
    e.preventDefault()
    setCreating(true)
    setError('')

    try {
      const result = await createInvitation(email, provider, selectedRoles, sendEmail)
      setCreatedInvite(result)
      loadData()
    } catch (err) {
      setError(err.response?.data?.error || 'Failed to create invitation')
    } finally {
      setCreating(false)
    }
  }

  const handleDelete = async (id) => {
    if (!confirm('Delete this invitation?')) return

    try {
      await deleteInvitation(id)
      loadData()
    } catch (err) {
      setError('Failed to delete invitation')
    }
  }

  const handleResendEmail = async (id) => {
    try {
      await resendInvitationEmail(id)
      setError('')
      alert('Email resent successfully!')
    } catch (err) {
      setError(err.response?.data?.error || 'Failed to resend email')
    }
  }

  const handleSelectInvitation = (id) => {
    const newSelected = new Set(selectedInvitations)
    if (newSelected.has(id)) {
      newSelected.delete(id)
    } else {
      newSelected.add(id)
    }
    setSelectedInvitations(newSelected)
  }

  const handleSelectAll = (e) => {
    if (e.target.checked) {
      setSelectedInvitations(new Set(invitations.map(inv => inv.id)))
    } else {
      setSelectedInvitations(new Set())
    }
  }

  const handleBulkDelete = async () => {
    if (selectedInvitations.size === 0) return
    if (!confirm(`Delete ${selectedInvitations.size} invitation(s)?`)) return

    setDeleting(true)
    setError('')

    try {
      await Promise.all(
        Array.from(selectedInvitations).map(id => deleteInvitation(id))
      )
      setSelectedInvitations(new Set())
      loadData()
    } catch (err) {
      setError('Failed to delete some invitations')
    } finally {
      setDeleting(false)
    }
  }

  const copyToClipboard = (text) => {
    navigator.clipboard.writeText(text)
    alert('Copied to clipboard!')
  }

  const resetCreateForm = () => {
    setEmail('')
    setProvider('local')
    setSelectedRoles([])
    setSendEmail(true)
    setCreatedInvite(null)
    setShowCreateModal(false)
  }

  const isAllSelected = invitations.length > 0 && selectedInvitations.size === invitations.length
  const isSomeSelected = selectedInvitations.size > 0 && !isAllSelected

  if (loading) return <AdminLayout title="Invitations"><p>Loading...</p></AdminLayout>

  return (
    <AdminLayout title="Invitations">
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
          Create Invitation
        </button>
        <button
          onClick={handleBulkDelete}
          className="btn btn-danger"
          disabled={selectedInvitations.size === 0 || deleting}
        >
          {deleting ? 'Deleting...' : `Delete Selected (${selectedInvitations.size})`}
        </button>
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
            <th>Email</th>
            <th>Provider</th>
            <th>Status</th>
            <th>Pre-assigned Roles</th>
            <th>Created</th>
            <th>Expires</th>
            <th>Actions</th>
          </tr>
        </thead>
        <tbody>
          {invitations.map((inv) => {
            const isExpired = new Date(inv.expiresAt) < new Date()
            const status = inv.isUsed ? 'used' : isExpired ? 'expired' : 'pending'
            const isSelected = selectedInvitations.has(inv.id)

            return (
              <tr key={inv.id} className={isSelected ? 'selected' : ''}>
                <td>
                  <input
                    type="checkbox"
                    checked={isSelected}
                    onChange={() => handleSelectInvitation(inv.id)}
                  />
                </td>
                <td>{inv.email}</td>
                <td>{inv.provider}</td>
                <td>
                  <span className={`status ${status}`}>
                    {status.charAt(0).toUpperCase() + status.slice(1)}
                  </span>
                </td>
                <td>
                  {inv.preAssignedRoles.length > 0 ? (
                    <ul className="role-list">
                      {inv.preAssignedRoles.map((role, i) => (
                        <li key={i}>{role}</li>
                      ))}
                    </ul>
                  ) : (
                    <span className="no-roles">None</span>
                  )}
                </td>
                <td>{new Date(inv.createdAt).toLocaleDateString()}</td>
                <td>{new Date(inv.expiresAt).toLocaleDateString()}</td>
                <td className="actions">
                  {status === 'pending' && (
                    <>
                      <button
                        onClick={() => copyToClipboard(inv.inviteUrl)}
                        className="btn btn-small"
                      >
                        Copy Link
                      </button>
                      <button
                        onClick={() => handleResendEmail(inv.id)}
                        className="btn btn-small"
                      >
                        Resend Email
                      </button>
                      <button
                        onClick={() => handleDelete(inv.id)}
                        className="btn btn-small btn-danger"
                      >
                        Delete
                      </button>
                    </>
                  )}
                </td>
              </tr>
            )
          })}
        </tbody>
      </table>

      {showCreateModal && (
        <div className="modal-overlay" onClick={resetCreateForm}>
          <div className="modal" onClick={(e) => e.stopPropagation()}>
            {createdInvite ? (
              <>
                <h2>Invitation Created!</h2>
                <p>Share this link with {createdInvite.email}:</p>
                <div className="invite-link">
                  <input
                    type="text"
                    value={createdInvite.inviteUrl}
                    readOnly
                  />
                  <button
                    onClick={() => copyToClipboard(createdInvite.inviteUrl)}
                    className="btn"
                  >
                    Copy
                  </button>
                </div>
                <button onClick={resetCreateForm} className="btn btn-primary">
                  Done
                </button>
              </>
            ) : (
              <>
                <h2>Create Invitation</h2>
                <form onSubmit={handleCreate}>
                  <div className="form-group">
                    <label>Email Address</label>
                    <input
                      type="email"
                      value={email}
                      onChange={(e) => setEmail(e.target.value)}
                      placeholder="user@example.com"
                      required
                    />
                  </div>

                  <div className="form-group">
                    <label>Authentication Provider</label>
                    <select
                      value={provider}
                      onChange={(e) => setProvider(e.target.value)}
                    >
                      <option value="google">Google only</option>
                      <option value="local">Local only</option>
                    </select>
                  </div>

                  <RoleSelector
                    applications={applications}
                    selectedRoles={selectedRoles}
                    onRolesChange={setSelectedRoles}
                    label="Pre-assign Roles (optional)"
                  />

                  <div className="form-group checkbox-group">
                    <label className="checkbox-label">
                      <input
                        type="checkbox"
                        checked={sendEmail}
                        onChange={(e) => setSendEmail(e.target.checked)}
                      />
                      <span>Send invitation email</span>
                    </label>
                  </div>

                  <div className="modal-actions">
                    <button type="button" onClick={resetCreateForm} className="btn">
                      Cancel
                    </button>
                    <button type="submit" className="btn btn-primary" disabled={creating}>
                      {creating ? 'Creating...' : 'Create Invitation'}
                    </button>
                  </div>
                </form>
              </>
            )}
          </div>
        </div>
      )}
    </AdminLayout>
  )
}
