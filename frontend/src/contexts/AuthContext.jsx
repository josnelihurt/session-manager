import { createContext, useContext, useState, useEffect } from 'react'
import { api } from '../api'

const AuthContext = createContext()

export function AuthProvider({ children }) {
  const [user, setUser] = useState(null)
  const [impersonation, setImpersonation] = useState(null)
  const [loading, setLoading] = useState(true)

  useEffect(() => {
    checkAuth()
  }, [])

  const checkAuth = async () => {
    try {
      const response = await api.get('/auth/me')
      setUser(response.data)
    } catch {
      setUser(null)
    } finally {
      setLoading(false)
    }
  }

  const checkImpersonationStatus = async () => {
    try {
      const response = await api.get('/impersonate/status')
      setImpersonation(response.data.data)
    } catch {
      setImpersonation(null)
    }
  }

  const login = async (username, password, otpCode = null) => {
    const response = await api.post('/auth/login', {
      username,
      password,
      otpCode
    })
    if (response.data.user) {
      setUser(response.data.user)
    }
    return response.data
  }

  const loginWithGoogle = () => {
    window.location.href = '/api/auth/login/google'
  }

  const loginWithAuth0 = () => {
    // Add forceLogin=true to require credentials each time (not SSO)
    window.location.href = '/api/auth/login/auth0?forceLogin=true'
  }

  const logout = () => {
    // Redirect to GET /api/auth/logout which handles OAuth provider logout
    window.location.href = '/api/auth/logout'
  }

  const startImpersonation = async (userId, reason, durationMinutes = 30) => {
    const response = await api.post(`/impersonate/${userId}`, {
      reason,
      durationMinutes
    })
    // After impersonation, reload the page to get the new session
    if (response.data.success) {
      window.location.reload()
    }
    return response.data
  }

  const endImpersonation = async () => {
    const response = await api.delete('/impersonate')
    // After ending impersonation, reload the page
    if (response.data.success) {
      window.location.reload()
    }
    return response.data
  }

  return (
    <AuthContext.Provider value={{
      user,
      impersonation,
      loading,
      login,
      loginWithGoogle,
      loginWithAuth0,
      logout,
      startImpersonation,
      endImpersonation,
      checkImpersonationStatus,
      isSuperAdmin: user?.isSuperAdmin || false,
      isImpersonating: impersonation?.isImpersonating || false,
    }}>
      {children}
    </AuthContext.Provider>
  )
}

export function useAuth() {
  const context = useContext(AuthContext)
  if (!context) {
    throw new Error('useAuth must be used within AuthProvider')
  }
  return context
}
