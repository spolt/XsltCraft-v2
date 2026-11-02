import { useEffect, useMemo, useState } from 'react'
import type { ProblemItem } from '../components/xslt-editor/ProblemsPanel'
import type { XsltError } from '../components/Xslteditor'
import { analyzeXslt1Compat, summarizeXslt1Compat, type Xslt1CompatFinding } from '../utils/xslt1Compat'

const COMPAT_DEBOUNCE_MS = 800
const MAX_MARKERS = 300

/**
 * XSLT metnindeki 2.0'a özgü kullanımları debounce'lu analiz eder.
 * Yalnızca uyarı üretir; kaydetme/önizleme akışını etkilemez.
 */
export function useXslt1CompatWarnings(xslt: string) {
  const [findings, setFindings] = useState<Xslt1CompatFinding[]>([])

  useEffect(() => {
    const t = setTimeout(() => setFindings(xslt ? analyzeXslt1Compat(xslt) : []), COMPAT_DEBOUNCE_MS)
    return () => clearTimeout(t)
  }, [xslt])

  const problems = useMemo<ProblemItem[]>(() => summarizeXslt1Compat(findings).map(f => ({
    source: 'xslt',
    ruleId: f.code,
    ruleName: 'XSLT 1.0 Uyumluluğu',
    severity: 'warning',
    message: f.message,
    line: f.line,
    column: f.column,
  })), [findings])

  const markers = useMemo<XsltError[]>(() => findings.slice(0, MAX_MARKERS).map(f => ({
    message: f.message,
    line: f.line,
    column: f.column,
    length: f.length,
    severity: 'warning',
    code: f.code,
  })), [findings])

  return { problems, markers }
}
