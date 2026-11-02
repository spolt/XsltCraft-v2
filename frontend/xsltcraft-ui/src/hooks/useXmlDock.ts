import { useCallback, useMemo, useRef, useState } from 'react'
import type { editor as MonacoEditor } from 'monaco-editor'
import type { DockTab } from '../components/xslt-editor/PreviewDock'
import { formatXmlForDisplay, looksMinified } from '../components/xslt-editor/xsltText'

interface Options {
  xml: string | null
  xmlValid: boolean | null
  /** Önizleme bölmesini görünür yapar (tam ekrandan çıkar, AI sekmesinden döner). */
  showPane: () => void
}

/** Önizleme bölmesinin sekmesi ve XML Kaynak görünümü. Ham XML'e dokunulmaz; biçimleme yalnız gösterimdedir. */
export function useXmlDock({ xml, xmlValid, showPane }: Options) {
  const [tab, setTab] = useState<DockTab>('preview')
  const [pretty, setPretty] = useState(true)
  const [cursorLine, setCursorLine] = useState<number | undefined>()
  const [selection, setSelection] = useState<string | undefined>()
  const editorRef = useRef<MonacoEditor.IStandaloneCodeEditor | null>(null)

  // Yalnız minify edilmiş ve well-formed belge biçimlendirilir (xml-formatter bozuk XML'e kendi etiketini ekler).
  const display = useMemo(() => {
    if (!xml) return ''
    if (!pretty || xmlValid !== true || !looksMinified(xml)) return xml
    return formatXmlForDisplay(xml)
  }, [xml, pretty, xmlValid])
  const reformatted = xml != null && display !== xml

  const openSource = useCallback(() => {
    showPane()
    setTab('xml')
  }, [showPane])

  // Problem satırları ham XML'e göredir; biçimli görünümde satırlar kaydığı için ham görünüme dönülür.
  const locateLine = useCallback((line: number, column?: number | null) => {
    openSource()
    if (reformatted) setPretty(false)
    setTimeout(() => {
      const editor = editorRef.current
      if (!editor) return
      editor.revealLineInCenter(line)
      editor.setPosition({ lineNumber: line, column: column ?? 1 })
      editor.focus()
    }, 100)
  }, [openSource, reformatted])

  const handleEditorMount = useCallback((editor: MonacoEditor.IStandaloneCodeEditor) => {
    editorRef.current = editor
    editor.onDidChangeCursorPosition((e) => setCursorLine(e.position.lineNumber))
    editor.onDidChangeCursorSelection((e) => {
      const text = editor.getModel()?.getValueInRange(e.selection) ?? ''
      setSelection(text.trim() || undefined)
    })
  }, [])

  return {
    tab,
    setTab,
    pretty,
    togglePretty: useCallback(() => setPretty((p) => !p), []),
    display,
    reformatted,
    /** Biçimli görünümde imleç satırı ham metinle aynı değildir: AI bağlamı ham metni pencereler, satır verilmez. */
    aiCursorLine: reformatted ? undefined : cursorLine,
    selection,
    openSource,
    locateLine,
    handleEditorMount,
  }
}
