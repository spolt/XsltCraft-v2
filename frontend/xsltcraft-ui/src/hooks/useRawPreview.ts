import { useCallback, useEffect, useRef, useState } from 'react'
import { previewFromRawXslt, type PreviewTimings, type XsltEngineName } from '../services/previewService'
import { toXsltError } from '../services/xsltErrors'

const PREVIEW_DEBOUNCE_MS = 1000

interface Options {
  xslt: string
  xml: string | null
  /** Önizleme hatası satır bilgisi taşıyorsa çağrılır (kolon bilinmiyorsa 1). */
  onLineError?: (message: string, line: number, column: number) => void
}

export interface RawPreview {
  html: string
  loading: boolean
  error: string | null
  lastMs: number | null
  timings: PreviewTimings | null
  engine: XsltEngineName | null
  /** Debounce beklemeden mevcut metinle yeniden önizle. */
  refresh: () => void
}

/**
 * Ham XSLT önizlemesi: XSLT ya da XML değiştikçe debounce'lu HTML alır. Yanıtlar sıra sayacıyla
 * süzülür: yalnız en son başlatılan isteğin sonucu uygulanır, geç gelen eski yanıt atılır.
 */
export function useRawPreview({ xslt, xml, onLineError }: Options): RawPreview {
  const [html, setHtml] = useState('')
  const [loading, setLoading] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [lastMs, setLastMs] = useState<number | null>(null)
  const [timings, setTimings] = useState<PreviewTimings | null>(null)
  const [engine, setEngine] = useState<XsltEngineName | null>(null)

  const seqRef = useRef(0)
  const onLineErrorRef = useRef(onLineError)
  useEffect(() => { onLineErrorRef.current = onLineError }, [onLineError])

  const runPreview = useCallback(async (currentXslt: string, currentXml: string) => {
    const seq = ++seqRef.current
    setLoading(true)
    setError(null)
    try {
      const result = await previewFromRawXslt(currentXslt, currentXml)
      if (seq !== seqRef.current) return
      setHtml(result.html)
      setLastMs(result.generationTimeMs)
      setTimings(result.timings ?? null)
      setEngine(result.engine ?? null)
    } catch (e: unknown) {
      if (seq !== seqRef.current) return
      const err = toXsltError(e)
      setError(err.message)
      if (err.line) {
        onLineErrorRef.current?.(err.message, err.line, err.column || 1)
      }
    } finally {
      // Eski istek loading'i kapatmaz; kapatma en son isteğindir.
      if (seq === seqRef.current) setLoading(false)
    }
  }, [])

  useEffect(() => {
    if (!xslt || !xml) return
    const t = setTimeout(() => runPreview(xslt, xml), PREVIEW_DEBOUNCE_MS)
    return () => clearTimeout(t)
  }, [xslt, xml, runPreview])

  const refresh = useCallback(() => {
    if (xslt && xml) void runPreview(xslt, xml)
  }, [xslt, xml, runPreview])

  return { html, loading, error, lastMs, timings, engine, refresh }
}
