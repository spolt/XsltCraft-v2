import defaultInvoiceXml from '../../assets/default-invoice.xml?raw'
import defaultDespatchXml from '../../assets/default-despatch.xml?raw'
import type { EasyEditDocumentType } from '../../services/easyEditService'

/**
 * Dashboard → XSLT Editör dosya aktarımı. Burada yalnız uzantıya göre ayrıştırma yapılır;
 * asıl doğrulama (uzantı/boyut/iyi biçimlilik/kök eleman) editördeki `readUploadFile` yolundadır.
 */

export interface EditorFiles {
  xslt: File | null
  xml: File | null
}

/** XsltEditorPage'in `location.state` sözleşmesi. */
export interface EditorPendingState {
  pendingXsltFile?: File
  pendingXmlFile?: File
  /** XML kullanıcı yerine örnek belgeden geldiyse yuvada gösterilecek etiket (ör. "Örnek fatura XML"). */
  pendingXmlLabel?: string
  /** "Editörde düzelt": imlecin gideceği hata satırı (editör desteklediğinde). */
  initialLine?: number
}

export type EditorFileKind = 'xslt' | 'xml'

export function fileKind(file: File): EditorFileKind | null {
  const name = file.name.toLowerCase()
  if (name.endsWith('.xsl') || name.endsWith('.xslt')) return 'xslt'
  if (name.endsWith('.xml')) return 'xml'
  return null
}

/** Bırakılan dosyaları uzantıya göre yuvalara ayırır (her türden ilki); tanınmayanları ayrıca döner. */
export function splitDroppedFiles(files: Iterable<File>): EditorFiles & { rejected: File[] } {
  const out: EditorFiles & { rejected: File[] } = { xslt: null, xml: null, rejected: [] }
  for (const f of files) {
    const kind = fileKind(f)
    if (kind && !out[kind]) out[kind] = f
    else if (!kind) out.rejected.push(f)
  }
  return out
}

export function editorPendingState(
  files: EditorFiles,
  extra: Pick<EditorPendingState, 'pendingXmlLabel' | 'initialLine'> = {},
): EditorPendingState | null {
  if (!files.xslt && !files.xml) return null
  const state: EditorPendingState = {}
  if (files.xslt) state.pendingXsltFile = files.xslt
  if (files.xml) state.pendingXmlFile = files.xml
  if (files.xml && extra.pendingXmlLabel) state.pendingXmlLabel = extra.pendingXmlLabel
  if (extra.initialLine && extra.initialLine > 0) state.initialLine = extra.initialLine
  return state
}

const DESPATCH_NS = 'urn:oasis:names:specification:ubl:schema:xsd:DespatchAdvice-2'
const INVOICE_NS = 'urn:oasis:names:specification:ubl:schema:xsd:Invoice-2'

/** Analiz yokken belge türü: yalnız irsaliye namespace'i geçen metin irsaliye sayılır. */
export function sniffDocumentType(xslt: string): EasyEditDocumentType {
  return xslt.includes(DESPATCH_NS) && !xslt.includes(INVOICE_NS) ? 'Despatch' : 'Invoice'
}

export const SAMPLE_XML_LABEL: Record<EasyEditDocumentType, string> = {
  Invoice: 'Örnek fatura XML',
  Despatch: 'Örnek irsaliye XML',
}

/** Belge türünün örnek XML metni (null tür → fatura). */
export function sampleXmlText(type: EasyEditDocumentType | null | undefined): string {
  return type === 'Despatch' ? defaultDespatchXml : defaultInvoiceXml
}

/** Örnek XML'in görüntülenen etiketi (null tür → fatura). */
export const sampleXmlLabel = (type: EasyEditDocumentType | null | undefined): string =>
  SAMPLE_XML_LABEL[type === 'Despatch' ? 'Despatch' : 'Invoice']

/** XML seçilmediğinde editöre eklenen örnek belge. */
export function sampleXmlFile(type: EasyEditDocumentType): File {
  return new File([sampleXmlText(type)], type === 'Despatch' ? 'ornek-irsaliye.xml' : 'ornek-fatura.xml', {
    type: 'application/xml',
  })
}
