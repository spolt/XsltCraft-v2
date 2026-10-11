import { useCallback, useEffect, useState } from 'react'
import type { AnchoredRect } from '../utils/floatingRect'
import {
  loadMiniPreviewPrefs,
  saveMiniPreviewPrefs,
  type MiniPreviewPrefs,
} from '../components/xslt-editor/miniPreviewPrefs'

export interface EditorMaximize {
  maximized: boolean
  miniOpen: boolean
  miniRect: AnchoredRect
  /** Mini önizleme penceresi şu an gösterilmeli mi (maximized && miniOpen). */
  showMini: boolean
  enter: (opts?: { withMini?: boolean }) => void
  exit: () => void
  toggle: () => void
  setMiniOpen: (open: boolean) => void
  toggleMini: () => void
  commitMiniRect: (rect: AnchoredRect) => void
}

/**
 * Tam ekran editör modu + mini önizleme tercihleri. Mod oturumluktur; mini pencerenin
 * açık/kapalı durumu ve konumu localStorage'da kalıcıdır. Ctrl+Shift+F (⌘⇧F) modu açıp kapatır.
 */
export function useEditorMaximize({ shortcutEnabled = true }: { shortcutEnabled?: boolean } = {}): EditorMaximize {
  const [maximized, setMaximized] = useState(false)
  const [prefs, setPrefs] = useState<MiniPreviewPrefs>(loadMiniPreviewPrefs)
  const [loadedPrefs] = useState(prefs)

  // Yalnız kullanıcı değiştirdiğinde yaz: sayfa açılışında başka sekmenin kaydını ezme.
  useEffect(() => {
    if (prefs !== loadedPrefs) saveMiniPreviewPrefs(prefs)
  }, [prefs, loadedPrefs])

  const setMiniOpen = useCallback((open: boolean) => {
    setPrefs(p => (p.open === open ? p : { ...p, open }))
  }, [])

  const enter = useCallback((opts?: { withMini?: boolean }) => {
    setMaximized(true)
    if (opts?.withMini) setMiniOpen(true)
  }, [setMiniOpen])

  const exit = useCallback(() => setMaximized(false), [])
  const toggle = useCallback(() => setMaximized(m => !m), [])
  const toggleMini = useCallback(() => setPrefs(p => ({ ...p, open: !p.open })), [])
  const commitMiniRect = useCallback((rect: AnchoredRect) => setPrefs(p => ({ ...p, rect })), [])

  useEffect(() => {
    // Devre dışıyken dinleyici hiç kurulmaz: olay yutulmaz, mod diyalog arkasında değişmez.
    if (!shortcutEnabled) return
    // Capture fazı: Monaco odaktayken de çalışsın. e.key kullanılır — Türkçe F klavyede
    // e.code farklı bir fiziksel tuşa denk gelir.
    function handleKeyDown(e: KeyboardEvent) {
      if (!(e.ctrlKey || e.metaKey) || !e.shiftKey || e.altKey || e.repeat) return
      if (e.key.toLowerCase() !== 'f') return
      e.preventDefault()
      e.stopPropagation()
      setMaximized(m => !m)
    }
    document.addEventListener('keydown', handleKeyDown, true)
    return () => document.removeEventListener('keydown', handleKeyDown, true)
  }, [shortcutEnabled])

  return {
    maximized,
    miniOpen: prefs.open,
    miniRect: prefs.rect,
    showMini: maximized && prefs.open,
    enter,
    exit,
    toggle,
    setMiniOpen,
    toggleMini,
    commitMiniRect,
  }
}
