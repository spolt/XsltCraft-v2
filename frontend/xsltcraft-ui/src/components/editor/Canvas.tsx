import { useEffect, useCallback } from 'react'
import { SortableContext, verticalListSortingStrategy } from '@dnd-kit/sortable'
import { useEditorStore } from '../../store/editorStore'
import SectionComponent from './SectionComponent'

export default function Canvas() {
  const sections = useEditorStore((s) => s.sections)
  const addSection = useEditorStore((s) => s.addSection)
  const undo = useEditorStore((s) => s.undo)
  const redo = useEditorStore((s) => s.redo)

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

  return (
    <main className="flex-1 overflow-y-auto bg-[#e8e8e8] p-4">
      {/* A4 kağıt alanı */}
      <div className="w-[794px] min-h-[1123px] mx-auto bg-white shadow-[0_4px_24px_rgba(0,0,0,0.10)] rounded-sm my-6 px-10 py-8 flex flex-col gap-4">
        {sections.length === 0 && (
          <div className="text-center py-16 text-gray-400">
            <p className="text-lg mb-2">Henüz bölüm yok</p>
            <p className="text-sm">Aşağıdan bölüm ekleyin, ardından blok sürükleyin.</p>
          </div>
        )}

        <SortableContext
          items={sections.map((s) => `sort-section-${s.id}`)}
          strategy={verticalListSortingStrategy}
        >
          {sections.map((section) => (
            <SectionComponent key={section.id} section={section} />
          ))}
        </SortableContext>
      </div>

      {/* + Bölüm ekle — A4 kutusunun dışında, altında */}
      <div className="w-[794px] mx-auto mb-6">
        <button
          onClick={() => addSection()}
          className="w-full py-3 rounded-lg border-2 border-dashed border-gray-400 text-sm text-gray-500 hover:border-blue-400 hover:text-blue-600 hover:bg-blue-50 transition-all"
        >
          + Bölüm ekle
        </button>
      </div>
    </main>
  )
}
