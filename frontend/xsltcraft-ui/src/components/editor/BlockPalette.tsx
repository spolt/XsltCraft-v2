import { useState, useMemo } from 'react'
import { useDraggable } from '@dnd-kit/core'
import type { BlockType } from '../../types/blocks'

interface PaletteItem {
  type: BlockType
  label: string
  icon: string
}

const CATEGORIES: { name: string; items: PaletteItem[] }[] = [
  {
    name: 'HAZIR BLOKLAR',
    items: [
      { type: 'PartyInfo', label: 'Taraf Bilgisi (Satıcı-Alıcı)', icon: '⊞' },
      { type: 'InvoiceHeader', label: 'Fatura Başlığı', icon: '☰' },
      { type: 'InvoiceLineTable', label: 'Fatura Satırları', icon: '⊟' },
      { type: 'InvoiceTotals', label: 'Fatura Dip Toplamları', icon: '₸' },
      { type: 'Notes', label: 'Notlar', icon: '✎' },
      { type: 'BankInfo', label: 'Banka Bilgisi', icon: '₺' },
    ],
  },
  {
    name: 'METİN',
    items: [
      { type: 'Text', label: 'Metin', icon: 'T' },
      { type: 'Heading', label: 'Başlık', icon: 'H' },
      { type: 'Paragraph', label: 'Paragraf', icon: '¶' },
      { type: 'ConditionalText', label: 'Koşullu Metin', icon: '?' },
    ],
  },
  {
    name: 'VERİ',
    items: [
      { type: 'Table', label: 'Tablo', icon: '⊞' },
      { type: 'TaxSummary', label: 'KDV Özeti', icon: 'Σ' },
      { type: 'DocumentInfo', label: 'Belge Bilgisi', icon: '⊙' },
      { type: 'Totals', label: 'Toplamlar', icon: 'Σ' },
    ],
  },
  {
    name: 'MEDYA',
    items: [
      { type: 'Image', label: 'Görsel', icon: '⬜' },
      { type: 'ETTN', label: 'Dinamik Karekod', icon: '◎' },
      { type: 'GibKarekod', label: 'GİB Karekod', icon: '◎' },
      { type: 'GibLogo', label: 'GİB LOGO', icon: '⊕' },
    ],
  },
  {
    name: 'DÜZEN',
    items: [
      { type: 'ForEach', label: 'For-Each', icon: '↺' },
      { type: 'Conditional', label: 'Koşul Bloğu', icon: '∨' },
      { type: 'Variable', label: 'Değişken', icon: '$' },
      { type: 'Divider', label: 'Ayırıcı', icon: '—' },
      { type: 'Spacer', label: 'Boşluk', icon: '↕' },
    ],
  },
]

function DraggablePaletteItem({ item }: { item: PaletteItem }) {
  const { attributes, listeners, setNodeRef, isDragging } = useDraggable({
    id: `palette-${item.type}`,
    data: { source: 'palette', blockType: item.type },
  })

  return (
    <div
      ref={setNodeRef}
      {...listeners}
      {...attributes}
      className="flex items-center cursor-grab select-none"
      style={{
        gap: 6,
        padding: '5px 12px',
        fontSize: 11,
        color: 'var(--color-text-secondary)',
        borderRadius: 0,
        opacity: isDragging ? 0.5 : 1,
        transition: 'background 120ms',
      }}
      onMouseEnter={e => {
        e.currentTarget.style.background = 'var(--color-surface-secondary)'
        e.currentTarget.style.color = 'var(--color-text-primary)'
      }}
      onMouseLeave={e => {
        e.currentTarget.style.background = 'transparent'
        e.currentTarget.style.color = 'var(--color-text-secondary)'
      }}
    >
      <span
        className="flex items-center justify-center flex-shrink-0"
        style={{
          width: 20,
          height: 20,
          fontSize: 10,
          border: '0.5px solid var(--color-border-default)',
          borderRadius: 4,
          background: 'var(--color-surface-card)',
          color: 'var(--color-text-secondary)',
        }}
      >
        {item.icon}
      </span>
      <span>{item.label}</span>
    </div>
  )
}

export default function BlockPalette() {
  const [search, setSearch] = useState('')

  const filtered = useMemo(() => {
    if (!search.trim()) return CATEGORIES
    const q = search.toLowerCase()
    return CATEGORIES.map((cat) => ({
      ...cat,
      items: cat.items.filter((i) => i.label.toLowerCase().includes(q) || i.type.toLowerCase().includes(q)),
    })).filter((cat) => cat.items.length > 0)
  }, [search])

  return (
    <aside
      className="flex-shrink-0 flex flex-col overflow-hidden"
      style={{
        width: 168,
        background: 'var(--color-surface-card)',
        borderRight: '0.5px solid var(--color-border-default)',
      }}
    >
      {/* Header */}
      <div style={{ padding: '10px 12px 6px', borderBottom: '0.5px solid var(--color-border-default)', flexShrink: 0 }}>
        <p style={{ fontSize: 9, fontWeight: 600, letterSpacing: '0.08em', color: 'var(--color-text-muted)', marginBottom: 6 }}>
          BLOKLAR
        </p>
        <input
          type="text"
          placeholder="⌕ Ara..."
          value={search}
          onChange={(e) => setSearch(e.target.value)}
          style={{
            width: '100%',
            height: 26,
            fontSize: 11,
            padding: '0 8px',
            boxSizing: 'border-box',
            background: 'var(--color-surface-secondary)',
            border: '0.5px solid var(--color-border-default)',
            borderRadius: 5,
            outline: 'none',
            color: 'var(--color-text-primary)',
            fontFamily: 'inherit',
          }}
          onFocus={e => (e.currentTarget.style.borderColor = 'var(--color-brand-primary)')}
          onBlur={e => (e.currentTarget.style.borderColor = 'var(--color-border-default)')}
        />
      </div>

      {/* Block list */}
      <div className="overflow-y-auto flex-1" style={{ paddingBottom: 8 }}>
        {filtered.map((cat) => (
          <div key={cat.name}>
            <p style={{ fontSize: 9, fontWeight: 600, letterSpacing: '0.08em', color: 'var(--color-text-muted)', padding: '10px 12px 3px' }}>
              {cat.name}
            </p>
            {cat.items.map((item) => (
              <DraggablePaletteItem key={item.type} item={item} />
            ))}
          </div>
        ))}
      </div>
    </aside>
  )
}
