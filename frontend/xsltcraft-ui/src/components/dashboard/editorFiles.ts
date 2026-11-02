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

export function editorPendingState(files: EditorFiles): EditorPendingState | null {
  const state: EditorPendingState = {}
  if (files.xslt) state.pendingXsltFile = files.xslt
  if (files.xml) state.pendingXmlFile = files.xml
  return files.xslt || files.xml ? state : null
}
