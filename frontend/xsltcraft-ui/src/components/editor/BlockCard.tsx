import { useSortable } from '@dnd-kit/sortable'
import { CSS } from '@dnd-kit/utilities'
import { useEditorStore } from '../../store/editorStore'
import type { Block } from '../../types/blocks'

const BLOCK_LABELS: Record<string, string> = {
  Text: 'Metin',
  Heading: 'Başlık',
  Paragraph: 'Paragraf',
  Table: 'Tablo',
  ForEach: 'For-Each',
  Conditional: 'Koşul',
  Image: 'Görsel',
  DocumentInfo: 'Belge Bilgisi',
  Totals: 'Toplamlar',
  Notes: 'Notlar',
  BankInfo: 'Banka Bilgisi',
  ETTN: 'ETTN / QR',
  Divider: 'Ayırıcı',
  Spacer: 'Boşluk',
}

const BLOCK_ICONS: Record<string, string> = {
  Text: 'T',
  Heading: 'H',
  Paragraph: '¶',
  Table: '⊞',
  ForEach: '↻',
  Conditional: '⑂',
  Image: '🖼',
  DocumentInfo: 'ⓘ',
  Totals: 'Σ',
  Notes: '✎',
  BankInfo: '₺',
  ETTN: '⬡',
  Divider: '—',
  Spacer: '↕',
}

interface BlockCardProps {
  block: Block
  sectionId: string
  onRemove: () => void
}

export default function BlockCard({ block, sectionId, onRemove }: BlockCardProps) {
  const selectedBlockId = useEditorStore((s) => s.selectedBlockId)
  const selectBlock = useEditorStore((s) => s.selectBlock)

  const isSelected = selectedBlockId === block.id

  const { attributes, listeners, setNodeRef, transform, transition, isDragging } = useSortable({
    id: block.id,
    data: { source: 'canvas', blockId: block.id, sectionId },
  })

  const style = {
    transform: CSS.Transform.toString(transform),
    transition,
  }

  return (
    <div
      ref={setNodeRef}
      style={style}
      onClick={() => selectBlock(block.id)}
      className={`group flex items-center gap-2 px-3 py-2 rounded-md border cursor-pointer select-none transition-all
        ${isDragging ? 'opacity-40 shadow-xl z-50' : ''}
        ${isSelected
          ? 'border-blue-500 bg-blue-50 shadow-sm'
          : 'border-gray-200 bg-white hover:border-gray-300 hover:bg-gray-50'
        }`}
    >
      {/* Drag handle */}
      <span
        {...attributes}
        {...listeners}
        className="cursor-grab text-gray-300 hover:text-gray-500 flex-shrink-0"
        onClick={(e) => e.stopPropagation()}
      >
        ⠿
      </span>

      {/* Icon */}
      <span className="w-6 h-6 flex items-center justify-center text-xs font-mono bg-gray-100 rounded text-gray-500 flex-shrink-0">
        {BLOCK_ICONS[block.type] ?? '?'}
      </span>

      {/* Label */}
      <span className={`text-sm flex-1 truncate ${isSelected ? 'text-blue-700 font-medium' : 'text-gray-700'}`}>
        {BLOCK_LABELS[block.type] ?? block.type}
      </span>

      {/* Remove button */}
      <button
        onClick={(e) => { e.stopPropagation(); onRemove() }}
        className="opacity-0 group-hover:opacity-100 text-gray-400 hover:text-red-500 transition-opacity text-sm leading-none px-1"
        title="Bloğu kaldır"
      >
        ✕
      </button>
    </div>
  )
}
