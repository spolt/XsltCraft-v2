import { useEffect, useRef, useState, useCallback, useMemo } from 'react'
import { useParams, useNavigate, useLocation } from 'react-router-dom'
import { Panel, Group as PanelGroup, Separator as PanelResizeHandle, useGroupRef, usePanelRef, type Layout } from 'react-resizable-panels'
import { TriangleAlert, Save, Download, Sparkles, Eye } from 'lucide-react'
import { useAiStore } from '../store/aiStore'
import AiAssistantPanel, { type AiPrompt } from '../components/ai/AiAssistantPanel'
import AiRefactorDialog from '../components/ai/AiRefactorDialog'
import type { ProblemItem } from '../components/xslt-editor/ProblemsPanel'
import type { editor as MonacoEditor } from 'monaco-editor'
import XsltEditor from '../components/Xslteditor'
import XsltEditorToolbar from '../components/xslt-editor/XsltEditorToolbar'
import FloatingPreviewWindow from '../components/xslt-editor/FloatingPreviewWindow'
import PreviewDock, { type DockTab } from '../components/xslt-editor/PreviewDock'
import XsltSideMenu, { type SideMenuFile } from '../components/xslt-editor/XsltSideMenu'
import XsltStatusBar from '../components/xslt-editor/XsltStatusBar'
import XsltEmptyState from '../components/xslt-editor/XsltEmptyState'
import {
  layoutName,
  MAIN_PANE,
  RIGHT_PANE,
  EDITOR_PANE,
  PREVIEW_PANE,
  SPLIT_LAYOUTS,
  separatorCls,
} from '../components/xslt-editor/editorLayouts'
import {
  aiPromptForProblem,
  LEAVE_CONFIRM,
  looksMinified,
  formatXmlForDisplay,
  stylesheetVersion,
  formatSize,
  replaceSelectionText,
  snippetPlainText,
} from '../components/xslt-editor/xsltText'
import { printActivePreview } from '../components/xslt-editor/previewFrame'
import { useEditorMaximize } from '../hooks/useEditorMaximize'
import SaveTemplateDialog from '../components/xslt-editor/SaveTemplateDialog'
import ProblemsPanel from '../components/xslt-editor/ProblemsPanel'
import { useRawPreview } from '../hooks/useRawPreview'
import { useXsltDiagnostics } from '../hooks/useXsltDiagnostics'
import SnippetManagerDialog from '../components/xslt-editor/SnippetManagerDialog'
import ShortcutsDialog from '../components/xslt-editor/ShortcutsDialog'
import WorkspaceHeader from '../components/shell/WorkspaceHeader'
import { HeaderButton, HeaderChip, HeaderStatus } from '../components/shell/headerParts'
import SegmentedTabs from '../components/ui/SegmentedTabs'
import { listSnippets, type UserSnippet } from '../services/snippetService'
import { reportDownload } from '../services/activityService'
import {
  getUserXsltTemplate,
  createUserXsltTemplate,
  updateUserXsltTemplate,
  acquireEditingLock,
  releaseEditingLock,
} from '../services/userXsltService'
import { toast } from '../store/toastStore'
import { readUploadFile, UPLOAD_ACCEPT } from '../utils/uploadValidation'
import { useEntitlementStore } from '../store/entitlementStore'
import { openUpgradeModal } from '../store/upgradeModalStore'
import { useUiPrefsStore, type EditorLayoutId } from '../store/uiPrefsStore'

const LOCK_HEARTBEAT_MS = 30_000

/** Sekmeli sağ sütunda "yarı yarıya" düzeninin genişliği (%). */
const TABS_HALF_SIZE = 50

const RIGHT_TABS = [
  { id: 'preview', label: 'Önizleme', icon: Eye },
  { id: 'ai', label: 'AI Asistan', icon: Sparkles },
] as const

export default function XsltEditorPage() {
  const { templateId: routeTemplateId } = useParams<{ templateId: string }>()
  const navigate = useNavigate()
  const location = useLocation()

  // Core state
  const [templateId, setTemplateId] = useState<string | null>(routeTemplateId ?? null)
  const [templateName, setTemplateName] = useState('')
  const [xsltContent, setXsltContent] = useState('')
  const [xmlContent, setXmlContent] = useState<string | null>(null)
  const [isDirty, setIsDirty] = useState(false)
  // Yalnız görüntüleme (sol menü / başlık): yüklenen dosyaların adları.
  const [xsltFileName, setXsltFileName] = useState<string | null>(null)
  const [xmlFileName, setXmlFileName] = useState<string | null>(null)

  // Eşzamanlı düzenleme kilidi
  const [lockedByOther, setLockedByOther] = useState(false)
  const [lockOwnerName, setLockOwnerName] = useState<string | null>(null)
  const loadedUpdatedAtRef = useRef<string | null>(null)
  const wasLockedRef = useRef(false)

  // Doğrulama + problemler ve önizleme (Geliştirici Modu ile ortak hook'lar)
  const diag = useXsltDiagnostics({ xslt: xsltContent, xml: xmlContent })
  const { validateXmlNow, setShowProblems } = diag
  const preview = useRawPreview({ xslt: xsltContent, xml: xmlContent, onLineError: diag.reportXsltError })

  // Save dialog
  const [showSaveDialog, setShowSaveDialog] = useState(false)
  const [saving, setSaving] = useState(false)

  // Snippets
  const [userSnippets, setUserSnippets] = useState<UserSnippet[]>([])
  const [showSnippetManager, setShowSnippetManager] = useState(false)

  // Shortcuts dialog
  const [showShortcuts, setShowShortcuts] = useState(false)

  // Önizleme paneli sekmesi: Önizleme | XML Kaynak
  const [rightTab, setRightTab] = useState<DockTab>('preview')
  // XML Kaynak görünümü biçimlendirilsin mi? Varsayılan açık; kullanıcı ham baytları
  // (ör. imza sindirimlerini birebir) görmek isterse kapatabilir.
  const [xmlPretty, setXmlPretty] = useState(true)

  // XML editor cursor / selection — AI bağlam kırpma için
  const [xmlCursorLine, setXmlCursorLine] = useState<number | undefined>()
  const [xmlSelection, setXmlSelection] = useState<string | undefined>()

  // XSLT editor cursor / selection — AI template alaka skoru için
  const [xsltCursorLine, setXsltCursorLine] = useState<number | undefined>()
  const [xsltCursorCol, setXsltCursorCol] = useState<number | undefined>()
  const [xsltSelection, setXsltSelection] = useState<string | undefined>()

  // AI paneli (sağda ayrı sütun)
  const aiEnabled = useAiStore(s => s.enabled === true)
  const refreshAi = useAiStore(s => s.refresh)
  // Dışarıdan tetiklenen AI sorusu. Panel remount EDİLMEZ — bkz. askAi().
  const [aiPrompt, setAiPrompt] = useState<AiPrompt | null>(null)
  const aiNonceRef = useRef(0)
  // AI paneli ilk açılışta mount olur; sonrasında hep mount kalır (kapatılsa da yalnız
  // gizlenir), böylece düzen/sekme değişince sohbet yok olmaz.
  const [aiMounted, setAiMounted] = useState(false)
  const [aiOpen, setAiOpen] = useState(false)
  const [refactorState, setRefactorState] = useState<{
    selection: string
    range: { startLine: number; endLine: number; startColumn: number }
  } | null>(null)

  // Tam ekran editör: editör bölmesi panel grubunu kaplar, önizleme kodun üzerinde
  // yüzen mini pencereye taşınır. Önizleme paneli mount kalır (yalnız görünmez olur).
  // Kısayol yalnız editör ekrandayken ve üzerinde diyalog yokken etkindir.
  const max = useEditorMaximize({
    shortcutEnabled: !!xsltContent && !!xmlContent
      && !showSaveDialog && !showShortcuts && !showSnippetManager && !refactorState,
  })
  const { enter: maxEnter, exit: maxExit, setMiniOpen } = max
  const editorHostRef = useRef<HTMLDivElement>(null)
  const focusEditorRef = useRef<(() => void) | null>(null)

  // ─── Düzen modları ──────────────────────────────────────────────────────────
  // Mod, tam ekran durumundan türetilir: tam ekran + mini → 'mini', tam ekran → 'editorOnly',
  // değilse yan panel / yarı yarıya. Monaco düğümü hiçbir modda ağaçta yer değiştirmez.
  const [splitMode, setSplitMode] = useState<'side' | 'half'>(
    () => (useUiPrefsStore.getState().editorDefaultLayout === 'half' ? 'half' : 'side'),
  )
  const groupRef = useGroupRef()
  const layoutMode: EditorLayoutId = max.maximized ? (max.miniOpen ? 'mini' : 'editorOnly') : splitMode

  // ─── Sağ panel modu (Ayarlar): yan yana | sekmeli ───────────────────────────
  // Sekmeli modda Önizleme ve AI aynı sağ sütunda sekmedir; iç gruptaki önizleme bölmesi
  // daralır (0). AI kapalıyken (veya AI özelliği yoksa) yan yana davranışı geçerlidir.
  const rightMode = useUiPrefsStore(s => s.xsltRightPanelMode)
  const tabsActive = rightMode === 'tabs' && aiEnabled
  const previewPanelRef = usePanelRef()
  const rightPanelRef = usePanelRef()
  // Sağ sütun genişliği: yan yana → AI açıksa; sekmeli → tam ekran dışında hep (önizleme
  // sekmesi), tam ekranda yalnız AI açıksa (önizleme o sırada mini penceredir).
  const rightExpanded = tabsActive ? (!max.maximized || aiOpen) : (aiEnabled && aiOpen)
  // Sekmeli sütunda o an görünen içerik.
  const rightShowsPreview = tabsActive && !max.maximized && !aiOpen

  const applyLayout = useCallback((mode: EditorLayoutId) => {
    if (mode === 'side' || mode === 'half') {
      maxExit()
      setSplitMode(mode)
      // Sekmeli modda iç gruptaki önizleme bölmesi kapalıdır; genişliği sağ sütun efekti uygular.
      if (!tabsActive) groupRef.current?.setLayout(SPLIT_LAYOUTS[mode])
    } else if (mode === 'mini') {
      maxEnter({ withMini: true })
    } else {
      maxEnter()
      setMiniOpen(false)
    }
  }, [maxEnter, maxExit, setMiniOpen, groupRef, tabsActive])

  // Varsayılan düzen, iki dosya ilk kez birlikte yüklendiğinde BİR KEZ uygulanır.
  const workspaceReady = !!xsltContent && !!xmlContent
  const defaultLayoutAppliedRef = useRef(false)
  useEffect(() => {
    if (!workspaceReady || defaultLayoutAppliedRef.current) return
    defaultLayoutAppliedRef.current = true
    applyLayout(useUiPrefsStore.getState().editorDefaultLayout)
  }, [workspaceReady, applyLayout])

  // İç gruptaki önizleme bölmesi: sekmeli modda daralır, yan yana modda düzen moduna döner.
  // Emperatif çağrılar bir kare ertelenir: panel kısıtları (collapsible) kütüphane deposuna
  // render sonrası işlenir; aynı karede çağrılırsa eski kısıtlarla doğrulanıp yok sayılır.
  const prevTabsActiveRef = useRef(tabsActive)
  useEffect(() => {
    if (!workspaceReady) return
    const leavingTabs = prevTabsActiveRef.current && !tabsActive
    prevTabsActiveRef.current = tabsActive
    const raf = requestAnimationFrame(() => {
      const panel = previewPanelRef.current
      if (!panel) return
      if (tabsActive) {
        if (!panel.isCollapsed()) panel.collapse()
      } else if (leavingTabs || panel.getSize().asPercentage < 1) {
        groupRef.current?.setLayout(SPLIT_LAYOUTS[splitMode])
      }
    })
    return () => cancelAnimationFrame(raf)
  }, [tabsActive, workspaceReady, splitMode, previewPanelRef, groupRef])

  // Sağ sütun: açılınca kullanıcının son ayarladığı genişliğe (uiPrefsStore) gelir, kapanınca 0.
  useEffect(() => {
    if (!workspaceReady) return
    const raf = requestAnimationFrame(() => {
      const panel = rightPanelRef.current
      if (!panel) return
      if (rightExpanded) {
        const prefs = useUiPrefsStore.getState()
        const size = tabsActive
          ? (splitMode === 'half' ? TABS_HALF_SIZE : prefs.xsltTabsPanelSize)
          : prefs.xsltAiPanelSize
        panel.resize(`${size}%`)
      } else if (!panel.isCollapsed()) {
        panel.collapse()
      }
    })
    return () => cancelAnimationFrame(raf)
  }, [rightExpanded, tabsActive, splitMode, workspaceReady, rightPanelRef])

  // Kullanıcı ayırıcıyı bırakınca (fare ya da klavye) sağ sütun genişliği kalıcı tercihe yazılır.
  // Programatik değişiklikler (açılış, kapanış, mod geçişi, kısıt kırpması) yazılmaz — yalnız
  // ayırıcı odaktayken gelen düzen değişikliği kullanıcınındır.
  const rightSeparatorRef = useRef<HTMLDivElement>(null)
  const handleOuterLayoutChanged = useCallback((layout: Layout) => {
    if (!rightSeparatorRef.current || document.activeElement !== rightSeparatorRef.current) return
    const size = layout[RIGHT_PANE]
    if (!size || size < 1) return
    const prefs = useUiPrefsStore.getState()
    if (tabsActive) {
      if (splitMode !== 'half') prefs.setXsltTabsPanelSize(size)
    } else if (aiOpen) {
      prefs.setXsltAiPanelSize(size)
    }
  }, [tabsActive, splitMode, aiOpen])

  // Moda girince odağı editöre al (örtülen önizleme panelinde ya da tıklanan butonda kalmasın).
  useEffect(() => {
    if (max.maximized) focusEditorRef.current?.()
  }, [max.maximized])

  useEffect(() => { refreshAi() }, [refreshAi])

  function handleAiRefactor(selection: string, range: { startLine: number; endLine: number; startColumn: number }) {
    setRefactorState({ selection, range })
  }

  function applyRefactor(newText: string) {
    if (!refactorState) return
    const next = replaceSelectionText(xsltContent, refactorState.selection, refactorState.range, newText)
    setRefactorState(null)
    if (next === null) {
      toast.warning('Seçili bölge bu arada değişti; öneri uygulanmadı. Bölgeyi yeniden seçip deneyin.', { title: 'AI refactor' })
      return
    }
    setXsltContent(next)
    setIsDirty(true)
  }

  // AI sohbetinden gelen değişikliği uygula: hesaplanmış TAM doküman doğrudan yazılır.
  // Controlled value → @monaco-editor/react executeEdits ile tek adımlık geri-alınabilir edit.
  function applyAiChange(newDoc: string) {
    setXsltContent(newDoc)
    setIsDirty(true)
  }

  /**
   * AI panelini açar ve soruyu kuyruğa koyar. Panel REMOUNT EDİLMEZ — mevcut sohbet
   * korunur. AI ayrı sütunda olduğundan tam ekran/mini düzeninde de görünür kalır.
   * Nonce sayacı ref: aynı soru iki kez sorulursa metin özdeş olur, artan sayaç
   * ikinci tetiklemeyi garantiler.
   */
  function askAi(text: string) {
    setAiPrompt({ nonce: ++aiNonceRef.current, text })
    setAiMounted(true)
    setAiOpen(true)
  }

  function openAiForProblem(problem: ProblemItem) {
    askAi(aiPromptForProblem(problem, xsltContent, xmlContent))
  }

  // Düz AI açılışı: yalnızca paneli göster, soru gönderme; mevcut sohbet korunur.
  // Sohbeti kullanıcı "Yeni sohbet" düğmesiyle kendisi sıfırlar.
  function openAiBlank() {
    setAiMounted(true)
    setAiOpen(true)
  }

  function toggleAi() {
    if (aiOpen) setAiOpen(false)
    else openAiBlank()
  }

  // Loading from saved template
  const [loadingTemplate, setLoadingTemplate] = useState(!!routeTemplateId)

  // Refs
  const goToRef = useRef<((term: string) => void) | null>(null)
  const revealLineRef = useRef<((lineNumber: number, column?: number) => void) | null>(null)
  const insertTextAtLineRef = useRef<((lineNumber: number, text: string) => void) | null>(null)
  const toggleCommentRef = useRef<(() => void) | null>(null)
  const formatDocumentRef = useRef<(() => void) | null>(null)
  // Sayfada TEK XSLT ve TEK XML gizli input'u vardır; tüm yükleme butonları bunları tetikler.
  const xsltInputRef = useRef<HTMLInputElement>(null)
  const xmlInputRef = useRef<HTMLInputElement>(null)
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
        if (lockedByOther) {
          toast.warning(
            `Şu an ${lockOwnerName ?? 'başka bir kullanıcı'} bu şablonu düzenliyor. Salt-okunur modda kaydedemezsiniz.`,
            { title: 'Şablon kilitli' },
          )
        } else {
          setShowSaveDialog(true)
        }
      }
    }
    document.addEventListener('keydown', handleKeyDown)
    return () => document.removeEventListener('keydown', handleKeyDown)
  }, [xsltContent, lockedByOther, lockOwnerName])

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
        loadedUpdatedAtRef.current = t.updatedAt
        setIsDirty(false)
      })
      .catch(() => navigate('/xslt-editor'))
      .finally(() => setLoadingTemplate(false))
  }, [routeTemplateId, navigate])

  // ─── Eşzamanlı düzenleme kilidi: kilit al + heartbeat ───────────────────────
  useEffect(() => {
    if (!templateId) return
    let cancelled = false

    async function tick() {
      try {
        const lock = await acquireEditingLock(templateId!)
        if (cancelled) return
        setLockedByOther(lock.lockedByOther)
        setLockOwnerName(lock.editingUserName)
        // Yalnızca kilitlenme anında (yükselen kenar) uyarı göster
        if (lock.lockedByOther && !wasLockedRef.current) {
          toast.warning(
            `Şu an ${lock.editingUserName ?? 'başka bir kullanıcı'} bu şablonu düzenliyor. Salt-okunur açıldı.`,
            { title: 'Şablon kilitli', durationMs: 6000 },
          )
        }
        wasLockedRef.current = lock.lockedByOther
      } catch {
        // sessizce yoksay (yetki/ağ hatası)
      }
    }

    tick()
    const intervalId = setInterval(tick, LOCK_HEARTBEAT_MS)
    return () => {
      cancelled = true
      clearInterval(intervalId)
      releaseEditingLock(templateId!).catch(() => {})
      wasLockedRef.current = false
    }
  }, [templateId])

  // ─── File upload handlers ───────────────────────────────────────────────────
  /** XSLT dosyasını doğrulayıp editöre yükler (input ve Dashboard'dan gelen dosya ortak yolu). */
  const loadXsltFile = useCallback(async (file: File) => {
    try {
      const content = await readUploadFile(file, 'xslt')
      setXsltContent(content)
      setIsDirty(true)
      setXsltFileName(file.name)
      setTemplateName(name => name || file.name.replace(/\.(xsl|xslt)$/i, ''))
    } catch (err) {
      toast.error((err as Error).message, { title: 'XSLT yüklenemedi' })
    }
  }, [])

  async function handleXsltFile(e: React.ChangeEvent<HTMLInputElement>) {
    const file = e.target.files?.[0]
    e.target.value = ''
    if (!file) return
    await loadXsltFile(file)
  }

  /** XML dosyasını doğrulayıp editöre yükler (input ve Dashboard'dan gelen dosya ortak yolu). */
  const loadXmlFile = useCallback(async (file: File) => {
    try {
      const content = await readUploadFile(file, 'xml')
      // Debounce'u beklemez: minify XML'in biçimli görünümü xmlValid'e bağlı, gecikmesin.
      validateXmlNow(content)
      setXmlContent(content)
      setXmlFileName(file.name)
    } catch (err) {
      toast.error((err as Error).message, { title: 'XML yüklenemedi' })
    }
  }, [validateXmlNow])

  async function handleXmlFile(e: React.ChangeEvent<HTMLInputElement>) {
    const file = e.target.files?.[0]
    e.target.value = ''
    if (!file) return
    await loadXmlFile(file)
  }

  // ─── Dashboard'dan gelen dosyalar (location.state.pendingXsltFile / pendingXmlFile) ─
  const pendingState = location.state as { pendingXsltFile?: unknown; pendingXmlFile?: unknown } | null
  const pendingXsltFile = pendingState?.pendingXsltFile instanceof File ? pendingState.pendingXsltFile : null
  const pendingXmlFile = pendingState?.pendingXmlFile instanceof File ? pendingState.pendingXmlFile : null
  const handledPendingFilesRef = useRef<Set<File>>(new Set())
  useEffect(() => {
    const handled = handledPendingFilesRef.current
    const fresh = [pendingXsltFile, pendingXmlFile].filter((f): f is File => f !== null && !handled.has(f))
    if (fresh.length === 0) return
    fresh.forEach(f => handled.add(f))
    if (pendingXsltFile && fresh.includes(pendingXsltFile)) void loadXsltFile(pendingXsltFile)
    if (pendingXmlFile && fresh.includes(pendingXmlFile)) void loadXmlFile(pendingXmlFile)
    // State temizlenir: yenilemede / geri gelişte dosya ikinci kez yüklenmesin.
    navigate(location.pathname, { replace: true, state: null })
  }, [pendingXsltFile, pendingXmlFile, loadXsltFile, loadXmlFile, navigate, location.pathname])

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

  // ─── Snippet ekleme (sol menü) ──────────────────────────────────────────────
  function handleInsertSnippet(snippet: UserSnippet) {
    if (lockedByOther) {
      toast.warning('Salt-okunur moddasınız; snippet eklenemez.', { title: 'Şablon kilitli' })
      return
    }
    insertTextAtLineRef.current?.(xsltCursorLine ?? 1, snippetPlainText(snippet.body))
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
    reportDownload('Xslt', templateId)
  }

  // ─── Save ───────────────────────────────────────────────────────────────────
  async function handleSave(name: string) {
    setSaving(true)
    try {
      if (templateId) {
        const updated = await updateUserXsltTemplate(templateId, {
          name,
          xsltContent,
          xmlContent: xmlContent ?? undefined,
          expectedUpdatedAt: loadedUpdatedAtRef.current ?? undefined,
        })
        setTemplateName(name)
        loadedUpdatedAtRef.current = updated.updatedAt
      } else {
        const result = await createUserXsltTemplate({
          name,
          xsltContent,
          xmlContent: xmlContent ?? undefined,
        })
        setTemplateId(result.id)
        setTemplateName(result.name)
        loadedUpdatedAtRef.current = result.updatedAt
        // Update URL without reload
        window.history.replaceState(null, '', `/xslt-editor/${result.id}`)
      }
      setIsDirty(false)
      setShowSaveDialog(false)
      toast.success('Şablon kaydedildi.', { durationMs: 2500 })
    } catch (err: unknown) {
      const status = (err as { response?: { status?: number } })?.response?.status
      if (status === 402) {
        // Standart kullanıcı saklama yapamaz → Pro'ya yönlendir.
        setShowSaveDialog(false)
        openUpgradeModal({
          title: 'Kaydetme Pro üyelik gerektirir',
          message: "'Şablonlarım'da saklama XsltCraft Pro’ya dahildir.",
        })
      } else if (status === 423) {
        // Başka kullanıcı düzenliyor — salt-okunura geç
        setLockedByOther(true)
        wasLockedRef.current = true
        setShowSaveDialog(false)
        toast.error('Bu şablonu şu an başka bir kullanıcı düzenliyor. Kaydedilemedi.', {
          title: 'Şablon kilitli', durationMs: null,
        })
      } else if (status === 409) {
        setShowSaveDialog(false)
        toast.error('Şablon siz düzenlerken değiştirildi. Lütfen sayfayı yenileyip tekrar deneyin.', {
          title: 'Çakışma', durationMs: null,
        })
      } else {
        toast.error('Kayıt sırasında bir hata oluştu.')
      }
    } finally {
      setSaving(false)
    }
  }

  // Kilitliyken kaydetmeyi engelle, kullanıcıyı uyar
  function requestSave() {
    if (lockedByOther) {
      toast.warning(
        `Şu an ${lockOwnerName ?? 'başka bir kullanıcı'} bu şablonu düzenliyor. Salt-okunur modda kaydedemezsiniz.`,
        { title: 'Şablon kilitli' },
      )
      return
    }
    // Ham XSLT'yi "Şablonlarım"a saklamak Pro üyelik gerektirir (Standart kullanıcı indirebilir ama saklayamaz).
    const ent = useEntitlementStore.getState().entitlements
    if (ent && !ent.canSaveRawXslt && !ent.isPrivileged) {
      openUpgradeModal({
        title: 'Kaydetme Pro üyelik gerektirir',
        message: "XSLT’yi indirebilirsin, ancak 'Şablonlarım'da saklamak XsltCraft Pro’ya dahildir.",
      })
      return
    }
    setShowSaveDialog(true)
  }

  // ─── Gezinme koruması (header sekmeleri, logo, avatar menüsü, boş durum linki) ─
  const confirmLeave = useCallback(() => !isDirty || window.confirm(LEAVE_CONFIRM), [isDirty])

  const guardedNavigate = useCallback((path: string) => {
    if (!confirmLeave()) return
    navigate(path)
  }, [confirmLeave, navigate])

  // ─── Preview → Editor navigation ───────────────────────────────────────────
  const handlePreviewClick = useCallback((text: string, opts?: { exact?: boolean }) => {
    if (!goToRef.current) return
    // Görsel tıklamaları: base64 imzasını birebir arat (kısaltma yapma)
    if (opts?.exact) {
      goToRef.current(text)
      return
    }
    // Try to find a meaningful short snippet to search for
    const searchTerm = text.length > 50 ? text.substring(0, 50) : text
    goToRef.current(searchTerm)
  }, [])

  // XML Kaynak sekmesinde gösterilecek metin. Yalnızca minify edilmiş belgeler
  // biçimlendirilir: zaten satırlara yayılmış bir XML'e dokunmayınca problem panelinden
  // "satıra git" ve AI'ya giden imleç-penceresi ham metinle birebir hizalı kalır.
  // Yalnızca well-formed belgeler biçimlendirilir: xml-formatter bozuk XML'de hata
  // fırlatmak yerine eksik kapanış etiketlerini KENDİSİ ekliyor — kullanıcıya dosyasında
  // olmayan içerik göstermek, tam da XML'i incelemeye çalışırken yanıltıcı olurdu.
  const xmlDisplay = useMemo(() => {
    if (!xmlContent) return ''
    if (!xmlPretty || diag.xmlValid !== true || !looksMinified(xmlContent)) return xmlContent
    return formatXmlForDisplay(xmlContent)
  }, [xmlContent, xmlPretty, diag.xmlValid])

  // Görüntülenen metin ham metinden farklıysa satır numaraları da kayar; kullanıcıyı uyar.
  const xmlDisplayReformatted = xmlContent != null && xmlDisplay !== xmlContent

  // Biçimli görünümde imleç satırı ham XML ile farklı koordinat uzayındadır; AI bağlamı
  // ham metni bu satıra göre pencerelediğinden yanlış (hatta boş) pencere üretirdi.
  const aiXmlCursorLine = xmlDisplayReformatted ? undefined : xmlCursorLine

  // Önizleme panelini görünür yapar: tam ekrandan çıkar; sekmeli modda Önizleme sekmesine geçer
  // (AI yalnız gizlenir, sohbet korunur).
  function showPreviewPane() {
    maxExit()
    if (tabsActive) setAiOpen(false)
  }

  // XML Kaynak'ta ilgili satıra git (UBL-TR / XML problemi). Önizleme paneli tam ekranda
  // örtülü olduğundan önce yan düzene dönülür.
  function handleLocateXmlLine(line: number, column?: number | null) {
    showPreviewPane()
    setRightTab('xml')
    // Problem satır/kolon numaraları HAM xmlContent üzerinden hesaplanır. Biçimlendirilmiş
    // görünümde satırlar kaydığı için hedefi ıskalardık — ham görünüme dönerek konumun
    // birebir doğru kalmasını sağlıyoruz.
    if (xmlDisplayReformatted) setXmlPretty(false)
    setTimeout(() => {
      if (xmlEditorRef.current) {
        xmlEditorRef.current.revealLineInCenter(line)
        xmlEditorRef.current.setPosition({ lineNumber: line, column: column ?? 1 })
        xmlEditorRef.current.focus()
      }
    }, 100)
  }

  function handleXmlEditorMount(editor: MonacoEditor.IStandaloneCodeEditor) {
    xmlEditorRef.current = editor
    editor.onDidChangeCursorPosition(e => {
      setXmlCursorLine(e.position.lineNumber)
    })
    editor.onDidChangeCursorSelection(e => {
      const sel = editor.getModel()?.getValueInRange(e.selection) ?? ''
      setXmlSelection(sel.trim() || undefined)
    })
  }

  function openXmlSource() {
    showPreviewPane()
    setRightTab('xml')
  }

  // ─── Print ──────────────────────────────────────────────────────────────────
  function handlePrint() {
    if (!printActivePreview()) {
      toast.warning('Yazdırmak için önizleme açık olmalı (Önizleme sekmesi veya mini pencere).', { title: 'Önizleme kapalı' })
    }
  }

  // ─── Kabuk parçaları ────────────────────────────────────────────────────────
  const version = useMemo(() => stylesheetVersion(xsltContent), [xsltContent])
  const xsltDisplayName = xsltFileName ?? (templateName ? `${templateName}.xslt` : 'şablon.xslt')
  const xmlDisplayName = xmlFileName ?? 'belge.xml'
  const xsltSize = useMemo(() => (xsltContent ? formatSize(xsltContent) : ''), [xsltContent])
  const xmlSize = useMemo(() => (xmlContent ? formatSize(xmlContent) : ''), [xmlContent])
  const sideXslt: SideMenuFile | null = xsltContent ? { name: xsltDisplayName, size: xsltSize, dirty: isDirty } : null
  const sideXml: SideMenuFile | null = xmlContent ? { name: xmlDisplayName, size: xmlSize } : null
  const xsltEol = useMemo(() => (xsltContent.includes('\r\n') ? 'CRLF' : 'LF'), [xsltContent])

  const header = (
    <WorkspaceHeader
      active="xslt"
      onNavigate={guardedNavigate}
      confirmLeave={confirmLeave}
      center={xsltContent ? (
        <>
          <span className="max-w-[260px] truncate font-mono font-medium">{templateName || xsltDisplayName}</span>
          {version && <HeaderChip>XSLT {version}</HeaderChip>}
          {lockedByOther && <HeaderChip>Salt-okunur</HeaderChip>}
          {isDirty && <HeaderStatus>Kaydedilmemiş değişiklik</HeaderStatus>}
        </>
      ) : null}
      actions={(
        <>
          <HeaderButton icon={Save} onClick={requestSave} disabled={!xsltContent}>Şablonlarıma kaydet</HeaderButton>
          <HeaderButton variant="primary" icon={Download} onClick={handleDownload} disabled={!xsltContent}>İndir</HeaderButton>
        </>
      )}
    />
  )

  const sideMenu = (
    <XsltSideMenu
      xsltFile={sideXslt}
      xmlFile={sideXml}
      onUploadXslt={() => xsltInputRef.current?.click()}
      onUploadXml={() => xmlInputRef.current?.click()}
      onOpenXslt={workspaceReady ? () => focusEditorRef.current?.() : undefined}
      onOpenXml={workspaceReady ? openXmlSource : undefined}
      snippets={userSnippets}
      onInsertSnippet={workspaceReady ? handleInsertSnippet : undefined}
      onManageSnippets={() => setShowSnippetManager(true)}
    />
  )

  let body
  if (loadingTemplate) {
    body = (
      <main className="flex flex-1 items-center justify-center">
        <span role="status" className="text-sm text-tx-muted">Şablon yükleniyor…</span>
      </main>
    )
  } else if (!workspaceReady) {
    // ─── Faz 1 / 1b: yükleme ekranı ──────────────────────────────────────────
    body = (
      <>
        {sideMenu}
        <XsltEmptyState
          hasXslt={!!xsltContent}
          hasXml={!!xmlContent}
          xsltName={xsltDisplayName}
          xmlName={xmlDisplayName}
          onUploadXslt={() => xsltInputRef.current?.click()}
          onUploadXml={() => xmlInputRef.current?.click()}
          onOpenSaved={() => guardedNavigate('/my-xslt-templates')}
        />
      </>
    )
  } else {
    // ─── Faz 2: editör + önizleme (+ AI) ─────────────────────────────────────
    body = (
      <>
        {sideMenu}

        <div className="flex min-w-0 flex-1 flex-col">
          {lockedByOther && (
            <div role="status" className="flex flex-none items-center gap-2 border-b border-fn-orange-line bg-fn-orange-tint px-4 py-2 text-xs text-fn-orange-ink">
              <TriangleAlert size={13} aria-hidden="true" className="flex-none" />
              <span>
                Şu an <strong>{lockOwnerName ?? 'başka bir kullanıcı'}</strong> bu şablonu düzenliyor —
                salt-okunur moddasınız, değişiklikleriniz kaydedilemez.
              </span>
            </div>
          )}

          {preview.error && (
            <div role="alert" className="flex flex-none items-start gap-2 border-b border-fn-red-line bg-fn-red-tint px-4 py-2 text-xs text-fn-red-ink">
              <TriangleAlert size={13} aria-hidden="true" className="mt-0.5 flex-none" />
              <span className="whitespace-pre-wrap break-all font-mono">{preview.error}</span>
            </div>
          )}

          <PanelGroup
            id="xslt-workspace-panels"
            orientation="horizontal"
            onLayoutChanged={handleOuterLayoutChanged}
            className="flex-1 overflow-hidden"
          >
            <Panel id={MAIN_PANE} minSize="30%">
          <PanelGroup
            id="xslt-editor-panels"
            groupRef={groupRef}
            orientation="horizontal"
            className="relative h-full overflow-hidden"
          >
            <Panel id={EDITOR_PANE} defaultSize={tabsActive ? '100%' : splitMode === 'half' ? '50%' : '65%'} minSize="25%">
              {/* Tam ekranda bu bölme panel grubunu kaplayan katmana dönüşür (grup `relative`).
                  Yalnız sınıf değişir: düğüm koşullu sarmalanır ya da farklı bir ağaca
                  çevrilirse Monaco remount olur (geri-al geçmişi, imleç, kaydırma kaybolur). */}
              <div className={max.maximized ? 'absolute inset-0 z-20 flex flex-col bg-ed-bg' : 'flex h-full flex-col bg-ed-bg'}>
                <XsltEditorToolbar
                  onFormat={() => formatDocumentRef.current?.()}
                  onToggleComment={() => toggleCommentRef.current?.()}
                  showProblems={diag.showProblems}
                  onToggleProblems={() => setShowProblems(s => !s)}
                  errorCount={diag.errorCount}
                  warningCount={diag.warningCount}
                  onValidateUblTr={diag.validateUblTr}
                  ublTrDisabled={!xmlContent || diag.ublTrLoading}
                  ublTrErrorCount={diag.ublTrErrorCount}
                  layout={layoutMode}
                  onLayoutChange={applyLayout}
                  maximized={max.maximized}
                  onToggleMaximize={max.toggle}
                  onShowMini={layoutMode === 'editorOnly' ? () => setMiniOpen(true) : undefined}
                  onPrint={handlePrint}
                  onShowShortcuts={() => setShowShortcuts(true)}
                  aiOpen={aiOpen}
                  onToggleAi={aiEnabled ? toggleAi : undefined}
                />
                <div ref={editorHostRef} className="relative flex-1 overflow-hidden">
                  <XsltEditor
                    value={xsltContent}
                    onChange={(v) => { setXsltContent(v); setIsDirty(true) }}
                    options={{ readOnly: lockedByOther }}
                    xmlContent={xmlContent}
                    userSnippets={userSnippets}
                    onEditorReady={({ goTo, revealLine, insertTextAtLine, toggleComment, formatDocument, focus }) => {
                      goToRef.current = goTo
                      revealLineRef.current = revealLine
                      insertTextAtLineRef.current = insertTextAtLine
                      toggleCommentRef.current = toggleComment
                      formatDocumentRef.current = formatDocument
                      focusEditorRef.current = focus
                    }}
                    onRequestImageInsert={handleRequestImageInsert}
                    errors={diag.editorMarkers}
                    aiEnabled={aiEnabled}
                    onAiRefactor={handleAiRefactor}
                    onCursorLineChange={(line, column) => { setXsltCursorLine(line); setXsltCursorCol(column) }}
                    onSelectionChange={setXsltSelection}
                  />
                  {max.showMini && (
                    <FloatingPreviewWindow
                      containerRef={editorHostRef}
                      html={preview.html}
                      status={preview.error ? 'error' : preview.loading ? 'loading' : 'idle'}
                      initialRect={max.miniRect}
                      onRectCommit={max.commitMiniRect}
                      onElementClick={handlePreviewClick}
                      onDock={() => { applyLayout('side'); showPreviewPane(); setRightTab('preview'); focusEditorRef.current?.() }}
                      onClose={() => { setMiniOpen(false); focusEditorRef.current?.() }}
                    />
                  )}
                </div>
                {diag.showProblems && (
                  <ProblemsPanel
                    xsltProblems={diag.xsltProblems}
                    xmlProblems={diag.xmlProblems}
                    ublTrProblems={diag.ublTrProblems}
                    ublTrLoading={diag.ublTrLoading}
                    ublTrError={diag.ublTrError}
                    onLocateXslt={(line, column) => revealLineRef.current?.(line, column ?? undefined)}
                    onLocateXml={handleLocateXmlLine}
                    onAskAi={openAiForProblem}
                    onClose={() => setShowProblems(false)}
                  />
                )}
              </div>
            </Panel>

            <PanelResizeHandle
              aria-label="Editör ile önizleme arasını boyutlandır"
              disabled={max.maximized || tabsActive}
              className={`${separatorCls} ${tabsActive ? 'w-0' : 'w-px'}`}
            />

            <Panel
              id={PREVIEW_PANE}
              panelRef={previewPanelRef}
              defaultSize={tabsActive ? '0%' : splitMode === 'half' ? '50%' : '35%'}
              minSize="20%"
              collapsible={tabsActive}
              collapsedSize="0%"
            >
              {/* Tam ekranda editör katmanının altında kalır: görünmez ve odaklanamaz, ama mount.
                  Sayfada aynı anda en fazla bir önizleme iframe'i bulunur (yazdırma onu hedefler):
                  buradaki yalnız yan yana modda ve tam ekran dışında, sekmeli sütundaki yalnız
                  Önizleme sekmesinde, mini pencere yalnız tam ekranda mount edilir. */}
              <div className={`h-full ${max.maximized ? 'invisible' : ''}`}>
                {!tabsActive && (
                  <PreviewDock
                    tab={rightTab}
                    onTabChange={setRightTab}
                    showFrame={!max.maximized}
                    html={preview.html}
                    onElementClick={handlePreviewClick}
                    previewLoading={preview.loading}
                    lastMs={preview.lastMs}
                    engine={preview.engine}
                    onRefresh={() => { if (xmlContent) preview.refresh() }}
                    onPopOut={() => applyLayout('mini')}
                    hasXml={!!xmlContent}
                    xmlDisplay={xmlDisplay}
                    xmlPretty={xmlPretty}
                    onToggleXmlPretty={() => setXmlPretty(p => !p)}
                    xmlDisplayReformatted={xmlDisplayReformatted}
                    onXmlEditorMount={handleXmlEditorMount}
                  />
                )}
              </div>
            </Panel>
          </PanelGroup>
            </Panel>

            <PanelResizeHandle
              elementRef={rightSeparatorRef}
              aria-label={tabsActive ? 'Sağ paneli boyutlandır' : 'AI panelini boyutlandır'}
              disabled={!rightExpanded}
              className={`${separatorCls} ${rightExpanded ? 'w-px' : 'w-0'}`}
            />

            {/* Sağ sütun: AI (yan yana) ya da Önizleme | AI sekmeleri (sekmeli). Kapalıyken 0'a
                daralır ama içerik mount kalır. AI paneli bu sütunda HEP aynı ağaç konumundadır
                (önündeki koşullu kardeşler yer tutar) — mod/sekme/boyut değişiminde sohbet kaybolmaz. */}
            <Panel
              id={RIGHT_PANE}
              panelRef={rightPanelRef}
              defaultSize={rightExpanded ? `${tabsActive ? useUiPrefsStore.getState().xsltTabsPanelSize : useUiPrefsStore.getState().xsltAiPanelSize}%` : '0%'}
              minSize="340px"
              maxSize="55%"
              collapsible={!rightExpanded}
              collapsedSize="0%"
            >
              <div className="flex h-full min-w-0 flex-col bg-ui-card">
                {tabsActive && !max.maximized && (
                  <SegmentedTabs
                    items={RIGHT_TABS}
                    value={aiOpen ? 'ai' : 'preview'}
                    onChange={(id) => (id === 'ai' ? openAiBlank() : setAiOpen(false))}
                    ariaLabel="Sağ panel"
                    variant="underline"
                    idPrefix="xslt-right"
                    className="h-[37px] flex-none gap-4 bg-ui-card px-3 [&_[role=tab]]:h-[36px] [&_[role=tab]]:text-[12.5px]"
                  />
                )}
                {rightShowsPreview && (
                  <div role="tabpanel" id="xslt-right-panel-preview" aria-labelledby="xslt-right-tab-preview" className="min-h-0 flex-1">
                    <PreviewDock
                      tab={rightTab}
                      onTabChange={setRightTab}
                      showFrame
                      html={preview.html}
                      onElementClick={handlePreviewClick}
                      previewLoading={preview.loading}
                      lastMs={preview.lastMs}
                      engine={preview.engine}
                      onRefresh={() => { if (xmlContent) preview.refresh() }}
                      onPopOut={() => applyLayout('mini')}
                      hasXml={!!xmlContent}
                      xmlDisplay={xmlDisplay}
                      xmlPretty={xmlPretty}
                      onToggleXmlPretty={() => setXmlPretty(p => !p)}
                      xmlDisplayReformatted={xmlDisplayReformatted}
                      onXmlEditorMount={handleXmlEditorMount}
                      hideTitle
                    />
                  </div>
                )}
                {/* AI Asistan: ilk açılışta mount olur, sonra kapatılsa da yalnız gizlenir. */}
                {aiEnabled && aiMounted && (
                  <aside
                    aria-label="AI Asistan"
                    role={tabsActive && !max.maximized ? 'tabpanel' : undefined}
                    id={tabsActive ? 'xslt-right-panel-ai' : undefined}
                    aria-labelledby={tabsActive && !max.maximized ? 'xslt-right-tab-ai' : undefined}
                    className={aiOpen ? 'flex min-h-0 flex-1 flex-col' : 'hidden'}
                  >
                    <AiAssistantPanel
                      xslt={xsltContent}
                      xml={xmlContent}
                      xmlCursorLine={aiXmlCursorLine}
                      xmlSelection={xmlSelection}
                      xsltCursorLine={xsltCursorLine}
                      xsltSelection={xsltSelection}
                      prompt={aiPrompt}
                      xmlDeclarationMissing={diag.declProblem != null}
                      onApplyXslt={applyAiChange}
                      onClose={() => setAiOpen(false)}
                    />
                  </aside>
                )}
              </div>
            </Panel>
          </PanelGroup>
        </div>
      </>
    )
  }

  return (
    <div className="flex h-screen flex-col overflow-hidden bg-ui-bg text-tx">
      {header}

      <div className="flex min-h-0 flex-1">{body}</div>

      {workspaceReady && !loadingTemplate && (
        <XsltStatusBar
          previewLoading={preview.loading}
          lastMs={preview.lastMs}
          lastTimings={preview.timings}
          engine={preview.engine}
          errorCount={diag.errorCount}
          warningCount={diag.warningCount}
          onToggleProblems={() => setShowProblems(s => !s)}
          cursorLine={xsltCursorLine}
          cursorColumn={xsltCursorCol}
          xsltVersion={version}
          eol={xsltEol}
          layoutLabel={tabsActive ? `${layoutName(layoutMode)} · sekmeli sağ panel` : layoutName(layoutMode)}
        />
      )}

      {/* Sayfadaki TEK XSLT ve TEK XML dosya girişi — tüm yükleme butonları bunları tetikler
          (aynı accept'li ikinci input Playwright strict mode'u bozar). */}
      <input ref={xsltInputRef} type="file" accept={UPLOAD_ACCEPT.xslt} className="hidden" onChange={handleXsltFile} />
      <input ref={xmlInputRef} type="file" accept={UPLOAD_ACCEPT.xml} className="hidden" onChange={handleXmlFile} />
      {/* Gizli görsel girişi — "Resim Ekle" bağlam menüsü için */}
      <input ref={imageFileInputRef} type="file" accept="image/*" className="hidden" onChange={handleImageFileSelect} />

      {showSnippetManager && (
        <SnippetManagerDialog
          onClose={() => setShowSnippetManager(false)}
          onSnippetsChanged={setUserSnippets}
        />
      )}

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
        <ShortcutsDialog onClose={() => setShowShortcuts(false)} showMaximize />
      )}

      {refactorState && (
        <AiRefactorDialog
          originalSelection={refactorState.selection}
          fullXslt={xsltContent}
          onAccept={applyRefactor}
          onClose={() => setRefactorState(null)}
        />
      )}
    </div>
  )
}
