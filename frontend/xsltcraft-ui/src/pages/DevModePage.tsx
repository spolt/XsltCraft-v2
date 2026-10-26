import { useEffect, useMemo, useRef, useState, useCallback } from 'react'
import { useParams, useNavigate } from 'react-router-dom'
import { Panel, Group as PanelGroup, Separator as PanelResizeHandle } from 'react-resizable-panels'
import Editor from '@monaco-editor/react'
import XsltEditor from '../components/Xslteditor'
import XsltEditorPreview from '../components/xslt-editor/XsltEditorPreview'
import { printActivePreview } from '../components/xslt-editor/previewFrame'
import ProblemsPanel from '../components/xslt-editor/ProblemsPanel'
import { useXslt1CompatWarnings } from '../hooks/useXslt1CompatWarnings'
import SnippetManagerDialog from '../components/xslt-editor/SnippetManagerDialog'
import ShortcutsDialog from '../components/xslt-editor/ShortcutsDialog'
import {
  Printer,
  ArrowLeft,
  TriangleAlert,
  CheckCircle2,
  Settings2,
  X,
  Download,
  WandSparkles,
  ListChecks,
  BookMarked,
  ShieldCheck,
  Keyboard,
  FileUp,
} from 'lucide-react'
import WorkspaceHeader from '../components/shell/WorkspaceHeader'
import { HeaderButton, HeaderChip } from '../components/shell/headerParts'
import { useUiPrefsStore } from '../store/uiPrefsStore'
import { defineMonacoThemes, monacoThemeId } from '../theme/monacoThemes'
import { getTemplate } from '../services/templateService'
import { toast } from '../store/toastStore'
import { readUploadFile, UPLOAD_ACCEPT } from '../utils/uploadValidation'
import { reportDownload } from '../services/activityService'
import { fetchThemeXslt, previewFromRawXslt, type BankInfoItem } from '../services/previewService'
import { composeTheme } from '../services/themeComposeService'
import type { ThemeSlotOption } from '../services/templateService'
import ImagePanel from '../components/theme-assets/ImagePanel'
import BankInfoPanel from '../components/theme-assets/BankInfoPanel'
import {
  defaultImg,
  effectiveSlot,
  filledBankRows,
  readAsDataUrl,
  toComposeAssets,
  hiddenThemeImages,
  themeImageRemovable,
  validateImageFile,
  type ImgState,
} from '../components/theme-assets/imageState'
import { validateBusinessRules, type BusinessRuleResult } from '../services/ublTrService'
import { listSnippets, type UserSnippet } from '../services/snippetService'
import api from '../services/apiService'
import { toXsltError } from '../services/xsltErrors'

const DEBOUNCE_MS = 1000
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
  const [slots, setSlots] = useState<ThemeSlotOption[]>([])
  // Panel yalnız kullanıcı bir şey değiştirdiğinde editöre yazar (açılışta mevcut bloğa dokunulmaz).
  const [assetsTouched, setAssetsTouched] = useState(false)

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
  // Sayfadaki tek XML dosya girişi (tüm "XML yükle / değiştir" butonları bunu tetikler).
  const xmlInputRef = useRef<HTMLInputElement>(null)
  const pendingInsertLineRef = useRef<number | null>(null)
  const editorTheme = useUiPrefsStore(s => s.editorTheme)
  const editorFontSize = useUiPrefsStore(s => s.editorFontSize)

  // ─── Load template name + XSLT ───────────────────────────────────────────────
  useEffect(() => {
    if (!templateId) return
    getTemplate(templateId)
      .then((t) => {
        setTemplateName(t.name)
        setSlots(t.slots ?? [])
      })
      .catch(() => {})
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

  // ─── Logo/imza/IBAN → XSLT (sunucuda birleştirilir; Tema Kullan ile aynı kod) ───────
  const xsltRef = useRef(xslt)
  useEffect(() => { xsltRef.current = xslt }, [xslt])

  useEffect(() => {
    if (!assetsTouched || !xsltReadyRef.current) return
    const controller = new AbortController()
    const timer = setTimeout(async () => {
      const requested = xsltRef.current
      try {
        const result = await composeTheme({
          xslt: requested,
          assets: toComposeAssets(logo, signature, slots),
          bankInfo: filledBankRows(bankInfo),
          hideThemeImages: hiddenThemeImages(logo, signature, slots),
          includeXslt: true,
          includeHtml: false,
        }, controller.signal)
        // Bu arada editörde yazıldıysa sonucu atma — kullanıcının metni ezilmesin; sonraki değişiklikte yeniden denenir.
        if (result.xslt && xsltRef.current === requested) setXslt(result.xslt)
        result.warnings.forEach((w) => toast.warning(w.message))
      } catch (err) {
        if (controller.signal.aborted) return
        const data = (err as { response?: { data?: { error?: string } } }).response?.data
        toast.error(data?.error ?? 'Görseller şablona eklenemedi.')
      }
    }, 600)
    return () => { clearTimeout(timer); controller.abort() }
  }, [logo, signature, bankInfo, slots, assetsTouched])

  // ─── Debounced preview ───────────────────────────────────────────────────────
  const runPreview = useCallback(async (currentXslt: string, currentXml: string) => {
    setPreviewLoading(true)
    setPreviewError(null)
    try {
      const result = await previewFromRawXslt(currentXslt, currentXml)
      setHtml(result.html)
      setLastMs(result.generationTimeMs)
    } catch (e: unknown) {
      setPreviewError(toXsltError(e).message)
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
          const err = toXsltError(data, 'XSLT hatası')
          setXsltErrors([{ message: err.message, line: err.line || 1, column: err.column || 1 }])
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
  async function handleXmlFile(e: React.ChangeEvent<HTMLInputElement>) {
    const file = e.target.files?.[0]
    e.target.value = ''
    if (!file) return
    try {
      setXmlContent(await readUploadFile(file, 'xml'))
    } catch (err) {
      toast.error((err as Error).message, { title: 'XML yüklenemedi' })
    }
  }

  async function handleImageUpload(
    e: React.ChangeEvent<HTMLInputElement>,
    setState: React.Dispatch<React.SetStateAction<ImgState>>,
    setBusy: (busy: boolean) => void,
  ) {
    const file = e.target.files?.[0]
    e.target.value = ''
    if (!file) return
    const invalid = validateImageFile(file)
    if (invalid) { toast.error(invalid); return }
    setBusy(true)
    try {
      const dataUrl = await readAsDataUrl(file)
      setState((prev) => ({ ...prev, dataUrl }))
      setAssetsTouched(true)
    } catch { toast.error('Görsel okunamadı.') }
    finally { setBusy(false) }
  }

  function patchImage(setState: React.Dispatch<React.SetStateAction<ImgState>>, patch: Partial<ImgState>) {
    setState((p) => ({ ...p, ...patch }))
    setAssetsTouched(true)
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
    printActivePreview()
  }

  function handleDownload() {
    const blob = new Blob([xslt], { type: 'application/xslt+xml' })
    const a = document.createElement('a')
    a.href = URL.createObjectURL(blob)
    a.download = `${templateName.replace(/[^a-z0-9çğıöşüÇĞİÖŞÜ]/gi, '_')}.xslt`
    a.click()
    URL.revokeObjectURL(a.href)
    reportDownload('Template', templateId)
  }

  const ublTrErrorCount = ublTrResults?.filter(r => r.severity === 'error').length ?? null
  const totalErrorCount = xsltErrors.length + xmlErrors.length + (ublTrErrorCount ?? 0)

  // XSLT 1.0 uyumluluk uyarıları (2.0'a özgü fonksiyon/yapı) — yalnız uyarı, hiçbir akışı engellemez
  const compat = useXslt1CompatWarnings(xslt)
  const editorMarkers = useMemo(() => [...xsltErrors, ...compat.markers], [xsltErrors, compat.markers])

  const header = (
    <WorkspaceHeader
      active="themes"
      center={(
        <>
          <span className="max-w-[260px] truncate font-medium">{templateName}</span>
          <HeaderChip>Geliştirici Modu</HeaderChip>
        </>
      )}
      actions={(
        <>
          <HeaderButton icon={ArrowLeft} onClick={() => navigate(`/theme-use/${templateId}`)}>Tema Kullan'a dön</HeaderButton>
          <HeaderButton variant="primary" icon={Download} onClick={handleDownload} disabled={!xslt}>İndir</HeaderButton>
        </>
      )}
    />
  )

  const xmlInput = (
    <input ref={xmlInputRef} type="file" accept={UPLOAD_ACCEPT.xml} className="hidden" onChange={handleXmlFile} />
  )

  const edBtn =
    'xc-focus h-7 px-2 flex items-center gap-1 rounded-md text-ed-text text-xs transition-colors hover:bg-ed-active disabled:opacity-40'
  const badge = 'inline-flex items-center justify-center min-w-[16px] h-4 px-1 rounded-full text-[10px] font-semibold'

  // ─── Phase 1: no XML ─────────────────────────────────────────────────────────
  if (!xmlContent) {
    return (
      <div className="h-screen flex flex-col overflow-hidden bg-ui-bg text-tx">
        {header}
        <main className="flex-1 flex flex-col items-center justify-center gap-6 px-4">
          <div className="text-center">
            <h1 className="text-xl font-semibold text-tx mb-1">Geliştirici Modu</h1>
            <p className="text-sm text-tx-muted">XSLT editörünü kullanmak için bir XML dosyası yükleyin.</p>
          </div>
          {xsltLoadError && (
            <div role="alert" className="flex items-center gap-2 text-sm text-fn-red-ink bg-fn-red-tint border border-fn-red-line rounded-lg px-4 py-3">
              <TriangleAlert size={15} className="flex-shrink-0" aria-hidden="true" />{xsltLoadError}
            </div>
          )}
          {!xsltLoadError && !loadingXslt && (
            <div role="status" className="flex items-center gap-2 text-sm text-fn-green-ink bg-fn-green-tint border border-fn-green-line rounded-lg px-4 py-3">
              <CheckCircle2 size={15} className="flex-shrink-0" aria-hidden="true" />XSLT dosyası yüklendi — XML seçmeye hazır.
            </div>
          )}
          <button
            type="button"
            onClick={() => xmlInputRef.current?.click()}
            className="xc-focus xc-lift flex flex-col items-center gap-3 border-2 border-dashed border-ui-soft-line rounded-xl px-14 py-10 bg-ui-card hover:bg-ui-soft transition-colors"
          >
            <span aria-hidden="true" className="flex h-12 w-12 items-center justify-center rounded-full bg-ui-soft text-ui-ink">
              <FileUp size={24} />
            </span>
            <span className="text-sm font-semibold text-ui-ink">XML Dosyası Yükle</span>
            <span className="text-xs text-tx-subtle">.xml — fatura veya irsaliye</span>
          </button>
        </main>
        {xmlInput}
      </div>
    )
  }

  // ─── Phase 2: editor + preview + optional edit panel ─────────────────────────
  return (
    <div className="h-screen flex flex-col overflow-hidden bg-ui-bg text-tx">
      {header}

      <div className="flex-1 flex flex-col overflow-hidden">
        {/* Durum + araçlar */}
        <div
          role="toolbar"
          aria-label="Geliştirici modu araçları"
          className="min-h-10 border-b border-ui-line bg-ui-card flex flex-wrap items-center gap-1.5 px-3 py-1 flex-shrink-0 text-xs"
        >
          {previewLoading && (
            <span role="status" className="text-tx-muted flex items-center gap-1 flex-shrink-0">
              <span aria-hidden="true" className="inline-block w-3 h-3 border-2 border-ui-accent border-t-transparent rounded-full animate-spin motion-reduce:animate-none" />
              Derleniyor
            </span>
          )}
          {!previewLoading && lastMs !== null && (
            <span className="text-tx-muted flex-shrink-0">{lastMs} ms</span>
          )}

          {xsltValid !== null && (
            <span className={`flex items-center gap-1 flex-shrink-0 ${xsltValid ? 'text-fn-green-ink' : 'text-fn-red-ink'}`} title={xsltValid ? 'XSLT geçerli' : 'XSLT hatası'}>
              {xsltValid ? <CheckCircle2 size={13} aria-hidden="true" /> : <TriangleAlert size={13} aria-hidden="true" />}
              XSLT
            </span>
          )}
          {xmlValid !== null && (
            <span className={`flex items-center gap-1 flex-shrink-0 ${xmlValid ? 'text-fn-green-ink' : 'text-fn-red-ink'}`} title={xmlValid ? 'XML geçerli' : 'XML hatası'}>
              {xmlValid ? <CheckCircle2 size={13} aria-hidden="true" /> : <TriangleAlert size={13} aria-hidden="true" />}
              XML
            </span>
          )}

          <div className="flex-1" />

          <button
            type="button"
            onClick={() => xmlInputRef.current?.click()}
            className="xc-focus h-7 flex items-center gap-1 rounded-md border border-field-line bg-ui-card px-2 text-tx-2 hover:bg-ui-hover flex-shrink-0"
          >
            <FileUp size={12} aria-hidden="true" />XML Değiştir
          </button>
          <button
            type="button"
            onClick={handlePrint}
            disabled={!html}
            className="xc-focus h-7 w-7 flex items-center justify-center rounded-md border border-field-line bg-ui-card text-tx-2 hover:bg-ui-hover disabled:opacity-40 flex-shrink-0"
            title="Yazdır"
            aria-label="Yazdır"
          >
            <Printer size={13} aria-hidden="true" />
          </button>
          {!editMode
            ? (
              <button
                type="button"
                onClick={() => setEditMode(true)}
                className="xc-focus h-7 flex items-center gap-1 rounded-md bg-ui-accent px-2 font-semibold text-white hover:brightness-110 flex-shrink-0"
              >
                <Settings2 size={12} aria-hidden="true" />Düzenle
              </button>
            )
            : (
              <button
                type="button"
                onClick={() => setEditMode(false)}
                className="xc-focus h-7 flex items-center gap-1 rounded-md border border-field-line bg-ui-card px-2 text-tx-2 hover:bg-ui-hover flex-shrink-0"
              >
                <X size={12} aria-hidden="true" />Kapat
              </button>
            )}
          <button
            type="button"
            onClick={() => setShowShortcuts(true)}
            className="xc-focus h-7 w-7 flex items-center justify-center rounded-md border border-field-line bg-ui-card text-tx-2 hover:bg-ui-hover flex-shrink-0"
            title="Klavye Kısayolları (F1)"
            aria-label="Klavye kısayolları"
          >
            <Keyboard size={13} aria-hidden="true" />
          </button>
        </div>

        {previewError && (
          <div role="alert" className="px-4 py-2 text-xs text-fn-red-ink bg-fn-red-tint border-b border-fn-red-line flex-shrink-0 flex items-start gap-2">
            <TriangleAlert size={13} className="flex-shrink-0 mt-0.5" aria-hidden="true" />
            <span className="font-mono whitespace-pre-wrap break-all">{previewError}</span>
          </div>
        )}

        <div className="flex-1 flex overflow-hidden">
          {/* Resizable: XSLT editor | right panel */}
          <PanelGroup orientation="horizontal" className="flex-1 overflow-hidden">
            <Panel defaultSize="50%" minSize="20%">
              <div className="h-full flex flex-col bg-ed-bg">
                <div
                  role="toolbar"
                  aria-label="Editör araçları"
                  className="px-2.5 py-1 bg-ed-panel border-b border-ed-border flex-shrink-0 flex items-center justify-between gap-2"
                >
                  <span className="text-xs text-ed-muted font-mono uppercase tracking-wide whitespace-nowrap">XSLT Editörü</span>
                  <div className="flex items-center gap-1 flex-shrink-0">
                    <button type="button" onClick={() => toggleCommentRef.current?.()} className={`${edBtn} font-mono`} title="Yorum Satırı Ekle / Kaldır (Ctrl+Shift+C)" aria-label="Açıklama satırı yap / kaldır">
                      {'<!--'}
                    </button>
                    <button type="button" onClick={() => formatDocumentRef.current?.()} className={edBtn} title="Belgeyi Biçimlendir (Shift+Alt+F)" aria-label="Belgeyi biçimlendir">
                      <WandSparkles size={12} aria-hidden="true" />
                    </button>
                    <button
                      type="button"
                      onClick={handleValidateUblTr}
                      disabled={!xmlContent || ublTrLoading}
                      className={`${edBtn} border border-ed-border`}
                      title="UBL-TR iş kurallarını kontrol et"
                    >
                      <ShieldCheck size={12} aria-hidden="true" />
                      UBL-TR kontrol
                      {ublTrErrorCount != null && ublTrErrorCount > 0 && (
                        <span className={`${badge} bg-fn-red text-white`}>{ublTrErrorCount}</span>
                      )}
                    </button>
                    <button type="button" onClick={() => setShowSnippetManager(true)} className={edBtn} title="Snippet Kütüphanesi" aria-label="Snippet Kütüphanesi">
                      <BookMarked size={12} aria-hidden="true" />
                      {userSnippets.length > 0 && (
                        <span className={`${badge} bg-ed-seg text-ed-text`}>{userSnippets.length}</span>
                      )}
                    </button>
                    <button
                      type="button"
                      onClick={() => setShowProblems(s => !s)}
                      aria-pressed={showProblems}
                      className={`${edBtn} ${showProblems ? 'bg-ed-active' : ''}`}
                      title="Problemler panelini aç/kapat"
                      aria-label="Problemler"
                    >
                      <ListChecks size={12} aria-hidden="true" />
                      {totalErrorCount > 0 && (
                        <span className={`${badge} bg-fn-red text-white`}>{totalErrorCount}</span>
                      )}
                    </button>
                  </div>
                </div>
                {loadingXslt ? (
                  <div className="flex-1 flex items-center justify-center bg-ed-bg">
                    <span role="status" className="text-xs text-ed-muted">XSLT yükleniyor…</span>
                  </div>
                ) : (
                  <div className="flex-1 overflow-hidden relative">
                    <XsltEditor
                      value={xslt}
                      onChange={(v) => setXslt(v)}
                      xmlContent={xmlContent}
                      userSnippets={userSnippets}
                      onRequestImageInsert={handleRequestImageInsert}
                      errors={editorMarkers}
                      onEditorReady={({ revealLine, insertTextAtLine, toggleComment, formatDocument }) => {
                        revealLineRef.current = revealLine
                        insertTextAtLineRef.current = insertTextAtLine
                        toggleCommentRef.current = toggleComment
                        formatDocumentRef.current = formatDocument
                      }}
                      options={{
                        minimap: { enabled: true },
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

            <PanelResizeHandle className="w-1 bg-ui-line hover:bg-ui-accent transition-colors cursor-col-resize flex-shrink-0" />

            <Panel defaultSize="50%" minSize="20%">
              <div className="h-full flex flex-col border-l border-ui-line">
                {/* Sekme başlıkları */}
                <div className="min-h-10 flex items-center border-b border-ui-line bg-ui-card px-2 flex-shrink-0">
                  <div role="group" aria-label="Önizleme görünümü" className="flex items-center gap-0.5 rounded-lg bg-ui-hover p-0.5">
                    {(['preview', 'xml'] as const).map(t => (
                      <button
                        key={t}
                        type="button"
                        aria-pressed={rightTab === t}
                        onClick={() => setRightTab(t)}
                        className={`xc-focus h-7 rounded-md px-2.5 text-[12.5px] transition-colors ${
                          rightTab === t ? 'bg-ui-card font-semibold text-tx shadow-[var(--ui-shadow-rest)]' : 'font-medium text-tx-muted hover:text-tx'
                        }`}
                      >
                        {t === 'preview' ? 'Önizleme' : 'XML Kaynak'}
                      </button>
                    ))}
                  </div>
                </div>

                {/* Sekme içerikleri */}
                <div className="flex-1 overflow-hidden">
                  {rightTab === 'preview' ? (
                    <XsltEditorPreview html={html} />
                  ) : (
                    <Editor
                      height="100%"
                      language="xml"
                      beforeMount={defineMonacoThemes}
                      theme={monacoThemeId(editorTheme)}
                      value={xmlContent ?? ''}
                      options={{
                        readOnly: true,
                        minimap: { enabled: false },
                        fontSize: editorFontSize,
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
            <aside aria-label="Tema varlıkları" className="flex-shrink-0 border-l border-ui-line bg-ui-card flex flex-col overflow-y-auto" style={{ width: 300 }}>
              <div className="p-4 flex flex-col gap-6">
                <ImagePanel
                  label="Logo" state={logo} uploading={logoUploading} slots={slots}
                  slotValue={effectiveSlot('logo', logo, slots)}
                  onUpload={(e) => handleImageUpload(e, setLogo, setLogoUploading)}
                  onChange={(patch) => patchImage(setLogo, patch)}
                  onRemove={() => patchImage(setLogo, { dataUrl: '' })}
                  themeImageRemovable={themeImageRemovable('logo', slots)}
                />

                <div className="border-t border-ui-line" />

                <ImagePanel
                  label="İmza / Kaşe" state={signature} uploading={signatureUploading} slots={slots}
                  slotValue={effectiveSlot('signature', signature, slots)}
                  onUpload={(e) => handleImageUpload(e, setSignature, setSignatureUploading)}
                  onChange={(patch) => patchImage(setSignature, patch)}
                  onRemove={() => patchImage(setSignature, { dataUrl: '' })}
                  themeImageRemovable={themeImageRemovable('signature', slots)}
                />

                <div className="border-t border-ui-line" />

                <BankInfoPanel rows={bankInfo} onChange={(rows) => { setBankInfo(rows); setAssetsTouched(true) }} />

                <div className="border-t border-ui-line pt-2">
                  <p className="text-xs text-tx-subtle leading-relaxed">
                    Düzenlemeler XSLT editörüne <code className="bg-ui-side px-1 rounded">DEV-MODE-OVERRIDES</code> bloğu olarak yansır.
                  </p>
                </div>
              </div>
            </aside>
          )}
        </div>

        {showProblems && (
          <ProblemsPanel
            xsltProblems={[
              ...xsltErrors.map(e => ({
                source: 'xslt' as const,
                severity: 'error' as const,
                ruleName: 'XSLT Sözdizimi',
                message: e.message,
                line: e.line,
                column: e.column,
              })),
              ...compat.problems,
            ]}
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

      {xmlInput}
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
