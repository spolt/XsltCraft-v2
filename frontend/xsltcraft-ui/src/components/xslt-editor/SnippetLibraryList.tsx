import { useMemo } from 'react'
import { Search, Library } from 'lucide-react'
import type { UserSnippet } from '../../services/snippetService'

interface Props {
  snippets: UserSnippet[]
  query: string
  onQueryChange: (query: string) => void
  onInsertSnippet?: (snippet: UserSnippet) => void
  onManageSnippets: () => void
  /** Dış dolguyu kaldır (kapsayıcı kendi dolgusunu veriyorsa, ör. akordeon gövdesi). */
  flush?: boolean
}

export default function SnippetLibraryList({
  snippets,
  query,
  onQueryChange,
  onInsertSnippet,
  onManageSnippets,
  flush = false,
}: Props) {
  const filteredSnippets = useMemo(() => {
    const q = query.trim().toLocaleLowerCase('tr')
    if (!q) return snippets
    return snippets.filter(s =>
      s.prefix.toLocaleLowerCase('tr').includes(q) || (s.description ?? '').toLocaleLowerCase('tr').includes(q),
    )
  }, [snippets, query])

  return (
    <div className={`flex flex-col gap-2 ${flush ? '' : 'p-2.5'}`}>
      <div className="flex items-center gap-2">
        <label className="flex h-8 min-w-0 flex-1 items-center gap-2 rounded-md border border-field-line bg-ui-side px-2.5 text-tx-subtle focus-within:border-ui-accent">
          <Search size={14} aria-hidden="true" className="flex-none" />
          <span className="sr-only">Snippet ara</span>
          <input
            type="search"
            value={query}
            onChange={e => onQueryChange(e.target.value)}
            placeholder="Snippet ara…"
            className="min-w-0 flex-1 bg-transparent text-[12.5px] text-tx outline-none placeholder:text-tx-subtle"
          />
        </label>
        <button
          type="button"
          onClick={onManageSnippets}
          className="xc-focus flex h-8 flex-none items-center gap-1 rounded-md border border-field-line bg-ui-card px-2 text-[12px] text-tx-2 hover:bg-ui-hover"
          title="Snippet Kütüphanesini yönet"
        >
          <Library size={13} aria-hidden="true" />
          Yönet
        </button>
      </div>
      {filteredSnippets.length === 0 ? (
        <p className="px-1 py-3 text-[12px] text-tx-subtle">
          {snippets.length === 0 ? 'Henüz snippet yok. "Yönet" ile ekleyebilirsin.' : 'Eşleşen snippet yok.'}
        </p>
      ) : (
        filteredSnippets.map(s => (
          <button
            key={s.id}
            type="button"
            disabled={!onInsertSnippet}
            onClick={() => onInsertSnippet?.(s)}
            title={onInsertSnippet ? 'İmlecin bulunduğu satıra ekle' : 'Snippet eklemek için önce XSLT ve XML yükleyin'}
            className="xc-focus flex flex-col gap-0.5 rounded-md border border-ui-line bg-ui-card px-2.5 py-2 text-left transition-colors enabled:hover:border-ui-soft-line enabled:hover:bg-ui-soft disabled:opacity-60"
          >
            <span className="font-mono text-[12.5px] font-semibold text-tx">{s.prefix}</span>
            {s.description && <span className="text-[11.5px] text-tx-subtle">{s.description}</span>}
          </button>
        ))
      )}
    </div>
  )
}
