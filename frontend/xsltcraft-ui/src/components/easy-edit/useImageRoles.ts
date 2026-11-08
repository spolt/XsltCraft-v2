import { useCallback, useEffect, useRef, useState } from 'react'
import { parseEasyEditError, type EasyEditAnalyzeResponse, type EasyEditImageRole } from '../../services/easyEditService'
import { isValidSlotId } from '../../services/easyEditGuards'
import { useThemeDraftStore } from '../../store/themeDraftStore'
import { openUpgradeModal } from '../../store/upgradeModalStore'
import type { EasyEditDocInfo } from '../theme-use/easyEditSource'
import { imageAfterReanalysis, initialBanks, isEditableVerdict, shouldAskImageRoles } from './docDraft'
import { analyzeCached, EASY_EDIT_PRO_MESSAGE, easyEditErrorText } from './analyzeCached'

const REANALYSIS_FAILED = 'Görseller yeniden analiz edilemedi; önceki seçimler korunuyor.'

/** Yeni analizi taslağa uygular: analiz ve konumlar yenilenir, görseller ve banka satırları yeniden kurulur (statik düzenlemeler sıfırlanır). */
function applyAnalysis(analysis: EasyEditAnalyzeResponse, session: number) {
  useThemeDraftStore.setState((s) => (s.session !== session ? s : {
    analysis,
    meta: s.meta ? { ...s.meta, slots: (analysis.slots ?? []).filter((x) => isValidSlotId(x.id)) } : s.meta,
    logo: imageAfterReanalysis(analysis, 'logo', s.logo),
    signature: imageAfterReanalysis(analysis, 'signature', s.signature),
    // Belge satır indisleri analize göredir: belge IBAN'ı (eski ya da yeni analizde) varsa satırlar yeni analizden kurulur.
    bankInfo: analysis.documentBank?.found || s.analysis?.documentBank?.found ? initialBanks(analysis) : s.bankInfo,
    fixedNotesSupported: analysis.notesSupported,
  }))
}

/**
 * Tek soru modalının durumu. Yüklenen dosyada işaretsiz görsel ya da önceki kayıt varsa kendiliğinden açılır;
 * "Görsel seçimlerini değiştir" yeniden açar. Roller değişirse özgün metin bu rollerle yeniden analiz edilir
 * (önce önbelleğe bakılır) ve yeni analiz base olur.
 */
export function useImageRoles(doc: EasyEditDocInfo | undefined) {
  const [open, setOpen] = useState(() => !!doc && shouldAskImageRoles(doc.analysis, doc.origin))
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const controllerRef = useRef<AbortController | null>(null)

  useEffect(() => () => controllerRef.current?.abort(), [])

  const close = useCallback(() => {
    controllerRef.current?.abort()
    setBusy(false)
    setError(null)
    setOpen(false)
  }, [])

  const show = useCallback(() => {
    setError(null)
    setOpen(true)
  }, [])

  const submit = useCallback(async (roles: EasyEditImageRole[]) => {
    const analysis = doc?.analysis
    if (!doc || !analysis) return
    const current = (analysis.images ?? []).map((i) => i.role)
    if (roles.length === current.length && roles.every((r, i) => r === current[i])) {
      close()
      return
    }
    controllerRef.current?.abort()
    const controller = new AbortController()
    controllerRef.current = controller
    const { session } = useThemeDraftStore.getState()
    setBusy(true)
    setError(null)
    try {
      const result = await analyzeCached(doc.originalXslt, roles, controller.signal)
      if (!result || controller.signal.aborted) return
      const next = result.analysis
      if (!isEditableVerdict(next)) {
        setError(next.message || 'Bu seçimlerle belge Kolay düzenle için uygun değil; önceki seçimler korunuyor.')
        return
      }
      applyAnalysis(next, session)
      setOpen(false)
    } catch (err) {
      const parsed = await parseEasyEditError(err)
      if (parsed.kind === 'canceled' || controller.signal.aborted) return
      if (parsed.kind === 'upgrade') {
        openUpgradeModal({ title: 'Kolay düzenle', message: parsed.message ?? EASY_EDIT_PRO_MESSAGE })
      }
      setError(easyEditErrorText(parsed, REANALYSIS_FAILED))
    } finally {
      if (!controller.signal.aborted) setBusy(false)
    }
  }, [doc, close])

  return { open, busy, error, show, close, submit }
}
