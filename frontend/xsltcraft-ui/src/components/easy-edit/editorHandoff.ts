import { useDocumentHandoffStore } from '../../store/documentHandoffStore'
import { templateDraftId } from './easyEditDoc'
import { resolveDocAnalysis } from './resolveDocAnalysis'

/**
 * Kolay düzenle → XSLT Editör aktarımı (`location.state`). Mevcut `pendingXsltFile` / `pendingXmlFile` girişinin yanında
 * durur; metin dosya doğrulamasından geçmez (uygulamanın kendi compose çıktısıdır) ve editör onu yalnız metin olarak
 * kullanır. `templateId` yalnız rotadaki kimlikle eşleşirse dikkate alınır.
 */
export interface EditorHandoff {
  pendingXsltContent: string
  /** Görüntülenen dosya adı (ör. `fatura.xslt`). */
  pendingXsltName?: string
  pendingXmlContent?: string
  pendingXmlName?: string
  /** Kayıtlı şablon kimliği; editör `/xslt-editor/:id` rotasıyla açılır. */
  templateId?: string
  /** Kaynak ekranın bildiği `updatedAt`; kayıtta çakışma denetimi bununla yapılır. */
  savedUpdatedAt?: string
  /** Kaynak ekranda kaydedilmemiş değişiklik vardı. */
  dirty?: boolean
}

const optionalString = (v: unknown) => (typeof v === 'string' && v ? v : undefined)

/** `location.state` içinden aktarımı okur; geçersizse `null`. */
export function readEditorHandoff(state: unknown): EditorHandoff | null {
  if (!state || typeof state !== 'object') return null
  const s = state as Record<string, unknown>
  if (typeof s.pendingXsltContent !== 'string' || !s.pendingXsltContent) return null
  return {
    pendingXsltContent: s.pendingXsltContent,
    pendingXsltName: optionalString(s.pendingXsltName),
    pendingXmlContent: optionalString(s.pendingXmlContent),
    pendingXmlName: optionalString(s.pendingXmlName),
    templateId: optionalString(s.templateId),
    savedUpdatedAt: optionalString(s.savedUpdatedAt),
    dirty: s.dirty === true,
  }
}

/** Metinde XsltCraft yuva işareti var (Kolay düzenle için hazırlanmış dosya). */
export const hasEasyEditMarkers = (xslt: string) => /\bdata-xc-(?:slot|placeholder)\b/.test(xslt)

/** XSLT Editör'deki belge: Kolay düzenle'ye aktarılacak alanlar. */
export interface EditorDocument {
  xslt: string
  xml: string | null
  templateId: string | null
  savedUpdatedAt: string | null
  name: string
  fileName: string
  dirty: boolean
}

export type EasyEditSwitchResult =
  | { ok: true; path: string }
  | { ok: false; message: string; canceled?: boolean }

const newDraftId = () =>
  typeof crypto !== 'undefined' && 'randomUUID' in crypto
    ? crypto.randomUUID()
    : `${Date.now().toString(36)}-${Math.random().toString(36).slice(2, 10)}`

/**
 * XSLT Editör → Kolay düzenle: analizi (önbellekte varsa onu) çözer, uygunsa belgeyi aktarıma bırakır ve açılacak
 * rotayı döndürür. Kayıtlı şablonda `templateDraftId(id)` + `/easy-edit/template/:id`, değilse `/easy-edit/:draftId`.
 */
export async function stageEasyEditFromEditor(doc: EditorDocument, signal: AbortSignal): Promise<EasyEditSwitchResult> {
  const result = await resolveDocAnalysis({ xslt: doc.xslt }, signal)
  if (!result.ok) return result
  const draftId = doc.templateId ? templateDraftId(doc.templateId) : newDraftId()
  useDocumentHandoffStore.getState().stage(draftId, {
    source: doc.templateId ? 'template' : 'upload',
    templateId: doc.templateId ?? undefined,
    savedUpdatedAt: doc.templateId ? doc.savedUpdatedAt ?? undefined : undefined,
    name: doc.name,
    fileName: doc.fileName,
    xslt: doc.xslt,
    xml: doc.xml ?? undefined,
    dirty: doc.dirty,
    analysis: result.analysis,
  })
  return { ok: true, path: doc.templateId ? `/easy-edit/template/${doc.templateId}` : `/easy-edit/${draftId}` }
}
