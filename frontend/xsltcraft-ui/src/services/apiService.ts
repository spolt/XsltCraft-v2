import axios from 'axios'
import { useAuthStore } from '../store/authStore'
import { refreshAccessToken } from './authSession'

const api = axios.create({
  baseURL: import.meta.env.VITE_API_URL ?? 'http://localhost:5000',
  withCredentials: true,
})

// Kimlik doğrulama uçlarında 401 "oturum yok/yanlış şifre" demektir; yenileme denenmez.
const NO_REFRESH_PATHS = ['/api/auth/login', '/api/auth/google', '/api/auth/refresh', '/api/auth/logout']

// Attach Bearer token to every request
api.interceptors.request.use((config) => {
  const token = useAuthStore.getState().accessToken
  if (token) {
    config.headers.Authorization = `Bearer ${token}`
  }
  return config
})

// On 401, try to refresh once then retry
api.interceptors.response.use(
  (response) => response,
  async (error) => {
    const original = error.config

    if (
      error.response?.status !== 401 ||
      !original ||
      original._retry ||
      NO_REFRESH_PATHS.some((p) => original.url?.includes(p))
    ) {
      return Promise.reject(error)
    }

    original._retry = true
    try {
      const token = await refreshAccessToken()
      original.headers.Authorization = `Bearer ${token}`
      return api(original)
    } catch {
      return Promise.reject(error)
    }
  }
)

export default api
