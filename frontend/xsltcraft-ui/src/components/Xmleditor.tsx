import Editor from "@monaco-editor/react"
import { useUiPrefsStore } from "../store/uiPrefsStore"
import { defineMonacoThemes, monacoThemeId } from "../theme/monacoThemes"

type Props = {
  value: string
  onChange: (value: string) => void
}

export default function XmlEditor({ value, onChange }: Props) {
  const editorTheme = useUiPrefsStore(s => s.editorTheme)
  const fontSize = useUiPrefsStore(s => s.editorFontSize)
  const wordWrap = useUiPrefsStore(s => s.editorWordWrap)

  return (
    <Editor
      height="100%"
      defaultLanguage="xml"
      value={value}
      beforeMount={defineMonacoThemes}
      theme={monacoThemeId(editorTheme)}
      options={{ fontSize, wordWrap: wordWrap ? 'on' : 'off' }}
      onChange={(v) => onChange(v || "")}
    />
  )
}
