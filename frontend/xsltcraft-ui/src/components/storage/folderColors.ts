// Klasör renk paleti. Tailwind purge'ü için sınıf adları statik tutulur (dinamik üretilmez).

export const FOLDER_COLORS = ['blue', 'emerald', 'amber', 'rose', 'violet', 'slate'] as const
export type FolderColor = (typeof FOLDER_COLORS)[number]

/** Renkli nokta (color swatch) arka plan sınıfı. */
export function folderDot(color: string | null | undefined): string {
  switch (color) {
    case 'emerald': return 'bg-emerald-500'
    case 'amber': return 'bg-amber-500'
    case 'rose': return 'bg-rose-500'
    case 'violet': return 'bg-violet-500'
    case 'slate': return 'bg-slate-400'
    case 'blue':
    default: return 'bg-blue-500'
  }
}

/** Klasör ikonunun metin (tint) rengi. */
export function folderText(color: string | null | undefined): string {
  switch (color) {
    case 'emerald': return 'text-emerald-500'
    case 'amber': return 'text-amber-500'
    case 'rose': return 'text-rose-500'
    case 'violet': return 'text-violet-500'
    case 'slate': return 'text-slate-400'
    case 'blue':
    default: return 'text-blue-500'
  }
}
