import { useCallback, useEffect, useMemo } from 'react'
import { useNavigate } from 'react-router-dom'
import { getTemplate, type ThemeSlotOption } from '../../services/templateService'
import { reportDownload } from '../../services/activityService'
import { parseGateError } from '../../services/entitlementService'
import type {
  ComposeDocumentBankEdit, ComposeFreeImage, ComposeStaticImage,
} from '../../services/themeComposeService'
import { createUserXsltTemplate, updateUserXsltTemplate, type XsltEditMode } from '../../services/userXsltService'
import { openUpgradeModal } from '../../store/upgradeModalStore'
import { useDraftField, useThemeDraftStore, type ThemeDraftMeta, type ThemeDraftSaved } from '../../store/themeDraftStore'
import type { DocBankView } from '../easy-edit/docBank'
import { extractXmlParseErrors } from '../xslt-editor/xsltText'
import { documentTypeFromXslt } from '../dev-mode/draftCompose'
import type { EasyEditAnalyzeResponse } from '../../services/easyEditService'

export type EasyEditDocType = ThemeDraftMeta['documentType']

/**
 * Önizleme/indirme isteğinin kaynağı. Gövdede `themeId` ile `xslt`'den yalnız biri bulunur; ikisi de yoksa istek gitmez
 * (ör. düzenlenmiş metin sözdizimi hatalıyken).
 */
export interface EasyEditComposeSource {
  themeId?: string
  xslt?: string
  /** Yalnız doc kaynağında (`xslt` dalı): her faturada çıkmayabilecek yuvalar. */
  conditionalSlots?: string[]
  /** Yalnız doc kaynağında (`xslt` dalı): dosyanın kendi görselleri için kümülatif düzenleme listesi. */
  staticImages?: ComposeStaticImage[]
  /** Yalnız doc kaynağında (`xslt` dalı): serbest görseller (önceki bloktakiler dahil tam liste). */
  freeImages?: ComposeFreeImage[]
  /** Yalnız doc kaynağında (`xslt` dalı): belgenin kendi IBAN satırları için kümülatif düzenleme listesi. */
  documentBankEdits?: ComposeDocumentBankEdit[]
}

/**
 * Sabit notların gönderimi: `patch` → dokunulmadıysa `null`, boşaltıldıysa `[]`; `filled` → yalnız dolu notlar
 * (yoksa alan gönderilmez); `list` → her zaman liste (boşsa `[]`).
 */
export type EasyEditNotesMode = 'patch' | 'filled' | 'list'

/** `openEditor`'a çalışma alanının verdiği bağlam. */
export interface EasyEditOpenEditorContext {
  /** Güncel ayarlarla birleştirilmiş XSLT (önizlemeyle aynı compose). */
  exportXslt: () => Promise<string>
  /** Kullanıcının yüklediği XML; örnek XML'de null. */
  xml: { content: string; fileName: string } | null
  confirmLeave: () => boolean
}

/** Kaynak tarafında `XSLT Editör` düzenlemesinin durumu. */
export interface EasyEditEditorState {
  /** Metin XSLT editöründe elle düzenlendi; önizleme ve indirme düzenlenmiş metinden yürür. */
  edited: boolean
  /** Düzenlenmiş metin XML olarak ayrıştırılamıyor; önizleme, indirme ve kaydetme kapalı. */
  broken: boolean
}

export interface EasyEditBreadcrumb {
  rootLabel: string
  rootTo: string
  /** Son kırıntı (ör. belge türü etiketi). */
  current: string
}

/** Yalnız doc kaynağında: analiz ve görsel rolleri için bağlam. */
export interface EasyEditDocInfo {
  /** Belgenin geldiği yer (sağ panel etiketi, modalın kendiliğinden açılması). */
  origin: 'upload' | 'template'
  /** Analizin girdisi olan özgün metin; rol değişiminde yeniden analiz edilir. */
  originalXslt: string
  analysis: EasyEditAnalyzeResponse | null
  /** Belge IBAN'ı bulunduysa panel kilitleri, IBAN hataları ve compose `bankInfo`'su; yoksa null. */
  bank?: DocBankView | null
}

export interface EasyEditSavedTemplate {
  id: string
  updatedAt: string
  name: string
}

/**
 * Kolay düzenle çalışma alanının kaynak bağdaştırıcısı. Çalışma alanı belgeyi yalnız bu sözleşmeden tanır;
 * kaynağa özgü gezinme, kayıt ve raporlama burada kalır.
 */
export interface EasyEditSource {
  kind: 'theme' | 'doc'
  name: string
  slots: ThemeSlotOption[]
  docType: EasyEditDocType | null
  composeSource: EasyEditComposeSource
  editor: EasyEditEditorState
  notesMode: EasyEditNotesMode
  breadcrumb: EasyEditBreadcrumb
  downloadName: string
  /** Kaydedilmemiş, kaynaktan yeniden üretilemeyen değişiklik var. */
  isDirty: boolean
  /** Kayıt kapalıysa nedeni (uyarı ve buton açıklaması olarak gösterilir); temada yok. */
  saveBlockedReason?: string
  /** Doc kaynağında belge çubuğunda gösterilen dosya adı; temada yok. */
  fileName?: string
  /** Doc kaynağında analiz bağlamı; temada yok. */
  doc?: EasyEditDocInfo
  openEditor: (ctx: EasyEditOpenEditorContext) => void | Promise<void>
  /** Kayıt hedefine yazar (`saved` yoksa yeni kayıt). */
  save: (saved: ThemeDraftSaved | null, xslt: string, xmlContent: string) => Promise<EasyEditSavedTemplate>
  onDownloaded: () => void
  /** Önizleme/indirme hata yanıtı (402 vb.); kaynağa özgü yönlendirme yapar. */
  onComposeError: (err: unknown) => Promise<void>
}

const NO_SLOTS: ThemeSlotOption[] = []

export const docTypeLabelOf = (docType: EasyEditDocType | null) =>
  docType === 'Despatch' ? 'e-İrsaliye' : docType === 'Invoice' ? 'e-Fatura / e-Arşiv' : '—'

export const toDownloadName = (name: string) => `${name.replace(/[^a-z0-9çğıöşüÇĞİÖŞÜ]/gi, '_')}.xslt`

/** Şablonlarım'a yazar: hedef varsa günceller (ad gönderilmez, kullanıcının verdiği ad ezilmesin), yoksa yeni kayıt. */
export async function saveUserTemplate(
  target: ThemeDraftSaved | null,
  { name, xslt, xmlContent, lastEditMode }: { name: string; xslt: string; xmlContent: string; lastEditMode?: XsltEditMode },
): Promise<EasyEditSavedTemplate> {
  const result = target
    ? await updateUserXsltTemplate(target.id, { xsltContent: xslt, xmlContent, expectedUpdatedAt: target.updatedAt, lastEditMode })
    : await createUserXsltTemplate({ name, xsltContent: xslt, xmlContent, lastEditMode })
  return { id: result.id, updatedAt: result.updatedAt, name: result.name }
}

/** Yanıt 402 ise yükseltme penceresini açar ve true döner. */
export async function openUpgradeOn402(err: unknown, title: string, fallbackMessage: string): Promise<boolean> {
  const gate = await parseGateError(err)
  if (gate.status !== 402) return false
  openUpgradeModal({ title, message: gate.message ?? fallbackMessage })
  return true
}

/** Hazır tema bağdaştırıcısı: taslak store'u, `/dev-mode` gezinmesi ve tema kataloğu. */
export function useThemeEasyEditSource(templateId: string | undefined): EasyEditSource {
  const navigate = useNavigate()

  const [meta, setMeta] = useDraftField('meta')
  const name = meta?.name ?? 'Hazır Tema'
  const slots = meta?.slots ?? NO_SLOTS
  // Geliştirici Modu'nda elle düzenlenen metin varsa önizleme ve indirme onun üzerinden yürür.
  const workingXslt = useThemeDraftStore((s) => s.workingXslt)
  const docType = meta?.documentType ?? (workingXslt !== null ? documentTypeFromXslt(workingXslt) : null)

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

  // Yalnız varlık girilmişse sormaz: bunlar temadan yeniden birleştirilebilir, elle düzenleme birleştirilemez.
  const isDirty = xsltEdited && workingXslt !== baselineXslt

  const onComposeError = useCallback(async (err: unknown) => {
    // Ücretli tema doğrudan URL ile açıldıysa backend 402 döner → Pro'ya yönlendir.
    if (await openUpgradeOn402(err, 'Ücretli tema', 'Bu temayı kullanmak için XsltCraft Pro üyeliği gerekir.')) {
      navigate('/templates')
    }
  }, [navigate])

  const save = useCallback(
    (saved: ThemeDraftSaved | null, xslt: string, xmlContent: string) => saveUserTemplate(saved, { name, xslt, xmlContent }),
    [name],
  )

  return {
    kind: 'theme',
    name,
    slots,
    docType,
    composeSource: {
      themeId: editedXslt === null ? templateId : undefined,
      xslt: editedXslt !== null && !editedXsltBroken ? editedXslt : undefined,
    },
    editor: { edited: xsltEdited, broken: editedXsltBroken },
    notesMode: editedXslt !== null ? 'patch' : 'filled',
    breadcrumb: {
      rootLabel: 'Tema Kütüphanesi',
      rootTo: `/templates?type=${docType ?? 'Invoice'}`,
      current: docTypeLabelOf(docType),
    },
    downloadName: toDownloadName(name),
    isDirty,
    openEditor: () => navigate(`/dev-mode/${templateId}`),
    save,
    onDownloaded: () => reportDownload('Template', templateId),
    onComposeError,
  }
}
