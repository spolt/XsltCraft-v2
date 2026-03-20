import { useEffect, useRef, useState } from 'react'
import { useParams, useNavigate, Link, useLocation } from 'react-router-dom'
import {
  LayoutDashboard,
  BookOpen,
  FilePlus,
  FolderOpen,
  Shield,
  Settings2,
  X,
  Printer,
  AlignLeft,
  AlignCenter,
  AlignRight,
  Code2,
} from 'lucide-react'
import { getTemplate } from '../services/templateService'
import { previewFromStoredXslt, type BankInfoItem, type Alignment, type ImageSettings } from '../services/previewService'
import { uploadAsset } from '../services/assetService'
import { useAuthStore } from '../store/authStore'

const DEBOUNCE_MS = 1200
const API_BASE = (import.meta.env.VITE_API_URL as string | undefined) ?? 'http://localhost:5000'

// ─── Sidebar ──────────────────────────────────────────────────────────────────
function EditorSidebar() {
  const { user } = useAuthStore()
  const location = useLocation()
  const isActive = (path: string) => location.pathname.startsWith(path)
  const base = 'flex items-center justify-center w-9 h-9 rounded-lg transition-colors'
  const active = 'bg-blue-50 text-blue-700'
  const inactive = 'text-gray-500 hover:bg-gray-100 hover:text-gray-900'
  return (
    <div className="w-12 border-r border-gray-200 bg-white py-2 flex flex-col gap-1 items-center flex-shrink-0">
      <Link to="/dashboard" className={`${base} ${isActive('/dashboard') ? active : inactive}`} title="Dashboard"><LayoutDashboard size={18} /></Link>
      <Link to="/templates" className={`${base} ${isActive('/templates') ? active : inactive}`} title="Tema Kütüphanesi"><BookOpen size={18} /></Link>
      <Link to="/editor/new" className={`${base} ${isActive('/editor/new') ? active : inactive}`} title="Yeni Şablon"><FilePlus size={18} /></Link>
      <Link to="/drafts" className={`${base} ${isActive('/drafts') ? active : inactive}`} title="Taslaklarım"><FolderOpen size={18} /></Link>
      {user?.role === 'Admin' && (
        <Link to="/admin/themes" className={`${base} ${isActive('/admin/themes') ? active : inactive}`} title="Admin Paneli"><Shield size={18} /></Link>
      )}
    </div>
  )
}

// ─── Image settings state ─────────────────────────────────────────────────────
interface ImgState {
  url: string
  width: string   // string so input is controllable; parsed to number on send
  height: string
  alignment: Alignment
}

const defaultImg = (): ImgState => ({ url: '', width: '250', height: '150', alignment: 'left' })

// ─── Alignment picker ─────────────────────────────────────────────────────────
function AlignPicker({ value, onChange }: { value: Alignment; onChange: (v: Alignment) => void }) {
  const opts: { v: Alignment; Icon: typeof AlignLeft }[] = [
    { v: 'left', Icon: AlignLeft },
    { v: 'center', Icon: AlignCenter },
    { v: 'right', Icon: AlignRight },
  ]
  return (
    <div className="flex gap-1">
      {opts.map(({ v, Icon }) => (
        <button
          key={v}
          onClick={() => onChange(v)}
          className={`flex items-center justify-center w-7 h-7 rounded border transition-colors ${
            value === v
              ? 'bg-blue-600 border-blue-600 text-white'
              : 'border-gray-200 text-gray-400 hover:border-blue-300 hover:text-blue-500'
          }`}
          title={v === 'left' ? 'Sola yasla' : v === 'center' ? 'Ortala' : 'Sağa yasla'}
        >
          <Icon size={13} />
        </button>
      ))}
    </div>
  )
}

// ─── Image upload + settings panel ───────────────────────────────────────────
function ImagePanel({
  label,
  state,
  uploading,
  onUpload,
  onChange,
}: {
  label: string
  state: ImgState
  uploading: boolean
  onUpload: (e: React.ChangeEvent<HTMLInputElement>) => void
  onChange: (patch: Partial<ImgState>) => void
}) {
  return (
    <section>
      <h3 className="text-xs font-semibold text-gray-500 uppercase tracking-wide mb-2">{label}</h3>

      {/* Upload area */}
      {state.url ? (
        <div className="flex flex-col gap-2 mb-3">
          <img src={state.url} alt={label} className="max-h-16 object-contain border border-gray-100 rounded p-1 bg-gray-50" />
          <label className="cursor-pointer text-xs text-blue-600 hover:underline">
            Değiştir
            <input type="file" accept="image/png,image/jpeg,image/svg+xml" className="hidden" onChange={onUpload} />
          </label>
        </div>
      ) : (
        <label className="cursor-pointer flex items-center gap-2 border border-dashed border-gray-300 rounded-lg px-3 py-3 hover:border-blue-300 hover:bg-blue-50 transition-colors mb-3">
          {uploading
            ? <span className="text-xs text-gray-400">Yükleniyor…</span>
            : <><span className="text-lg">🖼</span><span className="text-xs text-gray-500">Görsel yükle (PNG / JPG / SVG)</span></>
          }
          <input type="file" accept="image/png,image/jpeg,image/svg+xml" className="hidden" onChange={onUpload} disabled={uploading} />
        </label>
      )}

      {/* Size + alignment — always visible so user can set before upload too */}
      <div className="flex flex-col gap-2">
        {/* Width / Height */}
        <div className="flex gap-2">
          <div className="flex-1">
            <label className="text-xs text-gray-400 mb-0.5 block">Genişlik (px)</label>
            <input
              type="number"
              min={10}
              max={1200}
              placeholder="Otomatik"
              value={state.width}
              onChange={(e) => onChange({ width: e.target.value })}
              className="w-full text-xs border border-gray-200 rounded px-2 py-1.5 focus:outline-none focus:ring-1 focus:ring-blue-400"
            />
          </div>
          <div className="flex-1">
            <label className="text-xs text-gray-400 mb-0.5 block">Yükseklik (px)</label>
            <input
              type="number"
              min={10}
              max={1200}
              placeholder="Otomatik"
              value={state.height}
              onChange={(e) => onChange({ height: e.target.value })}
              className="w-full text-xs border border-gray-200 rounded px-2 py-1.5 focus:outline-none focus:ring-1 focus:ring-blue-400"
            />
          </div>
        </div>

        {/* Alignment */}
        <div>
          <label className="text-xs text-gray-400 mb-0.5 block">Hizalama</label>
          <AlignPicker value={state.alignment} onChange={(v) => onChange({ alignment: v })} />
        </div>
      </div>
    </section>
  )
}

// ─── Convert ImgState → ImageSettings for the service ────────────────────────
function toSettings(s: ImgState): ImageSettings | undefined {
  if (!s.url) return undefined
  return {
    url: s.url,
    width: s.width ? parseInt(s.width) : undefined,
    height: s.height ? parseInt(s.height) : undefined,
    alignment: s.alignment,
  }
}

// ─── Main page ─────────────────────────────────────────────────────────────────
export default function ThemeUsePage() {
  const { templateId } = useParams<{ templateId: string }>()
  const navigate = useNavigate()

  const [templateName, setTemplateName] = useState('Hazır Tema')
  const [xmlContent, setXmlContent] = useState<string | null>(null)
  const [editMode, setEditMode] = useState(false)

  const [logo, setLogo] = useState<ImgState>(defaultImg())
  const [signature, setSignature] = useState<ImgState>(defaultImg())
  const [bankInfo, setBankInfo] = useState<BankInfoItem[]>([{ bankName: '', iban: '' }])

  const [logoUploading, setLogoUploading] = useState(false)
  const [signatureUploading, setSignatureUploading] = useState(false)

  const [html, setHtml] = useState('')
  const [previewLoading, setPreviewLoading] = useState(false)
  const [previewError, setPreviewError] = useState<string | null>(null)
  const [lastMs, setLastMs] = useState<number | null>(null)
  const iframeRef = useRef<HTMLIFrameElement>(null)

  useEffect(() => {
    if (!templateId) return
    getTemplate(templateId).then((t) => setTemplateName(t.name)).catch(() => {})
  }, [templateId])

  // Debounced preview
  useEffect(() => {
    if (!xmlContent || !templateId) return
    const t = setTimeout(async () => {
      setPreviewLoading(true)
      setPreviewError(null)
      try {
        const result = await previewFromStoredXslt(
          templateId,
          xmlContent,
          toSettings(logo),
          toSettings(signature),
          bankInfo.filter((b) => b.bankName || b.iban),
        )
        setHtml(result.html)
        setLastMs(result.generationTimeMs)
      } catch (e: unknown) {
        const msg =
          e && typeof e === 'object' && 'response' in e
            ? (e as { response?: { data?: { error?: string } } }).response?.data?.error ?? 'Önizleme alınamadı.'
            : 'Önizleme alınamadı.'
        setPreviewError(msg)
      } finally {
        setPreviewLoading(false)
      }
    }, DEBOUNCE_MS)
    return () => clearTimeout(t)
  }, [xmlContent, logo, signature, bankInfo, templateId])

  function handleXmlFile(e: React.ChangeEvent<HTMLInputElement>) {
    const file = e.target.files?.[0]
    if (!file) return
    const reader = new FileReader()
    reader.onload = (ev) => setXmlContent(ev.target?.result as string)
    reader.readAsText(file, 'utf-8')
    e.target.value = ''
  }

  async function handleLogoUpload(e: React.ChangeEvent<HTMLInputElement>) {
    const file = e.target.files?.[0]
    if (!file) return
    setLogoUploading(true)
    try {
      const asset = await uploadAsset(file, 'Logo')
      setLogo((prev) => ({ ...prev, url: `${API_BASE}${asset.url}` }))
    } catch { alert('Logo yüklenemedi.') }
    finally { setLogoUploading(false); e.target.value = '' }
  }

  async function handleSignatureUpload(e: React.ChangeEvent<HTMLInputElement>) {
    const file = e.target.files?.[0]
    if (!file) return
    setSignatureUploading(true)
    try {
      const asset = await uploadAsset(file, 'Signature')
      setSignature((prev) => ({ ...prev, url: `${API_BASE}${asset.url}` }))
    } catch { alert('İmza yüklenemedi.') }
    finally { setSignatureUploading(false); e.target.value = '' }
  }

  function addBankRow() { setBankInfo((p) => [...p, { bankName: '', iban: '' }]) }
  function removeBankRow(idx: number) { setBankInfo((p) => p.filter((_, i) => i !== idx)) }
  function updateBankRow(idx: number, field: keyof BankInfoItem, value: string) {
    setBankInfo((p) => p.map((r, i) => (i === idx ? { ...r, [field]: value } : r)))
  }

  // ─── Phase 1: XML upload ──────────────────────────────────────────────────────
  if (!xmlContent) {
    return (
      <div className="h-screen flex overflow-hidden bg-gray-50">
        <EditorSidebar />
        <div className="flex-1 flex flex-col items-center justify-center gap-6 px-4">
          <div className="text-center">
            <p className="text-xs text-gray-400 mb-1">
              <button onClick={() => navigate('/templates')} className="hover:text-blue-600 transition-colors">← Tema Kütüphanesi</button>
            </p>
            <h1 className="text-xl font-semibold text-gray-800 mb-1">{templateName}</h1>
            <p className="text-sm text-gray-500">Önizleme için bir XML dosyası yükleyin.</p>
          </div>
          <label className="cursor-pointer flex flex-col items-center gap-3 border-2 border-dashed border-blue-300 rounded-xl px-14 py-10 bg-white hover:border-blue-400 hover:bg-blue-50 transition-colors">
            <svg className="w-10 h-10 text-blue-400" fill="none" viewBox="0 0 24 24" stroke="currentColor">
              <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={1.5} d="M3 16.5v2.25A2.25 2.25 0 005.25 21h13.5A2.25 2.25 0 0021 18.75V16.5m-13.5-9L12 3m0 0l4.5 4.5M12 3v13.5" />
            </svg>
            <span className="text-sm font-medium text-blue-600">XML Dosyası Yükle</span>
            <span className="text-xs text-gray-400">.xml — fatura veya irsaliye</span>
            <input type="file" accept=".xml,text/xml,application/xml" className="hidden" onChange={handleXmlFile} />
          </label>
        </div>
      </div>
    )
  }

  // ─── Phase 2 / 3 ─────────────────────────────────────────────────────────────
  return (
    <div className="h-screen flex overflow-hidden bg-gray-50">
      <EditorSidebar />

      <div className="flex-1 flex flex-col overflow-hidden">
        {/* Toolbar */}
        <div className="h-10 border-b border-gray-200 bg-white flex items-center gap-2 px-4 flex-shrink-0">
          <span className="text-sm font-medium text-gray-700 flex-1 truncate">{templateName}</span>
          {previewLoading && (
            <span className="text-xs text-blue-500 flex items-center gap-1">
              <span className="inline-block w-3 h-3 border-2 border-blue-400 border-t-transparent rounded-full animate-spin" />
              Yükleniyor
            </span>
          )}
          {!previewLoading && lastMs !== null && <span className="text-xs text-gray-400">{lastMs} ms</span>}
          <label className="cursor-pointer text-xs text-gray-500 border border-gray-200 rounded px-2 py-1 hover:bg-gray-50">
            XML Değiştir
            <input type="file" accept=".xml,text/xml,application/xml" className="hidden" onChange={handleXmlFile} />
          </label>
          <button onClick={() => iframeRef.current?.contentWindow?.print()} disabled={!html}
            className="text-gray-500 border border-gray-200 rounded p-1 hover:bg-gray-50 disabled:opacity-30" title="Yazdır">
            <Printer size={14} />
          </button>
          <button
            onClick={() => navigate(`/dev-mode/${templateId}`)}
            className="flex items-center gap-1.5 text-xs text-gray-600 border border-gray-200 rounded px-3 py-1 hover:bg-gray-50"
            title="XSLT editörünü aç"
          >
            <Code2 size={13} />Geliştirici Modu
          </button>
          {!editMode
            ? <button onClick={() => setEditMode(true)} className="flex items-center gap-1.5 text-xs font-medium text-white bg-blue-600 hover:bg-blue-700 rounded px-3 py-1">
                <Settings2 size={13} />Düzenle
              </button>
            : <button onClick={() => setEditMode(false)} className="flex items-center gap-1.5 text-xs text-gray-600 border border-gray-200 rounded px-3 py-1 hover:bg-gray-50">
                <X size={13} />Kapat
              </button>
          }
        </div>

        {previewError && (
          <div className="px-4 py-2 text-xs text-red-600 bg-red-50 border-b border-red-100 flex-shrink-0">{previewError}</div>
        )}

        <div className="flex-1 flex overflow-hidden">
          {/* Preview */}
          <div className="flex-1 overflow-auto bg-gray-300 flex justify-center">
            <iframe
              ref={iframeRef}
              srcDoc={html || '<html><body style="background:#d1d5db;margin:0"></body></html>'}
              sandbox="allow-same-origin allow-scripts allow-modals"
              className="border-none bg-white shadow-lg"
              style={{ width: 860, minHeight: '100%', height: 'max(100%, 1200px)' }}
              title="Canlı Önizleme"
            />
          </div>

          {/* Right panel */}
          {editMode && (
            <div className="w-76 flex-shrink-0 border-l border-gray-200 bg-white flex flex-col overflow-y-auto" style={{ width: 300 }}>
              <div className="p-4 flex flex-col gap-6">

                <ImagePanel
                  label="Logo"
                  state={logo}
                  uploading={logoUploading}
                  onUpload={handleLogoUpload}
                  onChange={(patch) => setLogo((p) => ({ ...p, ...patch }))}
                />

                <div className="border-t border-gray-100" />

                <ImagePanel
                  label="İmza / Kaşe"
                  state={signature}
                  uploading={signatureUploading}
                  onUpload={handleSignatureUpload}
                  onChange={(patch) => setSignature((p) => ({ ...p, ...patch }))}
                />

                <div className="border-t border-gray-100" />

                {/* IBAN */}
                <section>
                  <h3 className="text-xs font-semibold text-gray-500 uppercase tracking-wide mb-2">Banka / IBAN</h3>
                  <div className="flex flex-col gap-3">
                    {bankInfo.map((row, idx) => (
                      <div key={idx} className="relative bg-gray-50 rounded-lg p-3 border border-gray-100">
                        {bankInfo.length > 1 && (
                          <button onClick={() => removeBankRow(idx)} className="absolute top-2 right-2 text-gray-300 hover:text-red-400" title="Kaldır">
                            <X size={12} />
                          </button>
                        )}
                        <div className="flex flex-col gap-1.5">
                          <input type="text" placeholder="Banka adı" value={row.bankName}
                            onChange={(e) => updateBankRow(idx, 'bankName', e.target.value)}
                            className="text-xs border border-gray-200 rounded px-2 py-1.5 focus:outline-none focus:ring-1 focus:ring-blue-400 bg-white w-full" />
                          <input type="text" placeholder="TR00 0000 0000 0000 0000 0000 00" value={row.iban}
                            onChange={(e) => updateBankRow(idx, 'iban', e.target.value)}
                            className="text-xs border border-gray-200 rounded px-2 py-1.5 focus:outline-none focus:ring-1 focus:ring-blue-400 bg-white w-full font-mono tracking-wide" />
                        </div>
                      </div>
                    ))}
                    <button onClick={addBankRow} className="text-xs text-blue-600 hover:text-blue-700 text-left flex items-center gap-1">
                      + Banka hesabı ekle
                    </button>
                  </div>
                </section>

              </div>
            </div>
          )}
        </div>
      </div>
    </div>
  )
}
