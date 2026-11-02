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

// Panel kimlikleri: useGroupRef().setLayout bu anahtarlarla yüzde alır.
// Dış grup: [çalışma alanı | sağ sütun (AI; sekmeli modda Önizleme | AI)].
// İç grup (data-testid="xslt-editor-panels"): [editör | önizleme].
export const MAIN_PANE = 'xslt-main-pane'
export const RIGHT_PANE = 'xslt-right-pane'
export const EDITOR_PANE = 'xslt-editor-pane'
export const PREVIEW_PANE = 'xslt-preview-pane'
export const SPLIT_LAYOUTS: Record<'side' | 'half', Record<string, number>> = {
  side: { [EDITOR_PANE]: 65, [PREVIEW_PANE]: 35 },
  half: { [EDITOR_PANE]: 50, [PREVIEW_PANE]: 50 },
}

// Ayırıcı: 1px çizgi, sürükleme/odakta accent. role="separator" + ok tuşlarıyla boyutlandırma kütüphaneden.
export const separatorCls =
  'relative z-10 flex-none bg-ui-line outline-none transition-colors data-[separator=hover]:bg-ui-accent data-[separator=active]:bg-ui-accent focus-visible:bg-ui-accent focus-visible:shadow-[0_0_0_2px_var(--ui-accent)]'
