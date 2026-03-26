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
  const duplicateSection = useEditorStore((s) => s.duplicateSection)

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

  const colLabel = isThreeCol ? '3 sütun' : isTwoCol ? '2 sütun' : '1 sütun'

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
    <div
      ref={setSortRef}
      style={{
        ...sortStyle,
        background: 'var(--color-surface-card)',
        border: '0.5px solid #D3D1C7',
        borderRadius: 8,
        boxShadow: '0 1px 4px rgba(0,0,0,0.04)',
        overflow: 'hidden',
      }}
    >
      {/* Section header */}
      <div
        className="flex items-center"
        style={{
          padding: '8px 12px',
          borderBottom: '0.5px solid #E8E6E0',
          background: 'var(--color-surface-card)',
          gap: 6,
        }}
      >
        {/* Drag handle */}
        <span
          {...sortAttrs}
          {...sortListeners}
          style={{ color: '#C0BAB0', cursor: 'grab', fontSize: 13, flexShrink: 0, userSelect: 'none' }}
          title="Bölümü taşı"
          onClick={(e) => e.stopPropagation()}
        >
          ⠿
        </span>

        {isEditing ? (
          <input
            autoFocus
            style={{
              flex: 1,
              fontSize: 11,
              fontWeight: 500,
              background: 'var(--color-surface-card)',
              border: '0.5px solid var(--color-brand-primary)',
              borderRadius: 4,
              padding: '2px 6px',
              outline: 'none',
              color: 'var(--color-text-primary)',
              fontFamily: 'inherit',
            }}
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
            style={{ flex: 1, fontSize: 11, fontWeight: 500, color: 'var(--color-text-secondary)', cursor: 'text' }}
            onDoubleClick={() => setIsEditing(true)}
            title="Çift tıkla — adı düzenle"
          >
            {section.name}
          </span>
        )}

        {/* Layout toggle badge */}
        <button
          onClick={toggleLayout}
          title={
            isThreeCol ? 'Tek sütuna geç' :
            isTwoCol   ? 'Üç sütuna geç' :
            'İki sütuna geç'
          }
          style={{
            display: 'inline-flex',
            alignItems: 'center',
            gap: 3,
            height: 18,
            padding: '0 6px',
            fontSize: 10,
            background: 'var(--color-surface-secondary)',
            border: '0.5px solid var(--color-border-subtle)',
            borderRadius: 4,
            color: 'var(--color-text-muted)',
            cursor: 'pointer',
            fontFamily: 'inherit',
          }}
        >
          {isThreeCol ? <Columns3 size={10} /> : isTwoCol ? <Columns2 size={10} /> : <AlignLeft size={10} />}
          {colLabel}
        </button>

        {/* Blok sayısı badge */}
        <span
          style={{
            fontSize: 10,
            background: 'var(--color-surface-secondary)',
            border: '0.5px solid var(--color-border-subtle)',
            borderRadius: 4,
            padding: '1px 6px',
            color: 'var(--color-text-muted)',
          }}
        >
          {sectionBlocks.length} blok
        </span>

        {/* Kopyala */}
        <button
          onClick={() => duplicateSection(section.id)}
          title="Bölümü kopyala"
          style={{
            width: 22,
            height: 22,
            border: '0.5px solid var(--color-border-default)',
            borderRadius: 4,
            background: 'transparent',
            color: 'var(--color-text-muted)',
            cursor: 'pointer',
            fontSize: 11,
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'center',
          }}
          onMouseEnter={e => { e.currentTarget.style.color = 'var(--color-text-primary)'; e.currentTarget.style.background = 'var(--color-surface-secondary)' }}
          onMouseLeave={e => { e.currentTarget.style.color = 'var(--color-text-muted)'; e.currentTarget.style.background = 'transparent' }}
        >
          ⧉
        </button>

        {/* Sil */}
        <button
          onClick={() => removeSection(section.id)}
          title="Bölümü sil"
          style={{
            width: 22,
            height: 22,
            border: '0.5px solid var(--color-border-default)',
            borderRadius: 4,
            background: 'transparent',
            color: 'var(--color-text-muted)',
            cursor: 'pointer',
            fontSize: 11,
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'center',
          }}
          onMouseEnter={e => { e.currentTarget.style.color = 'var(--color-danger)'; e.currentTarget.style.background = 'var(--color-danger-bg)'; e.currentTarget.style.borderColor = 'var(--color-danger)' }}
          onMouseLeave={e => { e.currentTarget.style.color = 'var(--color-text-muted)'; e.currentTarget.style.background = 'transparent'; e.currentTarget.style.borderColor = 'var(--color-border-default)' }}
        >
          ✕
        </button>
      </div>

      {/* Drop zone */}
      <div
        ref={setDropRef}
        className={`flex flex-wrap content-start ${isTwoCol || isThreeCol ? 'justify-between' : ''}`}
        style={{
          minHeight: 56,
          padding: 8,
          background: isOver ? 'var(--color-brand-light)' : 'var(--color-surface-card)',
          transition: 'background 150ms',
          gap: 8,
        }}
      >
        <SortableContext items={section.blockIds} strategy={verticalListSortingStrategy}>
          {sectionBlocks.map((block) => {
            const w = block.layout?.width ?? 'full'
            const a = block.layout?.alignment ?? 'left'

            const widthClass =
              w === '1/2' ? 'w-[calc(50%-4px)]' :
              w === '1/3' ? 'w-[calc(33.333%-5.334px)]' :
              w === '2/3' ? 'w-[calc(66.667%-2.667px)]' :
              'w-full'

            const alignClass =
              a === 'center' ? 'mx-auto' :
              a === 'right'  ? 'ml-auto' :
              ''

            return (
              <div key={block.id} className={`${widthClass} ${alignClass}`}>
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
          <p style={{ width: '100%', fontSize: 11, color: 'var(--color-text-muted)', textAlign: 'center', padding: '12px 0', userSelect: 'none' }}>
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
    <div style={{ borderTop: '0.5px solid var(--color-border-default)', padding: '4px 8px' }}>
      {open ? (
        <div className="flex flex-wrap" style={{ gap: 4 }}>
          {QUICK_TYPES.map(({ type, label }) => (
            <button
              key={type}
              onClick={() => { onAdd(sectionId, type); setOpen(false) }}
              style={{
                fontSize: 10,
                padding: '2px 8px',
                borderRadius: 4,
                border: '0.5px solid var(--color-border-default)',
                background: 'var(--color-surface-secondary)',
                color: 'var(--color-text-secondary)',
                cursor: 'pointer',
                fontFamily: 'inherit',
              }}
              onMouseEnter={e => { e.currentTarget.style.borderColor = 'var(--color-brand-border)'; e.currentTarget.style.color = 'var(--color-brand-primary)'; e.currentTarget.style.background = 'var(--color-brand-light)' }}
              onMouseLeave={e => { e.currentTarget.style.borderColor = 'var(--color-border-default)'; e.currentTarget.style.color = 'var(--color-text-secondary)'; e.currentTarget.style.background = 'var(--color-surface-secondary)' }}
            >
              + {label}
            </button>
          ))}
          <button
            onClick={() => setOpen(false)}
            style={{ fontSize: 10, padding: '2px 6px', color: 'var(--color-text-muted)', background: 'none', border: 'none', cursor: 'pointer', fontFamily: 'inherit' }}
          >
            İptal
          </button>
        </div>
      ) : (
        <button
          onClick={() => setOpen(true)}
          style={{
            width: '100%',
            height: 30,
            borderRadius: 6,
            border: '1px dashed #D3D1C7',
            background: 'transparent',
            fontSize: 11,
            color: 'var(--color-text-muted)',
            cursor: 'pointer',
            transition: 'border-color 150ms, color 150ms, background 150ms',
            fontFamily: 'inherit',
          }}
          onMouseEnter={e => { e.currentTarget.style.borderColor = 'var(--color-brand-primary)'; e.currentTarget.style.color = 'var(--color-brand-primary)'; e.currentTarget.style.background = '#EBF3FC' }}
          onMouseLeave={e => { e.currentTarget.style.borderColor = '#D3D1C7'; e.currentTarget.style.color = 'var(--color-text-muted)'; e.currentTarget.style.background = 'transparent' }}
        >
          + Blok ekle
        </button>
      )}
    </div>
  )
}
