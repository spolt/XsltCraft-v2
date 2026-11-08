import { parseEasyEditError, type EasyEditAnalyzeResponse } from '../../services/easyEditService'
import type { DocumentHandoffPayload } from '../../store/documentHandoffStore'
import { analyzeCached, easyEditErrorText } from './analyzeCached'
import { isEditableVerdict } from './docDraft'

export type DocAnalysisResult =
  | { ok: true; analysis: EasyEditAnalyzeResponse }
  | { ok: false; message: string; canceled?: boolean }

const FALLBACK_MESSAGE = 'Bu belge Kolay düzenle için uygun değil.'
const CANCELED: DocAnalysisResult = { ok: false, canceled: true, message: '' }

/**
 * Belgenin analizini döndürür: önce aktarımla gelen, sonra önbellekteki, yoksa sunucudan yeni analiz.
 * Karar `suitable`/`partial` değilse ya da işaretli metin yoksa başarısız sayılır.
 */
export async function resolveDocAnalysis(
  payload: Pick<DocumentHandoffPayload, 'xslt' | 'analysis'>,
  signal: AbortSignal,
): Promise<DocAnalysisResult> {
  let analysis = payload.analysis
  if (!analysis) {
    try {
      const result = await analyzeCached(payload.xslt, undefined, signal)
      if (!result) return CANCELED
      analysis = result.analysis
    } catch (err) {
      const parsed = await parseEasyEditError(err)
      if (parsed.kind === 'canceled') return CANCELED
      return { ok: false, message: easyEditErrorText(parsed, 'Belge analiz edilemedi.') }
    }
  }
  if (!isEditableVerdict(analysis)) return { ok: false, message: analysis.message || FALLBACK_MESSAGE }
  return { ok: true, analysis }
}
