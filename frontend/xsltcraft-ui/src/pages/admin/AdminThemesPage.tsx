import { useEffect, useRef, useState } from 'react'
import { Link } from 'react-router-dom'
import {
  getAdminThemes,
  createTheme,
  updateTheme,
  deleteTheme,
} from '../../services/adminService'
import type { FreeTheme } from '../../services/templateService'

const DOC_TYPE_LABEL: Record<string, string> = {
  Invoice: 'e-Fatura',
  Despatch: 'İrsaliye',
}

// ---- Yeni tema yükleme formu ----
function UploadForm({ onCreated }: { onCreated: (t: FreeTheme) => void }) {
  const [name, setName] = useState('')
  const [docType, setDocType] = useState('Invoice')
  const [file, setFile] = useState<File | null>(null)
  const [loading, setLoading] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const fileRef = useRef<HTMLInputElement>(null)

  const submit = async (e: React.FormEvent) => {
    e.preventDefault()
    if (!file) return
    setLoading(true)
    setError(null)
    try {
      const created = await createTheme({ name, documentType: docType, file })
      onCreated(created)
      setName('')
      setDocType('Invoice')
      setFile(null)
      if (fileRef.current) fileRef.current.value = ''
    } catch (err: unknown) {
      const msg =
        (err as { response?: { data?: { message?: string } } })?.response?.data
          ?.message ?? 'Yükleme başarısız.'
      setError(msg)
    } finally {
      setLoading(false)
    }
  }

  return (
    <form
      onSubmit={submit}
      className="border border-gray-200 rounded-xl p-6 bg-white shadow-sm mb-8"
    >
      <h2 className="text-base font-semibold text-gray-800 mb-4">Yeni Tema Yükle</h2>

      {error && (
        <p className="text-sm text-red-600 bg-red-50 border border-red-200 rounded-lg px-3 py-2 mb-4">
          {error}
        </p>
      )}

      <div className="grid grid-cols-1 sm:grid-cols-3 gap-4 mb-4">
        <div className="flex flex-col gap-1">
          <label className="text-xs font-medium text-gray-600">Tema Adı</label>
          <input
            required
            value={name}
            onChange={(e) => setName(e.target.value)}
            placeholder="UBL e-Fatura Teması"
            className="border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500"
          />
        </div>

        <div className="flex flex-col gap-1">
          <label className="text-xs font-medium text-gray-600">Döküman Tipi</label>
          <select
            value={docType}
            onChange={(e) => setDocType(e.target.value)}
            className="border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500 bg-white"
          >
            <option value="Invoice">e-Fatura</option>
            <option value="Despatch">İrsaliye</option>
          </select>
        </div>

        <div className="flex flex-col gap-1">
          <label className="text-xs font-medium text-gray-600">.xslt Dosyası</label>
          <input
            ref={fileRef}
            required
            type="file"
            accept=".xslt"
            onChange={(e) => setFile(e.target.files?.[0] ?? null)}
            className="border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500 file:mr-2 file:border-0 file:bg-blue-50 file:text-blue-700 file:rounded file:px-2 file:py-1 file:text-xs"
          />
        </div>
      </div>

      <button
        type="submit"
        disabled={loading || !file}
        className="px-4 py-2 bg-blue-600 hover:bg-blue-700 disabled:opacity-50 disabled:cursor-not-allowed text-white text-sm font-medium rounded-lg transition"
      >
        {loading ? 'Yükleniyor…' : 'Yükle'}
      </button>
    </form>
  )
}

// ---- Güncelle satır inline formu ----
function UpdateRow({
  theme,
  onUpdated,
}: {
  theme: FreeTheme
  onUpdated: (t: FreeTheme) => void
}) {
  const [name, setName] = useState(theme.name)
  const [docType, setDocType] = useState<string>(theme.documentType)
  const [file, setFile] = useState<File | null>(null)
  const [loading, setLoading] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const fileRef = useRef<HTMLInputElement>(null)

  const submit = async (e: React.FormEvent) => {
    e.preventDefault()
    setLoading(true)
    setError(null)
    try {
      const updated = await updateTheme(theme.id, {
        name,
        documentType: docType,
        file: file ?? undefined,
      })
      onUpdated(updated)
      setFile(null)
      if (fileRef.current) fileRef.current.value = ''
    } catch (err: unknown) {
      const msg =
        (err as { response?: { data?: { message?: string } } })?.response?.data
          ?.message ?? 'Güncelleme başarısız.'
      setError(msg)
    } finally {
      setLoading(false)
    }
  }

  return (
    <form onSubmit={submit} className="flex flex-col gap-2">
      {error && <p className="text-xs text-red-600">{error}</p>}
      <div className="flex gap-2 flex-wrap">
        <input
          value={name}
          onChange={(e) => setName(e.target.value)}
          className="border border-gray-300 rounded-lg px-2 py-1.5 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500 w-44"
        />
        <select
          value={docType}
          onChange={(e) => setDocType(e.target.value)}
          className="border border-gray-300 rounded-lg px-2 py-1.5 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500 bg-white"
        >
          <option value="Invoice">e-Fatura</option>
          <option value="Despatch">İrsaliye</option>
        </select>
        <input
          ref={fileRef}
          type="file"
          accept=".xslt"
          onChange={(e) => setFile(e.target.files?.[0] ?? null)}
          className="text-xs border border-gray-300 rounded-lg px-2 py-1.5"
        />
        <button
          type="submit"
          disabled={loading}
          className="px-3 py-1.5 bg-blue-600 hover:bg-blue-700 disabled:opacity-50 text-white text-xs font-medium rounded-lg transition"
        >
          {loading ? '…' : 'Kaydet'}
        </button>
      </div>
    </form>
  )
}

// ---- Silme onay modal'ı ----
function DeleteModal({
  theme,
  onConfirm,
  onCancel,
}: {
  theme: FreeTheme
  onConfirm: () => void
  onCancel: () => void
}) {
  return (
    <div className="fixed inset-0 bg-black/50 flex items-center justify-center z-50">
      <div className="bg-white rounded-xl shadow-xl p-6 max-w-sm w-full mx-4">
        <h3 className="text-base font-semibold text-gray-900 mb-2">Temayı Sil</h3>
        <p className="text-sm text-gray-600 mb-6">
          <span className="font-medium text-gray-900">{theme.name}</span> teması kalıcı olarak
          silinecek. Bu işlem geri alınamaz.
        </p>
        <div className="flex gap-3 justify-end">
          <button
            onClick={onCancel}
            className="px-4 py-2 text-sm text-gray-700 border border-gray-300 hover:bg-gray-50 rounded-lg transition"
          >
            İptal
          </button>
          <button
            onClick={onConfirm}
            className="px-4 py-2 text-sm text-white bg-red-600 hover:bg-red-700 rounded-lg transition"
          >
            Evet, Sil
          </button>
        </div>
      </div>
    </div>
  )
}

// ---- Ana sayfa ----
export default function AdminThemesPage() {
  const [themes, setThemes] = useState<FreeTheme[]>([])
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)
  const [editingId, setEditingId] = useState<string | null>(null)
  const [deletingTheme, setDeletingTheme] = useState<FreeTheme | null>(null)
  const [deleteLoading, setDeleteLoading] = useState(false)

  useEffect(() => {
    getAdminThemes()
      .then(setThemes)
      .catch(() => setError('Temalar yüklenemedi.'))
      .finally(() => setLoading(false))
  }, [])

  const handleCreated = (t: FreeTheme) => setThemes((prev) => [t, ...prev])

  const handleUpdated = (t: FreeTheme) =>
    setThemes((prev) => prev.map((x) => (x.id === t.id ? t : x)))

  const handleDeleteConfirm = async () => {
    if (!deletingTheme) return
    setDeleteLoading(true)
    try {
      await deleteTheme(deletingTheme.id)
      setThemes((prev) => prev.filter((x) => x.id !== deletingTheme.id))
      setDeletingTheme(null)
    } catch {
      // silme hatası — modal açık kalır
    } finally {
      setDeleteLoading(false)
    }
  }

  return (
    <div className="max-w-5xl mx-auto px-6 py-10">
      {/* Başlık */}
      <div className="flex items-center justify-between mb-6">
        <div>
          <h1 className="text-2xl font-semibold text-gray-900">Admin Paneli</h1>
          <p className="text-gray-500 text-sm mt-1">Ücretsiz XSLT temalarını yönetin.</p>
        </div>
        <Link
          to="/dashboard"
          className="text-sm text-gray-600 hover:text-gray-900 border border-gray-300 px-3 py-1.5 rounded-lg transition"
        >
          ← Dashboard
        </Link>
      </div>

      {/* Yükleme formu */}
      <UploadForm onCreated={handleCreated} />

      {/* Tema tablosu */}
      <h2 className="text-base font-semibold text-gray-800 mb-3">Mevcut Temalar</h2>

      {loading && <p className="text-sm text-gray-500">Yükleniyor…</p>}
      {error && <p className="text-sm text-red-600">{error}</p>}

      {!loading && !error && themes.length === 0 && (
        <p className="text-sm text-gray-400">Henüz tema yok.</p>
      )}

      {!loading && themes.length > 0 && (
        <div className="border border-gray-200 rounded-xl overflow-hidden bg-white shadow-sm">
          <table className="w-full text-sm">
            <thead className="bg-gray-50 border-b border-gray-200">
              <tr>
                <th className="text-left px-4 py-3 font-medium text-gray-600">Ad</th>
                <th className="text-left px-4 py-3 font-medium text-gray-600">Tip</th>
                <th className="text-left px-4 py-3 font-medium text-gray-600">Yükleme Tarihi</th>
                <th className="px-4 py-3" />
              </tr>
            </thead>
            <tbody className="divide-y divide-gray-100">
              {themes.map((theme) => (
                <tr key={theme.id}>
                  <td className="px-4 py-4 text-gray-900">
                    {editingId === theme.id ? (
                      <UpdateRow
                        theme={theme}
                        onUpdated={(t) => {
                          handleUpdated(t)
                          setEditingId(null)
                        }}
                      />
                    ) : (
                      theme.name
                    )}
                  </td>
                  <td className="px-4 py-4 text-gray-600">
                    {DOC_TYPE_LABEL[theme.documentType] ?? theme.documentType}
                  </td>
                  <td className="px-4 py-4 text-gray-500">
                    {new Date(theme.createdAt).toLocaleDateString('tr-TR')}
                  </td>
                  <td className="px-4 py-4">
                    {editingId === theme.id ? (
                      <button
                        onClick={() => setEditingId(null)}
                        className="text-xs text-gray-500 hover:text-gray-800 underline"
                      >
                        İptal
                      </button>
                    ) : (
                      <div className="flex gap-2 justify-end">
                        <button
                          onClick={() => setEditingId(theme.id)}
                          className="px-3 py-1.5 text-xs font-medium text-blue-700 border border-blue-200 hover:bg-blue-50 rounded-lg transition"
                        >
                          Güncelle
                        </button>
                        <button
                          onClick={() => setDeletingTheme(theme)}
                          className="px-3 py-1.5 text-xs font-medium text-red-700 border border-red-200 hover:bg-red-50 rounded-lg transition"
                        >
                          Sil
                        </button>
                      </div>
                    )}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}

      {/* Silme modal'ı */}
      {deletingTheme && (
        <DeleteModal
          theme={deletingTheme}
          onConfirm={deleteLoading ? () => {} : handleDeleteConfirm}
          onCancel={() => setDeletingTheme(null)}
        />
      )}
    </div>
  )
}
