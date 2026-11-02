import { useEffect, useId, useMemo, useRef, useState, type KeyboardEvent } from 'react'
import { useNavigate } from 'react-router-dom'
import { Search } from 'lucide-react'
import IconTile from '../ui/IconTile'
import { loadRecentWork, useRecentWork, type WorkItem } from '../../hooks/useRecentWork'
import { SOURCE_COLOR, SOURCE_ICON } from '../../utils/workRoutes'

const RECENT_COUNT = 5
const RESULT_COUNT = 8
const isMac = typeof navigator !== 'undefined' && /Mac|iPhone|iPad/.test(navigator.platform)

/**
 * Başlıktaki "Şablonlarına hızlı git…" kutusu (role=combobox + listbox).
 * Boşken son açılan 5 çalışma, yazınca istemci tarafı ad filtresi; ↑/↓/Enter/Esc; Ctrl/⌘K odaklar.
 */
export default function QuickSearch() {
  const navigate = useNavigate()
  const { items, loading, error } = useRecentWork({ lazy: true })
  const [query, setQuery] = useState('')
  const [open, setOpen] = useState(false)
  const [active, setActive] = useState(0)
  const inputRef = useRef<HTMLInputElement>(null)
  const listId = useId()
  const optionId = (i: number) => `${listId}-opt-${i}`

  const q = query.trim().toLocaleLowerCase('tr-TR')
  const results = useMemo<WorkItem[]>(
    () =>
      q
        ? items.filter((i) => i.name.toLocaleLowerCase('tr-TR').includes(q)).slice(0, RESULT_COUNT)
        : items.slice(0, RECENT_COUNT),
    [items, q],
  )
  const activeIdx = Math.min(active, Math.max(0, results.length - 1))

  // Ctrl/⌘K → arama kutusuna odaklan (tarayıcının adres çubuğu kısayolunu ezer).
  useEffect(() => {
    const onKey = (e: globalThis.KeyboardEvent) => {
      if ((e.ctrlKey || e.metaKey) && !e.altKey && !e.shiftKey && e.key.toLowerCase() === 'k') {
        e.preventDefault()
        inputRef.current?.focus()
        inputRef.current?.select()
      }
    }
    window.addEventListener('keydown', onKey)
    return () => window.removeEventListener('keydown', onKey)
  }, [])

  function openItem(item: WorkItem) {
    setOpen(false)
    setQuery('')
    inputRef.current?.blur()
    navigate(item.to)
  }

  function onKeyDown(e: KeyboardEvent<HTMLInputElement>) {
    if (e.key === 'ArrowDown' || e.key === 'ArrowUp') {
      e.preventDefault()
      if (!open) setOpen(true)
      if (results.length === 0) return
      const d = e.key === 'ArrowDown' ? 1 : -1
      setActive((activeIdx + d + results.length) % results.length)
    } else if (e.key === 'Enter') {
      const item = results[activeIdx]
      if (open && item) {
        e.preventDefault()
        openItem(item)
      }
    } else if (e.key === 'Escape') {
      e.preventDefault()
      setOpen(false)
      inputRef.current?.blur()
    }
  }

  const expanded = open
  const status = loading && items.length === 0
    ? 'Yükleniyor…'
    : error
      ? 'Çalışmalar yüklenemedi.'
      : results.length === 0
        ? q ? 'Eşleşen şablon yok.' : 'Henüz bir çalışma yok.'
        : null

  return (
    <div className="relative w-full max-w-[560px]">
      <label className="flex h-10 items-center gap-2.5 rounded-lg bg-ui-search px-3.5 text-ui-bar-muted focus-within:shadow-[0_0_0_2px_var(--ui-accent)]">
        <Search size={16} strokeWidth={1.8} aria-hidden="true" />
        <span className="sr-only">Şablonlarına hızlı git</span>
        <input
          ref={inputRef}
          type="text"
          role="combobox"
          aria-expanded={expanded}
          aria-controls={listId}
          aria-autocomplete="list"
          aria-activedescendant={expanded && results.length > 0 ? optionId(activeIdx) : undefined}
          placeholder="Şablonlarına hızlı git…"
          autoComplete="off"
          spellCheck={false}
          value={query}
          onChange={(e) => {
            setQuery(e.target.value)
            setActive(0)
            setOpen(true)
          }}
          onFocus={() => {
            setOpen(true)
            setActive(0)
            void loadRecentWork()
          }}
          onBlur={() => setOpen(false)}
          onKeyDown={onKeyDown}
          className="min-w-0 flex-1 border-0 bg-transparent text-[14px] text-ui-bar-fg outline-none placeholder:text-ui-bar-muted"
        />
        <kbd aria-hidden="true" className="rounded border border-ui-btn-line px-1.5 py-px font-sans text-[11px]">
          {isMac ? '⌘K' : 'Ctrl K'}
        </kbd>
      </label>

      <div
        className={`absolute top-[46px] right-0 left-0 z-50 flex-col rounded-[10px] border border-field-line bg-ui-card p-1.5 text-tx shadow-[var(--ui-shadow-pop)] ${expanded ? 'flex' : 'hidden'}`}
      >
        <span className="px-2.5 py-1.5 text-[11px] font-semibold tracking-[.07em] text-tx-muted uppercase">
          {q ? 'Sonuçlar' : 'Son açılanlar'}
        </span>
        <ul id={listId} role="listbox" aria-label="Hızlı sonuçlar" className="m-0 flex list-none flex-col p-0">
          {results.map((item, i) => {
            const on = i === activeIdx
            return (
              <li
                key={item.key}
                id={optionId(i)}
                role="option"
                aria-selected={on}
                onMouseDown={(e) => e.preventDefault()}
                onMouseEnter={() => setActive(i)}
                onClick={() => openItem(item)}
                className={`group flex h-[38px] cursor-pointer items-center gap-2.5 rounded-md px-2.5 transition-colors ${on ? 'bg-ui-hover' : ''}`}
              >
                <IconTile icon={SOURCE_ICON[item.source]} color={SOURCE_COLOR[item.source]} size="sm" filled={on} />
                <span className={`min-w-0 flex-1 truncate font-medium ${item.kind === 'xslt' ? 'font-mono text-[12.5px]' : ''}`}>
                  {item.name}
                </span>
                <span className="flex-none text-[12px] text-tx-muted">{item.sourceLabel}</span>
              </li>
            )
          })}
        </ul>
        {status && (
          <p role="status" className="m-0 px-2.5 py-2 text-[12.5px] text-tx-muted">
            {status}
          </p>
        )}
      </div>
    </div>
  )
}
