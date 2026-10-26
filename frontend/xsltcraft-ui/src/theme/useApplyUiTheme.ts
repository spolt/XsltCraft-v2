import { useEffect } from 'react'
import { useUiPrefsStore } from '../store/uiPrefsStore'

/**
 * Seçili arayüz/editör temasını <html data-ui-theme / data-editor-theme> özniteliklerine yazar.
 * App kökünde bir kez çağrılır (ilk boyama index.html'deki inline script ile yapılır).
 */
export function useApplyUiTheme(): void {
  const uiTheme = useUiPrefsStore((s) => s.uiTheme)
  const editorTheme = useUiPrefsStore((s) => s.editorTheme)

  useEffect(() => {
    document.documentElement.dataset.uiTheme = uiTheme
  }, [uiTheme])

  useEffect(() => {
    document.documentElement.dataset.editorTheme = editorTheme
  }, [editorTheme])
}
