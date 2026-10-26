import { useEditorStore } from '../../store/editorStore'
import { useXmlStore } from '../../store/xmlStore'
import { blockLabel } from './paletteIcons'

const mm = (v: number) => `${Math.round(v * 10) / 10}`

interface DesignStatusBarProps {
  isSaving: boolean
}

/**
 * Tasarım Alanı durum çubuğu — yalnız mevcut veri: blok sayısı, seçili blok + mm konum/boyut,
 * ızgara adımı, etkin XML ve kayıt durumu.
 */
export default function DesignStatusBar({ isSaving }: DesignStatusBarProps) {
  const blocks = useEditorStore((s) => s.blocks)
  const selectedBlockId = useEditorStore((s) => s.selectedBlockId)
  const documentType = useEditorStore((s) => s.documentType)
  const templateId = useEditorStore((s) => s.templateId)
  const isDirty = useEditorStore((s) => s.isDirty)
  const activeXmlName = useXmlStore((s) => s.xmlFiles.find((f) => f.id === s.activeXmlId)?.name)

  const count = Object.keys(blocks).length
  const selected = selectedBlockId ? blocks[selectedBlockId] : undefined
  const gl = selected?.gridLayout

  const saveState = isSaving
    ? 'Kaydediliyor…'
    : isDirty
      ? 'Kaydedilmemiş değişiklik'
      : templateId
        ? 'Kaydedildi'
        : 'Henüz kaydedilmedi'

  return (
    <footer className="flex min-h-7 flex-none flex-wrap items-center gap-x-[18px] gap-y-0.5 border-t border-ui-line bg-ui-side px-4 py-1 text-[11.5px] text-tx-muted">
      <span>{count} blok</span>
      {selected && gl ? (
        <span>
          Seçili: <span className="text-tx-2">{blockLabel(selected.type, documentType)}</span>
          <span className="tabular-nums"> · x {mm(gl.x)} · y {mm(gl.y)} · {mm(gl.width)} × {mm(gl.height)} mm</span>
        </span>
      ) : (
        <span>Seçili blok yok</span>
      )}
      <span>5 mm ızgara</span>
      {activeXmlName && (
        <span className="max-w-[220px] truncate" title={activeXmlName}>
          XML: <span className="font-mono">{activeXmlName}</span>
        </span>
      )}
      <span aria-live="polite" className="ml-auto flex items-center gap-1.5">
        <span
          aria-hidden="true"
          className={`inline-block h-[7px] w-[7px] rounded-full ${isDirty ? 'bg-fn-orange' : templateId ? 'bg-fn-green' : 'bg-fn-gray'}`}
        />
        {saveState}
      </span>
    </footer>
  )
}
