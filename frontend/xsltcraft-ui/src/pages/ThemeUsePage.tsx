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
  Download,
  BookmarkCheck,
} from 'lucide-react'
import { getTemplate } from '../services/templateService'
import { previewFromStoredXslt, fetchThemeXslt, type BankInfoItem, type Alignment, type ImageSettings } from '../services/previewService'
import { createUserXsltTemplate } from '../services/userXsltService'
import { useAuthStore } from '../store/authStore'
import defaultInvoiceXml from '../assets/default-invoice.xml?raw'

const DEBOUNCE_MS = 1200

function readAsDataUrl(file: File): Promise<string> {
  return new Promise((resolve, reject) => {
    const reader = new FileReader()
    reader.onload = (e) => resolve(e.target!.result as string)
    reader.onerror = reject
    reader.readAsDataURL(file)
  })
}

// ─── XSLT injection helpers (DevModePage ile aynı mantık) ────────────────────
const OVERRIDE_START = '<!-- DEV-MODE-OVERRIDES:START -->'
const OVERRIDE_END   = '<!-- DEV-MODE-OVERRIDES:END -->'

function escapeXml(s: string): string {
  return s.replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;')
}

function buildInjection(logo: ImgState, signature: ImgState, bankInfo: BankInfoItem[]): string {
  const parts: string[] = []
  const hasLogo = !!logo.url
  const hasSig  = !!signature.url

  if (hasLogo || hasSig) {
    const parseNum = (s: string) => { const n = parseInt(s); return isNaN(n) ? null : n }
    const lines = [
      `<script>(function(){`,
      `  function jc(a){return a==='center'?'center':a==='right'?'flex-end':'flex-start';}`,
      `  function setImg(ids,url,w,h,align){`,
      `    ids.forEach(function(id){`,
      `      var el=document.getElementById(id);if(!el)return;`,
      `      el.style.cssText='display:flex;justify-content:'+jc(align)+';align-items:center;';`,
      `      var img=document.createElement('img');`,
      `      img.src=url;`,
      `      img.style.cssText=(w?'width:'+w+'px;':'width:auto;')+(h?'height:'+h+'px;':'height:auto;')+'max-width:100%;object-fit:contain;display:block;';`,
      `      el.innerHTML='';el.appendChild(img);`,
      `    });`,
      `  }`,
      `  window.addEventListener('load',function(){`,
    ]
    if (hasLogo) {
      const w = parseNum(logo.width)
      const h = parseNum(logo.height)
      const safeUrl = logo.url.replace(/&/g, '&amp;').replace(/'/g, '%27')
      lines.push(`    setImg(['company_logo','companyLogo'],'${safeUrl}',${w ?? 'null'},${h ?? 'null'},'${logo.alignment}');`)
    }
    if (hasSig) {
      const w = parseNum(signature.width)
      const h = parseNum(signature.height)
      const safeUrl = signature.url.replace(/&/g, '&amp;').replace(/'/g, '%27')
      lines.push(`    setImg(['imza','companyKase'],'${safeUrl}',${w ?? 'null'},${h ?? 'null'},'${signature.alignment}');`)
    }
    lines.push(`  });`, `})();</script>`)
    parts.push(lines.join('\n'))
  }

  const validBanks = bankInfo.filter(b => b.bankName || b.iban)
  if (validBanks.length > 0) {
    const rows = validBanks
      .map(b =>
        `      <tr>` +
        `<td style="padding:6px 10px;border:1px solid #ddd">${escapeXml(b.bankName)}</td>` +
        `<td style="padding:6px 10px;border:1px solid #ddd;font-family:monospace">${escapeXml(b.iban)}</td>` +
        `</tr>`,
      )
      .join('\n')
    parts.push(
      `<div style="font-family:Arial,sans-serif;font-size:12px;margin:16px 0;page-break-inside:avoid">\n` +
      `  <table style="width:100%;border-collapse:collapse">\n` +
      `    <thead><tr>\n` +
      `      <th style="text-align:left;padding:6px 10px;background:#f5f5f5;border:1px solid #ddd">Banka Adı</th>\n` +
      `      <th style="text-align:left;padding:6px 10px;background:#f5f5f5;border:1px solid #ddd">IBAN</th>\n` +
      `    </tr></thead>\n` +
      `    <tbody>\n${rows}\n    </tbody>\n` +
      `  </table>\n</div>`,
    )
  }

  if (parts.length === 0) return ''
  return `${OVERRIDE_START}\n${parts.join('\n')}\n${OVERRIDE_END}`
}

function applyInjection(xslt: string, injection: string): string {
  const si = xslt.indexOf(OVERRIDE_START)
  const ei = xslt.indexOf(OVERRIDE_END)
  if (si >= 0 && ei >= 0) {
    if (!injection) return xslt.slice(0, si).trimEnd() + '\n' + xslt.slice(ei + OVERRIDE_END.length).trimStart()
    return xslt.slice(0, si) + injection + xslt.slice(ei + OVERRIDE_END.length)
  }
  if (!injection) return xslt
  const bodyClose = xslt.lastIndexOf('</body>')
  if (bodyClose >= 0) return xslt.slice(0, bodyClose) + injection + '\n' + xslt.slice(bodyClose)
  const tmpl = xslt.lastIndexOf('</xsl:template>')
  if (tmpl >= 0) return xslt.slice(0, tmpl) + injection + '\n' + xslt.slice(tmpl)
  return xslt + '\n' + injection
}

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
  const [xslt, setXslt] = useState('')
  const xsltReadyRef = useRef(false)
  const [xmlContent, setXmlContent] = useState<string>(defaultInvoiceXml)
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
  const [isDownloading, setIsDownloading] = useState(false)
  const [isSaving, setIsSaving] = useState(false)
  const [saveSuccess, setSaveSuccess] = useState(false)
  const iframeRef = useRef<HTMLIFrameElement>(null)

  // Tema adını ve XSLT içeriğini yükle
  useEffect(() => {
    if (!templateId) return
    getTemplate(templateId).then((t) => setTemplateName(t.name)).catch(() => {})
    fetchThemeXslt(templateId)
      .then((content) => {
        setXslt(content)
        xsltReadyRef.current = true
      })
      .catch(() => {})
  }, [templateId])

  // Logo/imza/IBAN değişince enjeksiyonu XSLT'e göm
  useEffect(() => {
    if (!xsltReadyRef.current) return
    const injection = buildInjection(logo, signature, bankInfo)
    setXslt((prev) => applyInjection(prev, injection))
  }, [logo, signature, bankInfo])

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

  function handleDownload() {
    if (!xslt || isDownloading) return
    setIsDownloading(true)
    try {
      const blob = new Blob([xslt], { type: 'application/xslt+xml' })
      const a = document.createElement('a')
      a.href = URL.createObjectURL(blob)
      a.download = `${templateName.replace(/[^a-z0-9çğıöşüÇĞİÖŞÜ]/gi, '_')}.xslt`
      a.click()
      URL.revokeObjectURL(a.href)
    } finally {
      setIsDownloading(false)
    }
  }

  async function handleSave() {
    if (!xslt || isSaving) return
    setIsSaving(true)
    try {
      await createUserXsltTemplate({ name: templateName, xsltContent: xslt, xmlContent })
      setSaveSuccess(true)
      setTimeout(() => { navigate('/my-xslt-templates') }, 800)
    } catch { alert('Kaydetme başarısız.') }
    finally { setIsSaving(false) }
  }

  function handleXmlFile(e: React.ChangeEvent<HTMLInputElement>) {
    const file = e.target.files?.[0]
    if (!file) return
    if (file.size > 1 * 1024 * 1024) {
      alert('XML dosyası 1 MB\'dan büyük olamaz.')
      e.target.value = ''
      return
    }
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
      const dataUrl = await readAsDataUrl(file)
      setLogo((prev) => ({ ...prev, url: dataUrl }))
    } catch { alert('Logo yüklenemedi.') }
    finally { setLogoUploading(false); e.target.value = '' }
  }

  async function handleSignatureUpload(e: React.ChangeEvent<HTMLInputElement>) {
    const file = e.target.files?.[0]
    if (!file) return
    setSignatureUploading(true)
    try {
      const dataUrl = await readAsDataUrl(file)
      setSignature((prev) => ({ ...prev, url: dataUrl }))
    } catch { alert('İmza yüklenemedi.') }
    finally { setSignatureUploading(false); e.target.value = '' }
  }

  function addBankRow() { setBankInfo((p) => [...p, { bankName: '', iban: '' }]) }
  function removeBankRow(idx: number) { setBankInfo((p) => p.filter((_, i) => i !== idx)) }
  function updateBankRow(idx: number, field: keyof BankInfoItem, value: string) {
    setBankInfo((p) => p.map((r, i) => (i === idx ? { ...r, [field]: value } : r)))
  }

  return (
    <div className="h-screen flex overflow-hidden bg-gray-50">
      <EditorSidebar />

      <div className="flex-1 flex flex-col overflow-hidden">
        {/* Toolbar */}
        <div className="h-12 border-b border-gray-200 bg-white flex items-center gap-2 px-4 flex-shrink-0">
          <span className="text-base font-medium text-gray-700 flex-1 truncate">{templateName}</span>
          {previewLoading && (
            <span className="text-sm text-blue-500 flex items-center gap-1">
              <span className="inline-block w-4 h-4 border-2 border-blue-400 border-t-transparent rounded-full animate-spin" />
              Yükleniyor
            </span>
          )}
          {!previewLoading && lastMs !== null && <span className="text-sm text-gray-400">{lastMs} ms</span>}
          <label className="cursor-pointer text-sm text-gray-500 border border-gray-200 rounded px-3 py-1.5 hover:bg-gray-50">
            XML Değiştir
            <input type="file" accept=".xml,text/xml,application/xml" className="hidden" onChange={handleXmlFile} />
          </label>
          <button onClick={() => iframeRef.current?.contentWindow?.print()} disabled={!html}
            className="text-gray-500 border border-gray-200 rounded p-1.5 hover:bg-gray-50 disabled:opacity-30" title="Yazdır">
            <Printer size={16} />
          </button>
          <button
            onClick={() => navigate(`/dev-mode/${templateId}`)}
            className="flex items-center gap-1.5 text-sm text-gray-600 border border-gray-200 rounded px-3 py-1.5 hover:bg-gray-50"
            title="XSLT editörünü aç"
          >
            <Code2 size={15} />Geliştirici Modu
          </button>
          <button
            onClick={handleDownload}
            disabled={isDownloading || !xslt}
            className="flex items-center gap-1.5 text-sm text-gray-600 border border-gray-200 rounded px-3 py-1.5 hover:bg-gray-50 disabled:opacity-40"
            title="XSLT dosyasını indir"
          >
            <Download size={15} />{isDownloading ? '…' : 'İndir'}
          </button>
          <button
            onClick={handleSave}
            disabled={isSaving || saveSuccess || !xslt}
            className={`flex items-center gap-1.5 text-sm border rounded px-3 py-1.5 transition-colors disabled:opacity-40 ${
              saveSuccess
                ? 'text-green-700 border-green-300 bg-green-50'
                : 'text-gray-600 border-gray-200 hover:bg-gray-50'
            }`}
            title="Şablonlarıma kaydet"
          >
            <BookmarkCheck size={15} />{saveSuccess ? 'Kaydedildi' : isSaving ? '…' : 'Kaydet'}
          </button>
          {!editMode
            ? <button onClick={() => setEditMode(true)} className="flex items-center gap-1.5 text-sm font-medium text-white bg-blue-600 hover:bg-blue-700 rounded px-3 py-1.5">
                <Settings2 size={15} />Düzenle
              </button>
            : <button onClick={() => setEditMode(false)} className="flex items-center gap-1.5 text-sm text-gray-600 border border-gray-200 rounded px-3 py-1.5 hover:bg-gray-50">
                <X size={15} />Kapat
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
