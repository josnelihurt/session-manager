import { createContext, useContext, useState, useEffect } from 'react'
import { api } from '../api'

const AuthContext = createContext()

export function AuthProvider({ children }) {
  const [user, setUser] = useState(null)
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

  return (
    <AuthContext.Provider value={{
      user,
      loading,
      login,
      loginWithGoogle,
      loginWithAuth0,
      logout,
      isSuperAdmin: user?.isSuperAdmin || false,
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
