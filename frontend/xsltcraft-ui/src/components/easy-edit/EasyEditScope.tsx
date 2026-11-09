import { useEffect, useLayoutEffect, useRef, useState } from 'react'
import { Outlet, useNavigate, useParams } from 'react-router-dom'
import { getUserXsltTemplate, releaseEditingLock } from '../../services/userXsltService'
import { isProOrAbove } from '../../services/entitlementService'
import { useAuthStore } from '../../store/authStore'
import { useEntitlementStore } from '../../store/entitlementStore'
import { useDocumentHandoffStore, type DocumentHandoffPayload } from '../../store/documentHandoffStore'
import { draftKey, useThemeDraftStore } from '../../store/themeDraftStore'
import { toast } from '../../store/toastStore'
import { useEditingLock } from '../../hooks/useEditingLock'
import { isLockHandoff } from '../../hooks/lockHandoff'
import { toDownloadName } from '../theme-use/easyEditSource'
import { initialDocFields } from './docDraft'
import { resolveDocAnalysis } from './resolveDocAnalysis'
import { templateDraftId, type EasyEditDoc } from './easyEditDoc'
import DocumentLoader from '../ui/DocumentLoader'

type Gate = 'pending' | 'allowed' | 'denied' | 'error'

const TOAST_MS = 6000
const LOADING_STEPS = ['Şablon okunuyor', 'Düzenlenebilir alanlar çıkarılıyor', 'Kolay düzenle hazırlanıyor']
const hasToken = () => !!useAuthStore.getState().accessToken

/**
 * Kolay düzenle Pro ve üstüne açık. Yetkiler yüklenmediyse bir kez istenir (başka bir istek sürüyorsa o beklenir);
 * istek başarısızsa `error` döner.
 */
function useProGate(): Gate {
  const entitlements = useEntitlementStore((s) => s.entitlements)
  const loaded = useEntitlementStore((s) => s.loaded)
  const loading = useEntitlementStore((s) => s.loading)
  const [failed, setFailed] = useState(false)
  // StrictMode'da effect iki kez çalışır; istek yine de bir kez gider.
  const requestedRef = useRef(false)

  useEffect(() => {
    if (loaded || loading || requestedRef.current) return
    requestedRef.current = true
    void useEntitlementStore.getState().refresh().then(() => {
      if (!useEntitlementStore.getState().loaded) setFailed(true)
    })
  }, [loaded, loading])

  if (loaded) return isProOrAbove(entitlements) ? 'allowed' : 'denied'
  return failed ? 'error' : 'pending'
}

/**
 * Yüklenen dosya (`/easy-edit/:draftId`) ya da kayıtlı şablon (`/easy-edit/template/:templateId`) için Kolay düzenle
 * kapsamı. Taslağı belgeye bağlar, aktarımı bir kez tüketir (şablonda yoksa sunucudan yükler), analizi çözer ve
 * sayfayı ancak taslak hazır olunca render eder. Çıkışta taslak silinir ve kilit bırakılır.
 */
export default function EasyEditScope() {
  const { draftId, templateId } = useParams<{ draftId?: string; templateId?: string }>()
  const navigate = useNavigate()
  const isTemplateRoute = templateId !== undefined
  const handoffId = isTemplateRoute ? templateDraftId(templateId) : draftId
  const key = handoffId ? draftKey.doc(handoffId) : null

  const gate = useProGate()
  // Kapı bir kez açıldıysa açık kalır: yetkilerin sonradan yeniden yüklenmesi (ör. oturum kapanışı) taslağı boşaltmasın.
  const [unlocked, setUnlocked] = useState(false)
  if (gate === 'allowed' && !unlocked) setUnlocked(true)
  const navigateRef = useRef(navigate)
  useEffect(() => { navigateRef.current = navigate })
  const boundKey = useThemeDraftStore((s) => s.boundKey)
  const [ready, setReady] = useState<{ key: string; doc: EasyEditDoc } | null>(null)
  // StrictMode'da effect iki kez çalışır: aktarım bir kez tüketilir, ikinci çalışma aynı yükü kullanır.
  const consumedRef = useRef<{ id: string; payload: DocumentHandoffPayload | null } | null>(null)
  const deniedRef = useRef(false)

  const lockTemplateId = templateId ?? (ready?.key === key ? ready.doc.templateId : null)
  useEditingLock(unlocked ? lockTemplateId : null, {
    onLost: (ownerName) =>
      toast.warning(`Şu an ${ownerName ?? 'başka bir kullanıcı'} bu şablonu düzenliyor; kaydedilemez.`, {
        title: 'Şablon kilitli', durationMs: TOAST_MS,
      }),
    shouldRelease: hasToken,
  })

  // Rota koruması: Pro değilse (ya da yetki bilgisi alınamadıysa) kayıtlı şablon editörde, yüklenen dosya Dashboard'da açılır.
  useEffect(() => {
    if (unlocked || (gate !== 'denied' && gate !== 'error') || deniedRef.current) return
    deniedRef.current = true
    if (gate === 'error') {
      toast.error('Üyelik bilgisi alınamadı, tekrar deneyin.', { title: 'Kolay düzenle', durationMs: TOAST_MS })
    } else {
      toast.info('Kolay düzenle XsltCraft Pro’ya dahildir.', { title: 'Pro özellik', durationMs: TOAST_MS })
    }
    navigateRef.current(isTemplateRoute ? `/xslt-editor/${templateId}` : '/dashboard', {
      replace: true,
      // Editör kendi bilgi toast'unu göstermesin (çift toast).
      state: gate === 'denied' && isTemplateRoute ? { easyEditDenied: true } : undefined,
    })
  }, [gate, unlocked, isTemplateRoute, templateId])

  useLayoutEffect(() => {
    if (!key || !handoffId || !unlocked) return
    useThemeDraftStore.getState().bind(key, isTemplateRoute ? 'template' : 'upload')
    const { session } = useThemeDraftStore.getState()
    const controller = new AbortController()
    const aborted = () => controller.signal.aborted
    const navigate = navigateRef.current

    if (consumedRef.current?.id !== handoffId) {
      consumedRef.current = { id: handoffId, payload: useDocumentHandoffStore.getState().consume(handoffId) }
    }
    const staged = consumedRef.current.payload

    void (async () => {
      let payload = staged
      if (!payload && templateId !== undefined) {
        try {
          const t = await getUserXsltTemplate(templateId)
          payload = {
            source: 'template',
            templateId: t.id,
            savedUpdatedAt: t.updatedAt,
            name: t.name,
            fileName: toDownloadName(t.name),
            xslt: t.xsltContent,
            xml: t.xmlContent ?? undefined,
            dirty: false,
          }
        } catch {
          if (aborted()) return
          toast.error('Şablon yüklenemedi.', { durationMs: TOAST_MS })
          navigate('/my-xslt-templates', { replace: true })
          return
        }
      }
      if (aborted()) return
      if (!payload) {
        // Aktarım bellek içidir: sayfa yenilenince kaybolur.
        toast.info('Belge bulunamadı. Sayfa yenilendiyse dosyayı yeniden yükleyin.', { title: 'Kolay düzenle', durationMs: TOAST_MS })
        navigate('/dashboard', { replace: true })
        return
      }

      const result = await resolveDocAnalysis(payload, controller.signal)
      if (aborted() || (!result.ok && result.canceled)) return
      const docTemplateId = payload.templateId ?? null
      if (!result.ok) {
        toast.info(result.message, { title: 'Kolay düzenle açılamadı', durationMs: TOAST_MS })
        navigate(docTemplateId ? `/xslt-editor/${docTemplateId}` : '/dashboard', { replace: true })
        return
      }

      const { name, xml, savedUpdatedAt } = payload
      useThemeDraftStore.setState((s) => (s.session !== session ? s : {
        ...initialDocFields(result.analysis, name),
        source: payload.source,
        xml: xml ? { content: xml, fileName: 'Belgeyle gelen XML' } : null,
        saved: docTemplateId && savedUpdatedAt ? { id: docTemplateId, updatedAt: savedUpdatedAt, name } : null,
      }))
      setReady({
        key,
        doc: { source: payload.source, templateId: docTemplateId, fileName: payload.fileName, handoffDirty: payload.dirty, xslt: payload.xslt },
      })
    })()

    return () => {
      controller.abort()
      const { saved, reset } = useThemeDraftStore.getState()
      // Yüklenen dosya bu oturumda kaydedildiyse kilidi burada bırakılır; açılan şablonun kilidi useEditingLock'ta.
      // Editöre geçişte (aynı şablon) kilit bırakılmaz.
      if (saved && saved.id !== templateId && saved.id !== consumedRef.current?.payload?.templateId
        && !isLockHandoff(saved.id) && hasToken()) {
        releaseEditingLock(saved.id).catch(() => {})
      }
      reset()
      setReady(null)
    }
  }, [key, handoffId, unlocked, isTemplateRoute, templateId])

  if (!key || !unlocked || ready?.key !== key || boundKey !== key) {
    if (gate === 'denied' || gate === 'error') return <div className="h-screen bg-ui-bg" />
    return <DocumentLoader title="Belge hazırlanıyor" steps={LOADING_STEPS} />
  }
  return <Outlet context={ready.doc} />
}
