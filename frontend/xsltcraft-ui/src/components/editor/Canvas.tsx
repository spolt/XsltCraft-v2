import {
  DndContext,
  DragOverlay,
  PointerSensor,
  useSensor,
  useSensors,
  closestCorners,
  type DragStartEvent,
  type DragEndEvent,
  type DragOverEvent,
} from '@dnd-kit/core'
import { arrayMove } from '@dnd-kit/sortable'
import { useState, useEffect, useCallback } from 'react'
import { useEditorStore } from '../../store/editorStore'
import SectionComponent from './SectionComponent'
import type { BlockType } from '../../types/blocks'

export default function Canvas() {
  const sections = useEditorStore((s) => s.sections)
  const blocks = useEditorStore((s) => s.blocks)
  const addBlock = useEditorStore((s) => s.addBlock)
  const moveBlock = useEditorStore((s) => s.moveBlock)
  const addSection = useEditorStore((s) => s.addSection)
  const undo = useEditorStore((s) => s.undo)
  const redo = useEditorStore((s) => s.redo)

  const [activeId, setActiveId] = useState<string | null>(null)
  const [overId, setOverId] = useState<string | null>(null)

  // ---- Keyboard shortcuts ----
  const handleKeyDown = useCallback(
    (e: KeyboardEvent) => {
      if ((e.ctrlKey || e.metaKey) && e.key === 'z' && !e.shiftKey) {
        e.preventDefault()
        undo()
      }
      if (
        ((e.ctrlKey || e.metaKey) && e.key === 'y') ||
        ((e.ctrlKey || e.metaKey) && e.shiftKey && e.key === 'z')
      ) {
        e.preventDefault()
        redo()
      }
    },
    [undo, redo]
  )

  useEffect(() => {
    window.addEventListener('keydown', handleKeyDown)
    return () => window.removeEventListener('keydown', handleKeyDown)
  }, [handleKeyDown])

  // ---- dnd-kit sensors ----
  const sensors = useSensors(
    useSensor(PointerSensor, { activationConstraint: { distance: 5 } })
  )

  function findSectionByBlockId(blockId: string): string | undefined {
    return sections.find((s) => s.blockIds.includes(blockId))?.id
  }

  function handleDragStart({ active }: DragStartEvent) {
    setActiveId(String(active.id))
  }

  function handleDragOver({ over }: DragOverEvent) {
    setOverId(over ? String(over.id) : null)
  }

  function handleDragEnd({ active, over }: DragEndEvent) {
    setActiveId(null)
    setOverId(null)
    if (!over) return

    const activeData = active.data.current as { source: string; blockType?: BlockType; blockId?: string; sectionId?: string }
    const overId = String(over.id)

    // Palette → Canvas: yeni blok oluştur
    if (activeData?.source === 'palette') {
      const blockType = activeData.blockType!

      // Hedef: bir section droppable alanı
      if (overId.startsWith('section-')) {
        const sectionId = overId.replace('section-', '')
        addBlock(sectionId, blockType)
        return
      }

      // Hedef: mevcut bir block'un üzerine bırakıldı → o block'un section'ına ekle
      const targetSectionId = findSectionByBlockId(overId)
      if (targetSectionId) {
        const section = sections.find((s) => s.id === targetSectionId)!
        const targetIndex = section.blockIds.indexOf(overId)
        addBlock(targetSectionId, blockType, targetIndex)
      }
      return
    }

    // Canvas → Canvas: yeniden sırala veya section değiştir
    if (activeData?.source === 'canvas') {
      const blockId = activeData.blockId!
      const fromSectionId = activeData.sectionId ?? findSectionByBlockId(blockId)
      if (!fromSectionId) return

      let toSectionId: string | undefined
      let toIndex: number

      if (overId.startsWith('section-')) {
        toSectionId = overId.replace('section-', '')
        const toSection = sections.find((s) => s.id === toSectionId)!
        toIndex = toSection.blockIds.length
      } else {
        toSectionId = findSectionByBlockId(overId)
        if (!toSectionId) return
        const toSection = sections.find((s) => s.id === toSectionId)!
        toIndex = toSection.blockIds.indexOf(overId)

        // Aynı section içi sıralama
        if (fromSectionId === toSectionId) {
          const fromSection = sections.find((s) => s.id === fromSectionId)!
          const oldIndex = fromSection.blockIds.indexOf(blockId)
          if (oldIndex !== toIndex) {
            const newIds = arrayMove(fromSection.blockIds, oldIndex, toIndex)
            // moveBlock'u 0-index konumla çağır
            newIds.forEach((id, idx) => {
              if (id === blockId) moveBlock(fromSectionId, toSectionId!, blockId, idx)
            })
          }
          return
        }
      }

      if (toSectionId) {
        moveBlock(fromSectionId, toSectionId, blockId, toIndex)
      }
    }
  }

  // Active item için overlay (sürükleme sırasında görünen kopya)
  const activeBlock = activeId && !activeId.startsWith('palette-') ? blocks[activeId] : null

  return (
    <DndContext
      sensors={sensors}
      collisionDetection={closestCorners}
      onDragStart={handleDragStart}
      onDragOver={handleDragOver}
      onDragEnd={handleDragEnd}
    >
      <main className="flex-1 overflow-y-auto bg-gray-50 p-4">
        <div className="max-w-2xl mx-auto flex flex-col gap-4">
          {sections.length === 0 && (
            <div className="text-center py-16 text-gray-400">
              <p className="text-lg mb-2">Henüz bölüm yok</p>
              <p className="text-sm">Aşağıdan bölüm ekleyin, ardından blok sürükleyin.</p>
            </div>
          )}

          {sections.map((section) => (
            <SectionComponent key={section.id} section={section} />
          ))}

          <button
            onClick={() => addSection()}
            className="w-full py-3 rounded-lg border-2 border-dashed border-gray-300 text-sm text-gray-500 hover:border-blue-400 hover:text-blue-600 hover:bg-blue-50 transition-all"
          >
            + Bölüm ekle
          </button>
        </div>
      </main>

      <DragOverlay>
        {activeBlock && (
          <div className="px-3 py-2 rounded-md border border-blue-400 bg-white shadow-xl text-sm text-gray-700 opacity-90">
            {activeBlock.type}
          </div>
        )}
      </DragOverlay>
    </DndContext>
  )
}
