import { useCallback, useEffect, useRef, useState } from 'react'
import { LEAVE_CONFIRM, SAVE_CONFLICT_MESSAGE } from '../components/xslt-editor/xsltText'
import { parseGateError } from '../services/entitlementService'
import { createUserXsltTemplate, updateUserXsltTemplate } from '../services/userXsltService'
import { useEntitlementStore } from '../store/entitlementStore'
import { runDraftSave, useThemeDraftStore } from '../store/themeDraftStore'
import { toast } from '../store/toastStore'
import { openUpgradeModal } from '../store/upgradeModalStore'

interface Options {
  themeId: string | undefined
  /** Dialogun ilk kayıttaki ad önerisi (tema adı). */
  defaultName: string
  xmlContent: string | null
  /** Çalışma metni hazır değilken Ctrl+S kapalıdır. */
  enabled: boolean
  /** Kayıttan önce bekleyen panel değişikliklerini metne işler; false ise kayıt yapılmaz. */
  flush: () => Promise<boolean>
}

export interface TemplateSave {
  dialogOpen: boolean
  dialogName: string
  isUpdate: boolean
  saving: boolean
  requestSave: () => void
  closeDialog: () => void
  save: (name: string) => Promise<void>
}

/** Yükseltme penceresinden çıkış: karar anındaki metne bakar (kayıt sürerken metin değişmiş olabilir). */
function confirmDraftLeave() {
  const { workingXslt, baselineXslt } = useThemeDraftStore.getState()
  return workingXslt === baselineXslt || window.confirm(LEAVE_CONFIRM)
}

/**
 * Şablonlarıma kaydet: ilk kayıt oluşturur, sonrakiler aynı şablonu günceller. Sayfada kalınır. Gönderilen
 * metin baseline olur; kayıt sürerken yazılan metin kaydedilmemiş sayılmaya devam eder.
 */
export function useTemplateSave({ themeId, defaultName, xmlContent, enabled, flush }: Options): TemplateSave {
  const [dialogOpen, setDialogOpen] = useState(false)
  const [saving, setSaving] = useState(false)
  const savingRef = useRef(false)
  const isUpdate = useThemeDraftStore((s) => s.saved !== null)
  const savedName = useThemeDraftStore((s) => s.saved?.name)

  const requestSave = useCallback(() => {
    // Ham XSLT'yi "Şablonlarım"a saklamak Pro üyelik gerektirir (Standart kullanıcı indirebilir ama saklayamaz).
    const ent = useEntitlementStore.getState().entitlements
    if (ent && !ent.canSaveRawXslt && !ent.isPrivileged) {
      openUpgradeModal({
        title: 'Kaydetme Pro üyelik gerektirir',
        message: "XSLT’yi indirebilirsin, ancak 'Şablonlarım'da saklamak XsltCraft Pro’ya dahildir.",
        confirmLeave: confirmDraftLeave,
      })
      return
    }
    setDialogOpen(true)
  }, [])

  useEffect(() => {
    if (!enabled) return
    function handleKeyDown(e: KeyboardEvent) {
      if ((e.ctrlKey || e.metaKey) && e.key === 's') {
        e.preventDefault()
        requestSave()
      }
    }
    document.addEventListener('keydown', handleKeyDown)
    return () => document.removeEventListener('keydown', handleKeyDown)
  }, [enabled, requestSave])

  const save = useCallback(async (name: string) => {
    // Enter ile ikinci gönderim ikinci şablon oluşturmasın.
    if (savingRef.current || !themeId) return
    savingRef.current = true
    setSaving(true)
    try {
      await runDraftSave(async (saved) => {
        if (!(await flush())) return
        const { workingXslt: xsltContent, session, markSaved } = useThemeDraftStore.getState()
        if (xsltContent === null) return
        const xml = xmlContent ?? undefined
        const result = saved
          ? await updateUserXsltTemplate(saved.id, { name, xsltContent, xmlContent: xml, expectedUpdatedAt: saved.updatedAt })
          : await createUserXsltTemplate({ name, xsltContent, xmlContent: xml })
        markSaved(session, { id: result.id, updatedAt: result.updatedAt, name: result.name }, xsltContent)
        setDialogOpen(false)
        toast.success('Şablon kaydedildi.', { durationMs: 2500 })
      })
    } catch (err) {
      const { status } = await parseGateError(err)
      if (status === 402) {
        setDialogOpen(false)
        openUpgradeModal({
          title: 'Kaydetme Pro üyelik gerektirir',
          message: "'Şablonlarım'da saklama XsltCraft Pro’ya dahildir.",
          confirmLeave: confirmDraftLeave,
        })
      } else if (status === 423) {
        // Salt-okunura geçilmez: metin editörde kalır, yalnız bildirilir.
        setDialogOpen(false)
        toast.error('Bu şablonu şu an başka bir kullanıcı düzenliyor. Kaydedilemedi.', {
          title: 'Şablon kilitli', durationMs: null,
        })
      } else if (status === 409) {
        setDialogOpen(false)
        toast.error(SAVE_CONFLICT_MESSAGE, { title: 'Çakışma', durationMs: null })
      } else {
        toast.error('Kayıt sırasında bir hata oluştu.')
      }
    } finally {
      savingRef.current = false
      setSaving(false)
    }
  }, [themeId, xmlContent, flush])

  return {
    dialogOpen,
    dialogName: savedName ?? defaultName,
    isUpdate,
    saving,
    requestSave,
    // Süren kayıtta kapatılmaz: arka planda biten kayıt ekrandan bağımsız sürerdi.
    closeDialog: useCallback(() => { if (!savingRef.current) setDialogOpen(false) }, []),
    save,
  }
}
