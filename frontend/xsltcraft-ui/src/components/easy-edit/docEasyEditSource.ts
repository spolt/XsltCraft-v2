import { useCallback, useMemo, useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { reportDownload } from '../../services/activityService'
import { toast } from '../../store/toastStore'
import { useThemeDraftStore, type ThemeDraftSaved } from '../../store/themeDraftStore'
import {
  docTypeLabelOf, openUpgradeOn402, saveUserTemplate, toDownloadName, type EasyEditOpenEditorContext, type EasyEditSource,
} from '../theme-use/easyEditSource'
import type { ThemeSlotOption } from '../../services/templateService'
import { toStaticImages } from '../theme-assets/imageState'
import { toComposeFreeImages, vacatedThemeSlots } from '../theme-assets/freeImageState'
import { stripXsltExt, XSLT_EXT } from '../../utils/uploadValidation'
import { markLockHandoff } from '../../hooks/lockHandoff'
import { docComposeSource, hasSkippedPreviousImages, SKIPPED_PREVIOUS_MESSAGE } from './docDraft'
import type { EasyEditDoc } from './easyEditDoc'
import { docBankView } from './docBank'

const NO_SLOTS: ThemeSlotOption[] = []
const baseName = (fileName: string) => stripXsltExt(fileName) || 'Şablon'
const withXsltExt = (fileName: string) => (XSLT_EXT.test(fileName) ? fileName : `${fileName}.xslt`)

/** Kaydedilmemiş değişikliği saptamak için kullanıcının girdiği varlıkların özeti. */
function useAssetFingerprint() {
  const logo = useThemeDraftStore((s) => s.logo)
  const signature = useThemeDraftStore((s) => s.signature)
  const freeImages = useThemeDraftStore((s) => s.freeImages)
  const bankInfo = useThemeDraftStore((s) => s.bankInfo)
  const fixedNotes = useThemeDraftStore((s) => s.fixedNotes)
  return useMemo(
    () => JSON.stringify({ logo, signature, freeImages, bankInfo, fixedNotes }),
    [logo, signature, freeImages, bankInfo, fixedNotes],
  )
}

/** Yüklenen dosya / kayıtlı şablon bağdaştırıcısı. Taslak EasyEditScope'ta analizden doldurulmuş olarak gelir. */
export function useDocEasyEditSource(doc: EasyEditDoc): EasyEditSource {
  const navigate = useNavigate()
  const meta = useThemeDraftStore((s) => s.meta)
  const analysis = useThemeDraftStore((s) => s.analysis)
  const saved = useThemeDraftStore((s) => s.saved)
  const name = meta?.name ?? baseName(doc.fileName)
  const docType = meta?.documentType ?? null

  // Yüklenen dosya ya da kaydedilmemiş değişiklikle aktarılan metin, bu oturumda kaydedilene kadar kaydedilmemiş sayılır.
  // `saved` burada belirleyici değil: kayıtlı şablon açılırken de dolu gelir.
  const fingerprint = useAssetFingerprint()
  const [savedFingerprint, setSavedFingerprint] = useState(fingerprint)
  const [savedInSession, setSavedInSession] = useState(false)
  const unsavedSource = !savedInSession && (doc.source === 'upload' || doc.handoffDirty)
  const isDirty = unsavedSource || fingerprint !== savedFingerprint

  // Dosyanın kendi görsellerindeki değişiklikler (kümülatif tam liste); dokunulmadıysa boş.
  const logo = useThemeDraftStore((s) => s.logo)
  const signature = useThemeDraftStore((s) => s.signature)
  const staticImages = useMemo(() => toStaticImages(logo, signature), [logo, signature])
  const freeImageState = useThemeDraftStore((s) => s.freeImages)
  const vacated = useMemo(() => vacatedThemeSlots(logo, signature), [logo, signature])
  const freeImages = useMemo(() => toComposeFreeImages(freeImageState, vacated), [freeImageState, vacated])
  const bankInfo = useThemeDraftStore((s) => s.bankInfo)
  const bank = useMemo(() => docBankView(analysis, bankInfo), [analysis, bankInfo])
  const composeSource = useMemo(
    () => docComposeSource(analysis, staticImages, freeImages, bank?.edits, vacated),
    [analysis, staticImages, freeImages, bank, vacated],
  )
  const skippedPrevious = useMemo(() => hasSkippedPreviousImages(analysis), [analysis])

  const save = useCallback(async (target: ThemeDraftSaved | null, xslt: string, xmlContent: string) => {
    const result = await saveUserTemplate(target, { name: baseName(doc.fileName), xslt, xmlContent, lastEditMode: 'easyEdit' })
    setSavedFingerprint(fingerprint)
    setSavedInSession(true)
    return result
  }, [doc.fileName, fingerprint])

  const onComposeError = useCallback(async (err: unknown) => {
    await openUpgradeOn402(err, 'Kolay düzenle', 'Kolay düzenle XsltCraft Pro’ya dahildir.')
  }, [])

  /**
   * XSLT Editör'e geçiş: güncel ayarlarla birleştirilmiş metin `pendingXsltContent` olarak aktarılır (sözleşme:
   * `editorHandoff.ts`). Kayıtlı şablonda (bu oturumda kaydedilen dosya dahil) `/xslt-editor/:id` açılır; editör
   * aktarılan metni sunucudaki metnin yerine koyar, böylece kaydedilmemiş değişiklik kaybolmaz.
   */
  const savedId = saved?.id ?? null
  const savedUpdatedAt = saved?.updatedAt
  const openEditor = useCallback(async ({ exportXslt, xml }: EasyEditOpenEditorContext) => {
    let xslt: string
    try {
      xslt = await exportXslt()
    } catch (err) {
      await onComposeError(err)
      toast.error("XSLT Editör'e geçilemedi.")
      return
    }
    const id = doc.templateId ?? savedId
    // Aynı şablonun editör sayfası kilidi devralır; çıkışta bırakılmasın.
    if (id) markLockHandoff(id)
    navigate(id ? `/xslt-editor/${id}` : '/xslt-editor', {
      state: {
        pendingXsltContent: xslt,
        pendingXsltName: withXsltExt(doc.fileName),
        dirty: isDirty,
        ...(id ? { templateId: id, savedUpdatedAt: savedId === id ? savedUpdatedAt : undefined } : {}),
        ...(xml ? { pendingXmlContent: xml.content, pendingXmlName: xml.fileName } : {}),
      },
    })
  }, [doc.templateId, savedId, savedUpdatedAt, doc.fileName, isDirty, navigate, onComposeError])

  const isTemplate = doc.source === 'template'
  return {
    kind: 'doc',
    name,
    slots: meta?.slots ?? NO_SLOTS,
    docType,
    composeSource,
    editor: { edited: false, broken: false },
    notesMode: 'list',
    breadcrumb: {
      rootLabel: isTemplate ? 'Şablonlarım' : 'Dashboard',
      rootTo: isTemplate ? '/my-xslt-templates' : '/dashboard',
      current: docTypeLabelOf(docType),
    },
    fileName: withXsltExt(doc.fileName),
    downloadName: isTemplate ? toDownloadName(name) : withXsltExt(doc.fileName),
    isDirty,
    saveBlockedReason: skippedPrevious ? SKIPPED_PREVIOUS_MESSAGE : undefined,
    doc: { origin: doc.source, originalXslt: doc.xslt, analysis, bank },
    openEditor,
    save,
    onDownloaded: () => reportDownload('Xslt', doc.templateId ?? saved?.id ?? null),
    onComposeError,
  }
}
