import { useCallback, useEffect, useMemo, useRef, useState } from 'react'
import { useNavigate } from 'react-router-dom'
import axios from 'axios'
import { draftComposeFields, draftComposeKey } from '../components/dev-mode/draftCompose'
import { extractXmlParseErrors } from '../components/xslt-editor/xsltText'
import { parseGateError } from '../services/entitlementService'
import type { ThemeSlotOption } from '../services/templateService'
import { composeTheme, type ComposeWarning } from '../services/themeComposeService'
import { useDraftField, useThemeDraftStore } from '../store/themeDraftStore'
import { toast } from '../store/toastStore'
import { openUpgradeModal } from '../store/upgradeModalStore'

const COMPOSE_DEBOUNCE_MS = 1200
const NO_SLOTS: ThemeSlotOption[] = []
const NO_WARNINGS: ComposeWarning[] = []
const INVALID_FLUSH_WARNING = 'XSLT düzeltilmeden panel değişiklikleri uygulanamaz.'
const BUSY_MAX_RETRIES = 4
const BUSY_DEFAULT_DELAY_MS = 2000
const BUSY_MAX_DELAY_MS = 30_000

/** Sunucu meşgulse (429/503) beklenecek süre, değilse null. `Retry-After` okunamazsa (CORS) varsayılan süre. */
function busyRetryDelayMs(err: unknown): number | null {
  if (!axios.isAxiosError(err)) return null
  const status = err.response?.status
  if (status !== 429 && status !== 503) return null
  const seconds = Number(err.response?.headers?.['retry-after'])
  return seconds > 0 ? Math.min(seconds * 1000, BUSY_MAX_DELAY_MS) : BUSY_DEFAULT_DELAY_MS
}

export interface DevModeCompose {
  warnings: ComposeWarning[]
  error: string | null
  errorField: string | null
  /** Temada Notlar alanı var mı; null → geçerli metinden henüz yanıt alınmadı. */
  fixedNotesSupported: boolean | null
  /** Bekleyen varlık değişikliği var ama metin bozuk: düzeltilince uygulanacak. */
  pendingInvalid: boolean
  /** Debounce beklemeden uygula; aynı zamanda başarısız son denemeyi yeniden dener. */
  composeNow: () => void
  /**
   * İndir/Kaydet öncesi: bekleyen değişikliği beklemeden uygular ve bitmesini bekler; meşgul yanıtında yeniden
   * denemeleri de bekler. İstek başarısızsa ya da ekrandan çıkıldıysa false. Metin bozuksa uyarır ve true döner
   * (editördeki metin kullanılır).
   */
  flush: () => Promise<boolean>
}

/**
 * Varlık değişikliklerini çalışma metnine işler. Tek effect: istenen anahtar metne işlenmiş anahtardan
 * farklıyken istek atar. Metin değişince cleanup isteği iptal eder; yanıt metin ve anahtarı atomik yazar,
 * bu yüzden kullanıcının yazdığı metin ezilmez ve varlık değişikliği yeni metne yeniden uygulanır.
 * Sonuç kullanıcı düzenlemesi sayılmaz (`xsltEdited` değişmez).
 */
export function useDevModeCompose(initialWarnings: ComposeWarning[] | null): DevModeCompose {
  const navigate = useNavigate()
  const xslt = useThemeDraftStore((s) => s.workingXslt)
  const composedKey = useThemeDraftStore((s) => s.composedKey)
  const slots = useThemeDraftStore((s) => s.meta?.slots ?? NO_SLOTS)
  const logo = useThemeDraftStore((s) => s.logo)
  const signature = useThemeDraftStore((s) => s.signature)
  const bankInfo = useThemeDraftStore((s) => s.bankInfo)
  const fixedNotes = useThemeDraftStore((s) => s.fixedNotes)
  const notesTouched = useThemeDraftStore((s) => s.notesTouched)
  const session = useThemeDraftStore((s) => s.session)
  const [fixedNotesSupported, setFixedNotesSupported] = useDraftField('fixedNotesSupported')

  const [warnings, setWarnings] = useState<ComposeWarning[] | null>(null)
  const [failure, setFailure] = useState<{ message: string; field: string | null } | null>(null)
  const [nonce, setNonce] = useState(0)
  const immediateRef = useRef(false)
  const flushWaitersRef = useRef<Array<(ok: boolean) => void>>([])
  /** Meşgul yanıtı sonrası bekleniyor: flush beklemeyi kısaltmaz, aynı sonuca bağlanır. */
  const busyWaitRef = useRef(false)

  const settleFlush = useCallback((ok: boolean) => {
    flushWaitersRef.current.splice(0).forEach((resolve) => resolve(ok))
  }, [])

  const fields = useMemo(
    () => draftComposeFields({ logo, signature, bankInfo, fixedNotes, notesTouched }, slots),
    [logo, signature, bankInfo, fixedNotes, notesTouched, slots],
  )
  const desiredKey = useMemo(() => draftComposeKey(fields), [fields])

  const pending = xslt !== null && desiredKey !== composedKey
  // Sunucu bozuk metinde notları desteklemiyor der; bozuk metin hiç gönderilmez.
  const pendingInvalid = useMemo(
    () => pending && extractXmlParseErrors(xslt).length > 0,
    [pending, xslt],
  )

  useEffect(() => {
    const immediate = immediateRef.current
    immediateRef.current = false
    if (!pending || pendingInvalid) return

    const controller = new AbortController()
    // Sayaç bu çalıştırmaya aittir: metin, varlık ya da `composeNow` değişince effect baştan başlar.
    let busyRetries = 0
    let timer: ReturnType<typeof setTimeout>
    const attempt = async () => {
      busyWaitRef.current = false
      setFailure(null)
      try {
        const result = await composeTheme({ xslt, ...fields, includeXslt: true, includeHtml: false }, controller.signal)
        // Store yazımı effect cleanup'ından önce olabilir: iptale ek olarak metnin aynı kaldığına bakılır.
        const draft = useThemeDraftStore.getState()
        if (controller.signal.aborted || draft.workingXslt !== xslt) return
        draft.setComposedXslt(result.xslt ?? xslt, desiredKey, session)
        setWarnings(result.warnings)
        if (typeof result.fixedNotesSupported === 'boolean') setFixedNotesSupported(result.fixedNotesSupported)
      } catch (err) {
        if (axios.isCancel(err) || controller.signal.aborted) return
        const busyDelay = busyRetries < BUSY_MAX_RETRIES ? busyRetryDelayMs(err) : null
        if (busyDelay !== null) {
          busyRetries++
          busyWaitRef.current = true
          timer = setTimeout(attempt, busyDelay)
          return
        }
        const gate = await parseGateError(err)
        if (controller.signal.aborted) return
        if (gate.status === 402) {
          openUpgradeModal({
            title: 'Ücretli tema',
            message: gate.message ?? 'Bu temayı kullanmak için XsltCraft Pro üyeliği gerekir.',
          })
          settleFlush(false)
          navigate('/templates')
          return
        }
        const data = axios.isAxiosError(err)
          ? (err.response?.data as { error?: string; message?: string; field?: string } | undefined)
          : undefined
        const message = data?.message ?? data?.error ?? 'Değişiklikler XSLT\'ye işlenemedi.'
        setFailure({ message, field: data?.field ?? null })
        if (flushWaitersRef.current.length > 0) toast.error(message, { title: 'Panel değişiklikleri uygulanamadı' })
        settleFlush(false)
      }
    }
    timer = setTimeout(attempt, immediate ? 0 : COMPOSE_DEBOUNCE_MS)

    return () => {
      clearTimeout(timer)
      controller.abort()
      busyWaitRef.current = false
    }
  }, [pending, pendingInvalid, xslt, fields, desiredKey, session, nonce, navigate, setFixedNotesSupported, settleFlush])

  // Bekleyen flush: metin işlendiyse biter; bekleme sırasında metin bozulduysa istek gitmeyeceğinden uyarıp bırakır.
  useEffect(() => {
    if (!pending) {
      settleFlush(true)
    } else if (pendingInvalid && flushWaitersRef.current.length > 0) {
      toast.warning(INVALID_FLUSH_WARNING, { durationMs: 6000 })
      settleFlush(true)
    }
  }, [pending, pendingInvalid, settleFlush])

  useEffect(() => () => settleFlush(false), [settleFlush])

  const composeNow = useCallback(() => {
    immediateRef.current = true
    setNonce((n) => n + 1)
  }, [])

  const flush = useCallback(async () => {
    const draft = useThemeDraftStore.getState()
    const key = draftComposeKey(draftComposeFields(draft, draft.meta?.slots ?? NO_SLOTS))
    if (draft.workingXslt === null || key === draft.composedKey) return true
    if (extractXmlParseErrors(draft.workingXslt).length > 0) {
      toast.warning(INVALID_FLUSH_WARNING, { durationMs: 6000 })
      return true
    }
    return new Promise<boolean>((resolve) => {
      flushWaitersRef.current.push(resolve)
      if (!busyWaitRef.current) composeNow()
    })
  }, [composeNow])

  return {
    warnings: warnings ?? initialWarnings ?? NO_WARNINGS,
    error: pending ? failure?.message ?? null : null,
    errorField: pending ? failure?.field ?? null : null,
    fixedNotesSupported,
    pendingInvalid,
    composeNow,
    flush,
  }
}
