import { useCallback, useEffect, useMemo, useRef, useState } from 'react'
import axios from 'axios'
import { composeTheme, type AssetKind, type ComposeAsset, type ComposeFixedNote, type ComposeResponse, type ComposeWarning } from '../services/themeComposeService'
import type { BankInfoItem } from '../services/previewService'

const DEBOUNCE_MS = 1200

interface Options {
  themeId: string | undefined
  /** Elle düzenlenmiş XSLT; verilirse istekte `themeId`'nin yerine geçer. */
  xslt?: string
  xmlContent: string
  assets: ComposeAsset[]
  bankInfo: BankInfoItem[]
  /** Kendi görseli eklenmeden kaldırılan temadaki görseller. */
  hideThemeImages?: AssetKind[]
  /** Sabit notlar — önizleme ve indir/kaydet aynı değeri gönderir. */
  fixedNotes?: ComposeFixedNote[] | null
  /** Hata yanıtı (402 vb.) — çağıran yükseltme penceresi/yönlendirme yapar. */
  onError: (err: unknown) => void
}

export interface ThemeCompose {
  html: string
  loading: boolean
  error: string | null
  /** 400 yanıtındaki `field` (ör. "fixedNotes") — hatayı ilgili panelde göstermek için. */
  errorField: string | null
  warnings: ComposeWarning[]
  lastMs: number | null
  appliedSlots: ComposeResponse['appliedSlots']
  /** Temada Notlar alanı var mı; null → henüz bilinmiyor (yanıt gelmedi / sunucu bildirmedi). */
  fixedNotesSupported: boolean | null
  /** Debounce beklemeden hemen yeniden önizle (sürükle-bırak sonrası). */
  composeNow: () => void
  /** İndirme/kaydetme: mevcut ayarlarla birleştirilmiş XSLT (önizlemeyle aynı kod yolu). */
  exportXslt: () => Promise<string>
}

/** Kaynak alanı: gövdede `themeId` ve `xslt`'den yalnız biri bulunur. */
const composeSource = (themeId: string | undefined, xslt: string | undefined) => (xslt ? { xslt } : { themeId })

/**
 * Tema Kullan önizlemesi: logo/imza/IBAN değiştikçe sunucuda birleştirip HTML'i alır. Her yeni istek bir
 * öncekini iptal eder; yalnız son isteğin sonucu uygulanır.
 */
export function useThemeCompose({ themeId, xslt, xmlContent, assets, bankInfo, hideThemeImages, fixedNotes, onError }: Options): ThemeCompose {
  const [html, setHtml] = useState('')
  const [loading, setLoading] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [errorField, setErrorField] = useState<string | null>(null)
  const [fixedNotesSupported, setFixedNotesSupported] = useState<boolean | null>(null)
  const [warnings, setWarnings] = useState<ComposeWarning[]>([])
  const [lastMs, setLastMs] = useState<number | null>(null)
  const [appliedSlots, setAppliedSlots] = useState<ComposeResponse['appliedSlots']>({})
  const [nonce, setNonce] = useState(0)

  const immediateRef = useRef(false)
  const onErrorRef = useRef(onError)
  useEffect(() => { onErrorRef.current = onError }, [onError])

  // İstek gövdesi değişmedikçe yeniden istek atma (dizi kimlikleri her render'da değişebilir).
  const requestKey = useMemo(
    () => JSON.stringify({ themeId, xslt, xmlContent, assets, bankInfo, hideThemeImages, fixedNotes }),
    [themeId, xslt, xmlContent, assets, bankInfo, hideThemeImages, fixedNotes],
  )
  const latestRef = useRef({ themeId, xslt, xmlContent, assets, bankInfo, hideThemeImages, fixedNotes })
  useEffect(() => { latestRef.current = { themeId, xslt, xmlContent, assets, bankInfo, hideThemeImages, fixedNotes } },
    [themeId, xslt, xmlContent, assets, bankInfo, hideThemeImages, fixedNotes])

  useEffect(() => {
    if ((!themeId && !xslt) || !xmlContent) return
    const controller = new AbortController()
    const delay = immediateRef.current ? 0 : DEBOUNCE_MS
    immediateRef.current = false

    const timer = setTimeout(async () => {
      setLoading(true)
      setError(null)
      setErrorField(null)
      try {
        const { themeId: id, xslt: x, xmlContent: xml, assets: a, bankInfo: b, hideThemeImages: h, fixedNotes: n } = latestRef.current
        const result = await composeTheme(
          { ...composeSource(id, x), xmlContent: xml, assets: a, bankInfo: b, hideThemeImages: h, fixedNotes: n, includeHtml: true, includeXslt: false },
          controller.signal,
        )
        // İptalden hemen önce çözülen eski yanıt yenisinin yerine yazılmasın.
        if (controller.signal.aborted) return
        setHtml(result.html ?? '')
        setWarnings(result.warnings)
        setLastMs(result.generationTimeMs)
        setAppliedSlots(result.appliedSlots)
        setFixedNotesSupported(typeof result.fixedNotesSupported === 'boolean' ? result.fixedNotesSupported : null)
      } catch (err) {
        if (axios.isCancel(err)) return
        const data = axios.isAxiosError(err) ? (err.response?.data as { error?: string; message?: string; field?: string } | undefined) : undefined
        setError(data?.message ?? data?.error ?? 'Önizleme alınamadı.')
        setErrorField(data?.field ?? null)
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
  }, [requestKey, nonce, themeId, xslt, xmlContent])

  const composeNow = useCallback(() => {
    immediateRef.current = true
    setNonce((n) => n + 1)
  }, [])

  const exportXslt = useCallback(async () => {
    const { themeId: id, xslt: x, assets: a, bankInfo: b, hideThemeImages: h, fixedNotes: n } = latestRef.current
    const result = await composeTheme({ ...composeSource(id, x), assets: a, bankInfo: b, hideThemeImages: h, fixedNotes: n, includeHtml: false, includeXslt: true })
    return result.xslt ?? ''
  }, [])

  return { html, loading, error, errorField, warnings, lastMs, appliedSlots, fixedNotesSupported, composeNow, exportXslt }
}
