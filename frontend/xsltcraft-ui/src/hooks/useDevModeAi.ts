import { useCallback, useEffect, useRef, useState } from 'react'
import type { AiPrompt } from '../components/ai/AiAssistantPanel'
import { replaceSelectionText } from '../components/xslt-editor/xsltText'
import { useAiStore } from '../store/aiStore'
import { useThemeDraftStore } from '../store/themeDraftStore'
import { toast } from '../store/toastStore'

export type DevModePane = 'preview' | 'ai'

interface RefactorState {
  selection: string
  range: { startLine: number; endLine: number; startColumn: number }
}

/**
 * Önizleme bölmesinin Önizleme | AI Asistan durumu. Panel ilk açılışta mount olur, sonra yalnız gizlenir: sohbet sekme
 * değişiminde ve tam ekranda korunur. Tam ekrandan çıkmak çağıranın işidir (düzen bu kancaya bağlı değil).
 */
export function useDevModeAi() {
  const enabled = useAiStore((s) => s.enabled === true)
  const refresh = useAiStore((s) => s.refresh)
  const [selectedPane, setSelectedPane] = useState<DevModePane>('preview')
  const [mounted, setMounted] = useState(false)
  // Aynı soru iki kez sorulursa metin özdeştir; artan sayaç ikinci tetiklemeyi garanti eder.
  const [prompt, setPrompt] = useState<AiPrompt | null>(null)
  const nonceRef = useRef(0)
  const [refactor, setRefactor] = useState<RefactorState | null>(null)
  const [xsltSelection, setXsltSelection] = useState<string | undefined>()

  useEffect(() => { void refresh() }, [refresh])

  const open = useCallback(() => {
    setMounted(true)
    setSelectedPane('ai')
  }, [])

  const showPreview = useCallback(() => setSelectedPane('preview'), [])

  const ask = useCallback((text: string) => {
    setPrompt({ nonce: ++nonceRef.current, text })
    open()
  }, [open])

  const requestRefactor = useCallback((selection: string, range: RefactorState['range']) => {
    setRefactor({ selection, range })
  }, [])

  const applyRefactor = useCallback((newText: string) => {
    const draft = useThemeDraftStore.getState()
    if (!refactor || draft.workingXslt === null) return
    const next = replaceSelectionText(draft.workingXslt, refactor.selection, refactor.range, newText)
    setRefactor(null)
    if (next === null) {
      toast.warning('Seçili bölge bu arada değişti; öneri uygulanmadı. Bölgeyi yeniden seçip deneyin.', { title: 'AI refactor' })
      return
    }
    draft.setManualXslt(next)
  }, [refactor])

  return {
    enabled,
    /** AI kapalıysa (ya da sonradan kapandıysa) bölme hep önizlemedir. */
    pane: enabled ? selectedPane : 'preview' as DevModePane,
    mounted,
    prompt,
    open,
    showPreview,
    ask,
    xsltSelection,
    setXsltSelection,
    refactor,
    requestRefactor,
    applyRefactor,
    closeRefactor: useCallback(() => setRefactor(null), []),
  }
}

export type DevModeAi = ReturnType<typeof useDevModeAi>
