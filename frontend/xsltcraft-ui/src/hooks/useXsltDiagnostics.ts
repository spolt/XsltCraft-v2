import { useCallback, useEffect, useMemo, useState, type Dispatch, type SetStateAction } from 'react'
import type { XsltError } from '../components/Xslteditor'
import type { ProblemItem } from '../components/xslt-editor/ProblemsPanel'
import { extractXmlParseErrors, xmlDeclarationProblem } from '../components/xslt-editor/xsltText'
import api from '../services/apiService'
import { validateBusinessRules, type BusinessRuleResult } from '../services/ublTrService'
import { toXsltError } from '../services/xsltErrors'
import { useXslt1CompatWarnings } from './useXslt1CompatWarnings'

const XSLT_VALIDATION_DEBOUNCE_MS = 1500
const XML_VALIDATION_DEBOUNCE_MS = 500

interface Options {
  xslt: string
  xml: string | null
}

export interface XsltDiagnostics {
  xsltProblems: ProblemItem[]
  xmlProblems: ProblemItem[]
  /** null → UBL-TR kontrolü henüz çalışmadı ya da son çalışma başarısız oldu. */
  ublTrProblems: ProblemItem[] | null
  ublTrLoading: boolean
  ublTrError: string | null
  ublTrErrorCount: number | null
  errorCount: number
  warningCount: number
  editorMarkers: XsltError[]
  xmlValid: boolean | null
  /** İlk satırda XML bildirimi eksikse problem öğesi. */
  declProblem: ProblemItem | null
  /** UBL-TR iş kurallarını çalıştırır ve problemler panelini açar. */
  validateUblTr: () => Promise<void>
  showProblems: boolean
  setShowProblems: Dispatch<SetStateAction<boolean>>
  /** Önizlemeden gelen satırlı hatayı XSLT hatası olarak işler. */
  reportXsltError: (message: string, line: number, column: number) => void
  /** XML'i debounce beklemeden doğrular (yüklenen dosyanın biçimli görünümü gecikmesin). */
  validateXmlNow: (xml: string) => void
}

/** Editör tanılama: XSLT/XML doğrulama debounce'ları, 1.0 uyumluluğu, UBL-TR, problem listeleri ve marker'lar. */
export function useXsltDiagnostics({ xslt, xml }: Options): XsltDiagnostics {
  const [xsltErrors, setXsltErrors] = useState<{ message: string; line: number; column: number }[]>([])
  const [xmlValid, setXmlValid] = useState<boolean | null>(null)
  const [xmlErrors, setXmlErrors] = useState<{ message: string; line: number | null; column: number | null }[]>([])
  const [ublTrResults, setUblTrResults] = useState<BusinessRuleResult[] | null>(null)
  const [ublTrLoading, setUblTrLoading] = useState(false)
  const [ublTrError, setUblTrError] = useState<string | null>(null)
  // Problemler paneli varsayılan kapalı; UBL-TR kontrolü onu açar.
  const [showProblems, setShowProblems] = useState(false)

  useEffect(() => {
    if (!xslt) { setXsltErrors([]); return }
    const t = setTimeout(async () => {
      try {
        const { data } = await api.post<{ valid: boolean; error?: string; line?: number; column?: number }>(
          '/api/preview/validate-xslt',
          { xslt },
        )
        if (data.valid) {
          setXsltErrors([])
        } else {
          const err = toXsltError(data, 'XSLT hatası')
          setXsltErrors([{ message: err.message, line: err.line || 1, column: err.column || 1 }])
        }
      } catch {
        // silently fail validation
      }
    }, XSLT_VALIDATION_DEBOUNCE_MS)
    return () => clearTimeout(t)
  }, [xslt])

  const validateXmlNow = useCallback((content: string) => {
    const errs = extractXmlParseErrors(content)
    setXmlErrors(errs)
    setXmlValid(errs.length === 0)
  }, [])

  useEffect(() => {
    if (!xml) { setXmlValid(null); setXmlErrors([]); return }
    const t = setTimeout(() => validateXmlNow(xml), XML_VALIDATION_DEBOUNCE_MS)
    return () => clearTimeout(t)
  }, [xml, validateXmlNow])

  const reportXsltError = useCallback((message: string, line: number, column: number) => {
    setXsltErrors([{ message, line, column }])
  }, [])

  const validateUblTr = useCallback(async () => {
    if (!xml) return
    setUblTrLoading(true)
    setUblTrError(null)
    setShowProblems(true)
    try {
      const res = await validateBusinessRules(xml)
      setUblTrResults(res.results)
    } catch (e: unknown) {
      const msg = e && typeof e === 'object' && 'response' in e
        ? (e as { response?: { data?: { error?: string } } }).response?.data?.error ?? 'Doğrulama yapılamadı.'
        : 'Doğrulama yapılamadı.'
      setUblTrError(msg)
      setUblTrResults(null)
    } finally {
      setUblTrLoading(false)
    }
  }, [xml])

  // İlk satırdaki XML bildirimi eksikse XSLT problemleri arasına ekle
  const declProblem = useMemo(() => xmlDeclarationProblem(xslt), [xslt])

  // XSLT 1.0 uyumluluk uyarıları (2.0'a özgü fonksiyon/yapı) — yalnız uyarı, hiçbir akışı engellemez
  const compat = useXslt1CompatWarnings(xslt)
  const editorMarkers = useMemo(() => [...xsltErrors, ...compat.markers], [xsltErrors, compat.markers])

  const xsltProblems = useMemo<ProblemItem[]>(() => [
    ...(declProblem ? [declProblem] : []),
    ...xsltErrors.map(e => ({
      source: 'xslt' as const,
      severity: 'error' as const,
      ruleName: 'XSLT Sözdizimi',
      message: e.message,
      line: e.line,
      column: e.column,
    })),
    ...compat.problems,
  ], [declProblem, xsltErrors, compat.problems])

  const xmlProblems = useMemo<ProblemItem[]>(() => xmlErrors.map(e => ({
    source: 'xml' as const,
    severity: 'error' as const,
    ruleName: 'XML Ayrıştırma',
    message: e.message,
    line: e.line,
    column: e.column,
  })), [xmlErrors])

  const ublTrProblems = useMemo<ProblemItem[] | null>(() => ublTrResults?.map(r => ({
    source: 'ubl-tr' as const,
    ruleId: r.ruleId,
    ruleName: r.ruleName,
    severity: r.severity,
    message: r.message,
    line: r.line,
    column: r.column,
  })) ?? null, [ublTrResults])

  const allProblems = [...xsltProblems, ...xmlProblems, ...(ublTrProblems ?? [])]
  const errorCount = allProblems.filter(p => p.severity === 'error').length
  const warningCount = allProblems.filter(p => p.severity === 'warning').length
  const ublTrErrorCount = ublTrResults?.filter(r => r.severity === 'error').length ?? null

  return {
    xsltProblems,
    xmlProblems,
    ublTrProblems,
    ublTrLoading,
    ublTrError,
    ublTrErrorCount,
    errorCount,
    warningCount,
    editorMarkers,
    xmlValid,
    declProblem,
    validateUblTr,
    showProblems,
    setShowProblems,
    reportXsltError,
    validateXmlNow,
  }
}
