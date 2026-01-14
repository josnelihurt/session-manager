import { useAuth } from '../contexts/AuthContext'

export function ImpersonationBanner() {
  const { impersonation, endImpersonation } = useAuth()

  if (!impersonation?.isImpersonating) {
    return null
  }

  const handleEndImpersonation = async () => {
    if (confirm('Are you sure you want to end impersonation?')) {
      await endImpersonation()
    }
  }

  const formatTime = (dateString) => {
    const date = new Date(dateString)
    return date.toLocaleTimeString()
  }

  const getMinutesRemaining = () => {
    if (impersonation.remainingMinutes !== null) {
      return impersonation.remainingMinutes
    }
    if (impersonation.expiresAt) {
      const expires = new Date(impersonation.expiresAt)
      const now = new Date()
      return Math.max(0, Math.floor((expires - now) / 60000))
    }
    return 0
  }

  const minutesRemaining = getMinutesRemaining()
  const isExpiringSoon = minutesRemaining <= 5

  return (
    <div className={`impersonation-banner ${isExpiringSoon ? 'expiring-soon' : ''}`}>
      <div className="banner-icon">
        <svg xmlns="http://www.w3.org/2000/svg" width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
          <path d="M20 21v-2a4 4 0 0 0-4-4H8a4 4 0 0 0-4 4v2"></path>
          <circle cx="12" cy="7" r="4"></circle>
        </svg>
      </div>
      <div className="banner-content">
        <span className="banner-text">
          Viewing as <strong>{impersonation.targetUsername}</strong>
        </span>
        {minutesRemaining > 0 && (
          <span className={`banner-timer ${isExpiringSoon ? 'warning' : ''}`}>
            ({minutesRemaining} minute{minutesRemaining !== 1 ? 's' : ''} remaining)
          </span>
        )}
        {impersonation.originalUsername && (
          <span className="banner-original">
            (Original: {impersonation.originalUsername})
          </span>
        )}
      </div>
      <button
        className="banner-button"
        onClick={handleEndImpersonation}
        title="Return to your account"
      >
        End Impersonation
      </button>
    </div>
  )
}
