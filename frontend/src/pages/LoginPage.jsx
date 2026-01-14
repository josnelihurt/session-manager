import { useState } from 'react'
import { useAuth } from '../contexts/AuthContext'
import './LoginPage.css'

export function LoginPage() {
  const { login, loginWithGoogle, loginWithAuth0 } = useAuth()
  const [username, setUsername] = useState('')
  const [password, setPassword] = useState('')
  const [otpCode, setOtpCode] = useState('')
  const [error, setError] = useState('')
  const [loading, setLoading] = useState(false)
  const [step, setStep] = useState(1) // 1 = credentials, 2 = OTP
  const [userEmail, setUserEmail] = useState('')
  const [message, setMessage] = useState('')

  const handleCredentialsSubmit = async (e) => {
    e.preventDefault()
    setError('')
    setLoading(true)

    try {
      const result = await login(username, password)

      if (result.requiresOtp) {
        setStep(2)
        setUserEmail(result.email)
        setMessage(result.message)
      } else {
        window.location.href = '/dashboard'
      }
    } catch (err) {
      setError(err.response?.data?.error || 'Login failed')
    } finally {
      setLoading(false)
    }
  }

  const handleOtpSubmit = async (e) => {
    e.preventDefault()
    setError('')
    setLoading(true)

    try {
      await login(username, password, otpCode)
      window.location.href = '/dashboard'
    } catch (err) {
      setError(err.response?.data?.error || 'Invalid verification code')
    } finally {
      setLoading(false)
    }
  }

  const handleBack = () => {
    setStep(1)
    setOtpCode('')
    setError('')
    setMessage('')
  }

  return (
    <div className="login-page">
      <div className="login-container">
        <h1>Session Manager</h1>
        <p className="subtitle">{step === 1 ? 'Sign in to access your applications' : 'Enter verification code'}</p>

        {error && (
          <div className="error-alert">
            {error}
            <button onClick={() => setError('')} className="alert-close">×</button>
          </div>
        )}

        {step === 1 ? (
          <>
            <form onSubmit={handleCredentialsSubmit} className="login-form">
              <div className="form-group">
                <label htmlFor="username">Username</label>
                <input
                  id="username"
                  type="text"
                  value={username}
                  onChange={(e) => setUsername(e.target.value)}
                  required
                  autoFocus
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
                />
              </div>

              <button type="submit" className="btn btn-primary" disabled={loading}>
                {loading ? 'Signing in...' : 'Sign In'}
              </button>
            </form>

            <div className="divider">
              <span>or</span>
            </div>

            <button
              type="button"
              onClick={loginWithGoogle}
              className="btn btn-google"
            >
              <svg width="18" height="18" viewBox="0 0 18 18">
                <path d="M17.64 9.2c0-.637-.057-1.252-.164-1.841H9v3.481h4.844a4.14 4.14 0 0 1-1.796 2.716v2.259h2.908c1.702-1.567 2.684-3.875 2.684-6.615z" fill="#4285F4"/>
                <path d="M9 18c2.43 0 4.467-.806 5.956-2.18l-2.908-2.259c-.806.54-1.837.86-3.048.86-2.344 0-4.328-1.584-5.036-3.715H.957v2.332A8.997 8.997 0 0 0 9 18z" fill="#34A853"/>
                <path d="M3.964 10.71A5.41 5.41 0 0 1 3.682 9c0-.593.102-1.17.282-1.71V4.958H.957A8.996 8.996 0 0 0 0 9c0 1.452.348 2.827.957 4.042l3.007-2.332z" fill="#FBBC05"/>
                <path d="M9 3.58c1.321 0 2.508.454 3.44 1.345l2.582-2.58C13.463.891 11.426 0 9 0A8.997 8.997 0 0 0 .957 4.958L3.964 7.272C4.672 5.142 6.656 3.58 9 3.58z" fill="#EA4335"/>
              </svg>
              Sign in with Google
            </button>

            <button
              type="button"
              onClick={loginWithAuth0}
              className="btn btn-auth0"
            >
              <svg width="18" height="18" viewBox="0 0 18 18" fill="currentColor">
                <path d="M9 0C4.029 0 0 4.029 0 9s4.029 9 9 9 9-4.029 9-9-4.029-9-9-9zm0 16.2c-3.969 0-7.2-3.231-7.2-7.2S5.031 1.8 9 1.8s7.2 3.231 7.2 7.2-3.231 7.2-7.2 7.2zm.9-10.8h-1.8v5.4l4.5 2.7.9-1.44-3.6-2.16V5.4z"/>
              </svg>
              Sign in with Auth0
            </button>
          </>
        ) : (
          <>
            {message && (
              <div className="info-alert">
                {message}
                <button onClick={() => setMessage('')} className="alert-close">×</button>
              </div>
            )}

            <div className="otp-info">
              <p>A 6-digit verification code has been sent to:</p>
              <p className="email-display">{userEmail}</p>
            </div>

            <form onSubmit={handleOtpSubmit} className="login-form">
              <div className="form-group">
                <label htmlFor="otpCode">Verification Code</label>
                <input
                  id="otpCode"
                  type="text"
                  value={otpCode}
                  onChange={(e) => setOtpCode(e.target.value.replace(/\D/g, '').slice(0, 6))}
                  placeholder="123456"
                  maxLength={6}
                  required
                  autoFocus
                  pattern="\d{6}"
                  className="otp-input"
                />
                <p className="hint">Enter the 6-digit code from your email</p>
              </div>

              <div className="form-actions">
                <button type="button" onClick={handleBack} className="btn btn-secondary" disabled={loading}>
                  Back
                </button>
                <button type="submit" className="btn btn-primary" disabled={loading || otpCode.length !== 6}>
                  {loading ? 'Verifying...' : 'Verify'}
                </button>
              </div>
            </form>
          </>
        )}
      </div>
    </div>
  )
}
