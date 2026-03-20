import { useDraggable } from '@dnd-kit/core'
import type { BlockType } from '../../types/blocks'

interface PaletteItem {
  type: BlockType
  label: string
  icon: string
}

const CATEGORIES: { name: string; items: PaletteItem[] }[] = [
  {
    name: 'Metin',
    items: [
      { type: 'Text', label: 'Metin', icon: 'T' },
      { type: 'Heading', label: 'Başlık', icon: 'H' },
      { type: 'Paragraph', label: 'Paragraf', icon: '¶' },
      { type: 'ConditionalText', label: 'Koşullu Metin', icon: '?' },
    ],
  },
  {
    name: 'Veri',
    items: [
      { type: 'Table', label: 'Tablo', icon: '⊞' },
      { type: 'TaxSummary', label: 'KDV Özeti', icon: '%' },
      { type: 'DocumentInfo', label: 'Belge Bilgisi', icon: 'ⓘ' },
      { type: 'Totals', label: 'Toplamlar', icon: 'Σ' },
      { type: 'Notes', label: 'Notlar', icon: '✎' },
      { type: 'BankInfo', label: 'Banka Bilgisi', icon: '₺' },
    ],
  },
  {
    name: 'Medya',
    items: [
      { type: 'Image', label: 'Görsel', icon: '🖼' },
      { type: 'ETTN', label: 'Dinamik Karekod', icon: '⬡' },
      { type: 'GibKarekod', label: 'GİB Karekod', icon: '⬡' },
    ],
  },
  {
    name: 'Düzen',
    items: [
      { type: 'ForEach', label: 'For-Each', icon: '↻' },
      { type: 'Conditional', label: 'Koşul Bloğu', icon: '⑂' },
      { type: 'Variable', label: 'Değişken', icon: '$' },
      { type: 'Divider', label: 'Ayırıcı', icon: '—' },
      { type: 'Spacer', label: 'Boşluk', icon: '↕' },
    ],
  },
]

function DraggablePaletteItem({ item }: { item: PaletteItem }) {
  const { attributes, listeners, setNodeRef, isDragging } = useDraggable({
    id: `palette-${item.type}`,
    data: { source: 'palette', blockType: item.type },
  })

  return (
    <div
      ref={setNodeRef}
      {...listeners}
      {...attributes}
      className={`flex items-center gap-2 px-3 py-2 rounded-md border border-transparent cursor-grab select-none transition-all
        hover:bg-blue-50 hover:border-blue-200
        ${isDragging ? 'opacity-50 shadow-lg' : ''}`}
    >
      <span className="w-6 h-6 flex items-center justify-center text-sm font-mono bg-gray-100 rounded text-gray-600">
        {item.icon}
      </span>
      <span className="text-sm text-gray-700">{item.label}</span>
    </div>
  )
}

export default function BlockPalette() {
  return (
    <aside className="w-56 flex-shrink-0 border-r border-gray-200 bg-white overflow-y-auto flex flex-col">
      <div className="px-3 py-3 border-b border-gray-100">
        <h2 className="text-xs font-semibold text-gray-500 uppercase tracking-wide">Bloklar</h2>
      </div>
      <div className="p-2 flex flex-col gap-4">
        {CATEGORIES.map((cat) => (
          <div key={cat.name}>
            <p className="px-1 mb-1 text-xs font-medium text-gray-400 uppercase tracking-wide">
              {cat.name}
            </p>
            <div className="flex flex-col gap-0.5">
              {cat.items.map((item) => (
                <DraggablePaletteItem key={item.type} item={item} />
              ))}
            </div>
          </div>
        ))}
      </div>
    </aside>
  )
}
