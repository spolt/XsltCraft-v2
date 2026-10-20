import { useCallback, useEffect, useMemo, useState } from 'react'
import { useParams, useNavigate, Link, useLocation } from 'react-router-dom'
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
  LayoutDashboard,
  BookOpen,
  FilePlus,
  FolderOpen,
  Shield,
  Settings2,
  X,
  Printer,
  Code2,
  Download,
  BookmarkCheck,
  TriangleAlert,
} from 'lucide-react'
import { getTemplate, type ThemeSlotOption } from '../services/templateService'
import { reportDownload } from '../services/activityService'
import type { BankInfoItem } from '../services/previewService'
import type { AssetKind } from '../services/themeComposeService'
import { createUserXsltTemplate } from '../services/userXsltService'
import { useAuthStore } from '../store/authStore'
import { useEntitlementStore } from '../store/entitlementStore'
import { openUpgradeModal } from '../store/upgradeModalStore'
import { parseGateError } from '../services/entitlementService'
import { toast } from '../store/toastStore'
import { readUploadFile, UPLOAD_ACCEPT } from '../utils/uploadValidation'
import { printActivePreview } from '../components/xslt-editor/previewFrame'
import { useThemeCompose } from '../hooks/useThemeCompose'
import ImagePanel from '../components/theme-assets/ImagePanel'
import BankInfoPanel from '../components/theme-assets/BankInfoPanel'
import ThemeSlotPreview from '../components/theme-assets/ThemeSlotPreview'
import DraggableAssetChip, { AssetDragPreview } from '../components/theme-assets/DraggableAssetChip'
import { announcements, kindFromDragId, screenReaderInstructions, slotFromDropId } from '../components/theme-assets/slotDnd'
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
import defaultInvoiceXml from '../assets/default-invoice.xml?raw'
import defaultDespatchXml from '../assets/default-despatch.xml?raw'

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

// ─── Main page ─────────────────────────────────────────────────────────────────
export default function ThemeUsePage() {
  const { templateId } = useParams<{ templateId: string }>()
  const navigate = useNavigate()

  const [templateName, setTemplateName] = useState('Hazır Tema')
  const [slots, setSlots] = useState<ThemeSlotOption[]>([])
  const [xmlContent, setXmlContent] = useState<string>(defaultInvoiceXml)
  const [editMode, setEditMode] = useState(false)

  const [logo, setLogo] = useState<ImgState>(defaultImg())
  const [signature, setSignature] = useState<ImgState>(defaultImg())
  const [bankInfo, setBankInfo] = useState<BankInfoItem[]>([{ bankName: '', iban: '' }])
  const [uploading, setUploading] = useState<AssetKind | null>(null)

  const [isDownloading, setIsDownloading] = useState(false)
  const [isSaving, setIsSaving] = useState(false)
  const [saveSuccess, setSaveSuccess] = useState(false)
  const [dragKind, setDragKind] = useState<AssetKind | null>(null)

  // Tema adı, belge türü ve konum kataloğu
  useEffect(() => {
    if (!templateId) return
    getTemplate(templateId)
      .then((t) => {
        setTemplateName(t.name)
        setSlots(t.slots ?? [])
        if (t.documentType === 'Despatch') setXmlContent(defaultDespatchXml)
      })
      .catch(() => {})
  }, [templateId])

  const assets = useMemo(() => toComposeAssets(logo, signature, slots), [logo, signature, slots])
  const banks = useMemo(() => filledBankRows(bankInfo), [bankInfo])

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
  const compose = useThemeCompose({ themeId: templateId, xmlContent, assets, bankInfo: banks, hideThemeImages, onError: handleComposeError })

  const placements = useMemo(() => ({
    logo: logo.dataUrl ? effectiveSlot('logo', logo, slots) : null,
    signature: signature.dataUrl ? effectiveSlot('signature', signature, slots) : null,
  }), [logo, signature, slots])

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
      })
      return
    }

    setIsSaving(true)
    try {
      const xslt = await compose.exportXslt()
      await createUserXsltTemplate({ name: templateName, xsltContent: xslt, xmlContent })
      setSaveSuccess(true)
      setTimeout(() => { navigate('/my-xslt-templates') }, 800)
    } catch (err) {
      const gate = await parseGateError(err)
      if (gate.status === 402) {
        openUpgradeModal({
          title: 'Kaydetme Pro üyelik gerektirir',
          message: gate.message ?? "'Şablonlarım'da saklama XsltCraft Pro’ya dahildir.",
        })
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
      setXmlContent(await readUploadFile(file, 'xml'))
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

  return (
    <div className="h-screen flex overflow-hidden bg-gray-50">
      <EditorSidebar />

      <div className="flex-1 flex flex-col overflow-hidden">
        {/* Toolbar */}
        <div className="h-12 border-b border-gray-200 bg-white flex items-center gap-2 px-4 flex-shrink-0">
          <span className="text-base font-medium text-gray-700 flex-1 truncate">{templateName}</span>
          {compose.loading && (
            <span className="text-sm text-blue-500 flex items-center gap-1">
              <span className="inline-block w-4 h-4 border-2 border-blue-400 border-t-transparent rounded-full animate-spin" />
              Yükleniyor
            </span>
          )}
          {!compose.loading && compose.lastMs !== null && <span className="text-sm text-gray-400">{compose.lastMs} ms</span>}
          <label className="cursor-pointer text-sm text-gray-500 border border-gray-200 rounded px-3 py-1.5 hover:bg-gray-50">
            XML Değiştir
            <input type="file" accept={UPLOAD_ACCEPT.xml} className="hidden" onChange={handleXmlFile} />
          </label>
          <button onClick={() => printActivePreview()} disabled={!compose.html}
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
            disabled={isDownloading || !compose.html}
            className="flex items-center gap-1.5 text-sm text-gray-600 border border-gray-200 rounded px-3 py-1.5 hover:bg-gray-50 disabled:opacity-40"
            title="XSLT dosyasını indir"
          >
            <Download size={15} />{isDownloading ? '…' : 'İndir'}
          </button>
          <button
            onClick={handleSave}
            disabled={isSaving || saveSuccess || !compose.html}
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

        {compose.error && (
          <div className="px-4 py-2 text-xs text-red-600 bg-red-50 border-b border-red-100 flex-shrink-0">{compose.error}</div>
        )}
        {compose.warnings.length > 0 && (
          <div className="px-4 py-2 text-xs text-amber-800 bg-amber-50 border-b border-amber-100 flex-shrink-0 flex flex-col gap-0.5">
            {compose.warnings.map((w) => (
              <span key={w.code + (w.kind ?? '')} className="flex items-center gap-1.5">
                <TriangleAlert size={12} className="flex-shrink-0" />{w.message}
              </span>
            ))}
          </div>
        )}

        <DndContext
          sensors={sensors}
          collisionDetection={pointerWithin}
          onDragStart={handleDragStart}
          onDragEnd={handleDragEnd}
          onDragCancel={() => setDragKind(null)}
          accessibility={{ announcements, screenReaderInstructions }}
        >
          <div className="flex-1 flex overflow-hidden">
            {/* Preview */}
            <div className="flex-1 overflow-auto bg-gray-300 flex justify-center py-4" style={{ justifyContent: 'safe center' }}>
              <ThemeSlotPreview
                html={compose.html}
                slots={slots}
                showSlots={editMode && slots.length > 0}
                dragging={dragKind !== null}
                placements={placements}
              />
            </div>

            {/* Right panel */}
            {editMode && (
              <div className="flex-shrink-0 border-l border-gray-200 bg-white flex flex-col overflow-y-auto" style={{ width: 300 }}>
                <div className="p-4 flex flex-col gap-6">
                  {slots.length > 0 ? (
                    <p className="text-xs text-gray-500 bg-blue-50 border border-blue-100 rounded-lg px-3 py-2">
                      Görseli önizlemedeki mavi kesikli alanlardan birine sürükleyin ya da “Konum” listesinden seçin.
                    </p>
                  ) : (
                    <p className="text-xs text-gray-500 bg-gray-50 border border-gray-100 rounded-lg px-3 py-2">
                      Bu tema için konum seçimi yok; görseller temanın logo/imza alanına yerleşir.
                    </p>
                  )}

                  {(['logo', 'signature'] as const).map((kind, i) => {
                    const state = kind === 'logo' ? logo : signature
                    return (
                      <div key={kind} className="flex flex-col gap-6">
                        {i > 0 && <div className="border-t border-gray-100" />}
                        <ImagePanel
                          label={kind === 'logo' ? 'Logo' : 'İmza / Kaşe'}
                          state={state}
                          uploading={uploading === kind}
                          slots={slots}
                          slotValue={effectiveSlot(kind, state, slots)}
                          onUpload={(e) => handleImageUpload(kind, e)}
                          onChange={(patch) => setterFor(kind)((p) => ({ ...p, ...patch }))}
                          onRemove={() => setterFor(kind)((p) => ({ ...p, dataUrl: '' }))}
                          themeImageRemovable={themeImageRemovable(kind, slots)}
                          dragHandle={slots.length > 0 && state.dataUrl
                            ? <DraggableAssetChip kind={kind} dataUrl={state.dataUrl} />
                            : undefined}
                        />
                      </div>
                    )
                  })}

                  <div className="border-t border-gray-100" />
                  <BankInfoPanel rows={bankInfo} onChange={setBankInfo} />
                </div>
              </div>
            )}
          </div>

          <DragOverlay dropAnimation={null}>
            {dragKind && dragged?.dataUrl ? <AssetDragPreview kind={dragKind} dataUrl={dragged.dataUrl} /> : null}
          </DragOverlay>
        </DndContext>
      </div>
    </div>
  )
}
