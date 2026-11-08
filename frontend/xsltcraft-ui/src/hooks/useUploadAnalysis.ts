import { useCallback, useEffect, useRef, useState } from 'react'
import { parseEasyEditError, type EasyEditAnalyzeResponse } from '../services/easyEditService'
import { isProOrAbove } from '../services/entitlementService'
import { useEntitlementStore } from '../store/entitlementStore'
import { analyzeCached, easyEditErrorText } from '../components/easy-edit/analyzeCached'
import { xmlParseErrorsOf } from '../components/xslt-editor/xsltText'
import { validateParsedContent, validateUploadFile } from '../utils/uploadValidation'

/**
 * Dashboard'daki XSLT'nin Kolay düzenle uygunluğu. `ready` yalnız `suitable`/`partial` kararında ve işaretli metin
 * döndüğünde; `xslt` her durumda kullanıcının dosyasındaki ham metindir (aktarım ve önbellek anahtarı bununla).
 */
export type UploadAnalysis =
  | { status: 'idle' }
  | { status: 'analyzing' }
  | { status: 'fileError'; message: string }
  | { status: 'invalid'; xslt: string; line: number | null; message: string }
  | { status: 'locked'; xslt: string }
  | { status: 'retry'; xslt: string | null; message: string; retryAfterSeconds?: number }
  | { status: 'unsuitable'; xslt: string; message: string; analysis?: EasyEditAnalyzeResponse }
  | { status: 'rejected'; xslt: string; message: string }
  | { status: 'ready'; xslt: string; analysis: EasyEditAnalyzeResponse; key: string }

export type UploadAnalysisStatus = UploadAnalysis['status']

const PARSE_MESSAGE_MAX = 140
const REJECTED_MESSAGE = 'Dosya güvenlik denetiminden geçmedi.'
const UNSUITABLE_FALLBACK = 'Bu dosya Kolay düzenle için uygun değil.'
const BUSY_MESSAGE = 'Analiz şu an yapılamıyor; birazdan tekrar dene.'

function shortParseMessage(raw: string): string {
  return raw.length > PARSE_MESSAGE_MAX ? `${raw.slice(0, PARSE_MESSAGE_MAX - 1)}…` : raw
}

function fromAnalysis(xslt: string, key: string, a: EasyEditAnalyzeResponse): UploadAnalysis {
  switch (a.verdict) {
    case 'suitable':
    case 'partial':
      return a.xslt ? { status: 'ready', xslt, analysis: a, key } : { status: 'unsuitable', xslt, message: UNSUITABLE_FALLBACK, analysis: a }
    case 'invalid':
      return { status: 'invalid', xslt, line: a.error?.line ?? null, message: a.message || 'Dosya işlenemedi.' }
    case 'rejected':
      return { status: 'rejected', xslt, message: REJECTED_MESSAGE }
    case 'unavailable':
      return { status: 'retry', xslt, message: a.message || BUSY_MESSAGE }
    default:
      return { status: 'unsuitable', xslt, message: a.message || UNSUITABLE_FALLBACK, analysis: a }
  }
}

/** Dosyayı okur, istemcide (tek kez) ayrıştırır, Pro ise (önbellek → sunucu) analiz eder. İptalde `null`. */
async function analyzeFile(file: File, pro: boolean | null, signal: AbortSignal): Promise<UploadAnalysis | null> {
  const fileError = validateUploadFile(file, 'xslt')
  if (fileError) return { status: 'fileError', message: fileError }
  let xslt: string
  try {
    xslt = await file.text()
  } catch {
    return { status: 'fileError', message: 'Dosya okunamadı.' }
  }
  if (signal.aborted) return null

  const doc = new DOMParser().parseFromString(xslt, 'application/xml')
  const [parseError] = xmlParseErrorsOf(doc)
  if (parseError) return { status: 'invalid', xslt, line: parseError.line, message: shortParseMessage(parseError.message) }
  const contentError = validateParsedContent(doc, 'xslt')
  if (contentError) return { status: 'fileError', message: contentError }
  if (pro === false) return { status: 'locked', xslt }

  try {
    const result = await analyzeCached(xslt, undefined, signal)
    return result && fromAnalysis(xslt, result.key, result.analysis)
  } catch (err) {
    const e = await parseEasyEditError(err)
    switch (e.kind) {
      case 'canceled':
        return null
      case 'upgrade':
        return { status: 'locked', xslt }
      case 'badRequest':
        return { status: 'unsuitable', xslt, message: easyEditErrorText(e, UNSUITABLE_FALLBACK) }
      default:
        return { status: 'retry', xslt, message: easyEditErrorText(e), retryAfterSeconds: e.retryAfterSeconds }
    }
  }
}

interface Settled {
  file: File
  attempt: number
  pro: boolean | null
  value: UploadAnalysis
}

/**
 * Yetkiler henüz istenmediyse dosya seçilince bir kez istenir ve beklenir; istek başarısızsa `null` (karar sunucuya
 * bırakılır, 402 → `locked`).
 */
function useProForAnalysis(active: boolean): { pro: boolean | null; waiting: boolean } {
  const entitlements = useEntitlementStore((s) => s.entitlements)
  const loaded = useEntitlementStore((s) => s.loaded)
  const loading = useEntitlementStore((s) => s.loading)
  const [attempted, setAttempted] = useState(false)
  // StrictMode'da effect iki kez çalışır; istek yine de bir kez gider.
  const requestedRef = useRef(false)

  useEffect(() => {
    if (!active || loaded || loading || requestedRef.current) return
    requestedRef.current = true
    void useEntitlementStore.getState().refresh().finally(() => setAttempted(true))
  }, [active, loaded, loading])

  return {
    pro: loaded ? isProOrAbove(entitlements) : null,
    waiting: !loaded && (loading || !attempted),
  }
}

/**
 * Seçilen XSLT değişince önceki analiz iptal edilir ve yenisi başlar. Ayrıştırılamayan dosya sunucuya gitmez;
 * Pro olmayan kullanıcıda analiz çağrısı yapılmaz (`locked`).
 */
export function useUploadAnalysis(file: File | null): { analysis: UploadAnalysis; retry: () => void } {
  const { pro, waiting } = useProForAnalysis(file !== null)

  const [attempt, setAttempt] = useState(0)
  const [settled, setSettled] = useState<Settled | null>(null)

  useEffect(() => {
    if (!file || waiting) return
    const ctrl = new AbortController()
    void analyzeFile(file, pro, ctrl.signal).then((value) => {
      if (value && !ctrl.signal.aborted) setSettled({ file, attempt, pro, value })
    })
    return () => ctrl.abort()
  }, [file, pro, waiting, attempt])

  const retry = useCallback(() => setAttempt((n) => n + 1), [])

  let analysis: UploadAnalysis
  if (!file) analysis = { status: 'idle' }
  else if (settled && settled.file === file && settled.attempt === attempt && settled.pro === pro) analysis = settled.value
  else analysis = { status: 'analyzing' }

  return { analysis, retry }
}
