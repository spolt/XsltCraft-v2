import { useState } from 'react'
import { Link, useNavigate } from 'react-router-dom'
import api from '../../services/apiService'
import AuthLayout from '../../components/layout/AuthLayout'

const PASSWORD_RULES = /^(?=.*[A-Z])(?=.*\d).{8,}$/

export default function RegisterPage() {
  const navigate = useNavigate()

  const [displayName, setDisplayName] = useState('')
  const [email, setEmail] = useState('')
  const [password, setPassword] = useState('')
  const [error, setError] = useState<string | null>(null)
  const [loading, setLoading] = useState(false)

  const validate = (): string | null => {
    if (!PASSWORD_RULES.test(password))
      return 'Şifre en az 8 karakter, 1 büyük harf ve 1 rakam içermelidir.'
    return null
  }

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault()
    const validationError = validate()
    if (validationError) {
      setError(validationError)
      return
    }
    setError(null)
    setLoading(true)
    try {
      await api.post('/api/auth/register', { email, password, displayName })
      navigate('/auth/login', { state: { registered: true } })
    } catch (err: unknown) {
      const msg = (err as { response?: { data?: { message?: string } } })?.response?.data?.message
      setError(msg ?? 'Kayıt başarısız.')
    } finally {
      setLoading(false)
    }
  }

  return (
    <AuthLayout theme="B">
      <h1 className="text-2xl font-bold text-white mb-2">Hesap Oluştur</h1>
      <p className="text-gray-400 text-sm mb-6">
        Zaten hesabın var mı?{' '}
        <Link to="/auth/login" className="text-indigo-400 hover:underline">
          Giriş yap
        </Link>
      </p>

      {error && (
        <div className="mb-4 p-3 bg-red-900/40 border-l-4 border-red-500 text-red-300 rounded-lg text-sm">
          {error}
        </div>
      )}

      <form onSubmit={handleSubmit} className="space-y-4">
        <div>
          <label className="block text-sm text-gray-300 mb-1">Ad Soyad</label>
          <input
            type="text"
            value={displayName}
            onChange={(e) => setDisplayName(e.target.value)}
            className="w-full px-4 py-2.5 bg-gray-800/80 border border-gray-700 rounded-lg text-white placeholder-gray-500 focus:outline-none focus:border-indigo-500 focus:ring-2 focus:ring-indigo-500/20 transition"
            placeholder="Adınız"
          />
        </div>
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
          <p className="mt-1 text-xs text-gray-500">
            En az 8 karakter, 1 büyük harf, 1 rakam
          </p>
        </div>
        <button
          type="submit"
          disabled={loading}
          className="w-full py-2.5 bg-indigo-600 hover:bg-indigo-500 hover:shadow-[0_0_20px_rgba(99,102,241,0.4)] disabled:opacity-50 text-white font-semibold rounded-lg transition-all duration-200"
        >
          {loading ? 'Kayıt yapılıyor…' : 'Kayıt Ol'}
        </button>
      </form>
    </AuthLayout>
  )
}
