import { useEffect } from 'react'
import { Check, FolderInput } from 'lucide-react'
import { folderDot } from './folderColors'

interface MoveToFolderMenuProps {
  folders: { id: string; name: string; color: string | null }[]
  currentFolderId?: string | null
  onMove: (folderId: string | null) => void
  onClose: () => void
  /** Tetikleyiciye göre hizalama (varsayılan sağ). */
  align?: 'left' | 'right'
}

export default function MoveToFolderMenu({ folders, currentFolderId, onMove, onClose, align = 'right' }: MoveToFolderMenuProps) {
  useEffect(() => {
    function onKey(e: KeyboardEvent) {
      if (e.key === 'Escape') onClose()
    }
    document.addEventListener('keydown', onKey)
    return () => document.removeEventListener('keydown', onKey)
  }, [onClose])

  const item = 'xc-focus flex w-full items-center gap-2 px-3 py-1.5 text-left text-[13px] text-tx-2 hover:bg-ui-hover'
  return (
    <>
      {/* Dışarı tıklama yakalayıcı */}
      <div className="fixed inset-0 z-40" onClick={onClose} />
      <div
        role="menu"
        aria-label="Klasöre taşı"
        className={`absolute z-50 mt-1 max-h-72 w-56 overflow-y-auto rounded-xl border border-ui-line bg-ui-card py-1 shadow-[var(--ui-shadow-pop)] ${align === 'right' ? 'right-0' : 'left-0'}`}
        onClick={(e) => e.stopPropagation()}
      >
        <p className="m-0 px-3 py-1.5 text-[11px] font-semibold uppercase tracking-[.07em] text-tx-muted">Klasöre taşı</p>

        <button
          type="button"
          role="menuitemradio"
          aria-checked={currentFolderId === null || currentFolderId === undefined}
          onClick={() => { onMove(null); onClose() }}
          className={item}
        >
          <FolderInput size={14} aria-hidden="true" className="flex-none text-tx-subtle" />
          <span className="flex-1 truncate">Klasörden çıkar</span>
          {(currentFolderId === null || currentFolderId === undefined) && <Check size={14} aria-hidden="true" className="flex-none text-ui-accent" />}
        </button>

        {folders.length > 0 && <div className="my-1 border-t border-ui-line" />}

        {folders.map((f) => (
          <button
            key={f.id}
            type="button"
            role="menuitemradio"
            aria-checked={currentFolderId === f.id}
            onClick={() => { onMove(f.id); onClose() }}
            className={item}
          >
            <span aria-hidden="true" className={`h-2.5 w-2.5 flex-none rounded-[3px] ${folderDot(f.color)}`} />
            <span className="flex-1 truncate">{f.name}</span>
            {currentFolderId === f.id && <Check size={14} aria-hidden="true" className="flex-none text-ui-accent" />}
          </button>
        ))}

        {folders.length === 0 && (
          <p className="m-0 px-3 py-2 text-xs text-tx-subtle">Henüz klasör yok.</p>
        )}
      </div>
    </>
  )
}
