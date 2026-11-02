import { useState, type ReactNode } from 'react'
import { Folder as FolderIcon, Plus, MoreVertical, Check, X, Trash2, Pencil } from 'lucide-react'
import { FOLDER_COLORS, FOLDER_COLOR_NAME, folderDot, folderText } from './folderColors'

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
    <div role="group" aria-label="Klasör rengi" className="flex items-center gap-1.5 px-1 py-1">
      {FOLDER_COLORS.map((c) => (
        <button
          key={c}
          type="button"
          onClick={() => onPick(c)}
          aria-label={FOLDER_COLOR_NAME[c]}
          aria-pressed={value === c}
          title={FOLDER_COLOR_NAME[c]}
          className={`xc-focus h-4 w-4 rounded-full ${folderDot(c)} transition-transform hover:scale-110 motion-reduce:transition-none ${value === c ? 'ring-2 ring-tx-muted ring-offset-1 ring-offset-ui-card' : ''}`}
        />
      ))}
    </div>
  )
}

const ROW = 'xc-focus flex h-9 w-full items-center gap-2 rounded-[7px] px-2 text-left text-[13px] transition-colors'
const ROW_ON = 'bg-ui-soft font-semibold text-ui-ink'
const ROW_OFF = 'font-normal text-tx hover:bg-ui-hover'
const MINI_BTN = 'xc-focus inline-flex h-6 w-6 flex-none items-center justify-center rounded'
const INPUT = 'min-w-0 flex-1 rounded-md border border-ui-soft-line bg-ui-card px-2 py-0.5 text-[13px] text-tx outline-none focus:ring-2 focus:ring-ui-soft-line'

/** Klasörler kartı: Tümü / Favoriler / (Paylaşılan) + kullanıcı klasörleri ve klasör işlemleri. */
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

  function closeMenu() {
    setMenuId(null)
    setConfirmId(null)
  }

  return (
    <aside
      aria-label="Klasörler"
      className="flex w-full flex-col gap-0.5 self-start rounded-xl border border-ui-line bg-ui-card px-2 py-3 shadow-[var(--ui-shadow-rest)] md:w-[214px] md:flex-none"
    >
      <div className="flex items-center justify-between px-2 pb-1.5">
        <h2 className="m-0 text-[11px] font-semibold tracking-[.07em] text-tx-muted">KLASÖRLER</h2>
        <button
          type="button"
          onClick={() => setCreating(true)}
          aria-label="Yeni klasör"
          title="Yeni klasör"
          className={`${MINI_BTN} h-[26px] w-[26px] rounded-md text-tx-2 hover:bg-ui-hover`}
        >
          <Plus size={16} aria-hidden="true" />
        </button>
      </div>

      {/* Yeni klasör formu */}
      {creating && (
        <div className="px-1 py-1">
          <div className="flex items-center gap-1">
            <input
              autoFocus
              value={newName}
              onChange={(e) => setNewName(e.target.value)}
              placeholder="Klasör adı"
              aria-label="Yeni klasör adı"
              onKeyDown={(e) => {
                if (e.key === 'Enter') commitCreate()
                if (e.key === 'Escape') { setCreating(false); setNewName('') }
              }}
              className={INPUT}
            />
            <button type="button" onClick={commitCreate} aria-label="Oluştur" className={`${MINI_BTN} text-fn-green-ink hover:bg-fn-green-tint`}><Check size={14} aria-hidden="true" /></button>
            <button type="button" onClick={() => { setCreating(false); setNewName('') }} aria-label="Vazgeç" className={`${MINI_BTN} text-tx-subtle hover:bg-ui-hover`}><X size={14} aria-hidden="true" /></button>
          </div>
          <ColorRow value={newColor} onPick={setNewColor} />
        </div>
      )}

      {/* Sözde-klasörler: Tümü / Favoriler / (Paylaşılan) */}
      {specials.map((s) => {
        const active = activeKey === s.key
        return (
          <button
            key={s.key}
            type="button"
            onClick={() => onSelect(s.key)}
            aria-current={active ? 'true' : undefined}
            className={`${ROW} ${active ? ROW_ON : ROW_OFF}`}
          >
            <span aria-hidden="true" className={`flex flex-none ${active ? 'text-ui-accent' : 'text-tx-subtle'}`}>{s.icon}</span>
            <span className="flex-1 truncate">{s.label}</span>
            <span className="text-[11.5px] font-normal text-tx-subtle">{s.count}</span>
          </button>
        )
      })}

      {folders.length > 0 && <div className="mx-2 my-1.5 border-t border-ui-line" />}

      {/* Kullanıcı klasörleri */}
      {folders.map((f) => {
        const active = activeKey === f.id
        if (editingId === f.id) {
          return (
            <div key={f.id} className="flex items-center gap-1 px-1 py-1">
              <input
                autoFocus
                value={editName}
                aria-label="Klasör adı"
                onChange={(e) => setEditName(e.target.value)}
                onKeyDown={(e) => {
                  if (e.key === 'Enter') commitRename(f.id)
                  if (e.key === 'Escape') setEditingId(null)
                }}
                className={INPUT}
              />
              <button type="button" onClick={() => commitRename(f.id)} aria-label="Kaydet" className={`${MINI_BTN} text-fn-green-ink hover:bg-fn-green-tint`}><Check size={14} aria-hidden="true" /></button>
              <button type="button" onClick={() => setEditingId(null)} aria-label="Vazgeç" className={`${MINI_BTN} text-tx-subtle hover:bg-ui-hover`}><X size={14} aria-hidden="true" /></button>
            </div>
          )
        }
        const menuOpen = menuId === f.id
        return (
          <div key={f.id} className="group relative flex items-center">
            <button
              type="button"
              onClick={() => onSelect(f.id)}
              aria-current={active ? 'true' : undefined}
              className={`${ROW} pr-9 ${active ? ROW_ON : ROW_OFF}`}
            >
              <FolderIcon size={15} aria-hidden="true" className={`flex-none ${folderText(f.color)}`} />
              <span className="flex-1 truncate">{f.name}</span>
              <span className="text-[11.5px] font-normal text-tx-subtle">{f.count}</span>
            </button>
            <button
              type="button"
              aria-label={`${f.name} klasörü seçenekleri`}
              aria-haspopup="menu"
              aria-expanded={menuOpen}
              onClick={() => { setMenuId(menuOpen ? null : f.id); setConfirmId(null) }}
              className={`${MINI_BTN} absolute right-1 text-tx-subtle opacity-0 hover:bg-ui-hover hover:text-tx group-hover:opacity-100 focus-visible:opacity-100 ${menuOpen ? 'opacity-100' : ''}`}
            >
              <MoreVertical size={14} aria-hidden="true" />
            </button>

            {menuOpen && (
              <>
                <div className="fixed inset-0 z-40" onClick={closeMenu} />
                <div
                  role="menu"
                  aria-label={`${f.name} klasörü`}
                  onKeyDown={(e) => { if (e.key === 'Escape') closeMenu() }}
                  className="absolute top-9 right-1 z-50 w-48 rounded-xl border border-ui-line bg-ui-card py-1 shadow-[var(--ui-shadow-pop)]"
                >
                  <button
                    type="button"
                    role="menuitem"
                    onClick={() => { setEditingId(f.id); setEditName(f.name); setMenuId(null) }}
                    className="xc-focus flex w-full items-center gap-2 px-3 py-1.5 text-left text-[13px] text-tx-2 hover:bg-ui-hover"
                  >
                    <Pencil size={13} aria-hidden="true" className="text-tx-subtle" /> Yeniden adlandır
                  </button>
                  <div className="px-2 py-1">
                    <ColorRow value={f.color} onPick={(c) => { onRecolor(f.id, c); setMenuId(null) }} />
                  </div>
                  <div className="my-1 border-t border-ui-line" />
                  {confirmId === f.id ? (
                    <div className="flex items-center gap-2 px-3 py-1.5 text-[13px]">
                      <span className="flex-1 font-medium text-fn-red-ink">Silinsin mi?</span>
                      <button type="button" onClick={() => { onDelete(f.id); closeMenu() }} className="xc-focus rounded font-semibold text-fn-red-ink hover:underline">Evet</button>
                      <button type="button" onClick={() => setConfirmId(null)} className="xc-focus rounded text-tx-muted hover:text-tx">Hayır</button>
                    </div>
                  ) : (
                    <button
                      type="button"
                      role="menuitem"
                      onClick={() => setConfirmId(f.id)}
                      className="xc-focus flex w-full items-center gap-2 px-3 py-1.5 text-left text-[13px] text-fn-red-ink hover:bg-fn-red-tint"
                    >
                      <Trash2 size={13} aria-hidden="true" /> Sil
                    </button>
                  )}
                </div>
              </>
            )}
          </div>
        )
      })}
    </aside>
  )
}
