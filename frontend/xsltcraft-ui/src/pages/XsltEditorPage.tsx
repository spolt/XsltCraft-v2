import { useEffect, useRef, useState, useCallback } from 'react'
import { useParams, useNavigate, Link, useLocation } from 'react-router-dom'
import { Panel, Group as PanelGroup, Separator as PanelResizeHandle } from 'react-resizable-panels'
import {
  LayoutDashboard,
  BookOpen,
  FilePlus,
  FolderOpen,
  Shield,
  Code2,
  FileCode2,
  Upload,
  TriangleAlert,
  WandSparkles,
  ShieldCheck,
  ListChecks,
  Terminal,
  BookMarked,
  Library,
} from 'lucide-react'
import Editor from '@monaco-editor/react'
import type { editor as MonacoEditor } from 'monaco-editor'
import XsltEditor from '../components/Xslteditor'
import XsltEditorToolbar from '../components/xslt-editor/XsltEditorToolbar'
import XsltEditorPreview from '../components/xslt-editor/XsltEditorPreview'
import SaveTemplateDialog from '../components/xslt-editor/SaveTemplateDialog'
import ProblemsPanel from '../components/xslt-editor/ProblemsPanel'
import XPathConsolePanel from '../components/xslt-editor/XPathConsolePanel'
import SnippetManagerDialog from '../components/xslt-editor/SnippetManagerDialog'
import ShortcutsDialog from '../components/xslt-editor/ShortcutsDialog'
import { listSnippets, type UserSnippet } from '../services/snippetService'
import { previewFromRawXslt, type PreviewTimings } from '../services/previewService'
import { validateBusinessRules, type BusinessRuleResult } from '../services/ublTrService'
import {
  getUserXsltTemplate,
  createUserXsltTemplate,
  updateUserXsltTemplate,
} from '../services/userXsltService'
import { useAuthStore } from '../store/authStore'
import api from '../services/apiService'

const PREVIEW_DEBOUNCE_MS = 1000
const XSLT_VALIDATION_DEBOUNCE_MS = 1500
const XML_VALIDATION_DEBOUNCE_MS = 500

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

// ─── Compact Sidebar ─────────────────────────────────────────────────────────
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
      <Link to="/xslt-editor" className={`${base} ${isActive('/xslt-editor') ? active : inactive}`} title="XSLT Editör"><Code2 size={18} /></Link>
      <Link to="/my-xslt-templates" className={`${base} ${isActive('/my-xslt-templates') ? active : inactive}`} title="Şablonlarım"><FileCode2 size={18} /></Link>
      {user?.role === 'Admin' && (
        <>
          <Link to="/admin/themes" className={`${base} ${isActive('/admin/themes') ? active : inactive}`} title="Admin — Temalar"><Shield size={18} /></Link>
          <Link to="/admin/snippets" className={`${base} ${isActive('/admin/snippets') ? active : inactive}`} title="Admin — Snippet Kütüphanesi"><Library size={18} /></Link>
        </>
      )}
    </div>
  )
}

// ─── Main page ───────────────────────────────────────────────────────────────
export default function XsltEditorPage() {
  const { templateId: routeTemplateId } = useParams<{ templateId: string }>()
  const navigate = useNavigate()

  // Core state
  const [templateId, setTemplateId] = useState<string | null>(routeTemplateId ?? null)
  const [templateName, setTemplateName] = useState('')
  const [xsltContent, setXsltContent] = useState('')
  const [xmlContent, setXmlContent] = useState<string | null>(null)
  const [isDirty, setIsDirty] = useState(false)

  // Validation
  const [xsltValid, setXsltValid] = useState<boolean | null>(null)
  const [xsltErrors, setXsltErrors] = useState<{ message: string; line: number; column: number }[]>([])
  const [xmlValid, setXmlValid] = useState<boolean | null>(null)
  const [xmlErrors, setXmlErrors] = useState<{ message: string; line: number | null; column: number | null }[]>([])

  // Preview
  const [previewHtml, setPreviewHtml] = useState('')
  const [previewLoading, setPreviewLoading] = useState(false)
  const [previewError, setPreviewError] = useState<string | null>(null)
  const [lastMs, setLastMs] = useState<number | null>(null)
  const [lastTimings, setLastTimings] = useState<PreviewTimings | null>(null)

  // Save dialog
  const [showSaveDialog, setShowSaveDialog] = useState(false)
  const [saving, setSaving] = useState(false)

  // UBL-TR business rule validation
  const [ublTrResults, setUblTrResults] = useState<BusinessRuleResult[] | null>(null)
  const [ublTrLoading, setUblTrLoading] = useState(false)
  const [ublTrError, setUblTrError] = useState<string | null>(null)
  const [showProblems, setShowProblems] = useState(false)

  // XPath console
  const [showXPathConsole, setShowXPathConsole] = useState(false)
  const [xpathInitialExpr, setXpathInitialExpr] = useState('')

  // Snippets
  const [userSnippets, setUserSnippets] = useState<UserSnippet[]>([])
  const [showSnippetManager, setShowSnippetManager] = useState(false)

  // Shortcuts dialog
  const [showShortcuts, setShowShortcuts] = useState(false)

  // Right panel tab: preview | xml
  const [rightTab, setRightTab] = useState<'preview' | 'xml'>('preview')

  // Loading from saved template
  const [loadingTemplate, setLoadingTemplate] = useState(!!routeTemplateId)

  // Refs
  const goToRef = useRef<((term: string) => void) | null>(null)
  const revealLineRef = useRef<((lineNumber: number, column?: number) => void) | null>(null)
  const insertTextAtLineRef = useRef<((lineNumber: number, text: string) => void) | null>(null)
  const toggleCommentRef = useRef<(() => void) | null>(null)
  const formatDocumentRef = useRef<(() => void) | null>(null)
  const imageFileInputRef = useRef<HTMLInputElement>(null)
  const pendingInsertLineRef = useRef<number | null>(null)
  const xmlEditorRef = useRef<MonacoEditor.IStandaloneCodeEditor | null>(null)

  // ─── Load user snippets ─────────────────────────────────────────────────────
  useEffect(() => {
    listSnippets().then(setUserSnippets).catch(() => {})
  }, [])

  // ─── Global keyboard shortcuts ──────────────────────────────────────────────
  useEffect(() => {
    function handleKeyDown(e: KeyboardEvent) {
      // F1 — kısayollar
      if (e.key === 'F1') {
        e.preventDefault()
        setShowShortcuts(s => !s)
        return
      }
      // Ctrl+S / ⌘S — kaydet (sadece editör ekranı açıkken)
      if ((e.ctrlKey || e.metaKey) && e.key === 's' && xsltContent) {
        e.preventDefault()
        setShowSaveDialog(true)
      }
    }
    document.addEventListener('keydown', handleKeyDown)
    return () => document.removeEventListener('keydown', handleKeyDown)
  }, [xsltContent])

  // ─── Load saved template ────────────────────────────────────────────────────
  useEffect(() => {
    if (!routeTemplateId) return
    setLoadingTemplate(true)
    getUserXsltTemplate(routeTemplateId)
      .then((t) => {
        setTemplateId(t.id)
        setTemplateName(t.name)
        setXsltContent(t.xsltContent)
        setXmlContent(t.xmlContent)
        setIsDirty(false)
      })
      .catch(() => navigate('/xslt-editor'))
      .finally(() => setLoadingTemplate(false))
  }, [routeTemplateId, navigate])

  // ─── File upload handlers ───────────────────────────────────────────────────
  function handleXsltFile(e: React.ChangeEvent<HTMLInputElement>) {
    const file = e.target.files?.[0]
    if (!file) return
    const reader = new FileReader()
    reader.onload = (ev) => {
      setXsltContent(ev.target?.result as string)
      setIsDirty(true)
      if (!templateName) setTemplateName(file.name.replace(/\.(xsl|xslt)$/i, ''))
    }
    reader.readAsText(file, 'utf-8')
    e.target.value = ''
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
    reader.onload = (ev) => {
      const content = ev.target?.result as string
      const errs = extractXmlParseErrors(content)
      setXmlErrors(errs)
      setXmlValid(errs.length === 0)
      setXmlContent(content)
    }
    reader.readAsText(file, 'utf-8')
    e.target.value = ''
  }

  // ─── Debounced preview ──────────────────────────────────────────────────────
  const runPreview = useCallback(async (currentXslt: string, currentXml: string) => {
    setPreviewLoading(true)
    setPreviewError(null)
    try {
      const result = await previewFromRawXslt(currentXslt, currentXml)
      setPreviewHtml(result.html)
      setLastMs(result.generationTimeMs)
      setLastTimings(result.timings ?? null)
    } catch (e: unknown) {
      if (e && typeof e === 'object' && 'response' in e) {
        const data = (e as { response?: { data?: { error?: string; line?: number; column?: number } } }).response?.data
        const msg = data?.error ?? 'Önizleme alınamadı.'
        setPreviewError(msg)
        if (data?.line) {
          setXsltErrors([{ message: msg, line: data.line, column: data.column ?? 1 }])
          setXsltValid(false)
        }
      } else {
        setPreviewError('Önizleme alınamadı.')
      }
    } finally {
      setPreviewLoading(false)
    }
  }, [])

  useEffect(() => {
    if (!xsltContent || !xmlContent) return
    const t = setTimeout(() => runPreview(xsltContent, xmlContent), PREVIEW_DEBOUNCE_MS)
    return () => clearTimeout(t)
  }, [xsltContent, xmlContent, runPreview])

  // ─── Debounced XSLT validation ─────────────────────────────────────────────
  useEffect(() => {
    if (!xsltContent) { setXsltValid(null); setXsltErrors([]); return }
    const t = setTimeout(async () => {
      try {
        const { data } = await api.post<{ valid: boolean; error?: string; line?: number; column?: number }>(
          '/api/preview/validate-xslt',
          { xslt: xsltContent },
        )
        if (data.valid) {
          setXsltValid(true)
          setXsltErrors([])
        } else {
          setXsltValid(false)
          setXsltErrors([{ message: data.error ?? 'XSLT hatası', line: data.line ?? 1, column: data.column ?? 1 }])
        }
      } catch {
        // silently fail validation
      }
    }, XSLT_VALIDATION_DEBOUNCE_MS)
    return () => clearTimeout(t)
  }, [xsltContent])

  // ─── Debounced XML validation ───────────────────────────────────────────────
  useEffect(() => {
    if (!xmlContent) { setXmlValid(null); setXmlErrors([]); return }
    const t = setTimeout(() => {
      const errs = extractXmlParseErrors(xmlContent)
      setXmlErrors(errs)
      setXmlValid(errs.length === 0)
    }, XML_VALIDATION_DEBOUNCE_MS)
    return () => clearTimeout(t)
  }, [xmlContent])

  // ─── Image insert ───────────────────────────────────────────────────────────
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

  // ─── Download ───────────────────────────────────────────────────────────────
  function handleDownload() {
    if (!xsltContent) return
    const blob = new Blob([xsltContent], { type: 'application/xslt+xml' })
    const a = document.createElement('a')
    a.href = URL.createObjectURL(blob)
    a.download = `${(templateName || 'template').replace(/[^a-z0-9çğıöşüÇĞİÖŞÜ]/gi, '_')}.xslt`
    a.click()
    URL.revokeObjectURL(a.href)
  }

  // ─── Save ───────────────────────────────────────────────────────────────────
  async function handleSave(name: string) {
    setSaving(true)
    try {
      if (templateId) {
        await updateUserXsltTemplate(templateId, {
          name,
          xsltContent,
          xmlContent: xmlContent ?? undefined,
        })
        setTemplateName(name)
      } else {
        const result = await createUserXsltTemplate({
          name,
          xsltContent,
          xmlContent: xmlContent ?? undefined,
        })
        setTemplateId(result.id)
        setTemplateName(result.name)
        // Update URL without reload
        window.history.replaceState(null, '', `/xslt-editor/${result.id}`)
      }
      setIsDirty(false)
      setShowSaveDialog(false)
    } catch {
      alert('Kayıt sırasında bir hata oluştu.')
    } finally {
      setSaving(false)
    }
  }

  // ─── Preview → Editor navigation ───────────────────────────────────────────
  const handlePreviewClick = useCallback((text: string) => {
    if (!goToRef.current) return
    // Try to find a meaningful short snippet to search for
    const searchTerm = text.length > 50 ? text.substring(0, 50) : text
    goToRef.current(searchTerm)
  }, [])

  // ─── UBL-TR iş kuralı doğrulaması ───────────────────────────────────────────
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

  const ublTrErrorCount = ublTrResults?.filter(r => r.severity === 'error').length ?? null
  const totalErrorCount = xsltErrors.length + xmlErrors.length + (ublTrErrorCount ?? 0)

  // XPath console — seçili ifadeyi konsola gönder, konsolu aç
  function handleEvaluateXPath(expression: string) {
    setXpathInitialExpr(expression)
    setShowXPathConsole(true)
  }

  // XML editor'da ilgili satıra git (sekmeyi XML'e çevir + reveal)
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

  // ─── Print ──────────────────────────────────────────────────────────────────
  function handlePrint() {
    const iframe = document.querySelector<HTMLIFrameElement>('iframe[title="Canlı Önizleme"]')
    iframe?.contentWindow?.print()
  }

  // ─── Loading state ─────────────────────────────────────────────────────────
  if (loadingTemplate) {
    return (
      <div className="h-screen flex overflow-hidden bg-gray-900">
        <EditorSidebar />
        <div className="flex-1 flex items-center justify-center">
          <span className="text-sm text-gray-400">Şablon yükleniyor…</span>
        </div>
      </div>
    )
  }

  // ─── Phase 1: Upload screen ─────────────────────────────────────────────────
  if (!xsltContent && !xmlContent) {
    return (
      <div className="h-screen flex overflow-hidden bg-gray-50">
        <EditorSidebar />
        <div className="flex-1 flex flex-col items-center justify-center gap-8 px-4">
          <div className="text-center">
            <h1 className="text-xl font-semibold text-gray-800 mb-1">XSLT Editör</h1>
            <p className="text-sm text-gray-500">XSLT ve XML dosyalarınızı yükleyerek düzenlemeye başlayın.</p>
          </div>

          <div className="flex gap-6">
            {/* XSLT Upload */}
            <label className="cursor-pointer flex flex-col items-center gap-3 border-2 border-dashed border-blue-300 rounded-xl px-10 py-8 bg-white hover:border-blue-400 hover:bg-blue-50 transition-colors">
              <div className="w-12 h-12 rounded-full bg-blue-100 flex items-center justify-center">
                <Code2 size={24} className="text-blue-600" />
              </div>
              <span className="text-sm font-medium text-blue-600">XSLT Dosyası Yükle</span>
              <span className="text-xs text-gray-400">.xsl, .xslt</span>
              <input type="file" accept=".xsl,.xslt" className="hidden" onChange={handleXsltFile} />
            </label>

            {/* XML Upload */}
            <label className="cursor-pointer flex flex-col items-center gap-3 border-2 border-dashed border-green-300 rounded-xl px-10 py-8 bg-white hover:border-green-400 hover:bg-green-50 transition-colors">
              <div className="w-12 h-12 rounded-full bg-green-100 flex items-center justify-center">
                <Upload size={24} className="text-green-600" />
              </div>
              <span className="text-sm font-medium text-green-600">XML Dosyası Yükle</span>
              <span className="text-xs text-gray-400">.xml — fatura veya irsaliye</span>
              <input type="file" accept=".xml,text/xml,application/xml" className="hidden" onChange={handleXmlFile} />
            </label>
          </div>

          {/* Saved templates link */}
          <Link to="/my-xslt-templates" className="text-xs text-gray-400 hover:text-blue-600 transition-colors">
            veya kayıtlı şablonlarınızdan birini açın →
          </Link>
        </div>
      </div>
    )
  }

  // ─── Phase 1b: Only one file loaded ─────────────────────────────────────────
  if (!xsltContent || !xmlContent) {
    return (
      <div className="h-screen flex overflow-hidden bg-gray-50">
        <EditorSidebar />
        <div className="flex-1 flex flex-col items-center justify-center gap-6 px-4">
          <div className="text-center">
            <h1 className="text-xl font-semibold text-gray-800 mb-1">XSLT Editör</h1>
            <p className="text-sm text-gray-500">
              {xsltContent ? 'XML dosyası yükleyerek devam edin.' : 'XSLT dosyası yükleyerek devam edin.'}
            </p>
          </div>

          {xsltContent && (
            <div className="flex items-center gap-2 text-sm text-green-700 bg-green-50 border border-green-100 rounded-lg px-4 py-3">
              <Code2 size={15} className="flex-shrink-0" />
              XSLT dosyası yüklendi — XML seçmeye hazır.
            </div>
          )}

          {xmlContent && (
            <div className="flex items-center gap-2 text-sm text-green-700 bg-green-50 border border-green-100 rounded-lg px-4 py-3">
              <Upload size={15} className="flex-shrink-0" />
              XML dosyası yüklendi — XSLT seçmeye hazır.
            </div>
          )}

          <label className="cursor-pointer flex flex-col items-center gap-3 border-2 border-dashed border-blue-300 rounded-xl px-14 py-10 bg-white hover:border-blue-400 hover:bg-blue-50 transition-colors">
            <svg className="w-10 h-10 text-blue-400" fill="none" viewBox="0 0 24 24" stroke="currentColor">
              <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={1.5} d="M3 16.5v2.25A2.25 2.25 0 005.25 21h13.5A2.25 2.25 0 0021 18.75V16.5m-13.5-9L12 3m0 0l4.5 4.5M12 3v13.5" />
            </svg>
            <span className="text-sm font-medium text-blue-600">
              {xsltContent ? 'XML Dosyası Yükle' : 'XSLT Dosyası Yükle'}
            </span>
            <input
              type="file"
              accept={xsltContent ? '.xml,text/xml,application/xml' : '.xsl,.xslt'}
              className="hidden"
              onChange={xsltContent ? handleXmlFile : handleXsltFile}
            />
          </label>
        </div>
      </div>
    )
  }

  // ─── Phase 2: Editor + Preview ──────────────────────────────────────────────
  return (
    <div className="h-screen flex overflow-hidden bg-gray-900">
      <EditorSidebar />

      <div className="flex-1 flex flex-col overflow-hidden">
        <XsltEditorToolbar
          templateName={templateName}
          xsltValid={xsltValid}
          xmlValid={xmlValid}
          previewLoading={previewLoading}
          lastMs={lastMs}
          lastTimings={lastTimings}
          hasXslt={!!xsltContent}
          hasXml={!!xmlContent}
          isDirty={isDirty}
          onBack={() => navigate('/dashboard')}
          onUploadXslt={handleXsltFile}
          onUploadXml={handleXmlFile}
          onDownload={handleDownload}
          onSave={() => setShowSaveDialog(true)}
          onPrint={handlePrint}
          onShowShortcuts={() => setShowShortcuts(true)}
        />

        {previewError && (
          <div className="px-4 py-2 text-xs text-red-400 bg-red-950 border-b border-red-900 flex-shrink-0 flex items-start gap-2">
            <TriangleAlert size={13} className="flex-shrink-0 mt-0.5" />
            <span className="font-mono whitespace-pre-wrap break-all">{previewError}</span>
          </div>
        )}

        <PanelGroup orientation="horizontal" className="flex-1 overflow-hidden">
          <Panel defaultSize={50} minSize={20}>
            <div className="h-full flex flex-col">
              <div className="px-3 py-1.5 bg-gray-800 border-b border-gray-700 flex-shrink-0 flex items-center justify-between gap-2">
                <span className="text-xs text-gray-400 font-mono uppercase tracking-wide whitespace-nowrap">XSLT Editörü</span>
                <div className="flex items-center gap-1 flex-shrink-0">
                  {/* Yorum Satırı */}
                  <button
                    onClick={() => toggleCommentRef.current?.()}
                    className="h-7 px-2 flex items-center gap-1.5 rounded border border-gray-600 text-gray-300 hover:bg-gray-700 text-xs transition-colors"
                    title="Açıklama Satırı Yap / Kaldır (Ctrl + Shift + C)"
                  >
                    <span className="font-mono text-[11px]">{'<!--'}</span>
                    <span className="hidden 2xl:inline">Açıklama</span>
                  </button>

                  {/* Biçimlendir */}
                  <button
                    onClick={() => formatDocumentRef.current?.()}
                    disabled={!xsltContent}
                    className="h-7 px-2 flex items-center gap-1.5 rounded border border-gray-600 text-gray-300 hover:bg-gray-700 text-xs transition-colors disabled:opacity-30"
                    title="Belgeyi Biçimlendir (Shift+Alt+F)"
                  >
                    <WandSparkles size={13} />
                    <span className="hidden 2xl:inline">Biçimlendir</span>
                  </button>

                  {/* UBL-TR */}
                  <button
                    onClick={handleValidateUblTr}
                    disabled={!xmlContent || ublTrLoading}
                    className="h-7 px-2 flex items-center gap-1.5 rounded border border-gray-600 text-gray-300 hover:bg-gray-700 text-xs transition-colors disabled:opacity-30"
                    title="UBL-TR iş kurallarını kontrol et"
                  >
                    <ShieldCheck size={13} />
                    <span className="hidden 2xl:inline">UBL-TR</span>
                    {ublTrErrorCount != null && ublTrErrorCount > 0 && (
                      <span className="inline-flex items-center justify-center min-w-[16px] h-[16px] px-1 rounded-full bg-red-500 text-white text-[10px] font-semibold">
                        {ublTrErrorCount}
                      </span>
                    )}
                  </button>

                  {/* Problemler */}
                  <button
                    onClick={() => setShowProblems(s => !s)}
                    className={`h-7 px-2 flex items-center gap-1.5 rounded border border-gray-600 text-xs transition-colors ${
                      showProblems ? 'bg-gray-700 text-white' : 'text-gray-300 hover:bg-gray-700'
                    }`}
                    title="Problemler panelini aç/kapat"
                  >
                    <ListChecks size={13} />
                    <span className="hidden 2xl:inline">Problemler</span>
                    {totalErrorCount > 0 && (
                      <span className="inline-flex items-center justify-center min-w-[16px] h-[16px] px-1 rounded-full bg-red-500 text-white text-[10px] font-semibold">
                        {totalErrorCount}
                      </span>
                    )}
                  </button>

                  {/* XPath Konsolu */}
                  <button
                    onClick={() => setShowXPathConsole(s => !s)}
                    disabled={!xmlContent}
                    className={`h-7 px-2 flex items-center gap-1.5 rounded border border-gray-600 text-xs transition-colors disabled:opacity-30 ${
                      showXPathConsole ? 'bg-gray-700 text-white' : 'text-gray-300 hover:bg-gray-700'
                    }`}
                    title="XPath Konsolu"
                  >
                    <Terminal size={13} />
                    <span className="hidden 2xl:inline">XPath</span>
                  </button>

                  {/* Snippet Kütüphanesi */}
                  <button
                    onClick={() => setShowSnippetManager(true)}
                    className="h-7 px-2 flex items-center gap-1.5 rounded border border-gray-600 text-gray-300 hover:bg-gray-700 text-xs transition-colors"
                    title="Snippet Kütüphanesi"
                  >
                    <BookMarked size={13} />
                    <span className="hidden 2xl:inline">Snippet</span>
                    {userSnippets.length > 0 && (
                      <span className="inline-flex items-center justify-center min-w-[16px] h-[16px] px-1 rounded-full bg-gray-600 text-gray-300 text-[10px] font-medium">
                        {userSnippets.length}
                      </span>
                    )}
                  </button>
                </div>
              </div>
              <div className="flex-1 overflow-hidden relative">
                <XsltEditor
                  value={xsltContent}
                  onChange={(v) => { setXsltContent(v); setIsDirty(true) }}
                  xmlContent={xmlContent}
                  userSnippets={userSnippets}
                  onEvaluateXPath={handleEvaluateXPath}
                  onEditorReady={({ goTo, revealLine, insertTextAtLine, toggleComment, formatDocument }) => {
                    goToRef.current = goTo
                    revealLineRef.current = revealLine
                    insertTextAtLineRef.current = insertTextAtLine
                    toggleCommentRef.current = toggleComment
                    formatDocumentRef.current = formatDocument
                  }}
                  onRequestImageInsert={handleRequestImageInsert}
                  errors={xsltErrors}
                />
              </div>
            </div>
          </Panel>

          <PanelResizeHandle className="w-1 bg-gray-700 hover:bg-blue-500 transition-colors cursor-col-resize flex-shrink-0" />

          <Panel defaultSize={50} minSize={20}>
            <div className="h-full flex flex-col">
              {/* Sağ panel sekme başlıkları */}
              <div className="h-9 flex items-center border-b border-gray-700 bg-gray-800 px-2 gap-0.5 flex-shrink-0">
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
                  disabled={!xmlContent}
                  className={`px-3 py-1 rounded text-xs transition-colors disabled:opacity-30 ${
                    rightTab === 'xml' ? 'bg-gray-700 text-white' : 'text-gray-400 hover:bg-gray-700'
                  }`}
                >
                  XML Kaynak
                </button>
              </div>

              {/* Sağ panel içerik */}
              <div className="flex-1 overflow-hidden">
                {rightTab === 'preview' ? (
                  <XsltEditorPreview
                    html={previewHtml}
                    onElementClick={handlePreviewClick}
                  />
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
                      fontSize: 13,
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

      {/* Gizli image file input — Resim Ekle context menu için */}
      <input
        ref={imageFileInputRef}
        type="file"
        accept="image/*"
        className="hidden"
        onChange={handleImageFileSelect}
      />

      {showSaveDialog && (
        <SaveTemplateDialog
          defaultName={templateName || 'Yeni Şablon'}
          isUpdate={!!templateId}
          saving={saving}
          onSave={handleSave}
          onClose={() => setShowSaveDialog(false)}
        />
      )}

      {showShortcuts && (
        <ShortcutsDialog onClose={() => setShowShortcuts(false)} />
      )}
    </div>
  )
}
