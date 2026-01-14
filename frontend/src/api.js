import axios from 'axios'
import { config } from './config'

const apiClient = axios.create({
  baseURL: config.api.baseUrl,
  withCredentials: true,
  headers: {
    'Content-Type': 'application/json',
  },
})

// Add response interceptor to handle 403 errors
apiClient.interceptors.response.use(
  (response) => response,
  (error) => {
    if (error.response?.status === 403) {
      window.location.href = '/forbidden'
      return Promise.reject(error)
    }
    return Promise.reject(error)
  }
)

export const api = apiClient

export const getSessions = async () => {
  const response = await apiClient.get('/sessions')
  return response.data
}

export const deleteSession = async (fullKey) => {
  const encodedKey = encodeURIComponent(fullKey)
  const response = await apiClient.delete(`/sessions/${encodedKey}`)
  return response.data
}

export const deleteAllSessions = async () => {
  const response = await apiClient.delete('/sessions')
  return response.data
}

export const getProviders = async () => {
  const response = await apiClient.get('/auth/providers')
  return response.data
}

export const login = async (username, password) => {
  const response = await apiClient.post('/auth/login', { username, password })
  return response.data
}

export const register = async (token, provider, username, password) => {
  const response = await apiClient.post('/auth/register', { token, provider, username, password })
  return response.data
}

export const logout = async () => {
  const response = await apiClient.post('/auth/logout')
  return response.data
}

export const getCurrentUser = async () => {
  const response = await apiClient.get('/auth/me')
  return response.data
}

// ==================== USERS API ====================

export const getAllUsers = async () => {
  const response = await apiClient.get('/users')
  return response.data
}

export const getUserById = async (id) => {
  const response = await apiClient.get(`/users/${id}`)
  return response.data
}

export const assignUserRoles = async (userId, roleIds) => {
  const response = await apiClient.put(`/users/${userId}/roles`, { roleIds })
  return response.data
}

export const removeUserRole = async (userId, roleId) => {
  const response = await apiClient.delete(`/users/${userId}/roles/${roleId}`)
  return response.data
}

export const setUserActive = async (userId, isActive) => {
  const response = await apiClient.put(`/users/${userId}/active`, isActive)
  return response.data
}

export const deleteUser = async (userId) => {
  const response = await apiClient.delete(`/users/${userId}`)
  return response.data
}

// ==================== APPLICATIONS API ====================

export const getAllApplications = async () => {
  const response = await apiClient.get('/applications/all')
  return response.data
}

export const getMyApplications = async () => {
  const response = await apiClient.get('/applications')
  return response.data
}

export const createApplication = async (url, name, description) => {
  const response = await apiClient.post('/applications', { url, name, description })
  return response.data
}

export const updateApplication = async (id, url, name, description) => {
  const response = await apiClient.put(`/applications/${id}`, { url, name, description })
  return response.data
}

export const deleteApplication = async (id) => {
  const response = await apiClient.delete(`/applications/${id}`)
  return response.data
}

export const createApplicationRole = async (applicationId, name, permissions) => {
  const response = await apiClient.post(`/applications/${applicationId}/roles`, { name, permissions })
  return response.data
}

export const deleteApplicationRole = async (roleId) => {
  const response = await apiClient.delete(`/applications/roles/${roleId}`)
  return response.data
}

// ==================== INVITATIONS API ====================

export const getAllInvitations = async () => {
  const response = await apiClient.get('/invitations')
  return response.data
}

export const createInvitation = async (email, provider, preAssignedRoleIds, sendEmail = false) => {
  const response = await apiClient.post('/invitations', { email, provider, preAssignedRoleIds, sendEmail })
  return response.data
}

export const deleteInvitation = async (id) => {
  const response = await apiClient.delete(`/invitations/${id}`)
  return response.data
}

export const validateInvitationToken = async (token) => {
  const response = await apiClient.get(`/invitations/validate/${token}`)
  return response.data
}

export const resendInvitationEmail = async (id) => {
  const response = await apiClient.post(`/invitations/${id}/resend-email`)
  return response.data
}

// ==================== IMPERSONATION API ====================

export const getImpersonationStatus = async () => {
  const response = await apiClient.get('/impersonate/status')
  return response.data
}

export const startImpersonation = async (userId, reason, durationMinutes = 30) => {
  const response = await apiClient.post(`/impersonate/${userId}`, { reason, durationMinutes })
  return response.data
}

export const endImpersonation = async () => {
  const response = await apiClient.delete('/impersonate')
  return response.data
}

export const getActiveImpersonationSessions = async () => {
  const response = await apiClient.get('/impersonate/sessions')
  return response.data
}

export const forceEndImpersonationSession = async (sessionId) => {
  const response = await apiClient.delete(`/impersonate/sessions/${sessionId}`)
  return response.data
}
