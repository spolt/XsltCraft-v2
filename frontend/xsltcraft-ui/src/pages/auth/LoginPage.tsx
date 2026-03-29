import { useState } from 'react'
import { Link, useNavigate } from 'react-router-dom'
import { useGoogleLogin } from '@react-oauth/google'
import api from '../../services/apiService'
import { useAuthStore } from '../../store/authStore'
import AuthLayout from '../../components/layout/AuthLayout'

function GoogleIcon() {
  return (
    <svg width="18" height="18" viewBox="0 0 18 18" xmlns="http://www.w3.org/2000/svg">
      <path d="M17.64 9.2c0-.637-.057-1.251-.164-1.84H9v3.481h4.844a4.14 4.14 0 0 1-1.796 2.716v2.259h2.908c1.702-1.567 2.684-3.875 2.684-6.615z" fill="#4285F4" />
      <path d="M9 18c2.43 0 4.467-.806 5.956-2.184l-2.908-2.259c-.806.54-1.837.86-3.048.86-2.344 0-4.328-1.584-5.036-3.711H.957v2.332A8.997 8.997 0 0 0 9 18z" fill="#34A853" />
      <path d="M3.964 10.706A5.41 5.41 0 0 1 3.682 9c0-.593.102-1.17.282-1.706V4.962H.957A8.996 8.996 0 0 0 0 9c0 1.452.348 2.827.957 4.038l3.007-2.332z" fill="#FBBC05" />
      <path d="M9 3.58c1.321 0 2.508.454 3.44 1.345l2.582-2.58C13.463.891 11.426 0 9 0A8.997 8.997 0 0 0 .957 4.962L3.964 7.294C4.672 5.163 6.656 3.58 9 3.58z" fill="#EA4335" />
    </svg>
  )
}

export default function LoginPage() {
  const navigate = useNavigate()
  const login = useAuthStore((s) => s.login)

  const [email, setEmail] = useState('')
  const [password, setPassword] = useState('')
  const [error, setError] = useState<string | null>(null)
  const [loading, setLoading] = useState(false)

  const handleSubmit = async (e: { preventDefault(): void }) => {
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
    } catch (err: unknown) {
      const msg = (err as { response?: { data?: { message?: string } } })?.response?.data?.message
      setError(msg ?? 'Giriş başarısız.')
    } finally {
      setLoading(false)
    }
  }

  const loginWithGoogle = useGoogleLogin({
    flow: 'implicit',
    scope: 'openid email profile',
    onSuccess: async (tokenResponse) => {
      const idToken = (tokenResponse as typeof tokenResponse & { id_token?: string }).id_token
      if (!idToken) {
        setError('Google ile giriş başarısız.')
        return
      }
      setError(null)
      setLoading(true)
      try {
        const { data } = await api.post('/api/auth/google', { idToken })
        const me = await api.get('/api/auth/me', {
          headers: { Authorization: `Bearer ${data.accessToken}` },
        })
        login(data.accessToken, me.data)
        navigate('/dashboard')
      } catch (err: unknown) {
        const msg = (err as { response?: { data?: { message?: string } } })?.response?.data?.message
        setError(msg ?? 'Google ile giriş başarısız.')
      } finally {
        setLoading(false)
      }
    },
    onError: () => setError('Google ile giriş başarısız.'),
  })

  return (
    <AuthLayout theme="B">
      <h1 className="text-2xl font-bold text-white mb-2">Giriş Yap</h1>
      <p className="text-gray-400 text-sm mb-6">
        Hesabın yok mu?{' '}
        <Link to="/auth/register" className="text-indigo-400 hover:underline">
          Kayıt ol
        </Link>
      </p>

      {error && (
        <div className="mb-4 p-3 bg-red-900/40 border-l-4 border-red-500 text-red-300 rounded-lg text-sm">
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
            className="w-full px-4 py-2.5 bg-gray-800/80 border border-gray-700 rounded-lg text-white placeholder-gray-500 focus:outline-none focus:border-indigo-500 focus:ring-2 focus:ring-indigo-500/20 transition"
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
            className="w-full px-4 py-2.5 bg-gray-800/80 border border-gray-700 rounded-lg text-white placeholder-gray-500 focus:outline-none focus:border-indigo-500 focus:ring-2 focus:ring-indigo-500/20 transition"
            placeholder="••••••••"
          />
        </div>
        <button
          type="submit"
          disabled={loading}
          className="w-full py-2.5 bg-indigo-600 hover:bg-indigo-500 hover:shadow-[0_0_20px_rgba(99,102,241,0.4)] disabled:opacity-50 text-white font-semibold rounded-lg transition-all duration-200"
        >
          {loading ? 'Giriş yapılıyor…' : 'Giriş Yap'}
        </button>
      </form>

      <div className="relative my-6">
        <div className="absolute inset-0 flex items-center">
          <div className="w-full border-t border-gray-700/60" />
        </div>
        <div className="relative flex justify-center text-xs text-gray-500">
          <span className="px-2 bg-transparent">veya</span>
        </div>
      </div>

      <button
        type="button"
        onClick={() => loginWithGoogle()}
        disabled={loading}
        className="w-full py-2.5 flex items-center justify-center gap-3 bg-gray-800/80 hover:bg-gray-700/80 border border-gray-700 hover:border-gray-600 disabled:opacity-50 text-white font-semibold rounded-lg transition-all duration-200"
      >
        <GoogleIcon />
        Google ile oturum açın
      </button>
    </AuthLayout>
  )
}
