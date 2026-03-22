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
    <main
      className="flex-1 overflow-y-auto"
      style={{ background: 'var(--color-surface-page)', padding: '24px 20px' }}
    >
      <div
        className="mx-auto flex flex-col"
        style={{ maxWidth: 600, gap: 8 }}
      >
        {sections.length === 0 && (
          <div className="text-center" style={{ padding: '64px 0', color: 'var(--color-text-muted)' }}>
            <p style={{ fontSize: 13, marginBottom: 4 }}>Henüz bölüm yok</p>
            <p style={{ fontSize: 11 }}>Aşağıdan bölüm ekleyin, ardından blok sürükleyin.</p>
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

        {/* + Bölüm ekle */}
        <button
          onClick={() => addSection()}
          style={{
            width: '100%',
            height: 36,
            borderRadius: 6,
            border: '1px dashed var(--color-border-default)',
            background: 'transparent',
            fontSize: 11,
            color: 'var(--color-text-muted)',
            cursor: 'pointer',
            transition: 'border-color 150ms, color 150ms, background 150ms',
          }}
          onMouseEnter={e => {
            e.currentTarget.style.borderColor = 'var(--color-brand-primary)'
            e.currentTarget.style.color = 'var(--color-brand-primary)'
            e.currentTarget.style.background = 'var(--color-brand-light)'
          }}
          onMouseLeave={e => {
            e.currentTarget.style.borderColor = 'var(--color-border-default)'
            e.currentTarget.style.color = 'var(--color-text-muted)'
            e.currentTarget.style.background = 'transparent'
          }}
        >
          + Bölüm ekle
        </button>
      </div>
    </main>
  )
}
