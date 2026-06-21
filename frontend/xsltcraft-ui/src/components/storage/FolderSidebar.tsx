import { useState, type ReactNode } from 'react'
import { Folder as FolderIcon, FolderPlus, MoreVertical, Check, X, Trash2, Pencil } from 'lucide-react'
import { FOLDER_COLORS, folderDot, folderText } from './folderColors'

export interface SpecialEntry {
  key: string
  label: string
  icon: ReactNode
  count: number
}

export interface FolderItem {
  id: string
  name: string
  color: string | null
  count: number
}

interface FolderSidebarProps {
  specials: SpecialEntry[]
  folders: FolderItem[]
  activeKey: string
  onSelect: (key: string) => void
  onCreate: (name: string, color: string | null) => void
  onRename: (id: string, name: string) => void
  onRecolor: (id: string, color: string | null) => void
  onDelete: (id: string) => void
}

function ColorRow({ value, onPick }: { value: string | null; onPick: (c: string) => void }) {
  return (
    <div className="flex items-center gap-1.5 px-1 py-1">
      {FOLDER_COLORS.map((c) => (
        <button
          key={c}
          type="button"
          onClick={() => onPick(c)}
          className={`w-4 h-4 rounded-full ${folderDot(c)} transition-transform hover:scale-110 ${value === c ? 'ring-2 ring-offset-1 ring-gray-400' : ''}`}
          title={c}
        />
      ))}
    </div>
  )
}

export default function FolderSidebar({
  specials, folders, activeKey, onSelect, onCreate, onRename, onRecolor, onDelete,
}: FolderSidebarProps) {
  const [creating, setCreating] = useState(false)
  const [newName, setNewName] = useState('')
  const [newColor, setNewColor] = useState<string>('blue')
  const [editingId, setEditingId] = useState<string | null>(null)
  const [editName, setEditName] = useState('')
  const [menuId, setMenuId] = useState<string | null>(null)
  const [confirmId, setConfirmId] = useState<string | null>(null)

  function commitCreate() {
    const trimmed = newName.trim()
    if (trimmed) onCreate(trimmed, newColor)
    setNewName('')
    setNewColor('blue')
    setCreating(false)
  }

  function commitRename(id: string) {
    const trimmed = editName.trim()
    if (trimmed) onRename(id, trimmed)
    setEditingId(null)
  }

  return (
    <aside className="w-52 flex-shrink-0 border-r border-gray-200 bg-gray-50/60 overflow-y-auto hidden md:flex md:flex-col">
      <p className="px-4 pt-5 pb-2 text-[11px] font-semibold tracking-wider text-gray-400">KLASÖRLER</p>

      {/* Sözde-klasörler: Tümü / Favoriler / (Paylaşılan) */}
      <div className="px-2 flex flex-col gap-0.5">
        {specials.map((s) => {
          const active = activeKey === s.key
          return (
            <button
              key={s.key}
              onClick={() => onSelect(s.key)}
              className={`group flex items-center gap-2 px-2.5 py-1.5 rounded-lg text-sm transition-colors ${
                active ? 'bg-blue-50 text-blue-700 font-medium' : 'text-gray-600 hover:bg-gray-100'
              }`}
            >
              <span className={active ? 'text-blue-600' : 'text-gray-400'}>{s.icon}</span>
              <span className="flex-1 text-left truncate">{s.label}</span>
              <span className="text-xs text-gray-400">{s.count}</span>
            </button>
          )
        })}
      </div>

      <div className="mx-4 my-2 border-t border-gray-200" />

      {/* Kullanıcı klasörleri */}
      <div className="px-2 flex flex-col gap-0.5">
        {folders.map((f) => {
          const active = activeKey === f.id
          if (editingId === f.id) {
            return (
              <div key={f.id} className="flex items-center gap-1 px-2 py-1">
                <input
                  autoFocus
                  value={editName}
                  onChange={(e) => setEditName(e.target.value)}
                  onKeyDown={(e) => {
                    if (e.key === 'Enter') commitRename(f.id)
                    if (e.key === 'Escape') setEditingId(null)
                  }}
                  className="flex-1 min-w-0 text-sm border border-blue-300 rounded px-2 py-0.5 outline-none focus:ring-1 focus:ring-blue-200"
                />
                <button onClick={() => commitRename(f.id)} className="text-green-600 hover:text-green-700"><Check size={14} /></button>
                <button onClick={() => setEditingId(null)} className="text-red-400 hover:text-red-600"><X size={14} /></button>
              </div>
            )
          }
          return (
            <div key={f.id} className="relative">
              <button
                onClick={() => onSelect(f.id)}
                className={`group w-full flex items-center gap-2 px-2.5 py-1.5 rounded-lg text-sm transition-colors ${
                  active ? 'bg-blue-50 text-blue-700 font-medium' : 'text-gray-600 hover:bg-gray-100'
                }`}
              >
                <FolderIcon size={15} className={folderText(f.color)} />
                <span className="flex-1 text-left truncate">{f.name}</span>
                <span
                  role="button"
                  tabIndex={0}
                  onClick={(e) => { e.stopPropagation(); setMenuId(menuId === f.id ? null : f.id); setConfirmId(null) }}
                  className="opacity-0 group-hover:opacity-100 text-gray-400 hover:text-gray-700 transition-opacity"
                >
                  <MoreVertical size={14} />
                </span>
                <span className="text-xs text-gray-400 group-hover:hidden">{f.count}</span>
              </button>

              {menuId === f.id && (
                <>
                  <div className="fixed inset-0 z-40" onClick={() => { setMenuId(null); setConfirmId(null) }} />
                  <div className="absolute right-2 top-9 z-50 w-44 bg-white border border-gray-200 rounded-xl shadow-lg py-1">
                    <button
                      onClick={() => { setEditingId(f.id); setEditName(f.name); setMenuId(null) }}
                      className="w-full flex items-center gap-2 px-3 py-1.5 text-sm text-gray-700 hover:bg-gray-50 text-left"
                    >
                      <Pencil size={13} className="text-gray-400" /> Yeniden adlandır
                    </button>
                    <div className="px-2 py-1">
                      <ColorRow value={f.color} onPick={(c) => { onRecolor(f.id, c); setMenuId(null) }} />
                    </div>
                    <div className="my-1 border-t border-gray-100" />
                    {confirmId === f.id ? (
                      <div className="flex items-center gap-2 px-3 py-1.5 text-sm">
                        <span className="text-red-600 flex-1">Silinsin mi?</span>
                        <button onClick={() => { onDelete(f.id); setMenuId(null); setConfirmId(null) }} className="font-medium text-red-600 hover:text-red-700">Evet</button>
                        <button onClick={() => setConfirmId(null)} className="text-gray-500 hover:text-gray-700">Hayır</button>
                      </div>
                    ) : (
                      <button
                        onClick={() => setConfirmId(f.id)}
                        className="w-full flex items-center gap-2 px-3 py-1.5 text-sm text-red-600 hover:bg-red-50 text-left"
                      >
                        <Trash2 size={13} /> Sil
                      </button>
                    )}
                  </div>
                </>
              )}
            </div>
          )
        })}
      </div>

      {/* Yeni klasör */}
      <div className="px-2 mt-1 mb-4">
        {creating ? (
          <div className="px-1 py-1">
            <div className="flex items-center gap-1">
              <input
                autoFocus
                value={newName}
                onChange={(e) => setNewName(e.target.value)}
                placeholder="Klasör adı"
                onKeyDown={(e) => {
                  if (e.key === 'Enter') commitCreate()
                  if (e.key === 'Escape') { setCreating(false); setNewName('') }
                }}
                className="flex-1 min-w-0 text-sm border border-blue-300 rounded px-2 py-0.5 outline-none focus:ring-1 focus:ring-blue-200"
              />
              <button onClick={commitCreate} className="text-green-600 hover:text-green-700"><Check size={14} /></button>
              <button onClick={() => { setCreating(false); setNewName('') }} className="text-red-400 hover:text-red-600"><X size={14} /></button>
            </div>
            <ColorRow value={newColor} onPick={setNewColor} />
          </div>
        ) : (
          <button
            onClick={() => setCreating(true)}
            className="w-full flex items-center gap-2 px-2.5 py-1.5 rounded-lg text-sm text-gray-500 hover:bg-gray-100 hover:text-gray-700 transition-colors"
          >
            <FolderPlus size={15} /> Yeni klasör
          </button>
        )}
      </div>
    </aside>
  )
}
