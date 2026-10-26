import { useState, useMemo, useId } from 'react'
import { useDraggable } from '@dnd-kit/core'
import { FileText, Search } from 'lucide-react'
import { useEditorStore } from '../../store/editorStore'
import { DOCUMENT_TYPE_LABEL, paletteGroups, type PaletteItem } from './paletteIcons'

function DraggablePaletteItem({ item }: { item: PaletteItem }) {
  // DnD sözleşmesi: id `palette-{id}` ve data (source/blockType/configOverride) EditorPage.handleDragEnd ile eşleşir.
  const { attributes, listeners, setNodeRef, isDragging } = useDraggable({
    id: `palette-${item.id}`,
    data: { source: 'palette', blockType: item.type, configOverride: item.configOverride },
  })
  const Icon = item.icon

  return (
    <button
      type="button"
      ref={setNodeRef}
      {...listeners}
      {...attributes}
      title={`${item.label} — sayfaya sürükle`}
      className={`xc-focus flex h-9 min-w-0 cursor-grab select-none items-center gap-2 rounded-md border border-field-line bg-ui-card px-2.5 text-left text-[12px] text-tx transition-colors hover:border-ui-soft-line hover:bg-ui-soft active:cursor-grabbing ${
        isDragging ? 'opacity-50' : ''
      }`}
    >
      <Icon size={15} strokeWidth={1.8} aria-hidden="true" className="flex-none text-ui-accent" />
      <span className="truncate">{item.label}</span>
    </button>
  )
}

/** Sol paneldeki "Bloklar" sekmesi: belge türü (salt-okunur), arama ve gruplu blok kartları. */
export default function BlockPalette() {
  const [search, setSearch] = useState('')
  const documentType = useEditorStore((s) => s.documentType)
  const searchId = useId()

  const filtered = useMemo(() => {
    const groups = paletteGroups(documentType)
    const q = search.trim().toLocaleLowerCase('tr')
    if (!q) return groups
    return groups
      .map((g) => ({
        ...g,
        items: g.items.filter((i) => i.label.toLocaleLowerCase('tr').includes(q) || i.type.toLowerCase().includes(q)),
      }))
      .filter((g) => g.items.length > 0)
  }, [search, documentType])

  return (
    <div className="flex flex-col gap-3 px-4 py-3.5">
      <div className="flex items-center gap-2 rounded-md bg-ui-hover px-2.5 py-1.5 text-[12px] text-tx-muted">
        <FileText size={14} strokeWidth={1.8} aria-hidden="true" className="flex-none" />
        <span>Belge türü:</span>
        <span className="font-semibold text-tx">{DOCUMENT_TYPE_LABEL[documentType]}</span>
      </div>

      <label
        htmlFor={searchId}
        className="flex h-[34px] items-center gap-2 rounded-md border border-field-line bg-ui-side px-2.5 text-tx-subtle focus-within:border-ui-accent"
      >
        <Search size={15} strokeWidth={1.8} aria-hidden="true" />
        <span className="sr-only">Blok ara</span>
        <input
          id={searchId}
          type="search"
          placeholder="Blok ara…"
          value={search}
          onChange={(e) => setSearch(e.target.value)}
          className="min-w-0 flex-1 bg-transparent text-[13px] text-tx outline-none placeholder:text-tx-subtle"
        />
      </label>

      {filtered.length === 0 && (
        <p className="py-4 text-center text-[12px] text-tx-subtle">“{search}” ile eşleşen blok yok.</p>
      )}

      {filtered.map((group) => (
        <section key={group.id} aria-labelledby={`${searchId}-${group.id}`} className="flex flex-col gap-1.5">
          <div className="flex items-center justify-between">
            <h2
              id={`${searchId}-${group.id}`}
              className="m-0 text-[11px] font-semibold uppercase tracking-[0.07em] text-tx-muted"
            >
              {group.title}
            </h2>
            <span className="text-[11px] text-tx-subtle">{group.items.length} blok</span>
          </div>
          <div className="grid grid-cols-2 gap-1.5">
            {group.items.map((item) => (
              <DraggablePaletteItem key={item.id} item={item} />
            ))}
          </div>
        </section>
      ))}
    </div>
  )
}
