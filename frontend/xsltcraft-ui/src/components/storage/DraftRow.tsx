import { useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { Copy, Eye, FolderInput, Pencil, Trash2 } from 'lucide-react'
import type { TemplateDetail } from '../../services/templateService'
import type { Folder } from '../../services/folderService'
import IconTile from '../ui/IconTile'
import { fnStyle } from '../../theme/uiThemes'
import { DOC_TYPE_LABEL, SOURCE_LABEL, draftOpenPath, draftSource } from '../../utils/workRoutes'
import { formatWhen } from '../dashboard/formatWhen'
import { workVisual } from '../dashboard/workVisual'
import MoveToFolderMenu from './MoveToFolderMenu'
import { DeleteConfirm, FavoriteToggle, FolderCell, IconAction, RenameField, SelectBox } from './LibraryParts'

interface Props {
  template: TemplateDetail
  folders: Folder[]
  selected: boolean
  onToggleSelect: (id: string) => void
  onClone: (id: string) => void
  onDelete: (id: string) => void
  onRename: (id: string, name: string) => void
  onPreview: (template: TemplateDetail) => void
  onMove: (id: string, folderId: string | null) => void
  onToggleFavorite: (id: string, value: boolean) => void
}

/** Taslaklarım tablo satırı. */
export default function DraftRow({
  template, folders, selected, onToggleSelect, onClone, onDelete, onRename, onPreview, onMove, onToggleFavorite,
}: Props) {
  const navigate = useNavigate()
  const [editing, setEditing] = useState(false)
  const [showConfirm, setShowConfirm] = useState(false)
  const [moveOpen, setMoveOpen] = useState(false)

  const source = draftSource(template)
  const visual = workVisual({ source, documentType: template.documentType })
  const folder = folders.find((f) => f.id === template.folderId)
  const open = () => navigate(draftOpenPath(template.id))

  function commitRename(value: string) {
    const trimmed = value.trim()
    if (trimmed && trimmed !== template.name) onRename(template.id, trimmed)
    setEditing(false)
  }

  return (
    <tr
      style={fnStyle(visual.color)}
      className={`group border-t border-ui-line transition-colors ${showConfirm ? 'bg-fn-red-tint' : selected ? 'bg-ui-soft' : 'hover:bg-ui-hover'}`}
    >
      <td className="w-10 py-2 pl-3.5 pr-1">
        <SelectBox checked={selected} onChange={() => onToggleSelect(template.id)} label={`${template.name} seç`} />
      </td>
      <td className="py-2 pr-2 pl-1">
        <div className="flex min-w-0 items-center gap-2.5">
          <IconTile icon={visual.icon} size="md" />
          {editing ? (
            <RenameField initial={template.name} onCommit={commitRename} onCancel={() => setEditing(false)} />
          ) : (
            <div className="flex min-w-0 flex-col">
              <button type="button" onClick={open} className="xc-focus truncate rounded text-left font-medium text-tx hover:text-ui-ink">
                {template.name}
              </button>
              <span className="text-[11.5px] text-tx-subtle">{SOURCE_LABEL[source]}</span>
            </div>
          )}
        </div>
      </td>
      <td className="whitespace-nowrap px-2 py-2 text-tx-2">{DOC_TYPE_LABEL[template.documentType] ?? template.documentType}</td>
      <td className="px-2 py-2"><FolderCell folder={folder} /></td>
      <td className="whitespace-nowrap px-2 py-2 text-tx-muted">{formatWhen(template.updatedAt)}</td>
      <td className="py-2 pr-3.5 pl-2">
        {showConfirm ? (
          <DeleteConfirm onYes={() => onDelete(template.id)} onNo={() => setShowConfirm(false)} />
        ) : (
          <div className="flex items-center justify-end gap-0.5">
            <button
              type="button"
              onClick={open}
              aria-label={`${template.name} aç`}
              className="xc-focus h-7 rounded-md px-2.5 text-xs font-semibold text-(--fn-ink) hover:bg-(--fn-tint)"
            >
              Aç
            </button>
            <FavoriteToggle on={template.isFavorite} onToggle={() => onToggleFavorite(template.id, !template.isFavorite)} />
            <IconAction icon={Eye} label="Önizle" onClick={() => onPreview(template)} />
            <IconAction icon={Pencil} label="Yeniden adlandır" onClick={() => setEditing(true)} />
            <IconAction icon={Copy} label="Kopyala" onClick={() => onClone(template.id)} />
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
          </div>
        )}
      </td>
    </tr>
  )
}
