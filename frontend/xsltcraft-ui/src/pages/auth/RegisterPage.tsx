import { useState } from 'react'
import { Link, useNavigate } from 'react-router-dom'
import api from '../../services/apiService'
import AuthLayout from '../../components/layout/AuthLayout'

const PASSWORD_RULES = /^(?=.*[A-Z])(?=.*\d).{8,}$/

export default function RegisterPage() {
  const navigate = useNavigate()

  const [username, setUsername] = useState('')
  const [displayName, setDisplayName] = useState('')
  const [email, setEmail] = useState('')
  const [password, setPassword] = useState('')
  const [error, setError] = useState<string | null>(null)
  const [loading, setLoading] = useState(false)

  const USERNAME_REGEX = /^[a-zA-Z0-9_][a-zA-Z0-9_.]{1,28}[a-zA-Z0-9_]$/

  const validate = (): string | null => {
    if (!USERNAME_REGEX.test(username))
      return 'Kullanıcı adı 3-30 karakter olmalı, yalnızca harf, rakam, alt çizgi ve nokta içerebilir; başında veya sonunda nokta olamaz.'
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
      await api.post('/api/auth/register', { username, email, password, displayName })
      navigate('/auth/login', { state: { registered: true } })
    } catch (err: unknown) {
      const msg = (err as { response?: { data?: { message?: string } } })?.response?.data?.message
      setError(msg ?? 'Kayıt başarısız.')
    } finally {
      setLoading(false)
    }
  }

  return (
    <AuthLayout>
      <h1 className="xc-auth-title">Hesap Oluştur</h1>
      <p className="xc-auth-sub mb-7">
        Zaten hesabın var mı?{' '}
        <Link to="/auth/login" className="xc-auth-link">
          Giriş yap
        </Link>
      </p>

      {error && (
        <div id="register-error" role="alert" className="xc-auth-error">
          {error}
        </div>
      )}

      <form onSubmit={handleSubmit} className="space-y-5">
        <div>
          <label htmlFor="register-username" className="xc-auth-label">Kullanıcı Adı</label>
          <input
            id="register-username"
            type="text"
            required
            autoComplete="username"
            value={username}
            onChange={(e) => setUsername(e.target.value)}
            aria-describedby={error ? 'register-error register-username-hint' : 'register-username-hint'}
            className="xc-auth-input"
            placeholder="kullanici_adi"
          />
          <p id="register-username-hint" className="xc-auth-hint">3-30 karakter, harf/rakam/alt çizgi/nokta</p>
        </div>
        <div>
          <label htmlFor="register-display-name" className="xc-auth-label">Ad Soyad</label>
          <input
            id="register-display-name"
            type="text"
            value={displayName}
            onChange={(e) => setDisplayName(e.target.value)}
            aria-describedby={error ? 'register-error' : undefined}
            className="xc-auth-input"
            placeholder="Adınız"
          />
        </div>
        <div>
          <label htmlFor="register-email" className="xc-auth-label">E-posta</label>
          <input
            id="register-email"
            type="email"
            required
            value={email}
            onChange={(e) => setEmail(e.target.value)}
            aria-describedby={error ? 'register-error' : undefined}
            className="xc-auth-input"
            placeholder="ornek@mail.com"
          />
        </div>
        <div>
          <label htmlFor="register-password" className="xc-auth-label">Şifre</label>
          <input
            id="register-password"
            type="password"
            required
            value={password}
            onChange={(e) => setPassword(e.target.value)}
            aria-describedby={error ? 'register-error register-password-hint' : 'register-password-hint'}
            className="xc-auth-input"
            placeholder="••••••••"
          />
          <p id="register-password-hint" className="xc-auth-hint">En az 8 karakter, 1 büyük harf, 1 rakam</p>
        </div>
        <button type="submit" disabled={loading} className="xc-auth-btn xc-auth-btn--primary mt-7">
          {loading ? 'Kayıt yapılıyor…' : 'Kayıt Ol'}
        </button>
      </form>
    </AuthLayout>
  )
}
