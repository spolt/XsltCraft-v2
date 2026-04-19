import { useRef, useState } from 'react'
import { useEditorStore } from '../../store/editorStore'
import ResizeHandles from './ResizeHandles'
import { snapToGrid, clampToPage, pxToMm } from '../../utils/gridSnap'
import type { GridBlock } from '../../types/blocks'
import type { GridBlockLayout } from '../../types/template'

const BLOCK_LABEL_TR: Record<string, string> = {
  Text:             'Metin',
  Heading:          'Başlık',
  Paragraph:        'Paragraf',
  Table:            'Tablo',
  Image:            'Görsel',
  DocumentInfo:     'Belge Bilgisi',
  Totals:           'Toplamlar',
  Notes:            'Notlar',
  BankInfo:         'Banka Bilgisi',
  ETTN:             'Dinamik Karekod',
  Divider:          'Ayırıcı',
  Spacer:           'Boşluk',
  ConditionalText:  'Koşullu Metin',
  TaxSummary:       'KDV Özeti',
  GibKarekod:       'GİB Karekod',
  PartyInfo:        'Taraf Bilgisi',
  InvoiceLineTable: 'Fatura Satırları',
  InvoiceHeader:    'Fatura Başlığı',
  InvoiceTotals:    'Fatura Dip Toplamları',
  GibLogo:          'GİB Logo',
}

// Her blok tipi icin kisa ozet metni
function blockSummary(block: GridBlock): string {
  const c = block.config as unknown as Record<string, unknown>
  switch (block.type) {
    case 'Text':
    case 'Heading':
    case 'ConditionalText':
      return typeof c.content === 'string' && c.content ? c.content.slice(0, 40) : (block.type === 'Heading' ? (c.level as string ?? 'H2') : '')
    case 'Paragraph': {
      const lines = c.lines as Array<{ content?: string }> | undefined
      return lines?.[0]?.content?.slice(0, 40) ?? ''
    }
    case 'Table':
    case 'InvoiceLineTable': {
      const cols = c.columns as unknown[]
      return `${cols?.length ?? 0} sütun`
    }
    case 'PartyInfo': {
      const labels: Record<string, string> = {
        SupplierParty: 'Satıcı Bilgileri', CustomerParty: 'Alıcı Bilgileri',
        DespatchSupplierParty: 'Gönderici', DeliveryCustomerParty: 'Teslim Alıcı',
        BuyerCustomerParty: 'Alıcı Müşteri',
      }
      return labels[c.partyType as string] ?? ''
    }
    case 'Image': return c.assetType as string ?? 'logo'
    case 'InvoiceHeader': return (c.title as string) ?? 'Fatura Başlığı'
    case 'InvoiceTotals': return 'Dip Toplamlar'
    case 'TaxSummary': return 'KDV Özeti'
    case 'DocumentInfo': {
      const rows = c.rows as unknown[]
      return `${rows?.length ?? 0} satır`
    }
    case 'Notes': return (c.prefix as string) ?? 'Not'
    case 'BankInfo': return (c.bankName as string) || 'IBAN'
    case 'ETTN': return 'E-TTN / QR'
    case 'GibKarekod': return 'GİB Karekod'
    case 'GibLogo': return 'GİB Logo'
    case 'Divider': return '─────'
    case 'Spacer': return `Boşluk: ${(c.height as string) ?? ''}`
    default: return ''
  }
}

// Blok tipi icin renk
function blockColor(type: string): string {
  const colors: Record<string, string> = {
    PartyInfo: '#1565C0', InvoiceHeader: '#185FA5', InvoiceLineTable: '#2E7D32',
    InvoiceTotals: '#185FA5', TaxSummary: '#E65100', Notes: '#5D4037',
    BankInfo: '#37474F', Table: '#2E7D32', Image: '#6A1B9A',
    ETTN: '#7B1FA2', GibKarekod: '#7B1FA2', GibLogo: '#E8A838',
    ForEach: '#F57F17', Conditional: '#EF6C00', Divider: '#9E9E9E',
    Spacer: '#BDBDBD', Variable: '#757575',
  }
  return colors[type] ?? '#185FA5'
}

interface GridBlockItemProps {
  block: GridBlock
  scale: number
}

export default function GridBlockItem({ block, scale }: GridBlockItemProps) {
  const selectedBlockId = useEditorStore((s) => s.selectedBlockId)
  const selectBlock     = useEditorStore((s) => s.selectBlock)
  const moveBlock       = useEditorStore((s) => s.moveBlock)
  const updateBlockGridLayout = useEditorStore((s) => s.updateBlockGridLayout)
  const setDragGuide    = useEditorStore((s) => s.setDragGuide)

  const isSelected = selectedBlockId === block.id
  const gl = block.gridLayout

  // Surukleme sirasinda gorsel pozisyon ofseti (commit edilmeden once)
  const [dragOffset, setDragOffset] = useState<{ dx: number; dy: number } | null>(null)

  const dragStartRef = useRef<{ clientX: number; clientY: number } | null>(null)

  const color = blockColor(block.type)
  const summary = blockSummary(block)

  const visualX = gl.x + (dragOffset ? pxToMm(dragOffset.dx, scale) : 0)
  const visualY = gl.y + (dragOffset ? pxToMm(dragOffset.dy, scale) : 0)

  function handlePointerDown(e: React.PointerEvent<HTMLDivElement>) {
    // Resize handle'lardan gelen event'leri yoksay
    if ((e.target as HTMLElement).dataset.resizeHandle) return
    e.stopPropagation()
    e.currentTarget.setPointerCapture(e.pointerId)
    dragStartRef.current = { clientX: e.clientX, clientY: e.clientY }
    selectBlock(block.id)
  }

  function handlePointerMove(e: React.PointerEvent<HTMLDivElement>) {
    if (!dragStartRef.current) return
    const dx = e.clientX - dragStartRef.current.clientX
    const dy = e.clientY - dragStartRef.current.clientY
    // En az 3px hareket sonrasi drag baslat (click ile karistirmasin)
    if (Math.abs(dx) > 3 || Math.abs(dy) > 3) {
      setDragOffset({ dx, dy })
      const guideX = snapToGrid(gl.x + pxToMm(dx, scale))
      const guideY = snapToGrid(gl.y + pxToMm(dy, scale))
      setDragGuide({ x: Math.max(0, guideX), y: Math.max(0, guideY) })
    }
  }

  function handlePointerUp(e: React.PointerEvent<HTMLDivElement>) {
    e.currentTarget.releasePointerCapture(e.pointerId)

    if (dragOffset) {
      const dxMm = pxToMm(dragOffset.dx, scale)
      const dyMm = pxToMm(dragOffset.dy, scale)
      const newX = snapToGrid(gl.x + dxMm)
      const newY = snapToGrid(gl.y + dyMm)
      const clamped = clampToPage(newX, newY, gl.width, gl.height)
      moveBlock(block.id, { x: clamped.x, y: clamped.y })
    }

    dragStartRef.current = null
    setDragOffset(null)
    setDragGuide(null)
  }

  function handleResize(patch: Partial<GridBlockLayout>) {
    updateBlockGridLayout(block.id, patch)
  }

  const isDragging = dragOffset !== null

  return (
    <div
      onPointerDown={handlePointerDown}
      onPointerMove={handlePointerMove}
      onPointerUp={handlePointerUp}
      style={{
        position: 'absolute',
        left: visualX * scale,
        top: visualY * scale,
        width: gl.width * scale,
        height: gl.autoHeight ? undefined : gl.height * scale,
        minHeight: gl.height * scale,
        zIndex: Math.max(0, gl.zIndex ?? 0) + (isSelected ? 1000 : 0),
        border: isSelected ? `2px solid #185FA5` : `1.5px solid #B5D4F4`,
        borderRadius: 4,
        background: isSelected ? 'rgba(214,233,248,0.75)' : 'rgba(235,243,252,0.65)',
        boxShadow: isSelected ? '0 0 0 3px rgba(24,95,165,0.12)' : undefined,
        cursor: isDragging ? 'grabbing' : 'grab',
        display: 'flex',
        alignItems: 'flex-start',
        padding: '4px 6px',
        overflow: 'hidden',
        userSelect: 'none',
        touchAction: 'none',
        transition: isDragging ? undefined : 'box-shadow 120ms',
        willChange: isDragging ? 'left, top' : undefined,
      }}
    >
      {/* Blok ikonu */}
      <div
        style={{
          width: 18,
          height: 18,
          borderRadius: 3,
          background: color,
          display: 'flex',
          alignItems: 'center',
          justifyContent: 'center',
          fontSize: 9,
          fontWeight: 700,
          color: '#fff',
          flexShrink: 0,
          marginRight: 5,
        }}
      >
        {block.type.charAt(0)}
      </div>

      {/* Icerik */}
      <div style={{ overflow: 'hidden', minWidth: 0 }}>
        <div style={{ fontSize: 10, fontWeight: 600, color: '#2C2C2A', whiteSpace: 'nowrap', overflow: 'hidden', textOverflow: 'ellipsis' }}>
          {BLOCK_LABEL_TR[block.type] ?? block.type}
        </div>
        {summary && (
          <div style={{ fontSize: 9, color: '#5F5E5A', whiteSpace: 'nowrap', overflow: 'hidden', textOverflow: 'ellipsis', marginTop: 1 }}>
            {summary}
          </div>
        )}
      </div>

      {/* Silme butonu (seciliyken gozukur) — blok icinde sag ust kose */}
      {isSelected && (
        <button
          onPointerDown={(e) => e.stopPropagation()}
          onClick={(e) => {
            e.stopPropagation()
            useEditorStore.getState().removeBlock(block.id)
          }}
          style={{
            position: 'absolute',
            top: 3,
            right: 3,
            width: 13,
            height: 13,
            borderRadius: '50%',
            background: 'rgba(180,40,40,0.75)',
            color: '#fff',
            border: 'none',
            fontSize: 8,
            fontWeight: 700,
            lineHeight: 1,
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'center',
            cursor: 'pointer',
            zIndex: 1001,
            flexShrink: 0,
          }}
          title="Bloğu sil (Delete)"
        >
          ✕
        </button>
      )}

      {/* AutoHeight badge */}
      {gl.autoHeight && (
        <span style={{ position: 'absolute', bottom: 2, right: 4, fontSize: 8, color: '#388E3C', opacity: 0.8 }}>
          ↕ oto
        </span>
      )}

      {/* Resize handles - sadece seciliyken */}
      {isSelected && !isDragging && (
        <ResizeHandles layout={gl} scale={scale} onResize={handleResize} />
      )}
    </div>
  )
}
