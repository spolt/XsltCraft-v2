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
  return (
    <>
      {/* Dışarı tıklama yakalayıcı */}
      <div className="fixed inset-0 z-40" onClick={onClose} />
      <div
        className={`absolute z-50 mt-1 w-56 max-h-72 overflow-y-auto bg-white border border-gray-200 rounded-xl shadow-lg py-1 ${align === 'right' ? 'right-0' : 'left-0'}`}
        onClick={(e) => e.stopPropagation()}
      >
        <p className="px-3 py-1.5 text-[11px] font-medium text-gray-400 uppercase tracking-wide">Klasöre taşı</p>

        <button
          onClick={() => { onMove(null); onClose() }}
          className="w-full flex items-center gap-2 px-3 py-1.5 text-sm text-gray-600 hover:bg-gray-50 text-left"
        >
          <FolderInput size={14} className="text-gray-400 flex-shrink-0" />
          <span className="flex-1 truncate">Klasörden çıkar</span>
          {(currentFolderId === null || currentFolderId === undefined) && <Check size={14} className="text-blue-600 flex-shrink-0" />}
        </button>

        {folders.length > 0 && <div className="my-1 border-t border-gray-100" />}

        {folders.map((f) => (
          <button
            key={f.id}
            onClick={() => { onMove(f.id); onClose() }}
            className="w-full flex items-center gap-2 px-3 py-1.5 text-sm text-gray-700 hover:bg-gray-50 text-left"
          >
            <span className={`w-2.5 h-2.5 rounded-full flex-shrink-0 ${folderDot(f.color)}`} />
            <span className="flex-1 truncate">{f.name}</span>
            {currentFolderId === f.id && <Check size={14} className="text-blue-600 flex-shrink-0" />}
          </button>
        ))}

        {folders.length === 0 && (
          <p className="px-3 py-2 text-xs text-gray-400">Henüz klasör yok.</p>
        )}
      </div>
    </>
  )
}
