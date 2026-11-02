import { BookOpen, Code2, LayoutGrid, type LucideIcon } from 'lucide-react'
import type { TemplateDetail } from '../services/templateService'
import type { FnColor } from '../theme/uiThemes'

/** Çalışmanın kaynağı: tasarım alanı taslağı, temadan türetilmiş taslak ya da kullanıcı XSLT'si. */
export type WorkSource = 'design' | 'theme' | 'xslt'

export const SOURCE_LABEL: Record<WorkSource, string> = {
  design: 'Tasarım alanı',
  theme: 'Temadan',
  xslt: 'XSLT',
}

/** Kaynak çipi / ikon kutusu rengi. */
export const SOURCE_COLOR: Record<WorkSource, FnColor> = {
  design: 'blue',
  theme: 'green',
  xslt: 'purple',
}

/** Kaynak ikonu (IconTile icon=). */
export const SOURCE_ICON: Record<WorkSource, LucideIcon> = {
  design: LayoutGrid,
  theme: BookOpen,
  xslt: Code2,
}

export const DOC_TYPE_LABEL: Record<string, string> = {
  Invoice: 'e-Fatura / e-Arşiv',
  Despatch: 'e-İrsaliye',
}

/** Taslağı açma yolu — DraftsPage ile birebir: tüm taslaklar (temadan gelenler dahil) Tasarım Alanı'nda açılır. */
export function draftOpenPath(id: string): string {
  return `/editor/${id}`
}

/** Kullanıcı XSLT şablonunu açma yolu — MyXsltTemplatesPage ile birebir. */
export function xsltOpenPath(id: string): string {
  return `/xslt-editor/${id}`
}

function hasBlocks(blockTree: string | null): boolean {
  if (!blockTree) return false
  try {
    const raw = JSON.parse(blockTree) as { blocks?: Record<string, unknown> } | null
    return !!raw?.blocks && Object.keys(raw.blocks).length > 0
  } catch {
    return false
  }
}

/**
 * Temadan türetilmiş taslak: saklı XSLT var ve blok ağacı boş (EditorPreviewPanel'in
 * "useStoredXslt" koşuluyla aynı ölçüt).
 */
export function draftSource(t: Pick<TemplateDetail, 'hasStoredXslt' | 'blockTree'>): WorkSource {
  return t.hasStoredXslt && !hasBlocks(t.blockTree) ? 'theme' : 'design'
}
