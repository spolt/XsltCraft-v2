import { useState } from 'react'
import { useDroppable } from '@dnd-kit/core'
import { SortableContext, useSortable, verticalListSortingStrategy } from '@dnd-kit/sortable'
import { CSS } from '@dnd-kit/utilities'
import { Columns2, Columns3, AlignLeft } from 'lucide-react'
import { useEditorStore } from '../../store/editorStore'
import BlockCard from './BlockCard'
import type { Section } from '../../types/template'
import type { BlockType } from '../../types/blocks'

interface SectionComponentProps {
  section: Section
}

export default function SectionComponent({ section }: SectionComponentProps) {
  const [isEditing, setIsEditing] = useState(false)
  const [nameInput, setNameInput] = useState(section.name)

  const blocks = useEditorStore((s) => s.blocks)
  const removeBlock = useEditorStore((s) => s.removeBlock)
  const removeSection = useEditorStore((s) => s.removeSection)
  const updateSection = useEditorStore((s) => s.updateSection)
  const updateBlockLayout = useEditorStore((s) => s.updateBlockLayout)
  const addBlock = useEditorStore((s) => s.addBlock)

  const {
    attributes: sortAttrs,
    listeners: sortListeners,
    setNodeRef: setSortRef,
    transform,
    transition,
    isDragging,
  } = useSortable({ id: `sort-section-${section.id}`, data: { source: 'section', sectionId: section.id } })

  const sortStyle = {
    transform: CSS.Transform.toString(transform),
    transition,
    opacity: isDragging ? 0.4 : 1,
  }

  const { setNodeRef: setDropRef, isOver } = useDroppable({
    id: `section-${section.id}`,
    data: { sectionId: section.id },
  })

  const sectionBlocks = section.blockIds.map((id) => blocks[id]).filter(Boolean)
  const isTwoCol = section.layout === 'two-column'
  const isThreeCol = section.layout === 'three-column'

  function commitName() {
    if (nameInput.trim()) updateSection(section.id, { name: nameInput.trim() })
    else setNameInput(section.name)
    setIsEditing(false)
  }

  function toggleLayout() {
    const next =
      section.layout === 'single-column' ? 'two-column' :
      section.layout === 'two-column'    ? 'three-column' :
      'single-column'
    const widthMap = {
      'single-column': 'full',
      'two-column': '1/2',
      'three-column': '1/3',
    } as const
    updateSection(section.id, { layout: next })
    sectionBlocks.forEach((b) => {
      updateBlockLayout(b.id, { width: widthMap[next] })
    })
  }

  return (
    <div ref={setSortRef} style={sortStyle} className="rounded-lg border border-gray-200 bg-white shadow-sm overflow-hidden">
      {/* Section header */}
      <div className="flex items-center gap-2 px-2 py-2 bg-gray-50 border-b border-gray-200">
        <span
          {...sortAttrs}
          {...sortListeners}
          className="cursor-grab text-gray-300 hover:text-gray-500 px-1 flex-shrink-0 select-none"
          title="Bölümü taşı"
          onClick={(e) => e.stopPropagation()}
        >
          ⠿
        </span>

        {isEditing ? (
          <input
            autoFocus
            className="flex-1 text-sm font-medium bg-white border border-blue-300 rounded px-2 py-0.5 outline-none"
            value={nameInput}
            onChange={(e) => setNameInput(e.target.value)}
            onBlur={commitName}
            onKeyDown={(e) => {
              if (e.key === 'Enter') commitName()
              if (e.key === 'Escape') { setNameInput(section.name); setIsEditing(false) }
            }}
          />
        ) : (
          <span
            className="flex-1 text-sm font-semibold text-gray-700 cursor-text"
            onDoubleClick={() => setIsEditing(true)}
            title="Çift tıkla — adı düzenle"
          >
            {section.name}
          </span>
        )}

        {/* Layout toggle */}
        <button
          onClick={toggleLayout}
          title={
            isThreeCol ? 'Tek sütuna geç (tüm blokları tam genişlik yapar)' :
            isTwoCol   ? 'Üç sütuna geç (tüm blokları ⅓ genişlik yapar)' :
            'İki sütuna geç (tüm blokları ½ genişlik yapar)'
          }
          className={`flex items-center gap-1 px-2 py-0.5 rounded text-xs border transition-colors ${
            isThreeCol
              ? 'border-purple-400 bg-purple-50 text-purple-700'
              : isTwoCol
              ? 'border-blue-400 bg-blue-50 text-blue-700'
              : 'border-gray-200 text-gray-400 hover:border-gray-300 hover:text-gray-600'
          }`}
        >
          {isThreeCol ? <Columns3 size={12} /> : isTwoCol ? <Columns2 size={12} /> : <AlignLeft size={12} />}
          {isThreeCol ? '3 Sütun' : isTwoCol ? '2 Sütun' : '1 Sütun'}
        </button>

        <span className="text-xs text-gray-400">{sectionBlocks.length} blok</span>

        <button
          onClick={() => removeSection(section.id)}
          className="text-gray-300 hover:text-red-500 transition-colors text-xs px-1"
          title="Bölümü sil"
        >
          ✕
        </button>
      </div>

      {/* Drop zone — always flex-wrap to mirror the XSLT table-based output */}
      <div
        ref={setDropRef}
        className={`min-h-[56px] p-2 flex flex-wrap content-start transition-colors ${
          isOver ? 'bg-blue-50 ring-2 ring-inset ring-blue-300' : 'bg-white'
        }`}
      >
        <SortableContext items={section.blockIds} strategy={verticalListSortingStrategy}>
          {sectionBlocks.map((block) => {
            const w = block.layout?.width ?? 'full'
            const a = block.layout?.alignment ?? 'left'

            const widthClass =
              w === '1/2' ? 'w-[calc(50%-4px)]' :
              w === '1/3' ? 'w-[calc(33.333%-4px)]' :
              w === '2/3' ? 'w-[calc(66.667%-4px)]' :
              'w-full'

            const alignClass =
              a === 'center' ? 'mx-auto' :
              a === 'right'  ? 'ml-auto' :
              ''

            return (
              <div key={block.id} className={`${widthClass} ${alignClass} p-[2px]`}>
                <BlockCard
                  block={block}
                  sectionId={section.id}
                  onRemove={() => removeBlock(section.id, block.id)}
                />
              </div>
            )
          })}
        </SortableContext>

        {sectionBlocks.length === 0 && !isOver && (
          <p className="w-full text-xs text-gray-400 text-center py-3 select-none">
            Blok sürükle veya aşağıdan ekle
          </p>
        )}
      </div>

      {/* Add block shortcut */}
      <AddBlockRow sectionId={section.id} onAdd={addBlock} />
    </div>
  )
}

function AddBlockRow({
  sectionId,
  onAdd,
}: {
  sectionId: string
  onAdd: (sectionId: string, type: BlockType) => void
}) {
  const [open, setOpen] = useState(false)

  const QUICK_TYPES: { type: BlockType; label: string }[] = [
    { type: 'Text', label: 'Metin' },
    { type: 'Heading', label: 'Başlık' },
    { type: 'Table', label: 'Tablo' },
    { type: 'Divider', label: 'Ayırıcı' },
    { type: 'Spacer', label: 'Boşluk' },
  ]

  return (
    <div className="border-t border-gray-100 px-2 py-1.5">
      {open ? (
        <div className="flex flex-wrap gap-1">
          {QUICK_TYPES.map(({ type, label }) => (
            <button
              key={type}
              onClick={() => { onAdd(sectionId, type); setOpen(false) }}
              className="text-xs px-2 py-1 rounded border border-gray-200 bg-gray-50 hover:bg-blue-50 hover:border-blue-300 text-gray-600 hover:text-blue-700 transition-colors"
            >
              + {label}
            </button>
          ))}
          <button
            onClick={() => setOpen(false)}
            className="text-xs px-2 py-1 text-gray-400 hover:text-gray-600"
          >
            İptal
          </button>
        </div>
      ) : (
        <button
          onClick={() => setOpen(true)}
          className="w-full text-xs text-gray-400 hover:text-blue-600 hover:bg-blue-50 py-1 rounded transition-colors text-center"
        >
          + Blok ekle
        </button>
      )}
    </div>
  )
}
