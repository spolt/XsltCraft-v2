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
  Conditional: 'Koşul Bloğu',
  Image: 'Görsel',
  DocumentInfo: 'Belge Bilgisi',
  Totals: 'Toplamlar',
  Notes: 'Notlar',
  BankInfo: 'Banka Bilgisi',
  ETTN: 'Dinamik Karekod',
  GibKarekod: 'GİB Karekod',
  Divider: 'Ayırıcı',
  Spacer: 'Boşluk',
  Variable: 'Değişken',
  ConditionalText: 'Koşullu Metin',
  TaxSummary: 'KDV Özeti',
  InvoiceLineTable: 'Fatura Satırları',
  PartyInfo: 'Taraf Bilgisi',
  InvoiceHeader: 'Fatura Başlığı',
  InvoiceTotals: 'Fatura Dip Toplamları',
  GibLogo: 'GİB LOGO',
}

const BLOCK_ICONS: Record<string, string> = {
  Text: 'T',
  Heading: 'H',
  Paragraph: '¶',
  Table: '⊞',
  ForEach: '↺',
  Conditional: '∨',
  Image: '⬜',
  DocumentInfo: '⊙',
  Totals: 'Σ',
  Notes: '✎',
  BankInfo: '₺',
  ETTN: '◎',
  GibKarekod: '◎',
  Divider: '—',
  Spacer: '↕',
  Variable: '$',
  ConditionalText: '?',
  TaxSummary: 'Σ',
  InvoiceLineTable: '⊟',
  PartyInfo: '⊞',
  InvoiceHeader: '☰',
  InvoiceTotals: '₸',
  GibLogo: '⊕',
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
      style={{
        ...style,
        padding: '8px 12px',
        borderRadius: 6,
        border: isSelected ? '1.5px solid #185FA5' : '1.5px solid #B5D4F4',
        background: isSelected ? '#D6E9F8' : '#EBF3FC',
        boxShadow: isSelected ? '0 0 0 2px rgba(24,95,165,0.12)' : 'none',
        cursor: 'pointer',
        userSelect: 'none',
        opacity: isDragging ? 0.4 : 1,
        display: 'flex',
        alignItems: 'center',
        gap: 6,
        transition: 'border-color 120ms, background 120ms',
      }}
      onClick={() => selectBlock(block.id)}
      onMouseEnter={e => {
        if (!isSelected) {
          e.currentTarget.style.borderColor = '#378ADD'
          e.currentTarget.style.background = '#E0EDF8'
        }
      }}
      onMouseLeave={e => {
        if (!isSelected) {
          e.currentTarget.style.borderColor = '#B5D4F4'
          e.currentTarget.style.background = '#EBF3FC'
        }
      }}
    >
      {/* Drag handle */}
      <span
        {...attributes}
        {...listeners}
        style={{ color: '#B5D4F4', cursor: 'grab', fontSize: 12, flexShrink: 0 }}
        onClick={(e) => e.stopPropagation()}
      >
        ⠿
      </span>

      {/* Icon */}
      <span
        style={{
          width: 22,
          height: 22,
          display: 'flex',
          alignItems: 'center',
          justifyContent: 'center',
          fontSize: 11,
          background: '#D6E9F8',
          borderRadius: 4,
          color: '#185FA5',
          flexShrink: 0,
        }}
      >
        {BLOCK_ICONS[block.type] ?? '?'}
      </span>

      {/* Label */}
      <span style={{ fontSize: 12, fontWeight: 500, color: '#185FA5', flex: 1, overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }}>
        {BLOCK_LABELS[block.type] ?? block.type}
      </span>

      {/* Type badge */}
      <span style={{ fontSize: 10, background: '#C5DDF4', color: '#5B9BD5', padding: '1px 6px', borderRadius: 3, flexShrink: 0 }}>
        {block.type}
      </span>

      {/* Remove button */}
      <button
        onClick={(e) => { e.stopPropagation(); onRemove() }}
        style={{
          background: 'none',
          border: 'none',
          cursor: 'pointer',
          fontSize: 11,
          color: '#B5D4F4',
          padding: '0 2px',
          lineHeight: 1,
          flexShrink: 0,
          transition: 'color 120ms',
        }}
        title="Bloğu kaldır"
        onMouseEnter={e => (e.currentTarget.style.color = 'var(--color-danger)')}
        onMouseLeave={e => (e.currentTarget.style.color = '#B5D4F4')}
      >
        ✕
      </button>
    </div>
  )
}
