import React, { useState } from 'react'
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
  const moveBlockToCol = useEditorStore((s) => s.moveBlockToCol)
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
    const isNextMultiCol = next === 'two-column' || next === 'three-column'
    updateSection(section.id, { layout: next })
    sectionBlocks.forEach((b, idx) => {
      if (isNextMultiCol) {
        const assignedCol = b.layout?.col ?? idx % (next === 'three-column' ? 3 : 2)
        const width = next === 'three-column'
          ? (assignedCol === 0 ? '2/5' : '3/10')
          : '1/2'
        updateBlockLayout(b.id, { width, col: assignedCol })
      } else {
        // Single-col: clear col assignment
        const { col: _col, width: _width, ...rest } = b.layout ?? {}
        updateBlockLayout(b.id, { width: 'full', ...rest, col: undefined })
      }
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
        style={{
          minHeight: 56,
          padding: 8,
          background: isOver ? 'var(--color-brand-light)' : 'var(--color-surface-card)',
          transition: 'background 150ms',
        }}
      >
        <SortableContext items={section.blockIds} strategy={verticalListSortingStrategy}>
          {isTwoCol || isThreeCol ? (
            // Sütun gruplu görünüm
            <div style={{ display: 'flex', gap: 6 }}>
              {Array.from({ length: isThreeCol ? 3 : 2 }, (_, ci) => {
                const colBlocks = sectionBlocks.filter((b) => (b.layout?.col ?? 0) === ci)
                const maxCol = isThreeCol ? 2 : 1
                return (
                  <ColDropZone
                    key={ci}
                    sectionId={section.id}
                    colIdx={ci}
                  >
                    <div style={{ fontSize: 9, fontWeight: 600, color: 'var(--color-text-muted)', textAlign: 'center', marginBottom: 6, letterSpacing: 0.5, userSelect: 'none' }}>
                      SÜTUN {ci + 1}
                    </div>
                    <div style={{ display: 'flex', flexDirection: 'column', gap: 4 }}>
                      {colBlocks.map((block) => (
                        <div key={block.id}>
                          <BlockCard
                            block={block}
                            sectionId={section.id}
                            onRemove={() => removeBlock(section.id, block.id)}
                          />
                          <div style={{ display: 'flex', gap: 3, marginTop: 3 }}>
                            {ci > 0 && (
                              <button
                                onClick={(e) => { e.stopPropagation(); moveBlockToCol(block.id, ci - 1) }}
                                style={{ fontSize: 9, padding: '1px 6px', border: '0.5px solid var(--color-border-default)', borderRadius: 3, background: 'transparent', color: 'var(--color-text-muted)', cursor: 'pointer', fontFamily: 'inherit', lineHeight: 1.6 }}
                                title={`Sütun ${ci}'e taşı`}
                              >
                                ← S{ci}
                              </button>
                            )}
                            {ci < maxCol && (
                              <button
                                onClick={(e) => { e.stopPropagation(); moveBlockToCol(block.id, ci + 1) }}
                                style={{ fontSize: 9, padding: '1px 6px', border: '0.5px solid var(--color-border-default)', borderRadius: 3, background: 'transparent', color: 'var(--color-text-muted)', cursor: 'pointer', fontFamily: 'inherit', lineHeight: 1.6 }}
                                title={`Sütun ${ci + 2}'ye taşı`}
                              >
                                S{ci + 2} →
                              </button>
                            )}
                          </div>
                        </div>
                      ))}
                      {colBlocks.length === 0 && (
                        <p style={{ fontSize: 10, color: '#C8C6C0', textAlign: 'center', padding: '8px 0', userSelect: 'none' }}>
                          Boş
                        </p>
                      )}
                    </div>
                  </ColDropZone>
                )
              })}
            </div>
          ) : (
            // Tek sütun: düz liste
            <div className="flex flex-col" style={{ gap: 8 }}>
              {sectionBlocks.map((block) => (
                <BlockCard
                  key={block.id}
                  block={block}
                  sectionId={section.id}
                  onRemove={() => removeBlock(section.id, block.id)}
                />
              ))}
            </div>
          )}
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

function ColDropZone({
  sectionId,
  colIdx,
  children,
}: {
  sectionId: string
  colIdx: number
  children: React.ReactNode
}) {
  const { setNodeRef, isOver } = useDroppable({
    id: `colzone-${sectionId}-${colIdx}`,
    data: { type: 'colzone', sectionId, colIdx },
  })

  return (
    <div
      ref={setNodeRef}
      style={{
        flex: 1,
        minWidth: 0,
        border: isOver ? '1.5px dashed var(--color-brand-primary)' : '1px dashed #D0CEC8',
        borderRadius: 6,
        padding: 6,
        background: isOver ? 'var(--color-brand-light)' : 'var(--color-surface-secondary)',
        transition: 'border-color 120ms, background 120ms',
      }}
    >
      {children}
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
