import { useCallback, useEffect, useMemo, useRef, useState } from 'react'
import axios from 'axios'
import { composeTheme, type AssetKind, type ComposeAsset, type ComposeResponse, type ComposeWarning } from '../services/themeComposeService'
import type { BankInfoItem } from '../services/previewService'

const DEBOUNCE_MS = 1200

interface Options {
  themeId: string | undefined
  xmlContent: string
  assets: ComposeAsset[]
  bankInfo: BankInfoItem[]
  /** Kendi görseli eklenmeden kaldırılan temadaki görseller. */
  hideThemeImages?: AssetKind[]
  /** Hata yanıtı (402 vb.) — çağıran yükseltme penceresi/yönlendirme yapar. */
  onError: (err: unknown) => void
}

export interface ThemeCompose {
  html: string
  loading: boolean
  error: string | null
  warnings: ComposeWarning[]
  lastMs: number | null
  appliedSlots: ComposeResponse['appliedSlots']
  /** Debounce beklemeden hemen yeniden önizle (sürükle-bırak sonrası). */
  composeNow: () => void
  /** İndirme/kaydetme: mevcut ayarlarla birleştirilmiş XSLT (önizlemeyle aynı kod yolu). */
  exportXslt: () => Promise<string>
}

/**
 * Tema Kullan önizlemesi: logo/imza/IBAN değiştikçe sunucuda birleştirip HTML'i alır. Her yeni istek bir
 * öncekini iptal eder; yalnız son isteğin sonucu uygulanır.
 */
export function useThemeCompose({ themeId, xmlContent, assets, bankInfo, hideThemeImages, onError }: Options): ThemeCompose {
  const [html, setHtml] = useState('')
  const [loading, setLoading] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [warnings, setWarnings] = useState<ComposeWarning[]>([])
  const [lastMs, setLastMs] = useState<number | null>(null)
  const [appliedSlots, setAppliedSlots] = useState<ComposeResponse['appliedSlots']>({})
  const [nonce, setNonce] = useState(0)

  const immediateRef = useRef(false)
  const onErrorRef = useRef(onError)
  useEffect(() => { onErrorRef.current = onError }, [onError])

  // İstek gövdesi değişmedikçe yeniden istek atma (dizi kimlikleri her render'da değişebilir).
  const requestKey = useMemo(
    () => JSON.stringify({ themeId, xmlContent, assets, bankInfo, hideThemeImages }),
    [themeId, xmlContent, assets, bankInfo, hideThemeImages],
  )
  const latestRef = useRef({ themeId, xmlContent, assets, bankInfo, hideThemeImages })
  useEffect(() => { latestRef.current = { themeId, xmlContent, assets, bankInfo, hideThemeImages } },
    [themeId, xmlContent, assets, bankInfo, hideThemeImages])

  useEffect(() => {
    if (!themeId || !xmlContent) return
    const controller = new AbortController()
    const delay = immediateRef.current ? 0 : DEBOUNCE_MS
    immediateRef.current = false

    const timer = setTimeout(async () => {
      setLoading(true)
      setError(null)
      try {
        const { themeId: id, xmlContent: xml, assets: a, bankInfo: b, hideThemeImages: h } = latestRef.current
        const result = await composeTheme(
          { themeId: id, xmlContent: xml, assets: a, bankInfo: b, hideThemeImages: h, includeHtml: true, includeXslt: false },
          controller.signal,
        )
        // İptalden hemen önce çözülen eski yanıt yenisinin yerine yazılmasın.
        if (controller.signal.aborted) return
        setHtml(result.html ?? '')
        setWarnings(result.warnings)
        setLastMs(result.generationTimeMs)
        setAppliedSlots(result.appliedSlots)
      } catch (err) {
        if (axios.isCancel(err)) return
        const data = axios.isAxiosError(err) ? (err.response?.data as { error?: string; message?: string } | undefined) : undefined
        setError(data?.message ?? data?.error ?? 'Önizleme alınamadı.')
        onErrorRef.current(err)
      } finally {
        if (!controller.signal.aborted) setLoading(false)
      }
    }, delay)

    return () => {
      clearTimeout(timer)
      controller.abort()
      // İptal edilen istek loading'i kapatmaz (finally); sonraki effect erken dönerse (XML boşaldı, tema değişti)
      // sonsuz yükleniyor kalmasın. Yeni istek başlarken yeniden açılır.
      setLoading(false)
    }
  }, [requestKey, nonce, themeId, xmlContent])

  const composeNow = useCallback(() => {
    immediateRef.current = true
    setNonce((n) => n + 1)
  }, [])

  const exportXslt = useCallback(async () => {
    const { themeId: id, assets: a, bankInfo: b, hideThemeImages: h } = latestRef.current
    const result = await composeTheme({ themeId: id, assets: a, bankInfo: b, hideThemeImages: h, includeHtml: false, includeXslt: true })
    return result.xslt ?? ''
  }, [])

  return { html, loading, error, warnings, lastMs, appliedSlots, composeNow, exportXslt }
}
