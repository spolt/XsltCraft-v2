import { useState } from 'react'
import { Code2, Eye, FolderInput, Pencil, Share2, Trash2, Users } from 'lucide-react'
import type { UserXsltTemplateSummary } from '../../services/userXsltService'
import type { Folder } from '../../services/folderService'
import IconTile from '../ui/IconTile'
import { fnStyle } from '../../theme/uiThemes'
import { formatWhen } from '../dashboard/formatWhen'
import MoveToFolderMenu from './MoveToFolderMenu'
import { DeleteConfirm, FavoriteToggle, FolderCell, IconAction, RenameField, SelectBox } from './LibraryParts'

interface Props {
  template: UserXsltTemplateSummary
  folders: Folder[]
  selected: boolean
  onToggleSelect: (id: string) => void
  onOpen: () => void
  onDelete: (id: string) => void
  onRename: (id: string, name: string) => void
  onPreview: (id: string, name: string) => void
  onShare: (id: string, name: string) => void
  onMove: (id: string, folderId: string | null) => void
  onToggleFavorite: (id: string, value: boolean) => void
}

/** Şablonlarım (XSLT) tablo satırı. Paylaşılan (sahibi olunmayan) şablonda yalnız Aç + Önizle. */
export default function XsltRow({
  template, folders, selected, onToggleSelect, onOpen, onDelete, onRename, onPreview, onShare, onMove, onToggleFavorite,
}: Props) {
  const [editing, setEditing] = useState(false)
  const [showConfirm, setShowConfirm] = useState(false)
  const [moveOpen, setMoveOpen] = useState(false)
  const isOwner = template.isOwner
  const folder = folders.find((f) => f.id === template.folderId)

  function commitRename(value: string) {
    const trimmed = value.trim()
    if (trimmed && trimmed !== template.name) onRename(template.id, trimmed)
    setEditing(false)
  }

  return (
    <tr
      style={fnStyle('purple')}
      className={`group border-t border-ui-line transition-colors ${showConfirm ? 'bg-fn-red-tint' : selected ? 'bg-ui-soft' : 'hover:bg-ui-hover'}`}
    >
      <td className="w-10 py-2 pl-3.5 pr-1">
        {isOwner && <SelectBox checked={selected} onChange={() => onToggleSelect(template.id)} label={`${template.name} seç`} />}
      </td>
      <td className="py-2 pr-2 pl-1">
        <div className="flex min-w-0 items-center gap-2.5">
          <IconTile icon={Code2} size="md" />
          {editing ? (
            <RenameField initial={template.name} onCommit={commitRename} onCancel={() => setEditing(false)} mono />
          ) : (
            <div className="flex min-w-0 flex-col">
              <button type="button" onClick={onOpen} className="xc-focus truncate rounded text-left font-mono text-[12.5px] font-medium text-tx hover:text-ui-ink">
                {template.name}
              </button>
              {template.isShared ? (
                <span title={`Sahibi: ${template.ownerName}`} className="inline-flex items-center gap-1 text-[11.5px] text-fn-orange-ink">
                  <Users size={11} aria-hidden="true" /> Paylaşılan · {template.ownerName}
                </span>
              ) : (
                <span className="text-[11.5px] text-tx-subtle">XSLT</span>
              )}
            </div>
          )}
        </div>
      </td>
      <td className="px-2 py-2"><FolderCell folder={folder} /></td>
      <td className="whitespace-nowrap px-2 py-2 text-tx-muted">{formatWhen(template.updatedAt)}</td>
      <td className="py-2 pr-3.5 pl-2">
        {showConfirm ? (
          <DeleteConfirm onYes={() => onDelete(template.id)} onNo={() => setShowConfirm(false)} />
        ) : (
          <div className="flex items-center justify-end gap-0.5">
            <button
              type="button"
              onClick={onOpen}
              aria-label={`${template.name} aç`}
              className="xc-focus h-7 rounded-md px-2.5 text-xs font-semibold text-(--fn-ink) hover:bg-(--fn-tint)"
            >
              Aç
            </button>
            {isOwner && <FavoriteToggle on={template.isFavorite} onToggle={() => onToggleFavorite(template.id, !template.isFavorite)} />}
            <IconAction icon={Eye} label="Önizle" onClick={() => onPreview(template.id, template.name)} />
            {isOwner && (
              <>
                <IconAction icon={Pencil} label="Yeniden adlandır" onClick={() => setEditing(true)} />
                <IconAction icon={Share2} label="Paylaş" onClick={() => onShare(template.id, template.name)} />
                <div className="relative">
                  <IconAction
                    icon={FolderInput}
                    label="Klasöre taşı"
                    aria-haspopup="menu"
                    aria-expanded={moveOpen}
                    onClick={() => setMoveOpen((v) => !v)}
                  />
                  {moveOpen && (
                    <MoveToFolderMenu
                      folders={folders}
                      currentFolderId={template.folderId}
                      onMove={(fid) => onMove(template.id, fid)}
                      onClose={() => setMoveOpen(false)}
                    />
                  )}
                </div>
                <IconAction icon={Trash2} label="Sil" tone="danger" onClick={() => setShowConfirm(true)} />
              </>
            )}
          </div>
        )}
      </td>
    </tr>
  )
}
