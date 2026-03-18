import { useState } from 'react'
import { Link, useNavigate } from 'react-router-dom'
import { GoogleLogin } from '@react-oauth/google'
import api from '../../services/apiService'
import { useAuthStore } from '../../store/authStore'

export default function LoginPage() {
  const navigate = useNavigate()
  const login = useAuthStore((s) => s.login)

  const [email, setEmail] = useState('')
  const [password, setPassword] = useState('')
  const [error, setError] = useState<string | null>(null)
  const [loading, setLoading] = useState(false)

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault()
    setError(null)
    setLoading(true)
    try {
      const { data } = await api.post('/api/auth/login', { email, password })
      const me = await api.get('/api/auth/me', {
        headers: { Authorization: `Bearer ${data.accessToken}` },
      })
      login(data.accessToken, me.data)
      navigate('/dashboard')
    } catch (err: any) {
      setError(err.response?.data?.message ?? 'Giriş başarısız.')
    } finally {
      setLoading(false)
    }
  }

  const handleGoogleSuccess = async (credentialResponse: { credential?: string }) => {
    if (!credentialResponse.credential) return
    setError(null)
    setLoading(true)
    try {
      const { data } = await api.post('/api/auth/google', {
        idToken: credentialResponse.credential,
      })
      const me = await api.get('/api/auth/me', {
        headers: { Authorization: `Bearer ${data.accessToken}` },
      })
      login(data.accessToken, me.data)
      navigate('/dashboard')
    } catch {
      setError('Google ile giriş başarısız.')
    } finally {
      setLoading(false)
    }
  }

  return (
    <div className="min-h-screen flex items-center justify-center bg-gray-950">
      <div className="w-full max-w-md bg-gray-900 rounded-2xl shadow-xl p-8">
        <h1 className="text-2xl font-bold text-white mb-2">Giriş Yap</h1>
        <p className="text-gray-400 text-sm mb-6">
          Hesabın yok mu?{' '}
          <Link to="/auth/register" className="text-indigo-400 hover:underline">
            Kayıt ol
          </Link>
        </p>

        {error && (
          <div className="mb-4 p-3 bg-red-900/40 border border-red-700 text-red-300 rounded-lg text-sm">
            {error}
          </div>
        )}

        <form onSubmit={handleSubmit} className="space-y-4">
          <div>
            <label className="block text-sm text-gray-300 mb-1">E-posta</label>
            <input
              type="email"
              required
              value={email}
              onChange={(e) => setEmail(e.target.value)}
              className="w-full px-4 py-2.5 bg-gray-800 border border-gray-700 rounded-lg text-white placeholder-gray-500 focus:outline-none focus:border-indigo-500"
              placeholder="ornek@mail.com"
            />
          </div>
          <div>
            <label className="block text-sm text-gray-300 mb-1">Şifre</label>
            <input
              type="password"
              required
              value={password}
              onChange={(e) => setPassword(e.target.value)}
              className="w-full px-4 py-2.5 bg-gray-800 border border-gray-700 rounded-lg text-white placeholder-gray-500 focus:outline-none focus:border-indigo-500"
              placeholder="••••••••"
            />
          </div>
          <button
            type="submit"
            disabled={loading}
            className="w-full py-2.5 bg-indigo-600 hover:bg-indigo-500 disabled:opacity-50 text-white font-semibold rounded-lg transition"
          >
            {loading ? 'Giriş yapılıyor…' : 'Giriş Yap'}
          </button>
        </form>

        <div className="relative my-6">
          <div className="absolute inset-0 flex items-center">
            <div className="w-full border-t border-gray-700" />
          </div>
          <div className="relative flex justify-center text-xs text-gray-500">
            <span className="px-2 bg-gray-900">veya</span>
          </div>
        </div>

        <div className="flex justify-center">
          <GoogleLogin
            onSuccess={handleGoogleSuccess}
            onError={() => setError('Google ile giriş başarısız.')}
            theme="filled_black"
            shape="rectangular"
            width="368"
            text="signin_with"
          />
        </div>
      </div>
    </div>
  )
}
