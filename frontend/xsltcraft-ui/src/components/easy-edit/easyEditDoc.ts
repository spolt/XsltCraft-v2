import { useOutletContext } from 'react-router-dom'
import type { DocumentHandoffSource } from '../../store/documentHandoffStore'

/** EasyEditScope'un sayfaya verdiği belge bilgisi (taslak store'unda tutulmayan alanlar). */
export interface EasyEditDoc {
  source: DocumentHandoffSource
  /** Kayıtlı şablon kimliği; yüklenen dosyada null. */
  templateId: string | null
  /** Görüntülenen ve indirilen dosya adı. */
  fileName: string
  /** Metin kaydedilmemiş değişiklikle geldi (ör. editörden aktarım). */
  handoffDirty: boolean
  /** Analizin girdisi olan özgün metin; görsel rolleri değişince yeniden analiz edilir. Yalnız metin olarak kullanılır. */
  xslt: string
}

/** Yalnız `/easy-edit/template/:templateId` rotasının aktarım kimliği; W4'te editörden geçişte kullanılır. */
export const templateDraftId = (templateId: string) => `template-${templateId}`

export const useEasyEditDoc = () => useOutletContext<EasyEditDoc>()
