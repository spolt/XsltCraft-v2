import { useEffect, useRef, useState } from 'react'
import { useLocation, useNavigate } from 'react-router-dom'
import { isProOrAbove } from '../services/entitlementService'
import type { XsltEditMode } from '../services/userXsltService'
import { useEntitlementStore } from '../store/entitlementStore'
import { toast } from '../store/toastStore'
import { useUiPrefsStore } from '../store/uiPrefsStore'
import { openUpgradeModal } from '../store/upgradeModalStore'
import { stageEasyEditFromEditor } from '../components/easy-edit/editorHandoff'
import { EASY_EDIT_PRO_MESSAGE } from '../components/easy-edit/analyzeCached'
import { stripXsltExt } from '../utils/uploadValidation'
import { markLockHandoff } from './lockHandoff'
import { useEasyEditSuggestion } from './useEasyEditSuggestion'

const TOAST_MS = 6000

/** EasyEditScope'un Pro olmayan kullanıcıyı editöre yönlendirdiği `location.state` (kendi toast'unu gösterdi). */
const isDeniedState = (state: unknown) =>
  !!state && typeof state === 'object' && (state as { easyEditDenied?: unknown }).easyEditDenied === true

export interface EasyEditModeSwitchParams {
  xslt: string
  xml: string | null
  templateId: string | null
  templateName: string
  /** Görüntülenen dosya adı (ör. `fatura.xslt`). */
  fileName: string
  /** Yüklenen dosyanın adı (kaydedilmemiş belgede öneri bandının belge anahtarı). */
  uploadedFileName: string | null
  dirty: boolean
  /** Şablon yükleniyor: belge çubuğu gizli. */
  loading: boolean
  lastEditMode: XsltEditMode | null
  /** Kayıtta çakışma denetimi için bilinen `updatedAt`. */
  savedUpdatedAt: () => string | null
}

export interface EasyEditModeSwitch {
  /** Pro ve üstünde belge çubuğu (mod anahtarı + kaydet/indir) gösterilir. */
  showDocumentBar: boolean
  /** Öneri bandı görünür (kapatılmadı ve metin uygun görünüyor). */
  showHint: boolean
  dismissHint: () => void
  switching: boolean
  switchToEasyEdit: () => Promise<void>
}

/**
 * XSLT Editör → Kolay düzenle mod geçişi (Pro ve üstü), öneri bandı ve Pro olmayan kullanıcının en son Kolay düzenle ile
 * düzenlenen şablonu açtığındaki bilgi toast'u.
 */
export function useEasyEditModeSwitch(p: EasyEditModeSwitchParams): EasyEditModeSwitch {
  const navigate = useNavigate()
  const location = useLocation()
  const entitlements = useEntitlementStore((s) => s.entitlements)
  const entitlementsLoaded = useEntitlementStore((s) => s.loaded)
  const isPro = isProOrAbove(entitlements)
  const showDocumentBar = isPro && !!p.xslt && !p.loading
  const hintDismissed = useUiPrefsStore((s) => s.easyEditHintDismissed)
  const dismissHint = useUiPrefsStore((s) => s.dismissEasyEditHint)
  const suggest = useEasyEditSuggestion(p.xslt, p.templateId ?? p.uploadedFileName, showDocumentBar && !hintDismissed)
  const [switching, setSwitching] = useState(false)
  const switchAbortRef = useRef<AbortController | null>(null)
  useEffect(() => () => switchAbortRef.current?.abort(), [])

  /**
   * Metin (kaydedilmemiş değişiklikler dahil) Kolay düzenle'ye aktarılır; değişmeyen metinde önbellekteki analiz
   * kullanılır. Analiz uygun değilse nedeni toast ile gösterilir ve editörde kalınır.
   */
  async function switchToEasyEdit() {
    if (switching || !p.xslt) return
    if (!isPro) {
      openUpgradeModal({ title: 'Kolay düzenle Pro üyelik gerektirir', message: EASY_EDIT_PRO_MESSAGE })
      return
    }
    const controller = new AbortController()
    switchAbortRef.current = controller
    setSwitching(true)
    try {
      const result = await stageEasyEditFromEditor({
        xslt: p.xslt,
        xml: p.xml,
        templateId: p.templateId,
        savedUpdatedAt: p.savedUpdatedAt(),
        name: p.templateName || stripXsltExt(p.fileName),
        fileName: p.fileName,
        dirty: p.dirty,
      }, controller.signal)
      if (controller.signal.aborted) return
      if (!result.ok) {
        if (!result.canceled) toast.info(result.message, { title: 'Kolay düzenle açılamadı', durationMs: TOAST_MS })
        return
      }
      // Kolay düzenle aynı şablonun kilidini devralır; editör çıkışta bırakmasın.
      if (p.templateId) markLockHandoff(p.templateId)
      navigate(result.path)
    } finally {
      if (switchAbortRef.current === controller) {
        switchAbortRef.current = null
        setSwitching(false)
      }
    }
  }

  // EasyEditScope'un yönlendirmesiyle gelindiyse onun toast'u zaten gösterildi; bayrak okunur ve state temizlenir.
  const [arrivedDenied] = useState(() => isDeniedState(location.state))
  const deniedState = isDeniedState(location.state)
  useEffect(() => {
    if (deniedState) navigate(location.pathname, { replace: true, state: null })
  }, [deniedState, navigate, location.pathname])

  // En son Kolay düzenle ile düzenlenen şablonu Pro olmayan kullanıcı editörde açar: bir kez bilgilendirilir.
  const easyEditInfoShownRef = useRef(arrivedDenied)
  useEffect(() => {
    if (p.lastEditMode !== 'easyEdit' || !entitlementsLoaded || isPro || easyEditInfoShownRef.current) return
    easyEditInfoShownRef.current = true
    toast.info('Bu şablon en son Kolay düzenle ile düzenlendi. Kolay düzenle XsltCraft Pro’ya dahildir; şablon XSLT Editör’de açıldı.', {
      title: 'Pro özellik', durationMs: TOAST_MS,
    })
  }, [p.lastEditMode, entitlementsLoaded, isPro])

  return {
    showDocumentBar,
    showHint: showDocumentBar && suggest && !hintDismissed,
    dismissHint,
    switching,
    switchToEasyEdit,
  }
}
