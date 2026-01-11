import React, { useState } from 'react'
import { Link, useNavigate } from 'react-router-dom'
import { useAuth } from '../../contexts/AuthContext'
import './Navbar.css'

export function Navbar() {
  const { user, isSuperAdmin, logout } = useAuth()
  const navigate = useNavigate()

  const handleLogout = async () => {
    await logout()
    navigate('/login')
  }

  const [dropdownOpen, setDropdownOpen] = useState(false)

  return (
    <nav className="navbar">
      <div className="navbar-brand">
        <Link to="/dashboard">Session Manager</Link>
      </div>

      <div className="navbar-menu">
        <Link to="/dashboard" className="nav-link">Dashboard</Link>
        <Link to="/sessions" className="nav-link">Sessions</Link>

        {isSuperAdmin && (
          <div className="nav-dropdown"
               onMouseEnter={() => setDropdownOpen(true)}
               onMouseLeave={() => setDropdownOpen(false)}>
            <button className="nav-link dropdown-trigger">
              Admin <span className="arrow">▼</span>
            </button>
            {dropdownOpen && (
              <div className="dropdown-menu">
                <Link to="/admin/users">Users</Link>
                <Link to="/admin/invitations">Invitations</Link>
                <Link to="/admin/applications">Applications</Link>
              </div>
            )}
          </div>
        )}
      </div>

      <div className="navbar-user">
        <span className="user-info">
          {user?.username}
          {isSuperAdmin && <span className="badge">Admin</span>}
        </span>
        <button onClick={handleLogout} className="btn btn-small">
          Logout
        </button>
      </div>
    </nav>
  )
}
