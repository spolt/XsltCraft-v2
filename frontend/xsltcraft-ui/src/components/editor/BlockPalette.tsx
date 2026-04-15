import { useState, useMemo } from 'react'
import { useDraggable } from '@dnd-kit/core'
import type { BlockType } from '../../types/blocks'

interface BlockPaletteProps {
  isOpen: boolean
}

interface PaletteItem {
  id: string
  type: BlockType
  label: string
  icon: string
  configOverride?: Record<string, unknown>
}

const CATEGORIES: { name: string; items: PaletteItem[] }[] = [
  {
    name: 'HAZIR BLOKLAR',
    items: [
      {
        id: 'SupplierInfo', type: 'PartyInfo', label: 'Satıcı Bilgileri', icon: '⊞',
        configOverride: { partyType: 'SupplierParty', title: 'SATICI' },
      },
      {
        id: 'BuyerInfo', type: 'PartyInfo', label: 'Alıcı Bilgileri', icon: '⊞',
        configOverride: { partyType: 'CustomerParty', title: 'ALICI' },
      },
      { id: 'InvoiceHeader',    type: 'InvoiceHeader',    label: 'Fatura Başlığı',       icon: '☰' },
      { id: 'InvoiceLineTable', type: 'InvoiceLineTable', label: 'Fatura Satırları',      icon: '⊟' },
      { id: 'InvoiceTotals',    type: 'InvoiceTotals',    label: 'Fatura Dip Toplamları', icon: '₸' },
      { id: 'Notes',            type: 'Notes',            label: 'Notlar',                icon: '✎' },
      { id: 'BankInfo',         type: 'BankInfo',         label: 'Banka Bilgisi',         icon: '₺' },
    ],
  },
  {
    name: 'METİN',
    items: [
      { id: 'Text',            type: 'Text',            label: 'Metin',         icon: 'T' },
      { id: 'Heading',         type: 'Heading',         label: 'Başlık',        icon: 'H' },
      { id: 'Paragraph',       type: 'Paragraph',       label: 'Paragraf',      icon: '¶' },
      { id: 'ConditionalText', type: 'ConditionalText', label: 'Koşullu Metin', icon: '?' },
    ],
  },
  {
    name: 'VERİ',
    items: [
      { id: 'Table',        type: 'Table',        label: 'Tablo',         icon: '⊞' },
      { id: 'DocumentInfo', type: 'DocumentInfo', label: 'Belge Bilgisi', icon: '⊙' },
    ],
  },
  {
    name: 'MEDYA',
    items: [
      { id: 'Image',      type: 'Image',      label: 'Görsel',          icon: '⬜' },
      { id: 'ETTN',       type: 'ETTN',       label: 'Dinamik Karekod', icon: '◎' },
      { id: 'GibKarekod', type: 'GibKarekod', label: 'GİB Karekod',    icon: '◎' },
      { id: 'GibLogo',    type: 'GibLogo',    label: 'GİB Logo',        icon: '⊕' },
    ],
  },
  {
    name: 'DÜZEN',
    items: [
      { id: 'ForEach',    type: 'ForEach',    label: 'For-Each',    icon: '↺' },
      { id: 'Conditional',type: 'Conditional',label: 'Koşul Bloğu', icon: '∨' },
      { id: 'Variable',   type: 'Variable',   label: 'Değişken',    icon: '$' },
      { id: 'Divider',    type: 'Divider',    label: 'Ayırıcı',     icon: '—' },
      { id: 'Spacer',     type: 'Spacer',     label: 'Boşluk',      icon: '↕' },
    ],
  },
]

function DraggablePaletteItem({ item }: { item: PaletteItem }) {
  const { attributes, listeners, setNodeRef, isDragging } = useDraggable({
    id: `palette-${item.id}`,
    data: { source: 'palette', blockType: item.type, configOverride: item.configOverride },
  })

  return (
    <div
      ref={setNodeRef}
      {...listeners}
      {...attributes}
      className="select-none"
      style={{
        display: 'flex',
        alignItems: 'center',
        gap: 8,
        padding: '6px 8px',
        borderRadius: 6,
        fontSize: 12,
        color: '#2C2C2A',
        cursor: 'grab',
        transition: 'background 120ms',
        opacity: isDragging ? 0.5 : 1,
      }}
      onMouseEnter={e => {
        e.currentTarget.style.background = '#EBF3FC'
      }}
      onMouseLeave={e => {
        e.currentTarget.style.background = 'transparent'
      }}
    >
      <span
        style={{
          width: 24,
          height: 24,
          borderRadius: 4,
          background: '#EBF3FC',
          display: 'flex',
          alignItems: 'center',
          justifyContent: 'center',
          fontSize: 11,
          fontWeight: 700,
          color: '#185FA5',
          flexShrink: 0,
        }}
      >
        {item.icon}
      </span>
      <span>{item.label}</span>
    </div>
  )
}

export default function BlockPalette({ isOpen }: BlockPaletteProps) {
  const [search, setSearch] = useState('')

  const filtered = useMemo(() => {
    if (!search.trim()) return CATEGORIES
    const q = search.toLowerCase()
    return CATEGORIES.map((cat) => ({
      ...cat,
      items: cat.items.filter((i) => i.label.toLowerCase().includes(q) || i.type.toLowerCase().includes(q)),
    })).filter((cat) => cat.items.length > 0)
  }, [search])

  if (!isOpen) return null

  return (
    <aside
      style={{
        flexShrink: 0,
        width: 180,
        background: '#fff',
        borderRight: '1px solid #E0DDD8',
        display: 'flex',
        flexDirection: 'column',
        overflow: 'hidden',
      }}
    >
      {/* Search */}
      <div style={{ padding: '12px 8px 8px', flexShrink: 0 }}>
        <input
          type="text"
          placeholder="⌕ Ara..."
          value={search}
          onChange={(e) => setSearch(e.target.value)}
          style={{
            width: '100%',
            height: 28,
            fontSize: 11,
            padding: '0 8px',
            boxSizing: 'border-box',
            background: '#F9F8F6',
            border: '1px solid #E0DDD8',
            borderRadius: 5,
            outline: 'none',
            color: '#2C2C2A',
            fontFamily: 'inherit',
          }}
          onFocus={e => (e.currentTarget.style.borderColor = '#185FA5')}
          onBlur={e => (e.currentTarget.style.borderColor = '#E0DDD8')}
        />
      </div>

      {/* Block list */}
      <div style={{ overflowY: 'auto', flex: 1, paddingBottom: 8 }}>
        {filtered.map((cat, catIndex) => (
          <div key={cat.name}>
            <p style={{
              fontSize: 10,
              letterSpacing: '1px',
              color: '#888780',
              padding: catIndex === 0 ? '4px 12px 4px' : '12px 12px 4px',
              fontWeight: 600,
            }}>
              {cat.name}
            </p>
            <div style={{ padding: '0 4px' }}>
              {cat.items.map((item) => (
                <DraggablePaletteItem key={item.id} item={item} />
              ))}
            </div>
          </div>
        ))}
      </div>
    </aside>
  )
}
