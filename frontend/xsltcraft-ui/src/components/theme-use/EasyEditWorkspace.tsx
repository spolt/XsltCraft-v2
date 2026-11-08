import { useCallback, useEffect, useMemo, useRef, useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { DndContext, DragOverlay } from '@dnd-kit/core'
import {
  Printer,
  Code2,
  Download,
  BookmarkCheck,
  TriangleAlert,
  CircleX,
  FileText,
  Upload,
  ScanEye,
  LoaderCircle,
} from 'lucide-react'
import type { AssetKind } from '../../services/themeComposeService'
import { useEntitlementStore } from '../../store/entitlementStore'
import { useDraftField, useThemeDraftStore } from '../../store/themeDraftStore'
import { toast } from '../../store/toastStore'
import { readUploadFile, UPLOAD_ACCEPT } from '../../utils/uploadValidation'
import { printActivePreview } from '../xslt-editor/previewFrame'
import { LEAVE_CONFIRM } from '../xslt-editor/xsltText'
import { useThemeCompose } from '../../hooks/useThemeCompose'
import ImagePanel from '../theme-assets/ImagePanel'
import BankInfoPanel from '../theme-assets/BankInfoPanel'
import FixedNotesPanel from '../theme-assets/FixedNotesPanel'
import {
  countNotes,
  noteHasText,
  toComposeNotes,
  type FixedNoteState,
} from '../theme-assets/fixedNoteState'
import type { FixedNotePosition } from '../../services/themeComposeService'
import ThemeSlotPreview from '../theme-assets/ThemeSlotPreview'
import DraggableAssetChip, { AssetDragPreview } from '../theme-assets/DraggableAssetChip'
import {
  FREE_IMAGE_LABEL, freePlacementSlot, isFreeSlot, patchFreeImage, removeFreeImage, vacatedThemeSlots, type FreeImgState,
} from '../theme-assets/freeImageState'
import FreeImagesPanel from '../easy-edit/FreeImagesPanel'
import { announcements, screenReaderInstructions } from '../theme-assets/slotDnd'
import {
  effectiveSlot,
  filledBankRows,
  placementSlot,
  readAsDataUrl,
  toComposeAssets,
  hiddenThemeImages,
  themeImageRemovable,
  validateImageFile,
} from '../theme-assets/imageState'
import WorkspaceHeader from '../shell/WorkspaceHeader'
import DocumentBar from '../easy-edit/DocumentBar'
import { HeaderButton } from '../shell/headerParts'
import EasyEditAssetsPanel from './EasyEditAssetsPanel'
import ThemeInfoAside from './ThemeInfoAside'
import { useWorkspaceDnd } from './useWorkspaceDnd'
import { useDocImages } from '../easy-edit/useDocImages'
import { useImageRoles } from '../easy-edit/useImageRoles'
import ImageRolesDialog from '../easy-edit/ImageRolesDialog'
import DocAnalysisSummary from '../easy-edit/DocAnalysisSummary'
import { composeWarningText } from '../easy-edit/composeWarningText'
import { buildChecklist } from './themeChecklist'
import type { EasyEditSource } from './easyEditSource'
import { useWorkspaceSaveDownload } from './useWorkspaceSaveDownload'
import { sampleXmlLabel, sampleXmlText } from '../dashboard/editorFiles'

interface Props {
  source: EasyEditSource
}

/** Kolay düzenle çalışma alanı: varlık panelleri, sunucu önizlemesi, indir/kaydet. Belgeyi yalnız `source` üzerinden tanır. */
export default function EasyEditWorkspace({ source }: Props) {
  const navigate = useNavigate()

  const { name: templateName, slots, docType, editor, breadcrumb, notesMode, onComposeError } = source
  const isDoc = source.kind === 'doc'
  const [xml, setXml] = useDraftField('xml')
  const xmlContent = xml?.content ?? sampleXmlText(docType)
  /** Yalnız görüntüleme: kullanıcının yüklediği XML'in adı (null → örnek XML). */
  const xmlFileName = xml?.fileName ?? null
  /** Önizlemede yerleşim alanlarını (yuvaları) göster. */
  const [showFields, setShowFields] = useState(true)
  const xmlInputRef = useRef<HTMLInputElement>(null)

  const [logo, setLogo] = useDraftField('logo')
  const [signature, setSignature] = useDraftField('signature')
  const [freeImages, setFreeImages] = useDraftField('freeImages')
  const [bankInfo, setBankInfo] = useDraftField('bankInfo')
  const [fixedNotes, setFixedNotes] = useDraftField('fixedNotes')
  const notesTouched = useThemeDraftStore((s) => s.notesTouched)
  const [fixedNotesSupported, setFixedNotesSupported] = useDraftField('fixedNotesSupported')
  const [uploading, setUploading] = useState<AssetKind | 'free' | null>(null)

  const assets = useMemo(() => toComposeAssets(logo, signature, slots), [logo, signature, slots])
  const banks = useMemo(() => filledBankRows(bankInfo), [bankInfo])
  /** Belge IBAN'ı bulunduysa belge satırları `documentBankEdits` ile gider; `bankInfo` yalnız kendi tablomuzun satırlarıdır. */
  const docBank = source.doc?.bank ?? null
  const composeBanks = docBank ? docBank.ownRows : banks
  // Yama modunda: dokunulmadıysa null (nota dokunma), boşaltıldıysa [] (eski notları sil). Liste modunda her zaman liste.
  const composeNotes = useMemo(() => {
    if (notesMode === 'filled') return toComposeNotes(fixedNotes)
    if (notesMode === 'list') return toComposeNotes(fixedNotes) ?? []
    return notesTouched ? (toComposeNotes(fixedNotes) ?? []) : null
  }, [notesMode, fixedNotes, notesTouched])
  const noteCount = countNotes(fixedNotes)

  const saveBlockedReason = source.saveBlockedReason
  const hasUnsavedXslt = source.isDirty
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

  const hideThemeImages = useMemo(() => hiddenThemeImages(logo, signature, slots), [logo, signature, slots])
  const compose = useThemeCompose({
    themeId: source.composeSource.themeId,
    xslt: source.composeSource.xslt,
    xmlContent,
    assets,
    bankInfo: composeBanks,
    hideThemeImages,
    fixedNotes: composeNotes,
    conditionalSlots: source.composeSource.conditionalSlots,
    staticImages: source.composeSource.staticImages,
    freeImages: source.composeSource.freeImages,
    documentBankEdits: source.composeSource.documentBankEdits,
    onError: onComposeError,
  })

  const openEditor = useCallback(
    () => source.openEditor({ exportXslt: compose.exportXslt, xml, confirmLeave }),
    [source, compose.exportXslt, xml, confirmLeave],
  )

  // Bozuk metinde istek gitmez: değer son geçerli yanıttan kalır.
  useEffect(() => {
    if (compose.fixedNotesSupported !== null) setFixedNotesSupported(compose.fixedNotesSupported)
  }, [compose.fixedNotesSupported, setFixedNotesSupported])

  // Tür başına görselin bulunduğu yuva (etiket için).
  const placements = useMemo(() => ({
    logo: placementSlot('logo', logo, slots),
    signature: placementSlot('signature', signature, slots),
  }), [logo, signature, slots])
  // Doc: taşınan/kaldırılan dosya görselinin eski yeri (boş kutu) serbest görsel için de hedef olur.
  const vacated = useMemo(() => vacatedThemeSlots(logo, signature), [logo, signature])
  const freePlacements = useMemo(() => freeImages.map((f) => freePlacementSlot(f, vacated)), [freeImages, vacated])

  /** Metin değişimi debounce ile; stil/temizle (dolu notta) önizlemeyi hemen yeniler. */
  function handleNoteChange(position: FixedNotePosition, patch: Partial<FixedNoteState>, immediate: boolean) {
    const prev = fixedNotes[position]
    const next = { ...prev, ...patch }
    setFixedNotes((s) => ({ ...s, [position]: { ...s[position], ...patch } }))
    if (immediate && (noteHasText(prev) || noteHasText(next))) compose.composeNow()
  }

  const setterFor = (kind: AssetKind) => (kind === 'logo' ? setLogo : setSignature)

  // ── Doc: dosyanın görselleri ve görsel rolleri modalı ─────────────────────────
  const docAnalysis = source.doc?.analysis ?? null
  const docImages = useDocImages(docAnalysis, slots, compose.composeNow)
  const roles = useImageRoles(source.doc)
  const canChangeRoles = !!docAnalysis && ((docAnalysis.images?.length ?? 0) > 0 || docAnalysis.previous != null)

  // ── Doc: serbest görsel (panel ve sürükleme aynı taslak alanını yazar) ───────
  function changeFree(index: number, patch: Partial<FreeImgState>) {
    setFreeImages((list) => patchFreeImage(list, index, patch))
    if ('slot' in patch) compose.composeNow()
  }

  function removeFree(index: number) {
    setFreeImages((list) => removeFreeImage(list, index))
    compose.composeNow()
  }

  /** Önizlemede bırakılan yuvaya taşır; boşalmamış temadaki görsel yeri (`existing-*`) ve aynı yuva yok sayılır. */
  function moveFree(index: number, slot: string) {
    const state = freeImages[index]
    if (!state?.dataUrl || !isFreeSlot(slot, vacated) || state.slot === slot) return
    changeFree(index, { slot })
  }

  // ── Sürükle-bırak: bırakılan yuvaya doğrudan taşır ─────────
  const dnd = useWorkspaceDnd({
    keyboard: isDoc,
    onDrop: (dragged, slot) => {
      if (dragged.type === 'free') {
        if (isDoc) moveFree(dragged.index, slot)
        return
      }
      if (isDoc) {
        docImages.move(dragged.kind, slot)
        return
      }
      setterFor(dragged.kind)((p) => ({ ...p, slot }))
      compose.composeNow()
    },
  })
  const dragSource = dnd.dragSource

  // ── İndir / Kaydet: önizlemeyle aynı birleştirme ─────────────────────────────
  const saveDownload = useWorkspaceSaveDownload({
    source, exportXslt: compose.exportXslt, previewReady: !!compose.html, xmlContent, confirmLeave,
  })
  const { handleSave, handleDownload, saveBusy } = saveDownload

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

  /** Seçilen görseli doğrular ve okur; başarısızsa bildirim gösterip null döner. */
  async function readImageInput(
    e: React.ChangeEvent<HTMLInputElement>, target: AssetKind | 'free', errorTitle: string,
  ): Promise<string | null> {
    const file = e.target.files?.[0]
    e.target.value = ''
    if (!file) return null
    const invalid = validateImageFile(file)
    if (invalid) {
      toast.error(invalid, { title: errorTitle })
      return null
    }
    setUploading(target)
    try {
      return await readAsDataUrl(file)
    } catch {
      toast.error('Görsel okunamadı.')
      return null
    } finally {
      setUploading(null)
    }
  }

  async function handleImageUpload(kind: AssetKind, e: React.ChangeEvent<HTMLInputElement>) {
    const dataUrl = await readImageInput(e, kind, kind === 'logo' ? 'Logo yüklenemedi' : 'İmza yüklenemedi')
    if (!dataUrl) return
    if (isDoc) docImages.upload(kind, dataUrl)
    else setterFor(kind)((prev) => ({ ...prev, dataUrl }))
  }

  async function handleFreeUpload(index: number, e: React.ChangeEvent<HTMLInputElement>) {
    const dataUrl = await readImageInput(e, 'free', 'Görsel yüklenemedi')
    if (dataUrl) changeFree(index, { dataUrl, origin: 'upload' })
  }

  const draggedDataUrl = !dragSource ? ''
    : dragSource.type === 'free' ? freeImages[dragSource.index]?.dataUrl ?? ''
    : dragSource.kind === 'logo' ? logo.dataUrl : signature.dataUrl
  const freePlaced = freePlacements.some(Boolean)
  const freeCount = freeImages.filter((f) => f.dataUrl).length
  const showSlots = showFields && slots.length > 0

  const ent = useEntitlementStore((st) => st.entitlements)
  const saveNeedsPro = !!ent && !ent.canSaveRawXslt && !ent.isPrivileged
  const docTypeLabel = breadcrumb.current
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
      onClick={openEditor}
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
        onChange={(patch) => (isDoc ? docImages.change(kind, patch) : setterFor(kind)((p) => ({ ...p, ...patch })))}
        onRemove={() => (isDoc ? docImages.remove(kind) : setterFor(kind)((p) => ({ ...p, dataUrl: '' })))}
        onRestore={isDoc ? () => docImages.restore(kind) : undefined}
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
        active={isDoc ? null : 'themes'}
        onNavigate={guardedNavigate}
        confirmLeave={confirmLeave}
        actions={isDoc ? undefined : (
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
              onClick={openEditor}
              disabled={saveBusy}
              title="Geliştirici modunda XSLT'yi düzenle"
            >
              XSLT editörünü aç
            </HeaderButton>
            <HeaderButton
              icon={BookmarkCheck}
              onClick={handleSave}
              disabled={saveDownload.saveDisabled}
              title="Şablonlarıma kaydet"
            >
              {saveDownload.saveLabel}
            </HeaderButton>
            <HeaderButton
              variant="primary"
              icon={Download}
              onClick={handleDownload}
              disabled={saveDownload.downloadDisabled}
              title="XSLT dosyasını indir"
            >
              {saveDownload.downloadLabel('XSLT İndir')}
            </HeaderButton>
          </>
        )}
      />
      {isDoc && (
        <DocumentBar
          fileName={source.fileName ?? source.downloadName}
          dirty={source.isDirty}
          mode="easyEdit"
          onModeChange={(mode) => { if (mode === 'editor') void openEditor() }}
          modeDisabled={saveBusy}
          onPrint={() => printActivePreview()}
          onSave={handleSave}
          onDownload={handleDownload}
          printDisabled={!compose.html}
          saveDisabled={saveDownload.saveDisabled}
          saveTitle={saveBlockedReason}
          downloadDisabled={saveDownload.downloadDisabled}
          saveLabel={saveDownload.saveLabel}
          downloadLabel={saveDownload.downloadLabel('İndir')}
        />
      )}

      <DndContext
        sensors={dnd.sensors}
        collisionDetection={dnd.collisionDetection}
        onDragStart={dnd.onDragStart}
        onDragEnd={dnd.onDragEnd}
        onDragCancel={dnd.onDragCancel}
        accessibility={{ announcements, screenReaderInstructions }}
      >
        <div className="flex min-h-0 flex-1 flex-col overflow-y-auto lg:flex-row lg:overflow-hidden">
          {/* Sol: tema varlıkları */}
          <EasyEditAssetsPanel
            breadcrumb={breadcrumb}
            templateName={templateName}
            isDoc={isDoc}
            hasSlots={slots.length > 0}
            confirmLeave={confirmLeave}
            logo={{ status: logo.dataUrl ? 'Eklendi' : 'Boş', done: !!logo.dataUrl, content: renderImagePanel('logo') }}
            signature={{
              status: signature.dataUrl ? 'Eklendi' : 'Boş', done: !!signature.dataUrl, content: renderImagePanel('signature'),
            }}
            free={isDoc ? {
              status: freePlaced ? 'Eklendi' : freeCount > 0 ? 'Konum seç' : 'Boş',
              done: freePlaced,
              content: (
                <FreeImagesPanel
                  items={freeImages}
                  slots={slots}
                  vacated={vacated}
                  uploading={uploading === 'free'}
                  draggable={showSlots}
                  onUpload={(i, e) => void handleFreeUpload(i, e)}
                  onChange={changeFree}
                  onRemove={removeFree}
                />
              ),
            } : undefined}
            bank={{
              status: banks.length > 0 ? `${banks.length} hesap` : 'Boş',
              done: banks.length > 0,
              content: docBank ? (
                <BankInfoPanel
                  hideTitle
                  rows={bankInfo}
                  onChange={setBankInfo}
                  locks={docBank.locks}
                  canAdd={docBank.canAdd}
                  allowEmpty
                  ibanError={docBank.ibanErrors}
                />
              ) : <BankInfoPanel hideTitle rows={bankInfo} onChange={setBankInfo} />,
            }}
            notes={{
              status: fixedNotesSupported === false ? 'Uygun değil' : noteCount > 0 ? `${noteCount} not` : 'Boş',
              done: fixedNotesSupported !== false && noteCount > 0,
              content: (
                <FixedNotesPanel
                  notes={fixedNotes}
                  onChange={handleNoteChange}
                  supported={fixedNotesSupported}
                  error={compose.errorField === 'fixedNotes' ? compose.error : null}
                />
              ),
            }}
            onOpenEditor={isDoc ? () => void openEditor() : undefined}
            editorDisabled={saveBusy}
          />

          {/* Orta: önizleme */}
          <main className="flex min-h-[480px] min-w-0 flex-1 flex-col">
            <div className="flex flex-wrap items-center gap-2 border-b border-ui-line bg-ui-card px-4 py-2">
              <span className="text-[12px] text-tx-muted">Önizleme verisi</span>
              <span className="inline-flex h-[30px] max-w-[220px] items-center gap-1.5 rounded-md border border-field-line bg-ui-card px-2.5 text-[12px] text-tx-2">
                <FileText size={13} aria-hidden="true" className="flex-none" />
                <span className="truncate">{xmlFileName ?? sampleXmlLabel(docType)}</span>
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

            {saveBlockedReason && (
              <div role="alert" className="flex items-center gap-1.5 border-b border-fn-orange-line bg-fn-orange-tint px-4 py-2 text-[12px] text-fn-orange-ink">
                <TriangleAlert size={13} aria-hidden="true" className="flex-none" />
                {saveBlockedReason}
              </div>
            )}
            {editor.edited && !editor.broken && (
              <div role="status" className="flex items-center gap-1.5 border-b border-ui-soft-line bg-ui-soft px-4 py-2 text-[12px] text-tx-2">
                <Code2 size={13} aria-hidden="true" className="flex-none" />
                Geliştirici Modu'ndaki XSLT düzenlemeleri uygulanıyor.
              </div>
            )}
            {editor.broken && (
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
                {editor.edited && backToDevModeButton}
              </div>
            )}
            {compose.warnings.length > 0 && (
              <div className="flex flex-col gap-0.5 border-b border-fn-orange-line bg-fn-orange-tint px-4 py-2 text-[12px] text-fn-orange-ink">
                {compose.warnings.map((w, i) => (
                  <span key={`${i}-${w.code}-${w.kind ?? ''}`} className="flex items-center gap-1.5">
                    <TriangleAlert size={12} aria-hidden="true" className="flex-none" />{isDoc ? composeWarningText(w) : w.message}
                  </span>
                ))}
              </div>
            )}

            <div className="flex flex-1 overflow-auto px-6 py-8" style={{ justifyContent: 'safe center' }}>
              <ThemeSlotPreview
                html={compose.html}
                slots={slots}
                showSlots={showSlots}
                dragging={dragSource !== null}
                placements={placements}
                staticHandles={isDoc ? docImages.staticHandles : undefined}
                freePlacements={isDoc ? freePlacements : undefined}
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
            doc={source.doc && {
              sourceLabel: source.doc.origin === 'upload' ? 'Bilgisayarından yüklendi' : 'Şablonlarımdan',
              unsaved: source.isDirty,
              analysis: <DocAnalysisSummary analysis={docAnalysis} onChangeRoles={canChangeRoles ? roles.show : undefined} />,
            }}
          />
        </div>

        <DragOverlay dropAnimation={null}>
          {dragSource && draggedDataUrl
            ? dragSource.type === 'asset'
              ? <AssetDragPreview kind={dragSource.kind} dataUrl={draggedDataUrl} />
              : <AssetDragPreview label={FREE_IMAGE_LABEL} dataUrl={draggedDataUrl} />
            : null}
        </DragOverlay>
      </DndContext>

      {roles.open && docAnalysis && (
        <ImageRolesDialog
          analysis={docAnalysis}
          busy={roles.busy}
          error={roles.error}
          onSubmit={(r) => void roles.submit(r)}
          onClose={roles.close}
        />
      )}
    </div>
  )
}
