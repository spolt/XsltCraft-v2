import { create } from 'zustand'

export interface XsltError {
  message: string
  line?: number
  column?: number
}

interface XsltEditorState {
  // Template identity (null = unsaved)
  templateId: string | null
  templateName: string

  // Content
  xsltContent: string
  xmlContent: string | null

  // Validation
  xsltValid: boolean | null
  xsltErrors: XsltError[]
  xmlValid: boolean | null

  // Preview
  previewHtml: string
  previewLoading: boolean
  previewError: string | null
  lastPreviewMs: number | null

  // Dirty tracking
  isDirty: boolean

  // Actions
  setTemplateId: (id: string | null) => void
  setTemplateName: (name: string) => void
  setXsltContent: (xslt: string) => void
  setXmlContent: (xml: string | null) => void
  setPreview: (html: string, ms: number) => void
  setPreviewError: (error: string | null) => void
  setPreviewLoading: (loading: boolean) => void
  setXsltValidation: (valid: boolean, errors: XsltError[]) => void
  setXmlValidation: (valid: boolean) => void
  markClean: () => void
  reset: () => void
}

const initialState = {
  templateId: null as string | null,
  templateName: '',
  xsltContent: '',
  xmlContent: null as string | null,
  xsltValid: null as boolean | null,
  xsltErrors: [] as XsltError[],
  xmlValid: null as boolean | null,
  previewHtml: '',
  previewLoading: false,
  previewError: null as string | null,
  lastPreviewMs: null as number | null,
  isDirty: false,
}

export const useXsltEditorStore = create<XsltEditorState>((set) => ({
  ...initialState,

  setTemplateId: (id) => set({ templateId: id }),
  setTemplateName: (name) => set({ templateName: name }),
  setXsltContent: (xslt) => set({ xsltContent: xslt, isDirty: true }),
  setXmlContent: (xml) => set({ xmlContent: xml }),
  setPreview: (html, ms) => set({ previewHtml: html, lastPreviewMs: ms, previewLoading: false, previewError: null }),
  setPreviewError: (error) => set({ previewError: error, previewLoading: false }),
  setPreviewLoading: (loading) => set({ previewLoading: loading }),
  setXsltValidation: (valid, errors) => set({ xsltValid: valid, xsltErrors: errors }),
  setXmlValidation: (valid) => set({ xmlValid: valid }),
  markClean: () => set({ isDirty: false }),
  reset: () => set(initialState),
}))
