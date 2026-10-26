// Klasör renk paleti. Saklanan değerler (blue, emerald, …) aynen kalır; görünüm işlev renk tokenlarına
// (theme.css --color-fn-*) eşlenir. Tailwind taraması için sınıf adları statik tutulur.

export const FOLDER_COLORS = ['blue', 'emerald', 'amber', 'rose', 'violet', 'slate'] as const
export type FolderColor = (typeof FOLDER_COLORS)[number]

/** Renk seçicide gösterilen Türkçe ad (erişilebilir ad). */
export const FOLDER_COLOR_NAME: Record<FolderColor, string> = {
  blue: 'Mavi',
  emerald: 'Yeşil',
  amber: 'Turuncu',
  rose: 'Kırmızı',
  violet: 'Mor',
  slate: 'Gri',
}

/** Renkli nokta (color swatch) arka plan sınıfı. */
export function folderDot(color: string | null | undefined): string {
  switch (color) {
    case 'emerald': return 'bg-fn-green'
    case 'amber': return 'bg-fn-orange'
    case 'rose': return 'bg-fn-red'
    case 'violet': return 'bg-fn-violet'
    case 'slate': return 'bg-fn-gray'
    case 'blue':
    default: return 'bg-fn-blue'
  }
}

/** Klasör ikonunun metin rengi. */
export function folderText(color: string | null | undefined): string {
  switch (color) {
    case 'emerald': return 'text-fn-green'
    case 'amber': return 'text-fn-orange'
    case 'rose': return 'text-fn-red'
    case 'violet': return 'text-fn-violet'
    case 'slate': return 'text-fn-gray'
    case 'blue':
    default: return 'text-fn-blue'
  }
}
