import { useEffect, useState } from 'react'

/**
 * RoleSelector Component
 *
 * Two modes of operation:
 *
 * 1. Local state mode (default for create forms):
 *    - Pass `selectedRoles` (array of role IDs)
 *    - `onRolesChange` is called with new array when checkboxes change
 *    - Changes are tracked locally until form is submitted
 *
 * 2. Immediate update mode (for edit modals):
 *    - Pass `isRoleSelected(roleId)` function
 *    - Pass `onRoleToggle(roleId, checked)` function
 *    - Changes are sent immediately to parent/API
 */
export function RoleSelector({
  applications,
  selectedRoles = [],
  onRolesChange,
  isRoleSelected,
  onRoleToggle,
  label = 'Roles'
}) {
  // Determine which mode to use
  const useLocalState = !isRoleSelected || !onRoleToggle

  // Initialize local state if using local mode
  const [localRoles, setLocalRoles] = useState([])

  useEffect(() => {
    if (useLocalState) {
      setLocalRoles(selectedRoles)
    }
  }, [selectedRoles, useLocalState])

  const handleRoleToggle = (roleId) => {
    if (useLocalState) {
      // Local state mode: update local array and call onRolesChange
      const newRoles = localRoles.includes(roleId)
        ? localRoles.filter(id => id !== roleId)
        : [...localRoles, roleId]

      setLocalRoles(newRoles)
      if (onRolesChange) {
        onRolesChange(newRoles)
      }
    } else {
      // Immediate mode: call onRoleToggle with checked state
      const isChecked = isRoleSelected(roleId)
      onRoleToggle(roleId, !isChecked)
    }
  }

  const checkIsSelected = (roleId) => {
    if (useLocalState) {
      return localRoles.includes(roleId)
    }
    return isRoleSelected(roleId)
  }

  const getRoleForApplication = (app, roleName) => {
    return app.roles.find(r => r.name.toLowerCase() === roleName.toLowerCase())
  }

  return (
    <div className="form-group">
      <label>{label}</label>
      <table className="roles-table">
        <thead>
          <tr>
            <th>Application</th>
            <th>Admin</th>
            <th>User</th>
            <th>Viewer</th>
          </tr>
        </thead>
        <tbody>
          {applications.map((app) => {
            const adminRole = getRoleForApplication(app, 'admin')
            const userRole = getRoleForApplication(app, 'user')
            const viewerRole = getRoleForApplication(app, 'viewer')

            return (
              <tr key={app.id}>
                <td>{app.name}</td>
                <td className="role-checkbox-cell">
                  {adminRole && (
                    <input
                      type="checkbox"
                      checked={checkIsSelected(adminRole.id)}
                      onChange={() => handleRoleToggle(adminRole.id)}
                      aria-label={`${app.name} admin`}
                    />
                  )}
                </td>
                <td className="role-checkbox-cell">
                  {userRole && (
                    <input
                      type="checkbox"
                      checked={checkIsSelected(userRole.id)}
                      onChange={() => handleRoleToggle(userRole.id)}
                      aria-label={`${app.name} user`}
                    />
                  )}
                </td>
                <td className="role-checkbox-cell">
                  {viewerRole && (
                    <input
                      type="checkbox"
                      checked={checkIsSelected(viewerRole.id)}
                      onChange={() => handleRoleToggle(viewerRole.id)}
                      aria-label={`${app.name} viewer`}
                    />
                  )}
                </td>
              </tr>
            )
          })}
        </tbody>
      </table>
      {useLocalState && localRoles.length === 0 && (
        <p className="no-roles">No roles selected</p>
      )}
    </div>
  )
}
