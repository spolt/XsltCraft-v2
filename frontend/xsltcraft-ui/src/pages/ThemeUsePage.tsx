import { useCallback, useEffect, useMemo, useRef, useState } from 'react'
import { useParams, useNavigate, Link } from 'react-router-dom'
import {
  DndContext,
  DragOverlay,
  PointerSensor,
  TouchSensor,
  pointerWithin,
  useSensor,
  useSensors,
  type DragEndEvent,
  type DragStartEvent,
} from '@dnd-kit/core'
import {
  Printer,
  Code2,
  Download,
  BookmarkCheck,
  TriangleAlert,
  CircleX,
  Image as ImageIcon,
  Signature,
  Landmark,
  FileText,
  Upload,
  ScanEye,
  LoaderCircle,
  StickyNote,
} from 'lucide-react'
import { getTemplate, type ThemeSlotOption } from '../services/templateService'
import { reportDownload } from '../services/activityService'
import type { AssetKind } from '../services/themeComposeService'
import { createUserXsltTemplate, updateUserXsltTemplate } from '../services/userXsltService'
import { useEntitlementStore } from '../store/entitlementStore'
import { openUpgradeModal } from '../store/upgradeModalStore'
import { runDraftSave, useDraftField, useThemeDraftStore } from '../store/themeDraftStore'
import { parseGateError } from '../services/entitlementService'
import { toast } from '../store/toastStore'
import { readUploadFile, UPLOAD_ACCEPT } from '../utils/uploadValidation'
import { printActivePreview } from '../components/xslt-editor/previewFrame'
import { extractXmlParseErrors, LEAVE_CONFIRM, SAVE_CONFLICT_MESSAGE } from '../components/xslt-editor/xsltText'
import { documentTypeFromXslt } from '../components/dev-mode/draftCompose'
import { useThemeCompose } from '../hooks/useThemeCompose'
import ImagePanel from '../components/theme-assets/ImagePanel'
import BankInfoPanel from '../components/theme-assets/BankInfoPanel'
import FixedNotesPanel from '../components/theme-assets/FixedNotesPanel'
import {
  countNotes,
  noteHasText,
  toComposeNotes,
  type FixedNoteState,
} from '../components/theme-assets/fixedNoteState'
import type { FixedNotePosition } from '../services/themeComposeService'
import ThemeSlotPreview from '../components/theme-assets/ThemeSlotPreview'
import DraggableAssetChip, { AssetDragPreview } from '../components/theme-assets/DraggableAssetChip'
import { announcements, kindFromDragId, screenReaderInstructions, slotFromDropId } from '../components/theme-assets/slotDnd'
import {
  effectiveSlot,
  filledBankRows,
  readAsDataUrl,
  toComposeAssets,
  hiddenThemeImages,
  themeImageRemovable,
  validateImageFile,
} from '../components/theme-assets/imageState'
import WorkspaceHeader from '../components/shell/WorkspaceHeader'
import { HeaderButton } from '../components/shell/headerParts'
import AssetAccordionSection from '../components/theme-use/AssetAccordionSection'
import ThemeInfoAside from '../components/theme-use/ThemeInfoAside'
import { buildChecklist } from '../components/theme-use/themeChecklist'
import defaultInvoiceXml from '../assets/default-invoice.xml?raw'
import defaultDespatchXml from '../assets/default-despatch.xml?raw'

const NO_SLOTS: ThemeSlotOption[] = []

// ─── Main page ─────────────────────────────────────────────────────────────────
export default function ThemeUsePage() {
  const { templateId } = useParams<{ templateId: string }>()
  const navigate = useNavigate()

  const [meta, setMeta] = useDraftField('meta')
  const templateName = meta?.name ?? 'Hazır Tema'
  const slots = meta?.slots ?? NO_SLOTS
  // Geliştirici Modu'nda elle düzenlenen metin varsa önizleme ve indirme onun üzerinden yürür.
  const workingXslt = useThemeDraftStore((s) => s.workingXslt)
  const docType = meta?.documentType ?? (workingXslt !== null ? documentTypeFromXslt(workingXslt) : null)
  const [xml, setXml] = useDraftField('xml')
  const xmlContent = xml?.content ?? (docType === 'Despatch' ? defaultDespatchXml : defaultInvoiceXml)
  /** Yalnız görüntüleme: kullanıcının yüklediği XML'in adı (null → örnek XML). */
  const xmlFileName = xml?.fileName ?? null
  /** Önizlemede yerleşim alanlarını (yuvaları) göster. */
  const [showFields, setShowFields] = useState(true)
  const [openSections, setOpenSections] = useState({ logo: true, signature: false, bank: false, notes: false })
  const xmlInputRef = useRef<HTMLInputElement>(null)

  const [logo, setLogo] = useDraftField('logo')
  const [signature, setSignature] = useDraftField('signature')
  const [bankInfo, setBankInfo] = useDraftField('bankInfo')
  const [fixedNotes, setFixedNotes] = useDraftField('fixedNotes')
  const notesTouched = useThemeDraftStore((s) => s.notesTouched)
  const [fixedNotesSupported, setFixedNotesSupported] = useDraftField('fixedNotesSupported')
  const [uploading, setUploading] = useState<AssetKind | null>(null)

  const [isDownloading, setIsDownloading] = useState(false)
  const [isSaving, setIsSaving] = useState(false)
  const [saveSuccess, setSaveSuccess] = useState(false)
  const [dragKind, setDragKind] = useState<AssetKind | null>(null)
  const mountedRef = useRef(true)
  const saveRedirectRef = useRef<ReturnType<typeof setTimeout> | null>(null)

  useEffect(() => {
    mountedRef.current = true
    return () => {
      mountedRef.current = false
      if (saveRedirectRef.current !== null) clearTimeout(saveRedirectRef.current)
    }
  }, [])

  // Tema adı, belge türü ve konum kataloğu (Geliştirici Modu'ndan dönüşte taslakta hazırdır)
  useEffect(() => {
    if (!templateId || useThemeDraftStore.getState().meta) return
    getTemplate(templateId)
      .then((t) => setMeta({ name: t.name, slots: t.slots ?? [], documentType: t.documentType }))
      .catch(() => {})
  }, [templateId, setMeta])

  const baselineXslt = useThemeDraftStore((s) => s.baselineXslt)
  const xsltEdited = useThemeDraftStore((s) => s.xsltEdited)
  const editedXslt = xsltEdited ? workingXslt : null
  const editedXsltBroken = useMemo(
    () => editedXslt !== null && extractXmlParseErrors(editedXslt).length > 0,
    [editedXslt],
  )

  const assets = useMemo(() => toComposeAssets(logo, signature, slots), [logo, signature, slots])
  const banks = useMemo(() => filledBankRows(bankInfo), [bankInfo])
  // Düzenlenmiş metinde: dokunulmadıysa null (nota dokunma), boşaltıldıysa [] (eski notları sil).
  const composeNotes = useMemo(() => {
    if (editedXslt === null) return toComposeNotes(fixedNotes)
    return notesTouched ? (toComposeNotes(fixedNotes) ?? []) : null
  }, [editedXslt, fixedNotes, notesTouched])
  const noteCount = countNotes(fixedNotes)

  // Yalnız varlık girilmişse sormaz: bunlar temadan yeniden birleştirilebilir, elle düzenleme birleştirilemez.
  const hasUnsavedXslt = xsltEdited && workingXslt !== baselineXslt
  const confirmLeave = useCallback(() => !hasUnsavedXslt || window.confirm(LEAVE_CONFIRM), [hasUnsavedXslt])
  const guardedNavigate = useCallback((path: string) => {
    if (confirmLeave()) navigate(path)
  }, [confirmLeave, navigate])

  useEffect(() => {
    if (!hasUnsavedXslt) return
    const handler = (e: BeforeUnloadEvent) => { e.preventDefault(); e.returnValue = '' }
    window.addEventListener('beforeunload', handler)
    return () => window.removeEventListener('beforeunload', handler)
  }, [hasUnsavedXslt])

  const handleComposeError = useCallback(async (err: unknown) => {
    // Ücretli tema doğrudan URL ile açıldıysa backend 402 döner → Pro'ya yönlendir.
    const gate = await parseGateError(err)
    if (gate.status === 402) {
      openUpgradeModal({
        title: 'Ücretli tema',
        message: gate.message ?? 'Bu temayı kullanmak için XsltCraft Pro üyeliği gerekir.',
      })
      navigate('/templates')
    }
  }, [navigate])

  const hideThemeImages = useMemo(() => hiddenThemeImages(logo, signature, slots), [logo, signature, slots])
  const compose = useThemeCompose({
    themeId: editedXslt === null ? templateId : undefined,
    xslt: editedXslt !== null && !editedXsltBroken ? editedXslt : undefined,
    xmlContent,
    assets,
    bankInfo: banks,
    hideThemeImages,
    fixedNotes: composeNotes,
    onError: handleComposeError,
  })

  // Bozuk metinde istek gitmez: değer son geçerli yanıttan kalır.
  useEffect(() => {
    if (compose.fixedNotesSupported !== null) setFixedNotesSupported(compose.fixedNotesSupported)
  }, [compose.fixedNotesSupported, setFixedNotesSupported])

  const placements = useMemo(() => ({
    logo: logo.dataUrl ? effectiveSlot('logo', logo, slots) : null,
    signature: signature.dataUrl ? effectiveSlot('signature', signature, slots) : null,
  }), [logo, signature, slots])

  /** Metin değişimi debounce ile; stil/temizle (dolu notta) önizlemeyi hemen yeniler. */
  function handleNoteChange(position: FixedNotePosition, patch: Partial<FixedNoteState>, immediate: boolean) {
    const prev = fixedNotes[position]
    const next = { ...prev, ...patch }
    setFixedNotes((s) => ({ ...s, [position]: { ...s[position], ...patch } }))
    if (immediate && (noteHasText(prev) || noteHasText(next))) compose.composeNow()
  }

  const setterFor = (kind: AssetKind) => (kind === 'logo' ? setLogo : setSignature)

  // ── Sürükle-bırak ───────────────────────────────────────────────────────────
  const sensors = useSensors(
    useSensor(PointerSensor, { activationConstraint: { distance: 4 } }),
    useSensor(TouchSensor, { activationConstraint: { delay: 150, tolerance: 5 } }),
  )

  function handleDragStart(e: DragStartEvent) {
    setDragKind(kindFromDragId(e.active.id))
  }

  function handleDragEnd(e: DragEndEvent) {
    setDragKind(null)
    const kind = kindFromDragId(e.active.id)
    const slot = slotFromDropId(e.over?.id)
    if (!kind || !slot) return
    setterFor(kind)((p) => ({ ...p, slot }))
    compose.composeNow()
  }

  // ── İndir / Kaydet: önizlemeyle aynı birleştirme ─────────────────────────────
  async function handleDownload() {
    if (isDownloading) return
    setIsDownloading(true)
    try {
      const xslt = await compose.exportXslt()
      const blob = new Blob([xslt], { type: 'application/xslt+xml' })
      const a = document.createElement('a')
      a.href = URL.createObjectURL(blob)
      a.download = `${templateName.replace(/[^a-z0-9çğıöşüÇĞİÖŞÜ]/gi, '_')}.xslt`
      a.click()
      URL.revokeObjectURL(a.href)
      reportDownload('Template', templateId)
    } catch (err) {
      await handleComposeError(err)
      toast.error('İndirme başarısız.')
    } finally {
      setIsDownloading(false)
    }
  }

  async function handleSave() {
    if (isSaving) return

    // Proaktif gate: ham XSLT'yi "Şablonlarım"a saklamak Pro üyelik gerektirir.
    const ent = useEntitlementStore.getState().entitlements
    if (ent && !ent.canSaveRawXslt && !ent.isPrivileged) {
      openUpgradeModal({
        title: 'Kaydetme Pro üyelik gerektirir',
        message: "Şablonu indirebilirsin, ancak 'Şablonlarım'da saklamak XsltCraft Pro’ya dahildir.",
        confirmLeave,
      })
      return
    }

    const { session, markSaved } = useThemeDraftStore.getState()
    setIsSaving(true)
    try {
      // Kayıt hedefi hat alındığında, `await`'ten önce okunur: ekrandan çıkılırsa taslak silinir, `update` yerine `create` çalışmasın.
      await runDraftSave(async (saved) => {
        const xslt = await compose.exportXslt()
        if (!mountedRef.current || useThemeDraftStore.getState().session !== session) return
        // Ad gönderilmez: kullanıcının kayıtta verdiği ad temanın adıyla ezilmesin.
        const result = saved
          ? await updateUserXsltTemplate(saved.id, { xsltContent: xslt, xmlContent, expectedUpdatedAt: saved.updatedAt })
          : await createUserXsltTemplate({ name: templateName, xsltContent: xslt, xmlContent })
        markSaved(session, { id: result.id, updatedAt: result.updatedAt, name: result.name }, workingXslt)
        if (!mountedRef.current) return
        setSaveSuccess(true)
        saveRedirectRef.current = setTimeout(() => { navigate('/my-xslt-templates') }, 800)
      })
    } catch (err) {
      const gate = await parseGateError(err)
      if (gate.status === 402) {
        openUpgradeModal({
          title: 'Kaydetme Pro üyelik gerektirir',
          message: gate.message ?? "'Şablonlarım'da saklama XsltCraft Pro’ya dahildir.",
          confirmLeave: mountedRef.current ? confirmLeave : undefined,
        })
      } else if (gate.status === 423) {
        toast.error('Bu şablonu şu an başka bir kullanıcı düzenliyor. Kaydedilemedi.', {
          title: 'Şablon kilitli', durationMs: null,
        })
      } else if (gate.status === 409) {
        toast.error(SAVE_CONFLICT_MESSAGE, { title: 'Çakışma', durationMs: null })
      } else {
        toast.error('Kaydetme başarısız.')
      }
    } finally { setIsSaving(false) }
  }

  async function handleXmlFile(e: React.ChangeEvent<HTMLInputElement>) {
    const file = e.target.files?.[0]
    e.target.value = ''
    if (!file) return
    try {
      setXml({ content: await readUploadFile(file, 'xml'), fileName: file.name })
    } catch (err) {
      toast.error((err as Error).message, { title: 'XML yüklenemedi' })
    }
  }

  async function handleImageUpload(kind: AssetKind, e: React.ChangeEvent<HTMLInputElement>) {
    const file = e.target.files?.[0]
    e.target.value = ''
    if (!file) return
    const invalid = validateImageFile(file)
    if (invalid) {
      toast.error(invalid, { title: kind === 'logo' ? 'Logo yüklenemedi' : 'İmza yüklenemedi' })
      return
    }
    setUploading(kind)
    try {
      const dataUrl = await readAsDataUrl(file)
      setterFor(kind)((prev) => ({ ...prev, dataUrl }))
    } catch {
      toast.error('Görsel okunamadı.')
    } finally {
      setUploading(null)
    }
  }

  const dragged = dragKind === 'logo' ? logo : dragKind === 'signature' ? signature : null
  const showSlots = showFields && slots.length > 0
  const toggleSection = (key: keyof typeof openSections) => setOpenSections((o) => ({ ...o, [key]: !o[key] }))

  const ent = useEntitlementStore((st) => st.entitlements)
  const saveNeedsPro = !!ent && !ent.canSaveRawXslt && !ent.isPrivileged
  const docTypeLabel = docType === 'Despatch' ? 'e-İrsaliye' : docType === 'Invoice' ? 'e-Fatura / e-Arşiv' : '—'
  const checks = buildChecklist({
    hasLogo: !!logo.dataUrl,
    hasSignature: !!signature.dataUrl,
    bankCount: banks.length,
    noteCount,
    notesSupported: fixedNotesSupported,
    customXml: xmlFileName !== null,
    preview: { loading: compose.loading, error: compose.error, ready: !!compose.html, warningCount: compose.warnings.length },
  })

  const backToDevModeButton = (
    <button
      type="button"
      onClick={() => navigate(`/dev-mode/${templateId}`)}
      className="xc-focus ml-auto flex-none rounded-md px-2 py-0.5 font-semibold underline hover:no-underline"
    >
      Geliştirici Modu'na dön
    </button>
  )

  const renderImagePanel = (kind: AssetKind) => {
    const state = kind === 'logo' ? logo : signature
    return (
      <ImagePanel
        hideTitle
        label={kind === 'logo' ? 'Logo' : 'İmza / Kaşe'}
        state={state}
        uploading={uploading === kind}
        slots={slots}
        slotValue={effectiveSlot(kind, state, slots)}
        onUpload={(e) => handleImageUpload(kind, e)}
        onChange={(patch) => setterFor(kind)((p) => ({ ...p, ...patch }))}
        onRemove={() => setterFor(kind)((p) => ({ ...p, dataUrl: '' }))}
        themeImageRemovable={themeImageRemovable(kind, slots)}
        dragHandle={showSlots && state.dataUrl
          ? <DraggableAssetChip kind={kind} dataUrl={state.dataUrl} />
          : undefined}
      />
    )
  }

  return (
    <div className="flex h-screen flex-col overflow-hidden bg-ui-bg font-sans text-[13px] text-tx">
      <WorkspaceHeader
        active="themes"
        onNavigate={guardedNavigate}
        confirmLeave={confirmLeave}
        actions={
          <>
            <HeaderButton
              variant="icon"
              icon={Printer}
              aria-label="Yazdır"
              title="Yazdır"
              onClick={() => printActivePreview()}
              disabled={!compose.html}
            />
            <HeaderButton
              icon={Code2}
              onClick={() => navigate(`/dev-mode/${templateId}`)}
              disabled={isSaving || saveSuccess}
              title="Geliştirici modunda XSLT'yi düzenle"
            >
              XSLT editörünü aç
            </HeaderButton>
            <HeaderButton
              icon={BookmarkCheck}
              onClick={handleSave}
              disabled={isSaving || saveSuccess || !compose.html}
              title="Şablonlarıma kaydet"
            >
              {saveSuccess ? 'Kaydedildi' : isSaving ? 'Kaydediliyor…' : 'Şablonlarıma kaydet'}
            </HeaderButton>
            <HeaderButton
              variant="primary"
              icon={Download}
              onClick={handleDownload}
              disabled={isDownloading || !compose.html}
              title="XSLT dosyasını indir"
            >
              {isDownloading ? 'İndiriliyor…' : 'XSLT İndir'}
            </HeaderButton>
          </>
        }
      />

      <DndContext
        sensors={sensors}
        collisionDetection={pointerWithin}
        onDragStart={handleDragStart}
        onDragEnd={handleDragEnd}
        onDragCancel={() => setDragKind(null)}
        accessibility={{ announcements, screenReaderInstructions }}
      >
        <div className="flex min-h-0 flex-1 flex-col overflow-y-auto lg:flex-row lg:overflow-hidden">
          {/* Sol: tema varlıkları */}
          <aside
            aria-label="Tema düzenleme"
            className="flex flex-col border-r border-ui-line bg-ui-card lg:w-80 lg:flex-none lg:overflow-y-auto"
          >
            <div className="border-b border-ui-line px-4 pt-3.5 pb-3">
              <nav aria-label="Konum">
                <ol className="m-0 flex list-none gap-1.5 p-0 text-[12px] text-tx-muted">
                  <li>
                    <Link
                      to={`/templates?type=${docType ?? 'Invoice'}`}
                      onClick={(e) => {
                        // Ctrl/⌘/orta tık yeni sekme açar; bu sayfadan ayrılınmaz.
                        if (e.button !== 0 || e.metaKey || e.ctrlKey || e.shiftKey || e.altKey) return
                        if (!confirmLeave()) e.preventDefault()
                      }}
                      className="xc-focus rounded text-ui-ink no-underline hover:underline"
                    >
                      Tema Kütüphanesi
                    </Link>
                  </li>
                  <li aria-hidden="true">/</li>
                  <li>{docTypeLabel}</li>
                </ol>
              </nav>
              <h1 className="mt-1 mb-0 text-[17px] font-semibold text-tx">{templateName}</h1>
              <p className="mt-1 mb-0 text-[12px] leading-normal text-tx-muted">
                Logonuzu, imzanızı, banka bilgilerinizi ve sabit notlarınızı ekleyin; tema düzeni korunur.
              </p>
              <p className="mt-2.5 mb-0 rounded-md border border-ui-soft-line bg-ui-soft px-3 py-2 text-[12px] leading-normal text-tx-2">
                {slots.length > 0
                  ? 'Görseli önizlemedeki mavi kesikli alanlardan birine sürükleyin ya da “Konum” listesinden seçin.'
                  : 'Bu tema için konum seçimi yok; görseller temanın logo/imza alanına yerleşir.'}
              </p>
            </div>

            <AssetAccordionSection
              title="Logo"
              icon={ImageIcon}
              status={logo.dataUrl ? 'Eklendi' : 'Boş'}
              done={!!logo.dataUrl}
              open={openSections.logo}
              onToggle={() => toggleSection('logo')}
            >
              {renderImagePanel('logo')}
            </AssetAccordionSection>
            <AssetAccordionSection
              title="İmza / Kaşe"
              icon={Signature}
              status={signature.dataUrl ? 'Eklendi' : 'Boş'}
              done={!!signature.dataUrl}
              open={openSections.signature}
              onToggle={() => toggleSection('signature')}
            >
              {renderImagePanel('signature')}
            </AssetAccordionSection>
            <AssetAccordionSection
              title="Banka / IBAN"
              icon={Landmark}
              status={banks.length > 0 ? `${banks.length} hesap` : 'Boş'}
              done={banks.length > 0}
              open={openSections.bank}
              onToggle={() => toggleSection('bank')}
            >
              <BankInfoPanel hideTitle rows={bankInfo} onChange={setBankInfo} />
            </AssetAccordionSection>
            <AssetAccordionSection
              title="Sabit notlar"
              icon={StickyNote}
              status={fixedNotesSupported === false ? 'Uygun değil' : noteCount > 0 ? `${noteCount} not` : 'Boş'}
              done={fixedNotesSupported !== false && noteCount > 0}
              open={openSections.notes}
              onToggle={() => toggleSection('notes')}
            >
              <FixedNotesPanel
                notes={fixedNotes}
                onChange={handleNoteChange}
                supported={fixedNotesSupported}
                error={compose.errorField === 'fixedNotes' ? compose.error : null}
              />
            </AssetAccordionSection>
          </aside>

          {/* Orta: önizleme */}
          <main className="flex min-h-[480px] min-w-0 flex-1 flex-col">
            <div className="flex flex-wrap items-center gap-2 border-b border-ui-line bg-ui-card px-4 py-2">
              <span className="text-[12px] text-tx-muted">Önizleme verisi</span>
              <span className="inline-flex h-[30px] max-w-[220px] items-center gap-1.5 rounded-md border border-field-line bg-ui-card px-2.5 text-[12px] text-tx-2">
                <FileText size={13} aria-hidden="true" className="flex-none" />
                <span className="truncate">{xmlFileName ?? (docType === 'Despatch' ? 'Örnek irsaliye XML' : 'Örnek fatura XML')}</span>
              </span>
              <button
                type="button"
                onClick={() => xmlInputRef.current?.click()}
                className="xc-focus inline-flex h-[30px] items-center gap-1.5 rounded-md px-2 text-[12px] font-semibold text-ui-ink hover:bg-ui-hover"
              >
                <Upload size={13} aria-hidden="true" />
                Kendi XML'ini yükle
              </button>
              <input ref={xmlInputRef} type="file" accept={UPLOAD_ACCEPT.xml} className="hidden" onChange={handleXmlFile} />
              <div className="flex-1" />
              <span aria-live="polite" className="inline-flex items-center gap-1.5 text-[12px] text-tx-subtle">
                {compose.loading ? (
                  <>
                    <LoaderCircle size={14} aria-hidden="true" className="animate-spin text-ui-accent motion-reduce:animate-none" />
                    Yükleniyor
                  </>
                ) : compose.lastMs !== null ? `${compose.lastMs} ms` : null}
              </span>
              {slots.length > 0 && (
                <button
                  type="button"
                  aria-pressed={showFields}
                  onClick={() => setShowFields((v) => !v)}
                  className={`xc-focus inline-flex h-[30px] items-center gap-1.5 rounded-md border px-2.5 text-[12px] transition-colors ${
                    showFields
                      ? 'border-ui-soft-line bg-ui-soft text-ui-ink'
                      : 'border-field-line bg-ui-card text-tx-2 hover:bg-ui-hover'
                  }`}
                >
                  <ScanEye size={13} aria-hidden="true" />
                  Alanları göster
                </button>
              )}
            </div>

            {xsltEdited && !editedXsltBroken && (
              <div role="status" className="flex items-center gap-1.5 border-b border-ui-soft-line bg-ui-soft px-4 py-2 text-[12px] text-tx-2">
                <Code2 size={13} aria-hidden="true" className="flex-none" />
                Geliştirici Modu'ndaki XSLT düzenlemeleri uygulanıyor.
              </div>
            )}
            {editedXsltBroken && (
              <div role="alert" className="flex items-center gap-1.5 border-b border-fn-red-line bg-fn-red-tint px-4 py-2 text-[12px] text-fn-red-ink">
                <CircleX size={13} aria-hidden="true" className="flex-none" />
                Geliştirici Modu'ndaki XSLT'de sözdizimi hatası var; düzeltilene kadar önizleme, indirme ve kaydetme kapalı.
                {backToDevModeButton}
              </div>
            )}
            {compose.error && (
              <div role="alert" className="flex items-center gap-1.5 border-b border-fn-red-line bg-fn-red-tint px-4 py-2 text-[12px] text-fn-red-ink">
                <CircleX size={13} aria-hidden="true" className="flex-none" />
                {compose.error}
                {xsltEdited && backToDevModeButton}
              </div>
            )}
            {compose.warnings.length > 0 && (
              <div className="flex flex-col gap-0.5 border-b border-fn-orange-line bg-fn-orange-tint px-4 py-2 text-[12px] text-fn-orange-ink">
                {compose.warnings.map((w) => (
                  <span key={w.code + (w.kind ?? '')} className="flex items-center gap-1.5">
                    <TriangleAlert size={12} aria-hidden="true" className="flex-none" />{w.message}
                  </span>
                ))}
              </div>
            )}

            <div className="flex flex-1 overflow-auto px-6 py-8" style={{ justifyContent: 'safe center' }}>
              <ThemeSlotPreview
                html={compose.html}
                slots={slots}
                showSlots={showSlots}
                dragging={dragKind !== null}
                placements={placements}
              />
            </div>
          </main>

          {/* Sağ: tema bilgisi + kontrol listesi */}
          <ThemeInfoAside
            templateName={templateName}
            docTypeLabel={docTypeLabel}
            slotCount={slots.length}
            saveNeedsPro={saveNeedsPro}
            checks={checks}
          />
        </div>

        <DragOverlay dropAnimation={null}>
          {dragKind && dragged?.dataUrl ? <AssetDragPreview kind={dragKind} dataUrl={dragged.dataUrl} /> : null}
        </DragOverlay>
      </DndContext>
    </div>
  )
}
