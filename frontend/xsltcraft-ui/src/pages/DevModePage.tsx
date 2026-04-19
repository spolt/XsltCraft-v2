import { useEffect, useRef, useState, useCallback } from 'react'
import { useParams, useNavigate, Link, useLocation } from 'react-router-dom'
import { Panel, Group as PanelGroup, Separator as PanelResizeHandle } from 'react-resizable-panels'
import Editor from '@monaco-editor/react'
import type { editor as MonacoEditor } from 'monaco-editor'
import XsltEditor from '../components/Xslteditor'
import XsltEditorPreview from '../components/xslt-editor/XsltEditorPreview'
import ProblemsPanel from '../components/xslt-editor/ProblemsPanel'
import XPathConsolePanel from '../components/xslt-editor/XPathConsolePanel'
import SnippetManagerDialog from '../components/xslt-editor/SnippetManagerDialog'
import ShortcutsDialog from '../components/xslt-editor/ShortcutsDialog'
import {
  LayoutDashboard,
  BookOpen,
  FilePlus,
  FolderOpen,
  Shield,
  Printer,
  ArrowLeft,
  TriangleAlert,
  CheckCircle2,
  Settings2,
  X,
  AlignLeft,
  AlignCenter,
  AlignRight,
  Download,
  WandSparkles,
  ListChecks,
  Terminal,
  BookMarked,
  ShieldCheck,
  Keyboard,
} from 'lucide-react'
import { getTemplate } from '../services/templateService'
import { fetchThemeXslt, previewFromRawXslt, type BankInfoItem, type Alignment } from '../services/previewService'
import { validateBusinessRules, type BusinessRuleResult } from '../services/ublTrService'
import { listSnippets, type UserSnippet } from '../services/snippetService'
import { useAuthStore } from '../store/authStore'
import api from '../services/apiService'

const DEBOUNCE_MS = 1000
const XSLT_VALIDATION_DEBOUNCE_MS = 1500
const XML_VALIDATION_DEBOUNCE_MS = 500

function readAsDataUrl(file: File): Promise<string> {
  return new Promise((resolve, reject) => {
    const reader = new FileReader()
    reader.onload = (e) => resolve(e.target!.result as string)
    reader.onerror = reject
    reader.readAsDataURL(file)
  })
}

const OVERRIDE_START = '<!-- DEV-MODE-OVERRIDES:START -->'
const OVERRIDE_END   = '<!-- DEV-MODE-OVERRIDES:END -->'

// ─── XSLT injection helpers ───────────────────────────────────────────────────

function escapeXml(s: string): string {
  return s.replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;')
}

/** Builds the injection block from current edit-panel state. Returns '' if nothing to inject. */
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

/** Inserts or replaces the injection block inside the XSLT string. */
function applyInjection(xslt: string, injection: string): string {
  const si = xslt.indexOf(OVERRIDE_START)
  const ei = xslt.indexOf(OVERRIDE_END)

  if (si >= 0 && ei >= 0) {
    if (!injection) {
      return xslt.slice(0, si).trimEnd() + '\n' + xslt.slice(ei + OVERRIDE_END.length).trimStart()
    }
    return xslt.slice(0, si) + injection + xslt.slice(ei + OVERRIDE_END.length)
  }

  if (!injection) return xslt

  const bodyClose = xslt.lastIndexOf('</body>')
  if (bodyClose >= 0)
    return xslt.slice(0, bodyClose) + injection + '\n' + xslt.slice(bodyClose)

  const tmpl = xslt.lastIndexOf('</xsl:template>')
  if (tmpl >= 0)
    return xslt.slice(0, tmpl) + injection + '\n' + xslt.slice(tmpl)

  return xslt + '\n' + injection
}

/** DOMParser parsererror çıktısından satır/kolon çıkarır. */
function extractXmlParseErrors(xml: string): { message: string; line: number | null; column: number | null }[] {
  const parser = new DOMParser()
  const doc = parser.parseFromString(xml, 'text/xml')
  const err = doc.querySelector('parsererror')
  if (!err) return []
  const raw = (err.textContent ?? 'XML ayrıştırma hatası').replace(/\s+/g, ' ').trim()
  const lineMatch = /line\s*(?:number)?\s*[:\s]\s*(\d+)/i.exec(raw)
  const colMatch = /column\s*(\d+)/i.exec(raw)
  return [{
    message: raw,
    line: lineMatch ? Number(lineMatch[1]) : null,
    column: colMatch ? Number(colMatch[1]) : null,
  }]
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

// ─── Image state / helpers ────────────────────────────────────────────────────
interface ImgState { url: string; width: string; height: string; alignment: Alignment }
const defaultImg = (): ImgState => ({ url: '', width: '250', height: '150', alignment: 'left' })

// ─── Alignment picker ─────────────────────────────────────────────────────────
function AlignPicker({ value, onChange }: { value: Alignment; onChange: (v: Alignment) => void }) {
  const opts: { v: Alignment; Icon: typeof AlignLeft }[] = [
    { v: 'left', Icon: AlignLeft }, { v: 'center', Icon: AlignCenter }, { v: 'right', Icon: AlignRight },
  ]
  return (
    <div className="flex gap-1">
      {opts.map(({ v, Icon }) => (
        <button key={v} onClick={() => onChange(v)}
          className={`flex items-center justify-center w-7 h-7 rounded border transition-colors ${value === v ? 'bg-blue-600 border-blue-600 text-white' : 'border-gray-200 text-gray-400 hover:border-blue-300 hover:text-blue-500'}`}
          title={v === 'left' ? 'Sola yasla' : v === 'center' ? 'Ortala' : 'Sağa yasla'}>
          <Icon size={13} />
        </button>
      ))}
    </div>
  )
}

// ─── Image panel ─────────────────────────────────────────────────────────────
function ImagePanel({ label, state, uploading, onUpload, onChange }: {
  label: string; state: ImgState; uploading: boolean
  onUpload: (e: React.ChangeEvent<HTMLInputElement>) => void
  onChange: (patch: Partial<ImgState>) => void
}) {
  return (
    <section>
      <h3 className="text-xs font-semibold text-gray-500 uppercase tracking-wide mb-2">{label}</h3>
      {state.url ? (
        <div className="flex flex-col gap-2 mb-3">
          <img src={state.url} alt={label} className="max-h-16 object-contain border border-gray-100 rounded p-1 bg-gray-50" />
          <label className="cursor-pointer text-xs text-blue-600 hover:underline">Değiştir
            <input type="file" accept="image/png,image/jpeg,image/svg+xml" className="hidden" onChange={onUpload} />
          </label>
        </div>
      ) : (
        <label className="cursor-pointer flex items-center gap-2 border border-dashed border-gray-300 rounded-lg px-3 py-3 hover:border-blue-300 hover:bg-blue-50 transition-colors mb-3">
          {uploading ? <span className="text-xs text-gray-400">Yükleniyor…</span>
            : <><span className="text-lg">🖼</span><span className="text-xs text-gray-500">Görsel yükle (PNG / JPG / SVG)</span></>}
          <input type="file" accept="image/png,image/jpeg,image/svg+xml" className="hidden" onChange={onUpload} disabled={uploading} />
        </label>
      )}
      <div className="flex flex-col gap-2">
        <div className="flex gap-2">
          <div className="flex-1">
            <label className="text-xs text-gray-400 mb-0.5 block">Genişlik (px)</label>
            <input type="number" min={10} max={1200} value={state.width}
              onChange={(e) => onChange({ width: e.target.value })}
              className="w-full text-xs border border-gray-200 rounded px-2 py-1.5 focus:outline-none focus:ring-1 focus:ring-blue-400" />
          </div>
          <div className="flex-1">
            <label className="text-xs text-gray-400 mb-0.5 block">Yükseklik (px)</label>
            <input type="number" min={10} max={1200} value={state.height}
              onChange={(e) => onChange({ height: e.target.value })}
              className="w-full text-xs border border-gray-200 rounded px-2 py-1.5 focus:outline-none focus:ring-1 focus:ring-blue-400" />
          </div>
        </div>
        <div>
          <label className="text-xs text-gray-400 mb-0.5 block">Hizalama</label>
          <AlignPicker value={state.alignment} onChange={(v) => onChange({ alignment: v })} />
        </div>
      </div>
    </section>
  )
}

// ─── Main page ────────────────────────────────────────────────────────────────
export default function DevModePage() {
  const { templateId } = useParams<{ templateId: string }>()
  const navigate = useNavigate()

  const [templateName, setTemplateName] = useState('Geliştirici Modu')
  const [xslt, setXslt] = useState('')
  const [xmlContent, setXmlContent] = useState<string | null>(null)
  const [editMode, setEditMode] = useState(false)

  const [loadingXslt, setLoadingXslt] = useState(true)
  const [xsltLoadError, setXsltLoadError] = useState<string | null>(null)

  const [logo, setLogo] = useState<ImgState>(defaultImg())
  const [signature, setSignature] = useState<ImgState>(defaultImg())
  const [bankInfo, setBankInfo] = useState<BankInfoItem[]>([{ bankName: '', iban: '' }])

  const [logoUploading, setLogoUploading] = useState(false)
  const [signatureUploading, setSignatureUploading] = useState(false)

  const [html, setHtml] = useState('')
  const [previewLoading, setPreviewLoading] = useState(false)
  const [previewError, setPreviewError] = useState<string | null>(null)
  const [lastMs, setLastMs] = useState<number | null>(null)

  // Validation
  const [xsltValid, setXsltValid] = useState<boolean | null>(null)
  const [xsltErrors, setXsltErrors] = useState<{ message: string; line: number; column: number }[]>([])
  const [xmlValid, setXmlValid] = useState<boolean | null>(null)
  const [xmlErrors, setXmlErrors] = useState<{ message: string; line: number | null; column: number | null }[]>([])

  // Problems panel
  const [showProblems, setShowProblems] = useState(false)
  const [ublTrResults, setUblTrResults] = useState<BusinessRuleResult[] | null>(null)
  const [ublTrLoading, setUblTrLoading] = useState(false)
  const [ublTrError, setUblTrError] = useState<string | null>(null)

  // XPath console
  const [showXPathConsole, setShowXPathConsole] = useState(false)
  const [xpathInitialExpr, setXpathInitialExpr] = useState('')

  // Snippets
  const [userSnippets, setUserSnippets] = useState<UserSnippet[]>([])
  const [showSnippetManager, setShowSnippetManager] = useState(false)

  // Right panel tab
  const [rightTab, setRightTab] = useState<'preview' | 'xml'>('preview')

  // Shortcuts dialog
  const [showShortcuts, setShowShortcuts] = useState(false)

  // Refs
  const xsltReadyRef = useRef(false)
  const toggleCommentRef = useRef<(() => void) | null>(null)
  const formatDocumentRef = useRef<(() => void) | null>(null)
  const revealLineRef = useRef<((lineNumber: number, column?: number) => void) | null>(null)
  const insertTextAtLineRef = useRef<((lineNumber: number, text: string) => void) | null>(null)
  const imageFileInputRef = useRef<HTMLInputElement>(null)
  const pendingInsertLineRef = useRef<number | null>(null)
  const xmlEditorRef = useRef<MonacoEditor.IStandaloneCodeEditor | null>(null)

  // ─── Load template name + XSLT ───────────────────────────────────────────────
  useEffect(() => {
    if (!templateId) return
    getTemplate(templateId).then((t) => setTemplateName(t.name)).catch(() => {})
    fetchThemeXslt(templateId)
      .then((content) => {
        setXslt(content)
        setLoadingXslt(false)
        xsltReadyRef.current = true
      })
      .catch(() => {
        setXsltLoadError('XSLT dosyası yüklenemedi.')
        setLoadingXslt(false)
      })
  }, [templateId])

  // ─── Load user snippets ──────────────────────────────────────────────────────
  useEffect(() => {
    listSnippets().then(setUserSnippets).catch(() => {})
  }, [])

  // ─── Global keyboard shortcuts ────────────────────────────────────────────────
  useEffect(() => {
    function handleKeyDown(e: KeyboardEvent) {
      if (e.key === 'F1') {
        e.preventDefault()
        setShowShortcuts(s => !s)
      }
    }
    document.addEventListener('keydown', handleKeyDown)
    return () => document.removeEventListener('keydown', handleKeyDown)
  }, [])

  // ─── Injection block → XSLT ─────────────────────────────────────────────────
  useEffect(() => {
    if (!xsltReadyRef.current) return
    const injection = buildInjection(logo, signature, bankInfo)
    setXslt((prev) => applyInjection(prev, injection))
  }, [logo, signature, bankInfo])

  // ─── Debounced preview ───────────────────────────────────────────────────────
  const runPreview = useCallback(async (currentXslt: string, currentXml: string) => {
    setPreviewLoading(true)
    setPreviewError(null)
    try {
      const result = await previewFromRawXslt(currentXslt, currentXml)
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
  }, [])

  useEffect(() => {
    if (!xslt || !xmlContent) return
    const t = setTimeout(() => runPreview(xslt, xmlContent), DEBOUNCE_MS)
    return () => clearTimeout(t)
  }, [xslt, xmlContent, runPreview])

  // ─── Debounced XSLT validation ────────────────────────────────────────────────
  useEffect(() => {
    if (!xslt) { setXsltValid(null); setXsltErrors([]); return }
    const t = setTimeout(async () => {
      try {
        const { data } = await api.post<{ valid: boolean; error?: string; line?: number; column?: number }>(
          '/api/preview/validate-xslt',
          { xslt },
        )
        if (data.valid) {
          setXsltValid(true)
          setXsltErrors([])
        } else {
          setXsltValid(false)
          setXsltErrors([{ message: data.error ?? 'XSLT hatası', line: data.line ?? 1, column: data.column ?? 1 }])
        }
      } catch { /* silently fail */ }
    }, XSLT_VALIDATION_DEBOUNCE_MS)
    return () => clearTimeout(t)
  }, [xslt])

  // ─── Debounced XML validation ─────────────────────────────────────────────────
  useEffect(() => {
    if (!xmlContent) { setXmlValid(null); setXmlErrors([]); return }
    const t = setTimeout(() => {
      const errs = extractXmlParseErrors(xmlContent)
      setXmlErrors(errs)
      setXmlValid(errs.length === 0)
    }, XML_VALIDATION_DEBOUNCE_MS)
    return () => clearTimeout(t)
  }, [xmlContent])

  // ─── Handlers ────────────────────────────────────────────────────────────────
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

  async function handleValidateUblTr() {
    if (!xmlContent) return
    setUblTrLoading(true)
    setUblTrError(null)
    setShowProblems(true)
    try {
      const res = await validateBusinessRules(xmlContent)
      setUblTrResults(res.results)
    } catch (e: unknown) {
      const msg = e && typeof e === 'object' && 'response' in e
        ? (e as { response?: { data?: { error?: string } } }).response?.data?.error ?? 'Doğrulama yapılamadı.'
        : 'Doğrulama yapılamadı.'
      setUblTrError(msg)
      setUblTrResults(null)
    } finally {
      setUblTrLoading(false)
    }
  }

  function handleEvaluateXPath(expression: string) {
    setXpathInitialExpr(expression)
    setShowXPathConsole(true)
  }

  function handleLocateXmlLine(line: number, column?: number | null) {
    setRightTab('xml')
    setTimeout(() => {
      if (xmlEditorRef.current) {
        xmlEditorRef.current.revealLineInCenter(line)
        xmlEditorRef.current.setPosition({ lineNumber: line, column: column ?? 1 })
        xmlEditorRef.current.focus()
      }
    }, 100)
  }

  function handleRequestImageInsert(lineNumber: number) {
    pendingInsertLineRef.current = lineNumber
    imageFileInputRef.current?.click()
  }

  function handleImageFileSelect(e: React.ChangeEvent<HTMLInputElement>) {
    const file = e.target.files?.[0]
    if (!file || pendingInsertLineRef.current === null) return
    const reader = new FileReader()
    reader.onload = (ev) => {
      const dataUrl = ev.target?.result as string
      const imgHtml = `<div><img src="${dataUrl}" style="max-width:100%;height:auto;" /></div>`
      insertTextAtLineRef.current?.(pendingInsertLineRef.current!, imgHtml)
      pendingInsertLineRef.current = null
    }
    reader.readAsDataURL(file)
    e.target.value = ''
  }

  function handlePrint() {
    const iframe = document.querySelector<HTMLIFrameElement>('iframe[title="Canlı Önizleme"]')
    iframe?.contentWindow?.print()
  }

  const ublTrErrorCount = ublTrResults?.filter(r => r.severity === 'error').length ?? null
  const totalErrorCount = xsltErrors.length + xmlErrors.length + (ublTrErrorCount ?? 0)

  // ─── Phase 1: no XML ─────────────────────────────────────────────────────────
  if (!xmlContent) {
    return (
      <div className="h-screen flex overflow-hidden bg-gray-50">
        <EditorSidebar />
        <div className="flex-1 flex flex-col items-center justify-center gap-6 px-4">
          <div className="text-center">
            <p className="text-xs text-gray-400 mb-1">
              <button onClick={() => navigate(`/theme-use/${templateId}`)} className="hover:text-blue-600 transition-colors">
                ← {templateName}
              </button>
            </p>
            <h1 className="text-xl font-semibold text-gray-800 mb-1">Geliştirici Modu</h1>
            <p className="text-sm text-gray-500">XSLT editörünü kullanmak için bir XML dosyası yükleyin.</p>
          </div>
          {xsltLoadError && (
            <div className="flex items-center gap-2 text-sm text-red-600 bg-red-50 border border-red-100 rounded-lg px-4 py-3">
              <TriangleAlert size={15} className="flex-shrink-0" />{xsltLoadError}
            </div>
          )}
          {!xsltLoadError && !loadingXslt && (
            <div className="flex items-center gap-2 text-sm text-green-700 bg-green-50 border border-green-100 rounded-lg px-4 py-3">
              <CheckCircle2 size={15} className="flex-shrink-0" />XSLT dosyası yüklendi — XML seçmeye hazır.
            </div>
          )}
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

  // ─── Phase 2: editor + preview + optional edit panel ─────────────────────────
  return (
    <div className="h-screen flex overflow-hidden bg-gray-900">
      <EditorSidebar />

      <div className="flex-1 flex flex-col overflow-hidden">
        {/* Toolbar */}
        <div className="h-10 border-b border-gray-700 bg-gray-900 flex items-center gap-1.5 px-3 flex-shrink-0">
          <button onClick={() => navigate(`/theme-use/${templateId}`)}
            className="flex items-center gap-1.5 text-xs text-gray-400 hover:text-gray-200 transition-colors mr-1" title="Geri dön">
            <ArrowLeft size={13} />
          </button>
          <span className="text-sm font-medium text-gray-200 flex-1 truncate min-w-0">{templateName} — Geliştirici Modu</span>

          {/* Loading / timing */}
          {previewLoading && (
            <span className="text-xs text-blue-400 flex items-center gap-1 flex-shrink-0">
              <span className="inline-block w-3 h-3 border-2 border-blue-400 border-t-transparent rounded-full animate-spin" />
              Derleniyor
            </span>
          )}
          {!previewLoading && lastMs !== null && (
            <span className="text-xs text-gray-500 flex-shrink-0">{lastMs} ms</span>
          )}

          {/* Validation status */}
          {xsltValid !== null && (
            <span className={`flex items-center gap-1 text-xs flex-shrink-0 ${xsltValid ? 'text-green-400' : 'text-red-400'}`} title={xsltValid ? 'XSLT geçerli' : 'XSLT hatası'}>
              {xsltValid ? <CheckCircle2 size={13} /> : <TriangleAlert size={13} />}
              XSLT
            </span>
          )}
          {xmlValid !== null && (
            <span className={`flex items-center gap-1 text-xs flex-shrink-0 ${xmlValid ? 'text-green-400' : 'text-red-400'}`} title={xmlValid ? 'XML geçerli' : 'XML hatası'}>
              {xmlValid ? <CheckCircle2 size={13} /> : <TriangleAlert size={13} />}
              XML
            </span>
          )}

          <div className="w-px h-4 bg-gray-700 mx-0.5 flex-shrink-0" />

          <label className="cursor-pointer text-xs text-gray-400 border border-gray-600 rounded px-2 py-1 hover:bg-gray-800 flex-shrink-0">
            XML Değiştir
            <input type="file" accept=".xml,text/xml,application/xml" className="hidden" onChange={handleXmlFile} />
          </label>
          <button
            onClick={() => {
              const blob = new Blob([xslt], { type: 'application/xslt+xml' })
              const a = document.createElement('a')
              a.href = URL.createObjectURL(blob)
              a.download = `${templateName.replace(/[^a-z0-9çğıöşüÇĞİÖŞÜ]/gi, '_')}.xslt`
              a.click()
              URL.revokeObjectURL(a.href)
            }}
            disabled={!xslt}
            className="flex items-center gap-1 text-xs text-gray-400 border border-gray-600 rounded px-2 py-1 hover:bg-gray-800 disabled:opacity-30 flex-shrink-0"
            title="XSLT dosyasını indir"
          >
            <Download size={12} />İndir
          </button>
          <button onClick={handlePrint} disabled={!html}
            className="text-gray-400 border border-gray-600 rounded p-1 hover:bg-gray-800 disabled:opacity-30 flex-shrink-0" title="Yazdır">
            <Printer size={13} />
          </button>
          {!editMode
            ? <button onClick={() => setEditMode(true)} className="flex items-center gap-1 text-xs font-medium text-white bg-blue-600 hover:bg-blue-700 rounded px-2 py-1 flex-shrink-0">
                <Settings2 size={12} />Düzenle
              </button>
            : <button onClick={() => setEditMode(false)} className="flex items-center gap-1 text-xs text-gray-300 border border-gray-600 rounded px-2 py-1 hover:bg-gray-800 flex-shrink-0">
                <X size={12} />Kapat
              </button>
          }

          <div className="w-px h-4 bg-gray-700 mx-0.5 flex-shrink-0" />

          {/* Shortcuts */}
          <button
            onClick={() => setShowShortcuts(true)}
            className="h-6 px-2 flex items-center rounded border border-gray-600 text-gray-300 hover:bg-gray-800 text-xs transition-colors flex-shrink-0"
            title="Klavye Kısayolları (F1)"
          >
            <Keyboard size={12} />
          </button>
        </div>

        {previewError && (
          <div className="px-4 py-2 text-xs text-red-400 bg-red-950 border-b border-red-900 flex-shrink-0 flex items-start gap-2">
            <TriangleAlert size={13} className="flex-shrink-0 mt-0.5" />
            <span className="font-mono whitespace-pre-wrap break-all">{previewError}</span>
          </div>
        )}

        <div className="flex-1 flex overflow-hidden">
          {/* Resizable: XSLT editor | right panel */}
          <PanelGroup orientation="horizontal" className="flex-1 overflow-hidden">
            <Panel defaultSize={50} minSize={20}>
              <div className="h-full flex flex-col">
                <div className="px-3 py-1.5 bg-gray-800 border-b border-gray-700 flex-shrink-0 flex items-center justify-between gap-2">
                  <span className="text-xs text-gray-400 font-mono uppercase tracking-wide whitespace-nowrap">XSLT Editörü</span>
                  <div className="flex items-center gap-1 flex-shrink-0">
                    <button
                      onClick={() => toggleCommentRef.current?.()}
                      className="h-6 px-2 flex items-center justify-center rounded border border-gray-600 text-gray-300 hover:bg-gray-700 text-xs font-mono transition-colors"
                      title="Yorum Satırı Ekle / Kaldır (Ctrl+Shift+C)"
                    >
                      {'<!--'}
                    </button>
                    <button
                      onClick={() => formatDocumentRef.current?.()}
                      className="h-6 px-2 flex items-center gap-1 rounded border border-gray-600 text-gray-300 hover:bg-gray-700 text-xs transition-colors"
                      title="Belgeyi Biçimlendir (Shift+Alt+F)"
                    >
                      <WandSparkles size={12} />
                    </button>

                    <button
                      onClick={handleValidateUblTr}
                      disabled={!xmlContent || ublTrLoading}
                      className="h-6 px-2 flex items-center gap-1 rounded border border-gray-600 text-gray-300 hover:bg-gray-700 text-xs transition-colors disabled:opacity-30"
                      title="UBL-TR iş kurallarını kontrol et"
                    >
                      <ShieldCheck size={12} />
                      {ublTrErrorCount != null && ublTrErrorCount > 0 && (
                        <span className="inline-flex items-center justify-center min-w-[14px] h-[14px] px-0.5 rounded-full bg-red-500 text-white text-[9px] font-semibold">
                          {ublTrErrorCount}
                        </span>
                      )}
                    </button>

                    <button
                      onClick={() => setShowXPathConsole(s => !s)}
                      disabled={!xmlContent}
                      className={`h-6 px-2 flex items-center rounded border border-gray-600 text-xs transition-colors disabled:opacity-30 ${
                        showXPathConsole ? 'bg-gray-700 text-white' : 'text-gray-300 hover:bg-gray-700'
                      }`}
                      title="XPath Konsolu"
                    >
                      <Terminal size={12} />
                    </button>

                    <button
                      onClick={() => setShowSnippetManager(true)}
                      className="h-6 px-2 flex items-center gap-1 rounded border border-gray-600 text-gray-300 hover:bg-gray-700 text-xs transition-colors"
                      title="Snippet Kütüphanesi"
                    >
                      <BookMarked size={12} />
                      {userSnippets.length > 0 && (
                        <span className="inline-flex items-center justify-center min-w-[14px] h-[14px] px-0.5 rounded-full bg-gray-600 text-gray-300 text-[9px] font-medium">
                          {userSnippets.length}
                        </span>
                      )}
                    </button>

                    <button
                      onClick={() => setShowProblems(s => !s)}
                      className={`h-6 px-2 flex items-center gap-1 rounded border border-gray-600 text-xs transition-colors ${
                        showProblems ? 'bg-gray-700 text-white' : 'text-gray-300 hover:bg-gray-700'
                      }`}
                      title="Problemler panelini aç/kapat"
                    >
                      <ListChecks size={12} />
                      {totalErrorCount > 0 && (
                        <span className="inline-flex items-center justify-center min-w-[14px] h-[14px] px-0.5 rounded-full bg-red-500 text-white text-[9px] font-semibold">
                          {totalErrorCount}
                        </span>
                      )}
                    </button>
                  </div>
                </div>
                {loadingXslt ? (
                  <div className="flex-1 flex items-center justify-center bg-gray-900">
                    <span className="text-xs text-gray-500">XSLT yükleniyor…</span>
                  </div>
                ) : (
                  <div className="flex-1 overflow-hidden relative">
                    <XsltEditor
                      value={xslt}
                      onChange={(v) => setXslt(v)}
                      xmlContent={xmlContent}
                      userSnippets={userSnippets}
                      onEvaluateXPath={handleEvaluateXPath}
                      onRequestImageInsert={handleRequestImageInsert}
                      errors={xsltErrors}
                      onEditorReady={({ revealLine, insertTextAtLine, toggleComment, formatDocument }) => {
                        revealLineRef.current = revealLine
                        insertTextAtLineRef.current = insertTextAtLine
                        toggleCommentRef.current = toggleComment
                        formatDocumentRef.current = formatDocument
                      }}
                      options={{
                        fontSize: 12,
                        minimap: { enabled: true },
                        wordWrap: 'off',
                        scrollBeyondLastLine: false,
                        automaticLayout: true,
                        lineNumbers: 'on',
                        renderLineHighlight: 'line',
                      }}
                    />
                  </div>
                )}
              </div>
            </Panel>

            <PanelResizeHandle className="w-1 bg-gray-700 hover:bg-blue-500 transition-colors cursor-col-resize flex-shrink-0" />

            <Panel defaultSize={50} minSize={20}>
              <div className="h-full flex flex-col">
                {/* Sekme başlıkları */}
                <div className="h-8 flex items-center border-b border-gray-700 bg-gray-800 px-2 gap-0.5 flex-shrink-0">
                  <button
                    onClick={() => setRightTab('preview')}
                    className={`px-3 py-1 rounded text-xs transition-colors ${
                      rightTab === 'preview' ? 'bg-gray-700 text-white' : 'text-gray-400 hover:bg-gray-700'
                    }`}
                  >
                    Önizleme
                  </button>
                  <button
                    onClick={() => setRightTab('xml')}
                    className={`px-3 py-1 rounded text-xs transition-colors ${
                      rightTab === 'xml' ? 'bg-gray-700 text-white' : 'text-gray-400 hover:bg-gray-700'
                    }`}
                  >
                    XML Kaynak
                  </button>
                </div>

                {/* Sekme içerikleri */}
                <div className="flex-1 overflow-hidden">
                  {rightTab === 'preview' ? (
                    <XsltEditorPreview html={html} />
                  ) : (
                    <Editor
                      height="100%"
                      language="xml"
                      theme="vs-dark"
                      value={xmlContent ?? ''}
                      onMount={(editor) => { xmlEditorRef.current = editor }}
                      options={{
                        readOnly: true,
                        minimap: { enabled: false },
                        fontSize: 12,
                        wordWrap: 'on',
                        lineNumbers: 'on',
                        scrollBeyondLastLine: false,
                        folding: true,
                      }}
                    />
                  )}
                </div>
              </div>
            </Panel>
          </PanelGroup>

          {/* Right edit panel */}
          {editMode && (
            <div className="flex-shrink-0 border-l border-gray-700 bg-white flex flex-col overflow-y-auto" style={{ width: 300 }}>
              <div className="p-4 flex flex-col gap-6">
                <ImagePanel label="Logo" state={logo} uploading={logoUploading} onUpload={handleLogoUpload}
                  onChange={(patch) => setLogo((p) => ({ ...p, ...patch }))} />

                <div className="border-t border-gray-100" />

                <ImagePanel label="İmza / Kaşe" state={signature} uploading={signatureUploading} onUpload={handleSignatureUpload}
                  onChange={(patch) => setSignature((p) => ({ ...p, ...patch }))} />

                <div className="border-t border-gray-100" />

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

                <div className="border-t border-gray-100 pt-2">
                  <p className="text-xs text-gray-400 leading-relaxed">
                    Düzenlemeler XSLT editörüne <code className="bg-gray-100 px-1 rounded">DEV-MODE-OVERRIDES</code> bloğu olarak yansır.
                  </p>
                </div>
              </div>
            </div>
          )}
        </div>

        {showProblems && (
          <ProblemsPanel
            xsltProblems={xsltErrors.map(e => ({
              source: 'xslt' as const,
              severity: 'error' as const,
              ruleName: 'XSLT Sözdizimi',
              message: e.message,
              line: e.line,
              column: e.column,
            }))}
            xmlProblems={xmlErrors.map(e => ({
              source: 'xml' as const,
              severity: 'error' as const,
              ruleName: 'XML Ayrıştırma',
              message: e.message,
              line: e.line,
              column: e.column,
            }))}
            ublTrProblems={ublTrResults?.map(r => ({
              source: 'ubl-tr' as const,
              ruleId: r.ruleId,
              ruleName: r.ruleName,
              severity: r.severity,
              message: r.message,
              line: r.line,
              column: r.column,
            })) ?? null}
            ublTrLoading={ublTrLoading}
            ublTrError={ublTrError}
            onLocateXslt={(line, column) => revealLineRef.current?.(line, column ?? undefined)}
            onClose={() => setShowProblems(false)}
          />
        )}

        {showXPathConsole && (
          <XPathConsolePanel
            xmlContent={xmlContent}
            initialExpression={xpathInitialExpr}
            onLocateLine={handleLocateXmlLine}
            onClose={() => setShowXPathConsole(false)}
          />
        )}
      </div>

      {showSnippetManager && (
        <SnippetManagerDialog
          onClose={() => setShowSnippetManager(false)}
          onSnippetsChanged={setUserSnippets}
        />
      )}

      {showShortcuts && (
        <ShortcutsDialog onClose={() => setShowShortcuts(false)} />
      )}

      <input
        ref={imageFileInputRef}
        type="file"
        accept="image/*"
        className="hidden"
        onChange={handleImageFileSelect}
      />
    </div>
  )
}
