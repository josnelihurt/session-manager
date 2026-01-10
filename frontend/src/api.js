import axios from 'axios'
import { config } from './config'

const apiClient = axios.create({
  baseURL: config.api.baseUrl,
})

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
