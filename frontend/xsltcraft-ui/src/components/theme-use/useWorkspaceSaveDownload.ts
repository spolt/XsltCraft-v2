import { useEffect, useRef, useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { parseGateError } from '../../services/entitlementService'
import { useEntitlementStore } from '../../store/entitlementStore'
import { runDraftSave, useThemeDraftStore } from '../../store/themeDraftStore'
import { toast } from '../../store/toastStore'
import { openUpgradeModal } from '../../store/upgradeModalStore'
import { SAVE_CONFLICT_MESSAGE } from '../xslt-editor/xsltText'
import { composeErrorCode, XSLT_TOO_LARGE_CODE, XSLT_TOO_LARGE_MESSAGE } from '../easy-edit/composeWarningText'
import type { EasyEditSource } from './easyEditSource'

interface Params {
  source: EasyEditSource
  /** Önizlemeyle aynı birleştirmeyi yapar (indirme / kayıt metni). */
  exportXslt: () => Promise<string>
  /** Önizleme hazır (kayıt ve indirme ancak o zaman açık). */
  previewReady: boolean
  xmlContent: string
  confirmLeave: () => boolean
}

/**
 * Kolay düzenle çalışma alanının "Şablonlarıma kaydet" ve "İndir" işlemleri; düğme metinleri ve kapalılık durumu
 * başlık düğmeleri ile belge çubuğunda ortak kullanılır.
 */
export function useWorkspaceSaveDownload({ source, exportXslt, previewReady, xmlContent, confirmLeave }: Params) {
  const navigate = useNavigate()
  const workingXslt = useThemeDraftStore((s) => s.workingXslt)
  const [isDownloading, setIsDownloading] = useState(false)
  const [isSaving, setIsSaving] = useState(false)
  const [saveSuccess, setSaveSuccess] = useState(false)
  const mountedRef = useRef(true)
  const saveRedirectRef = useRef<ReturnType<typeof setTimeout> | null>(null)
  const saveBlockedReason = source.saveBlockedReason
  const isDoc = source.kind === 'doc'
  const tooLarge = (err: unknown) => isDoc && composeErrorCode(err) === XSLT_TOO_LARGE_CODE

  useEffect(() => {
    mountedRef.current = true
    return () => {
      mountedRef.current = false
      if (saveRedirectRef.current !== null) clearTimeout(saveRedirectRef.current)
    }
  }, [])

  async function handleDownload() {
    if (isDownloading) return
    setIsDownloading(true)
    try {
      const xslt = await exportXslt()
      const blob = new Blob([xslt], { type: 'application/xslt+xml' })
      const a = document.createElement('a')
      a.href = URL.createObjectURL(blob)
      a.download = source.downloadName
      a.click()
      URL.revokeObjectURL(a.href)
      source.onDownloaded()
    } catch (err) {
      await source.onComposeError(err)
      toast.error(tooLarge(err) ? XSLT_TOO_LARGE_MESSAGE : 'İndirme başarısız.')
    } finally {
      setIsDownloading(false)
    }
  }

  async function handleSave() {
    if (isSaving || saveBlockedReason) return

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
        const xslt = await exportXslt()
        if (!mountedRef.current || useThemeDraftStore.getState().session !== session) return
        const result = await source.save(saved, xslt, xmlContent)
        markSaved(session, result, workingXslt)
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
        toast.error(tooLarge(err) ? XSLT_TOO_LARGE_MESSAGE : 'Kaydetme başarısız.')
      }
    } finally { setIsSaving(false) }
  }

  /** Kayıt sürüyor ya da tamamlandı (yönlendirme bekleniyor): mod geçişi ve editör düğmesi kapalı. */
  const saveBusy = isSaving || saveSuccess
  return {
    handleSave,
    handleDownload,
    saveBusy,
    saveDisabled: saveBusy || !previewReady || !!saveBlockedReason,
    saveLabel: saveSuccess ? 'Kaydedildi' : isSaving ? 'Kaydediliyor…' : 'Şablonlarıma kaydet',
    downloadDisabled: isDownloading || !previewReady,
    /** İndirme sürerken "İndiriliyor…", değilse `idle`. */
    downloadLabel: (idle: string) => (isDownloading ? 'İndiriliyor…' : idle),
  }
}
