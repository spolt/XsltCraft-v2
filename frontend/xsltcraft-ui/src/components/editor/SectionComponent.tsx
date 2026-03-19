import { useState } from 'react'
import { useDroppable } from '@dnd-kit/core'
import { SortableContext, verticalListSortingStrategy } from '@dnd-kit/sortable'
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
  const addBlock = useEditorStore((s) => s.addBlock)

  const { setNodeRef, isOver } = useDroppable({
    id: `section-${section.id}`,
    data: { sectionId: section.id },
  })

  const sectionBlocks = section.blockIds
    .map((id) => blocks[id])
    .filter(Boolean)

  function commitName() {
    if (nameInput.trim()) updateSection(section.id, { name: nameInput.trim() })
    else setNameInput(section.name)
    setIsEditing(false)
  }

  return (
    <div className="rounded-lg border border-gray-200 bg-white shadow-sm overflow-hidden">
      {/* Section header */}
      <div className="flex items-center gap-2 px-4 py-2 bg-gray-50 border-b border-gray-200">
        {isEditing ? (
          <input
            autoFocus
            className="flex-1 text-sm font-medium bg-white border border-blue-300 rounded px-2 py-0.5 outline-none"
            value={nameInput}
            onChange={(e) => setNameInput(e.target.value)}
            onBlur={commitName}
            onKeyDown={(e) => { if (e.key === 'Enter') commitName(); if (e.key === 'Escape') { setNameInput(section.name); setIsEditing(false) } }}
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

        <span className="text-xs text-gray-400">{sectionBlocks.length} blok</span>

        <button
          onClick={() => removeSection(section.id)}
          className="text-gray-300 hover:text-red-500 transition-colors text-xs px-1"
          title="Bölümü sil"
        >
          ✕
        </button>
      </div>

      {/* Drop zone */}
      <div
        ref={setNodeRef}
        className={`min-h-[56px] p-2 flex flex-col gap-1 transition-colors
          ${isOver ? 'bg-blue-50 ring-2 ring-inset ring-blue-300' : 'bg-white'}`}
      >
        <SortableContext
          items={section.blockIds}
          strategy={verticalListSortingStrategy}
        >
          {sectionBlocks.map((block) => (
            <BlockCard
              key={block.id}
              block={block}
              sectionId={section.id}
              onRemove={() => removeBlock(section.id, block.id)}
            />
          ))}
        </SortableContext>

        {sectionBlocks.length === 0 && !isOver && (
          <p className="text-xs text-gray-400 text-center py-3 select-none">
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
