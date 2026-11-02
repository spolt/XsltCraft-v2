import { FileText } from 'lucide-react'
import { PAGE_HEIGHT_MM, PAGE_WIDTH_MM } from '../../store/editorStore'

/**
 * Tasarım alanının üstündeki bilgi çubuğu. Yalnız mevcut veriyi gösterir:
 * sayfa biçimi (A4 · Dikey, sabit) ve sayfanın alana sığdırıldığı ölçek (salt-okunur).
 */
export default function CanvasToolbar({ zoomPercent }: { zoomPercent: number }) {
  return (
    <div className="flex flex-none flex-wrap items-center gap-2 border-b border-ui-line bg-ui-card px-4 py-2">
      <span className="inline-flex h-[30px] items-center gap-1.5 rounded-md border border-field-line bg-ui-card px-2.5 text-[12px] text-tx-2">
        <FileText size={13} strokeWidth={1.8} aria-hidden="true" className="text-tx-subtle" />
        A4 · Dikey
      </span>
      <span className="text-[12px] text-tx-subtle">
        {PAGE_WIDTH_MM} × {PAGE_HEIGHT_MM} mm
      </span>
      <div className="flex-1" />
      <span
        className="inline-flex h-[30px] min-w-[64px] items-center justify-center rounded-md border border-field-line px-2.5 text-[12px] tabular-nums text-tx-2"
        title="Sayfa alana sığdırılır"
        aria-label={`Yakınlaştırma yüzde ${zoomPercent}`}
      >
        %{zoomPercent}
      </span>
    </div>
  )
}
