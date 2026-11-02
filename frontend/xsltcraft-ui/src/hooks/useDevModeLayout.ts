import { useCallback, useEffect, useRef, useState } from 'react'
import { useGroupRef } from 'react-resizable-panels'
import { SPLIT_LAYOUTS } from '../components/xslt-editor/editorLayouts'
import { useUiPrefsStore, type EditorLayoutId } from '../store/uiPrefsStore'
import { useEditorMaximize } from './useEditorMaximize'

interface Options {
  /** Çalışma alanı hazır: varsayılan düzen bu ilk olduğunda bir kez uygulanır. */
  ready: boolean
  shortcutEnabled: boolean
}

/** Geliştirici Modu düzeni: editör | önizleme bölünmesi, tam ekran ve mini pencere. */
export function useDevModeLayout({ ready, shortcutEnabled }: Options) {
  const max = useEditorMaximize({ shortcutEnabled })
  const { enter: maxEnter, exit: maxExit, setMiniOpen } = max
  const [splitMode, setSplitMode] = useState<'side' | 'half'>(
    () => (useUiPrefsStore.getState().editorDefaultLayout === 'half' ? 'half' : 'side'),
  )
  const groupRef = useGroupRef()
  const layoutMode: EditorLayoutId = max.maximized ? (max.miniOpen ? 'mini' : 'editorOnly') : splitMode

  const applyLayout = useCallback((mode: EditorLayoutId) => {
    if (mode === 'side' || mode === 'half') {
      maxExit()
      setSplitMode(mode)
      groupRef.current?.setLayout(SPLIT_LAYOUTS[mode])
    } else if (mode === 'mini') {
      maxEnter({ withMini: true })
    } else {
      maxEnter()
      setMiniOpen(false)
    }
  }, [maxEnter, maxExit, setMiniOpen, groupRef])

  const defaultLayoutAppliedRef = useRef(false)
  useEffect(() => {
    if (!ready || defaultLayoutAppliedRef.current) return
    const raf = requestAnimationFrame(() => {
      defaultLayoutAppliedRef.current = true
      applyLayout(useUiPrefsStore.getState().editorDefaultLayout)
    })
    return () => cancelAnimationFrame(raf)
  }, [ready, applyLayout])

  return { max, splitMode, groupRef, layoutMode, applyLayout }
}
