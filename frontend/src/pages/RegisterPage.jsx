import { useState, useEffect } from 'react'
import { useNavigate, useSearchParams } from 'react-router-dom'
import { useAuth } from '../contexts/AuthContext'
import * as api from '../api'
import './LoginPage.css'

export function RegisterPage() {
  const { loginWithGoogle } = useAuth()
  const navigate = useNavigate()
  const [searchParams] = useSearchParams()
  const [token, setToken] = useState('')
  const [invitation, setInvitation] = useState(null)
  const [loading, setLoading] = useState(true)
  const [registering, setRegistering] = useState(false)
  const [error, setError] = useState('')
  const [provider, setProvider] = useState('local')
  const [username, setUsername] = useState('')
  const [password, setPassword] = useState('')
  const [confirmPassword, setConfirmPassword] = useState('')

  useEffect(() => {
    const invitationToken = searchParams.get('token')
    if (!invitationToken) {
      setError('Invalid registration link. Missing invitation token.')
      setLoading(false)
      return
    }

    setToken(invitationToken)
    validateInvitation(invitationToken)
  }, [searchParams])

  const validateInvitation = async (invitationToken) => {
    try {
      const response = await api.validateInvitationToken(invitationToken)
      setInvitation(response)
      setError('')
    } catch (err) {
      setError(err.response?.data?.error || 'Invalid or expired invitation token')
    } finally {
      setLoading(false)
    }
  }

  const handleProviderChange = (selectedProvider) => {
    setProvider(selectedProvider)
    setError('')
  }

  const handleGoogleRegister = () => {
    // Redirect to Google OAuth with invitation token
    window.location.href = `/api/auth/login/google?invitation=${token}`
  }

  const handleSubmit = async (e) => {
    e.preventDefault()
    setError('')

    // Validation
    if (password.length < 8) {
      setError('Password must be at least 8 characters')
      return
    }

    if (password !== confirmPassword) {
      setError('Passwords do not match')
      return
    }

    setRegistering(true)

    try {
      await api.register(token, provider, username, password)
      navigate('/dashboard')
    } catch (err) {
      setError(err.response?.data?.error || 'Registration failed')
    } finally {
      setRegistering(false)
    }
  }

  if (loading) {
    return (
      <div className="login-page">
        <div className="login-container">
          <h1>Session Manager</h1>
          <p className="subtitle">Validating invitation...</p>
        </div>
      </div>
    )
  }

  if (error && !invitation) {
    return (
      <div className="login-page">
        <div className="login-container">
          <h1>Session Manager</h1>
          <div className="error-alert">{error}</div>
          <button className="btn btn-secondary" onClick={() => navigate('/login')}>
            Back to Login
          </button>
        </div>
      </div>
    )
  }

  return (
    <div className="login-page">
      <div className="login-container">
        <h1>Create Account</h1>
        <p className="subtitle">Invitation for {invitation?.email}</p>

        {error && (
          <div className="error-alert">
            {error}
          </div>
        )}

        {invitation && (
          <div className="invitation-info">
            <p>You've been invited to join Session Manager</p>
            {invitation.roles && invitation.roles.length > 0 && (
              <p className="roles-info">With access to: {invitation.roles.join(', ')}</p>
            )}
          </div>
        )}

        <div className="provider-selection">
          <button
            type="button"
            className={`btn btn-provider ${provider === 'local' ? 'active' : ''}`}
            onClick={() => handleProviderChange('local')}
          >
            <svg width="18" height="18" viewBox="0 0 18 18" fill="currentColor">
              <path d="M9 1C4.5 1 1 4.5 1 9s3.5 8 8 8 8-3.5 8-8-3.5-8-8-8zm0 14c-3.3 0-6-2.7-6-6s2.7-6 6-6 6 2.7 6 6-2.7 6-6 6zm0-10c-2.2 0-4 1.8-4 4s1.8 4 4 4 4-1.8 4-4-1.8-4-4-4z"/>
            </svg>
            Username & Password
          </button>
          <button
            type="button"
            className={`btn btn-provider ${provider === 'google' ? 'active' : ''}`}
            onClick={() => handleProviderChange('google')}
          >
            <svg width="18" height="18" viewBox="0 0 18 18">
              <path d="M17.64 9.2c0-.637-.057-1.252-.164-1.841H9v3.481h4.844a4.14 4.14 0 0 1-1.796 2.716v2.259h2.908c1.702-1.567 2.684-3.875 2.684-6.615z" fill="#4285F4"/>
              <path d="M9 18c2.43 0 4.467-.806 5.956-2.18l-2.908-2.259c-.806.54-1.837.86-3.048.86-2.344 0-4.328-1.584-5.036-3.715H.957v2.332A8.997 8.997 0 0 0 9 18z" fill="#34A853"/>
              <path d="M3.964 10.71A5.41 5.41 0 0 1 3.682 9c0-.593.102-1.17.282-1.71V4.958H.957A8.996 8.996 0 0 0 0 9c0 1.452.348 2.827.957 4.042l3.007-2.332z" fill="#FBBC05"/>
              <path d="M9 3.58c1.321 0 2.508.454 3.44 1.345l2.582-2.58C13.463.891 11.426 0 9 0A8.997 8.997 0 0 0 .957 4.958L3.964 7.272C4.672 5.142 6.656 3.58 9 3.58z" fill="#EA4335"/>
            </svg>
            Google
          </button>
        </div>

        {provider === 'local' ? (
          <form onSubmit={handleSubmit} className="login-form">
            <div className="form-group">
              <label htmlFor="username">Username</label>
              <input
                id="username"
                type="text"
                value={username}
                onChange={(e) => setUsername(e.target.value)}
                required
                minLength={3}
                placeholder="Choose a username"
              />
            </div>

            <div className="form-group">
              <label htmlFor="password">Password</label>
              <input
                id="password"
                type="password"
                value={password}
                onChange={(e) => setPassword(e.target.value)}
                required
                minLength={8}
                placeholder="At least 8 characters"
              />
            </div>

            <div className="form-group">
              <label htmlFor="confirmPassword">Confirm Password</label>
              <input
                id="confirmPassword"
                type="password"
                value={confirmPassword}
                onChange={(e) => setConfirmPassword(e.target.value)}
                required
                minLength={8}
                placeholder="Confirm your password"
              />
            </div>

            <button type="submit" className="btn btn-primary" disabled={registering}>
              {registering ? 'Creating account...' : 'Create Account'}
            </button>
          </form>
        ) : (
          <div className="google-register">
            <p>Continue with Google to create your account</p>
            <button
              type="button"
              onClick={handleGoogleRegister}
              className="btn btn-google"
            >
              <svg width="18" height="18" viewBox="0 0 18 18">
                <path d="M17.64 9.2c0-.637-.057-1.252-.164-1.841H9v3.481h4.844a4.14 4.14 0 0 1-1.796 2.716v2.259h2.908c1.702-1.567 2.684-3.875 2.684-6.615z" fill="#4285F4"/>
                <path d="M9 18c2.43 0 4.467-.806 5.956-2.18l-2.908-2.259c-.806.54-1.837.86-3.048.86-2.344 0-4.328-1.584-5.036-3.715H.957v2.332A8.997 8.997 0 0 0 9 18z" fill="#34A853"/>
                <path d="M3.964 10.71A5.41 5.41 0 0 1 3.682 9c0-.593.102-1.17.282-1.71V4.958H.957A8.996 8.996 0 0 0 0 9c0 1.452.348 2.827.957 4.042l3.007-2.332z" fill="#FBBC05"/>
                <path d="M9 3.58c1.321 0 2.508.454 3.44 1.345l2.582-2.58C13.463.891 11.426 0 9 0A8.997 8.997 0 0 0 .957 4.958L3.964 7.272C4.672 5.142 6.656 3.58 9 3.58z" fill="#EA4335"/>
              </svg>
              Sign up with Google
            </button>
          </div>
        )}

        <div className="login-links">
          <span>Already have an account?</span>
          <button type="button" className="btn-link" onClick={() => navigate('/login')}>
            Sign In
          </button>
        </div>
      </div>
    </div>
  )
}
