import { Columns2, PanelRight, PictureInPicture2, Square, type LucideIcon } from 'lucide-react'
import type { EditorLayoutId } from '../../store/uiPrefsStore'

export interface LayoutOption {
  id: EditorLayoutId
  /** Uzun ad (aria-label, durum çubuğu). */
  name: string
  /** Kısa ad (Ayarlar). */
  short: string
  icon: LucideIcon
}

/** XSLT editörünün dört düzen modu (araç çubuğu + Ayarlar → varsayılan düzen). */
export const EDITOR_LAYOUTS: readonly LayoutOption[] = [
  { id: 'side', name: 'Editör + yan önizleme', short: 'Yan panel', icon: PanelRight },
  { id: 'half', name: 'Yarı yarıya', short: 'Yarı yarıya', icon: Columns2 },
  { id: 'mini', name: 'Mini pencere önizleme', short: 'Mini pencere', icon: PictureInPicture2 },
  { id: 'editorOnly', name: 'Yalnız editör', short: 'Yalnız editör', icon: Square },
]

export function layoutName(id: EditorLayoutId): string {
  return EDITOR_LAYOUTS.find(l => l.id === id)?.name ?? ''
}
