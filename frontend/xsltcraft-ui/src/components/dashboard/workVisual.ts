import { Truck, type LucideIcon } from 'lucide-react'
import type { WorkItem } from '../../hooks/useRecentWork'
import type { FnColor } from '../../theme/uiThemes'
import { SOURCE_COLOR, SOURCE_ICON } from '../../utils/workRoutes'

/** Çalışmanın ikon + işlev rengi: Tasarım alanındaki e-İrsaliye taslağı turuncu kamyon, diğerleri kaynağa göre. */
export function workVisual(item: Pick<WorkItem, 'source' | 'documentType'>): { icon: LucideIcon; color: FnColor } {
  if (item.source === 'design' && item.documentType === 'Despatch') return { icon: Truck, color: 'orange' }
  return { icon: SOURCE_ICON[item.source], color: SOURCE_COLOR[item.source] }
}
