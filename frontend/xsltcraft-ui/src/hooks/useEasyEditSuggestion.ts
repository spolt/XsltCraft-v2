import { useEffect, useMemo, useState } from 'react'
import { isEditableVerdict } from '../components/easy-edit/docDraft'
import { hasEasyEditMarkers } from '../components/easy-edit/editorHandoff'
import { analysisKey, useDocumentHandoffStore } from '../store/documentHandoffStore'

const CHECK_DELAY_MS = 400

/**
 * Editördeki metin Kolay düzenle'ye uygun görünüyor mu: XsltCraft yuva işareti var ya da bu belgenin bir sürümü
 * önbellekteki analizde uygun bulundu. Analiz isteği atmaz; yalnız önbelleğe bakar. Uygunluk `docKey` değişene dek
 * akılda kalır (küçük düzenlemeler bandı gizlemesin); `docKey` yokken yalnız o metin için geçerlidir (başka bir
 * belgeye taşınmasın).
 */
export function useEasyEditSuggestion(xslt: string, docKey: string | null, enabled: boolean): boolean {
  const markers = useMemo(() => enabled && hasEasyEditMarkers(xslt), [enabled, xslt])
  const cache = useDocumentHandoffStore((s) => s.analysisCache)
  const [suitableFor, setSuitableFor] = useState<{ docKey: string | null; xslt: string } | null>(null)
  const suitable = suitableFor !== null
    && (docKey !== null ? suitableFor.docKey === docKey : suitableFor.docKey === null && suitableFor.xslt === xslt)

  useEffect(() => {
    if (!enabled || markers || suitable || !xslt) return
    let cancelled = false
    const timer = window.setTimeout(() => {
      void analysisKey(xslt).then((key) => {
        if (cancelled) return
        if (isEditableVerdict(useDocumentHandoffStore.getState().getCachedAnalysis(key))) setSuitableFor({ docKey, xslt })
      })
    }, CHECK_DELAY_MS)
    return () => {
      cancelled = true
      window.clearTimeout(timer)
    }
  }, [enabled, markers, suitable, xslt, docKey, cache])

  return enabled && (markers || suitable)
}
