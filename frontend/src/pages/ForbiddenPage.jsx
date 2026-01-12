import { useState } from 'react'
import { forbiddenMessages } from '../data/403Messages'
import './ForbiddenPage.css'

export function ForbiddenPage() {
  const [message] = useState(() => {
    const randomIndex = Math.floor(Math.random() * forbiddenMessages.length)
    return forbiddenMessages[randomIndex]
  })

  return (
    <div className="forbidden-page">
      <div className="forbidden-content">
        <div className="wizard-container">
          <img
            src="/403.png"
            alt="Wizard"
            className="wizard-image"
          />
        </div>

        <div className="forbidden-text">
          <h1 className="forbidden-title">403 - Forbidden Access</h1>

          <p className="forbidden-message">
            {message.message}
          </p>

          <p className="forbidden-hint">
            💡 {message.hint}
          </p>

          <div className="forbidden-info">
            <p className="forbidden-status">
              <strong>Status:</strong> The royal guards have denied thee entry.
            </p>
            <p className="forbidden-reason">
              <strong>Reason:</strong> Thou lackest the required permissions to access this domain.
            </p>
          </div>

          <div className="forbidden-actions">
            <a href="https://josnelihurt.me" className="btn-return">
              ← Return to josnelihurt.me
            </a>
            <button
              onClick={() => window.history.back()}
              className="btn-back"
            >
              Go Back
            </button>
          </div>
        </div>
      </div>

      <div className="forbidden-footer">
        <p>
          ⚔️ If thou believe this is an error, contact the realm's administrator. ⚔️
        </p>
      </div>
    </div>
  )
}
