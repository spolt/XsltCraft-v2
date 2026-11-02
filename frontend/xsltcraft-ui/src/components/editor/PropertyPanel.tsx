import { useRef, useState } from 'react'
import { ChevronDown, ChevronUp, RefreshCw, Star, Trash2, Upload, X, Bold } from 'lucide-react'
import { useEditorStore } from '../../store/editorStore'
import { uploadAsset, getAssetUrl, deleteAsset } from '../../services/assetService'
import SegmentedTabs from '../ui/SegmentedTabs'
import { BlockTypeIcon, blockLabel } from './paletteIcons'
import type { GridBlock, BlockConfig, PartyType, PartyLabelStyle, ColumnFormat_ILT, InvoiceHeaderField, InvoiceTotalsField } from '../../types/blocks'
import { PARTY_TYPE_LABELS, PARTY_TYPE_ROOT_XPATH, DEFAULT_INVOICE_TOTALS_FIELDS } from '../../types/blocks'
import { GIB_LOGO_BASE64 } from '../../assets/gibLogo'
import MatchInfo from './panel/MatchInfo'
import {
  AddButton,
  CheckToggle,
  Checkbox,
  ColorInput,
  Field,
  FieldRow,
  IconButton,
  InfoNote,
  ItemCard,
  ListBox,
  MonoPath,
  NumberInput,
  PanelSection,
  Select,
  TextInput,
  XPathInput,
} from './panel/fields'

// ── Ortak seçenekler ──────────────────────────────────────────────────────────

const BORDER_STYLES = [
  { value: 'solid', label: 'Düz' }, { value: 'dashed', label: 'Kesik' }, { value: 'dotted', label: 'Noktalı' },
] as const

const ALIGNMENTS = [
  { value: 'left', label: 'Sol' }, { value: 'center', label: 'Orta' }, { value: 'right', label: 'Sağ' },
] as const

const clampOpacity = (v: string) => Math.min(100, Math.max(0, parseInt(v) || 0))

// ── Grid pozisyon / boyut kontrolleri (V2) ────────────────────────────────────

function GridPositionControls({ block }: { block: GridBlock }) {
  const updateBlockGridLayout = useEditorStore((s) => s.updateBlockGridLayout)
  const gl = block.gridLayout

  function numInput(label: string, value: number, onChange: (v: number) => void) {
    return (
      <Field label={label}>
        <NumberInput
          value={Math.round(value * 10) / 10}
          step={5}
          onChange={(raw) => {
            const n = parseFloat(raw)
            if (!isNaN(n)) onChange(n)
          }}
        />
      </Field>
    )
  }

  return (
    <>
      <PanelSection title="Pozisyon ve boyut (mm)">
        <div className="grid grid-cols-2 gap-2.5">
          {numInput('X (sol)', gl.x, (v) => updateBlockGridLayout(block.id, { x: v }))}
          {numInput('Y (üst)', gl.y, (v) => updateBlockGridLayout(block.id, { y: v }))}
          {numInput('Genişlik', gl.width, (v) => updateBlockGridLayout(block.id, { width: v }))}
          {numInput('Yükseklik', gl.height, (v) => updateBlockGridLayout(block.id, { height: v }))}
          {numInput('Z-index', gl.zIndex ?? 0, (v) => updateBlockGridLayout(block.id, { zIndex: Math.max(0, v) }))}
        </div>
        <Checkbox
          label="Otomatik yükseklik"
          checked={gl.autoHeight ?? false}
          onChange={(v) => updateBlockGridLayout(block.id, { autoHeight: v })}
        />
      </PanelSection>

      <PanelSection title="Kenar boşlukları (mm)">
        <div className="grid grid-cols-2 gap-2.5">
          {numInput('Üst', gl.marginTop ?? 0, (v) => updateBlockGridLayout(block.id, { marginTop: Math.max(0, v) }))}
          {numInput('Alt', gl.marginBottom ?? 0, (v) => updateBlockGridLayout(block.id, { marginBottom: Math.max(0, v) }))}
          {numInput('Sol', gl.marginLeft ?? 0, (v) => updateBlockGridLayout(block.id, { marginLeft: Math.max(0, v) }))}
          {numInput('Sağ', gl.marginRight ?? 0, (v) => updateBlockGridLayout(block.id, { marginRight: Math.max(0, v) }))}
        </div>
      </PanelSection>
    </>
  )
}

// ── Block-type-specific sub-panels ────────────────────────────────────────────

type Config<T extends BlockConfig['type']> = Extract<BlockConfig, { type: T }>['config']
type UpdateFn = (patch: Partial<BlockConfig['config']>) => void

function FontSizeField({ config, update, placeholder = '9.5pt' }: { config: { fontSize?: string }; update: UpdateFn; placeholder?: string }) {
  return (
    <Field label="Yazı boyutu">
      <TextInput value={config.fontSize ?? ''} onChange={(v) => update({ fontSize: v })} placeholder={placeholder} />
    </Field>
  )
}

function BorderStyleField({ value, update }: { value?: 'solid' | 'dashed' | 'dotted'; update: UpdateFn }) {
  return (
    <Field label="Kenarlık stili">
      <Select value={value ?? 'solid'} onChange={(v) => update({ borderStyle: v })} options={BORDER_STYLES} />
    </Field>
  )
}

function OpacityField({ value, update }: { value?: number; update: UpdateFn }) {
  return (
    <Field label="Saydamlık (0–100)">
      <NumberInput min={0} max={100} step={5} value={value ?? 100} onChange={(v) => update({ opacity: clampOpacity(v) })} />
    </Field>
  )
}

function TextStyleFields({ config, update }: { config: Config<'Text'> | Config<'Heading'>; update: UpdateFn }) {
  return (
    <PanelSection title="Biçim">
      <FieldRow>
        <Field label="Yazı boyutu">
          <TextInput value={config.fontSize ?? ''} onChange={(v) => update({ fontSize: v })} placeholder="10pt" />
        </Field>
        <Field label="Renk">
          <ColorInput value={config.color ?? '#111111'} onChange={(v) => update({ color: v })} placeholder="#111111" />
        </Field>
      </FieldRow>
      <div className="flex gap-3">
        <Checkbox label="Kalın" checked={config.fontWeight === 'bold'} onChange={(v) => update({ fontWeight: v ? 'bold' : 'normal' })} />
        <Checkbox label="İtalik" checked={config.fontStyle === 'italic'} onChange={(v) => update({ fontStyle: v ? 'italic' : 'normal' })} />
      </div>
    </PanelSection>
  )
}

function TextPanel({ config, update }: { config: Config<'Text'>; update: UpdateFn }) {
  return (
    <>
      <PanelSection title="İçerik">
        <Field label="Metin">
          <TextInput value={config.content ?? ''} onChange={(v) => update({ content: v })} placeholder="Statik metin..." />
        </Field>
        <Checkbox label="Statik metin" checked={config.isStatic} onChange={(v) => update({ isStatic: v })} />
      </PanelSection>
      {!config.isStatic && (
        <PanelSection title="Veri kaynağı (XPath)">
          <XPathInput
            ariaLabel="Veri kaynağı XPath"
            value={config.binding?.xpath ?? ''}
            onChange={(v) => update({ binding: { ...config.binding, xpath: v, fallback: config.binding?.fallback } })}
            placeholder="//cbc:Name"
          />
          <Field label="Yedek metin (eşleşme yoksa)">
            <TextInput
              value={config.binding?.fallback ?? ''}
              onChange={(v) => update({ binding: { ...config.binding, xpath: config.binding?.xpath ?? '', fallback: v } })}
              placeholder="—"
            />
          </Field>
        </PanelSection>
      )}
      <TextStyleFields config={config} update={update} />
    </>
  )
}

function HeadingPanel({ config, update }: { config: Config<'Heading'>; update: UpdateFn }) {
  return (
    <>
      <PanelSection title="İçerik">
        <Field label="Seviye">
          <Select value={config.level} onChange={(v) => update({ level: v })} options={[
            { value: 'H1', label: 'H1' }, { value: 'H2', label: 'H2' },
            { value: 'H3', label: 'H3' }, { value: 'H4', label: 'H4' },
          ]} />
        </Field>
        <Field label="Metin">
          <TextInput value={config.content ?? ''} onChange={(v) => update({ content: v })} placeholder="Başlık metni..." />
        </Field>
        <Checkbox label="Statik metin" checked={config.isStatic} onChange={(v) => update({ isStatic: v })} />
      </PanelSection>
      {!config.isStatic && (
        <PanelSection title="Veri kaynağı (XPath)">
          <XPathInput
            ariaLabel="Veri kaynağı XPath"
            value={config.binding?.xpath ?? ''}
            onChange={(v) => update({ binding: { xpath: v } })}
            placeholder="//cbc:..."
          />
        </PanelSection>
      )}
      <TextStyleFields config={config} update={update} />
    </>
  )
}

function ParagraphPanel({ config, update }: { config: Config<'Paragraph'>; update: UpdateFn }) {
  function updateLine(index: number, patch: object) {
    const lines = config.lines.map((l, i) => (i === index ? { ...l, ...patch } : l))
    update({ lines })
  }

  return (
    <>
      <PanelSection title="Satırlar" meta={config.lines.length}>
        {config.lines.map((line, i) => (
          <ItemCard
            key={i}
            title={`Satır ${i + 1}`}
            actions={
              <IconButton danger label="Satırı sil" onClick={() => update({ lines: config.lines.filter((_, idx) => idx !== i) })}>
                <X size={13} aria-hidden="true" />
              </IconButton>
            }
          >
            <Checkbox label="Statik" checked={line.isStatic} onChange={(v) => updateLine(i, { isStatic: v })} />
            {line.isStatic ? (
              <TextInput ariaLabel={`Satır ${i + 1} metni`} value={line.content ?? ''} onChange={(v) => updateLine(i, { content: v })} placeholder="Metin..." />
            ) : (
              <XPathInput ariaLabel={`Satır ${i + 1} XPath`} value={line.xpath ?? ''} onChange={(v) => updateLine(i, { xpath: v })} placeholder="XPath..." />
            )}
          </ItemCard>
        ))}
        <AddButton onClick={() => update({ lines: [...config.lines, { isStatic: true, content: '' }] })}>+ Satır ekle</AddButton>
      </PanelSection>
      <PanelSection title="Biçim">
        <FontSizeField config={config} update={update} />
      </PanelSection>
    </>
  )
}

function TablePanel({ config, update }: { config: Config<'Table'>; update: UpdateFn }) {
  function updateColumn(index: number, patch: object) {
    const columns = config.columns.map((c, i) => (i === index ? { ...c, ...patch } : c))
    update({ columns })
  }

  return (
    <>
      <PanelSection title="Satır kaynağı (XPath)">
        <XPathInput ariaLabel="Satır kaynağı XPath" match="nodes" value={config.iterateOver} onChange={(v) => update({ iterateOver: v })} placeholder="//cac:InvoiceLine" />
      </PanelSection>
      <PanelSection title="Sütunlar" meta={config.columns.length}>
        {config.columns.map((col, i) => (
          <ItemCard
            key={i}
            title={`Sütun ${i + 1}`}
            actions={
              <IconButton danger label="Sütunu sil" onClick={() => update({ columns: config.columns.filter((_, idx) => idx !== i) })}>
                <X size={13} aria-hidden="true" />
              </IconButton>
            }
          >
            <TextInput ariaLabel={`Sütun ${i + 1} başlığı`} value={col.header} onChange={(v) => updateColumn(i, { header: v })} placeholder="Başlık" />
            <XPathInput
              ariaLabel={`Sütun ${i + 1} XPath`}
              value={col.xpath}
              onChange={(v) => updateColumn(i, { xpath: v })}
              placeholder="XPath (cbc:ID)"
              contextXPath={config.iterateOver}
            />
            <FieldRow>
              <Field label="Genişlik">
                <TextInput value={col.width ?? ''} onChange={(v) => updateColumn(i, { width: v })} placeholder="10%" />
              </Field>
              <Field label="Format">
                <Select
                  value={(col.format ?? 'text') as 'text' | 'currency' | 'number' | 'date'}
                  onChange={(v) => updateColumn(i, { format: v })}
                  options={[
                    { value: 'text', label: 'Metin' },
                    { value: 'currency', label: 'Para' },
                    { value: 'number', label: 'Sayı' },
                    { value: 'date', label: 'Tarih' },
                  ]}
                />
              </Field>
            </FieldRow>
          </ItemCard>
        ))}
        <AddButton onClick={() => update({ columns: [...config.columns, { header: '', xpath: '', width: '' }] })}>+ Sütun ekle</AddButton>
      </PanelSection>
      <PanelSection title="Biçim">
        <Checkbox label="Başlık satırı göster" checked={config.showHeader} onChange={(v) => update({ showHeader: v })} />
        <Field label="Başlık arka planı">
          <ColorInput value={config.headerBackgroundColor ?? '#E0E0E0'} onChange={(v) => update({ headerBackgroundColor: v })} placeholder="#E0E0E0" />
        </Field>
      </PanelSection>
    </>
  )
}

function ImagePanel({ config, update }: { config: Config<'Image'>; update: UpdateFn }) {
  const fileInputRef = useRef<HTMLInputElement>(null)
  const [uploading, setUploading] = useState(false)
  const [uploadError, setUploadError] = useState<string | null>(null)

  async function handleFileChange(e: React.ChangeEvent<HTMLInputElement>) {
    const file = e.target.files?.[0]
    if (!file) return
    setUploading(true)
    setUploadError(null)
    try {
      const result = await uploadAsset(file, config.assetType)
      update({ assetId: result.id })
    } catch (err) {
      // Sunucu nedeni (ör. "Yalnızca PNG, JPG ve JPEG…", "içerik uzantıyla uyuşmuyor") gösterilsin.
      const message = (err as { response?: { data?: { message?: string } } })?.response?.data?.message
      setUploadError(message ?? 'Yükleme başarısız.')
    } finally {
      setUploading(false)
      e.target.value = ''
    }
  }

  async function handleRemoveAsset() {
    if (!config.assetId) return
    try {
      await deleteAsset(config.assetId)
    } catch { /* sunucudan silinmese de yerelden temizle */ }
    update({ assetId: undefined })
  }

  return (
    <>
      <PanelSection title="Görsel">
        <Field label="Varlık tipi">
          <Select value={config.assetType} onChange={(v) => update({ assetType: v })} options={[
            { value: 'logo', label: 'Logo' },
            { value: 'signature', label: 'İmza' },
            { value: 'other', label: 'Diğer' },
          ]} />
        </Field>
        <input ref={fileInputRef} type="file" accept=".png,.jpg,.jpeg,image/png,image/jpeg" className="hidden" onChange={handleFileChange} />
        {config.assetId ? (
          <div className="flex flex-col gap-1.5">
            <img
              src={getAssetUrl(config.assetId)}
              alt="önizleme"
              className="max-h-20 w-full rounded-md border border-ui-line bg-ui-side object-contain"
            />
            <button
              type="button"
              onClick={handleRemoveAsset}
              className="xc-focus flex items-center gap-1 self-start rounded px-1 py-0.5 text-[12px] font-medium text-fn-red-ink hover:underline"
            >
              <Trash2 size={13} aria-hidden="true" /> Görseli kaldır
            </button>
          </div>
        ) : (
          <button
            type="button"
            onClick={() => fileInputRef.current?.click()}
            disabled={uploading}
            className="xc-focus flex w-full items-center justify-center gap-1.5 rounded-md border-2 border-dashed border-ui-line py-2.5 text-[12px] text-tx-muted transition-colors hover:border-ui-soft-line hover:text-ui-ink disabled:opacity-50"
          >
            <Upload size={14} aria-hidden="true" />
            {uploading ? 'Yükleniyor…' : 'PNG / JPG / SVG yükle'}
          </button>
        )}
        {uploadError && <span role="alert" className="text-[12px] text-fn-red-ink">{uploadError}</span>}
      </PanelSection>

      <PanelSection title="Biçim">
        <Field label="Hizalama">
          <Select value={config.alignment} onChange={(v) => update({ alignment: v })} options={ALIGNMENTS} />
        </Field>
        <FieldRow>
          <Field label="Genişlik"><TextInput value={config.width ?? ''} onChange={(v) => update({ width: v })} placeholder="150px" /></Field>
          <Field label="Yükseklik"><TextInput value={config.height ?? ''} onChange={(v) => update({ height: v })} placeholder="80px" /></Field>
        </FieldRow>
        <Field label="Alt metni"><TextInput value={config.altText ?? ''} onChange={(v) => update({ altText: v })} placeholder="Logo" /></Field>
        <OpacityField value={config.opacity} update={update} />
        <Checkbox label="Ücretsiz temada düzenlenebilir" checked={config.editableOnFreeTheme} onChange={(v) => update({ editableOnFreeTheme: v })} />
      </PanelSection>
    </>
  )
}

function DocumentInfoPanel({ config, update }: { config: Config<'DocumentInfo'>; update: UpdateFn }) {
  function updateRow(i: number, patch: object) {
    update({ rows: config.rows.map((r, idx) => (idx === i ? { ...r, ...patch } : r)) })
  }
  return (
    <>
      <PanelSection title="Satırlar" meta={config.rows.length}>
        {config.rows.map((row, i) => (
          <ItemCard
            key={i}
            title={`Satır ${i + 1}`}
            actions={
              <IconButton danger label="Satırı sil" onClick={() => update({ rows: config.rows.filter((_, idx) => idx !== i) })}>
                <X size={13} aria-hidden="true" />
              </IconButton>
            }
          >
            <TextInput ariaLabel={`Satır ${i + 1} etiketi`} value={row.label} onChange={(v) => updateRow(i, { label: v })} placeholder="Etiket" />
            <XPathInput ariaLabel={`Satır ${i + 1} XPath`} value={row.xpath} onChange={(v) => updateRow(i, { xpath: v })} placeholder="XPath" />
          </ItemCard>
        ))}
        <AddButton onClick={() => update({ rows: [...config.rows, { label: '', xpath: '' }] })}>+ Satır ekle</AddButton>
      </PanelSection>
      <PanelSection title="Biçim">
        <Checkbox label="Kenarlıklı" checked={config.bordered ?? true} onChange={(v) => update({ bordered: v })} />
        {(config.bordered ?? true) && <BorderStyleField value={config.borderStyle} update={update} />}
        <FontSizeField config={config} update={update} />
      </PanelSection>
    </>
  )
}

function TotalsPanel({ config, update }: { config: Config<'Totals'>; update: UpdateFn }) {
  function updateRow(i: number, patch: object) {
    update({ rows: config.rows.map((r, idx) => (idx === i ? { ...r, ...patch } : r)) })
  }
  return (
    <>
      <PanelSection title="Satırlar" meta={config.rows.length}>
        {config.rows.map((row, i) => (
          <ItemCard
            key={i}
            title={`Satır ${i + 1}`}
            actions={
              <IconButton danger label="Satırı sil" onClick={() => update({ rows: config.rows.filter((_, idx) => idx !== i) })}>
                <X size={13} aria-hidden="true" />
              </IconButton>
            }
          >
            <TextInput ariaLabel={`Satır ${i + 1} etiketi`} value={row.label} onChange={(v) => updateRow(i, { label: v })} placeholder="Etiket" />
            <XPathInput ariaLabel={`Satır ${i + 1} XPath`} value={row.xpath} onChange={(v) => updateRow(i, { xpath: v })} placeholder="XPath" />
            <Checkbox label="Vurgula" checked={row.highlight ?? false} onChange={(v) => updateRow(i, { highlight: v })} />
          </ItemCard>
        ))}
        <AddButton onClick={() => update({ rows: [...config.rows, { label: '', xpath: '' }] })}>+ Satır ekle</AddButton>
      </PanelSection>
      <PanelSection title="Biçim">
        <FieldRow>
          <FontSizeField config={config} update={update} />
          <Field label="Hizalama">
            <Select value={config.alignment} onChange={(v) => update({ alignment: v })} options={ALIGNMENTS} />
          </Field>
        </FieldRow>
        <Field label="Etiket sütun genişliği">
          <TextInput value={config.labelWidth ?? ''} onChange={(v) => update({ labelWidth: v })} placeholder="180px" />
        </Field>
      </PanelSection>
    </>
  )
}

function NotesPanel({ config, update }: { config: Config<'Notes'>; update: UpdateFn }) {
  const lines: string[] = config.staticLines ?? []
  const position = config.staticPosition ?? 'after'

  function updateLine(index: number, value: string) {
    const next = [...lines]
    next[index] = value
    update({ staticLines: next })
  }

  function removeLine(index: number) {
    update({ staticLines: lines.filter((_, i) => i !== index) })
  }

  function addLine() {
    update({ staticLines: [...lines, ''] })
  }

  return (
    <>
      <PanelSection title="Veri kaynağı (XPath)">
        <XPathInput ariaLabel="Not kaynağı XPath" match="nodes" noun="not" value={config.iterateOver} onChange={(v) => update({ iterateOver: v })} placeholder="//cbc:Note" />
        <Field label="Önek">
          <TextInput value={config.prefix ?? ''} onChange={(v) => update({ prefix: v })} placeholder="Not: " />
        </Field>
      </PanelSection>

      <PanelSection title="Sabit notlar" meta={lines.length}>
        {lines.length === 0 && <p className="m-0 text-[12px] text-tx-subtle">Henüz sabit not yok.</p>}
        {lines.map((line, i) => (
          <div key={i} className="flex items-center gap-1">
            <TextInput ariaLabel={`Sabit not ${i + 1}`} value={line} onChange={(v) => updateLine(i, v)} placeholder={`Sabit not ${i + 1}`} />
            <IconButton danger label="Notu sil" onClick={() => removeLine(i)}>
              <X size={13} aria-hidden="true" />
            </IconButton>
          </div>
        ))}
        <AddButton onClick={addLine}>+ Not ekle</AddButton>
        {lines.length > 0 && (
          <Field label="Sabit notların yeri">
            <Select
              value={position}
              onChange={(v) => update({ staticPosition: v })}
              options={[
                { value: 'before', label: 'XPath notlarından önce' },
                { value: 'after', label: 'XPath notlarından sonra' },
              ]}
            />
          </Field>
        )}
      </PanelSection>

      <PanelSection title="Biçim">
        <Checkbox label="Dış kenarlık" checked={config.bordered ?? false} onChange={(v) => update({ bordered: v })} />
        {config.bordered && (
          <FieldRow>
            <BorderStyleField value={config.borderStyle} update={update} />
            <Field label="Kenarlık rengi">
              <ColorInput value={config.borderColor ?? '#555555'} onChange={(v) => update({ borderColor: v })} />
            </Field>
          </FieldRow>
        )}
        <FontSizeField config={config} update={update} />
      </PanelSection>
    </>
  )
}

function BankInfoPanel({ config, update }: { config: Config<'BankInfo'>; update: UpdateFn }) {
  return (
    <>
      <PanelSection title="Banka bilgisi">
        <Field label="Banka adı">
          <TextInput value={config.bankName} onChange={(v) => update({ bankName: v })} placeholder="ör. YAPIKREDI" />
        </Field>
        <Field label="IBAN">
          <TextInput mono value={config.iban} onChange={(v) => update({ iban: v })} placeholder="ör. TR66 0006 7010 0000 0020 1934 37" />
        </Field>
        <Field label="IBAN etiketi">
          <TextInput value={config.ibanLabel ?? 'IBAN: '} onChange={(v) => update({ ibanLabel: v })} placeholder="IBAN: " />
        </Field>
      </PanelSection>
      <PanelSection title="Biçim">
        <Checkbox label="Dış kenarlık" checked={config.bordered ?? true} onChange={(v) => update({ bordered: v })} />
        {config.bordered && (
          <FieldRow>
            <BorderStyleField value={config.borderStyle} update={update} />
            <Field label="Kenarlık rengi">
              <ColorInput value={config.borderColor ?? '#555555'} onChange={(v) => update({ borderColor: v })} />
            </Field>
          </FieldRow>
        )}
        <FontSizeField config={config} update={update} />
      </PanelSection>
    </>
  )
}

function ETTNPanel({ config, update }: { config: Config<'ETTN'>; update: UpdateFn }) {
  return (
    <>
      <PanelSection title="Veri kaynağı (XPath)">
        <XPathInput ariaLabel="ETTN XPath" value={config.ettnXpath} onChange={(v) => update({ ettnXpath: v })} placeholder="//cbc:UUID" />
      </PanelSection>
      <PanelSection title="Biçim">
        <Checkbox label="ETTN metnini göster" checked={config.showEttn ?? true} onChange={(v) => update({ showEttn: v })} />
        <Checkbox label="QR kodu göster" checked={config.showQR} onChange={(v) => update({ showQR: v })} />
        {config.showQR && (
          <>
            <FieldRow>
              <Field label="QR genişlik (px)">
                <TextInput value={String(config.qrWidth ?? 80)} onChange={(v) => update({ qrWidth: parseInt(v) || 80 })} placeholder="80" />
              </Field>
              <Field label="QR yükseklik (px)">
                <TextInput value={String(config.qrHeight ?? 80)} onChange={(v) => update({ qrHeight: parseInt(v) || 80 })} placeholder="80" />
              </Field>
            </FieldRow>
            <Field label="QR hizalama">
              <Select value={config.qrAlignment ?? 'right'} onChange={(v) => update({ qrAlignment: v })} options={ALIGNMENTS} />
            </Field>
          </>
        )}
      </PanelSection>
    </>
  )
}

function DividerPanel({ config, update }: { config: Config<'Divider'>; update: UpdateFn }) {
  return (
    <PanelSection title="Biçim">
      <FieldRow>
        <Field label="Stil">
          <Select value={config.style} onChange={(v) => update({ style: v })} options={BORDER_STYLES} />
        </Field>
        <Field label="Kalınlık"><TextInput value={config.thickness ?? '1px'} onChange={(v) => update({ thickness: v })} placeholder="1px" /></Field>
      </FieldRow>
      <Field label="Renk"><ColorInput value={config.color ?? '#CCCCCC'} onChange={(v) => update({ color: v })} placeholder="#CCCCCC" /></Field>
      <FieldRow>
        <Field label="Üst boşluk"><TextInput value={config.marginTop ?? '8px'} onChange={(v) => update({ marginTop: v })} placeholder="8px" /></Field>
        <Field label="Alt boşluk"><TextInput value={config.marginBottom ?? '8px'} onChange={(v) => update({ marginBottom: v })} placeholder="8px" /></Field>
      </FieldRow>
    </PanelSection>
  )
}

function SpacerPanel({ config, update }: { config: Config<'Spacer'>; update: UpdateFn }) {
  return (
    <PanelSection title="Biçim">
      <Field label="Yükseklik">
        <TextInput value={config.height} onChange={(v) => update({ height: v })} placeholder="24px" />
      </Field>
    </PanelSection>
  )
}

function ConditionalTextPanel({ config, update }: { config: Config<'ConditionalText'>; update: UpdateFn }) {
  const OPERATORS = [
    { value: 'equals', label: 'Eşit (=)' },
    { value: 'notEquals', label: 'Eşit Değil (≠)' },
    { value: 'contains', label: 'İçeriyor' },
    { value: 'greaterThan', label: 'Büyüktür (>)' },
    { value: 'lessThan', label: 'Küçüktür (<)' },
    { value: 'exists', label: 'Mevcut' },
    { value: 'notExists', label: 'Mevcut Değil' },
  ] as const

  return (
    <>
      <PanelSection title="Koşul">
        <XPathInput
          ariaLabel="Koşul XPath"
          value={config.condition.xpath}
          onChange={(v) => update({ condition: { ...config.condition, xpath: v } })}
          placeholder="//cbc:InvoiceTypeCode"
        />
        <Field label="Operatör">
          <Select
            value={config.condition.operator}
            onChange={(v) => update({ condition: { ...config.condition, operator: v } })}
            options={OPERATORS}
          />
        </Field>
        {!['exists', 'notExists'].includes(config.condition.operator) && (
          <Field label="Değer">
            <TextInput
              value={config.condition.value ?? ''}
              onChange={(v) => update({ condition: { ...config.condition, value: v } })}
              placeholder="TEMELFATURA"
            />
          </Field>
        )}
      </PanelSection>
      <PanelSection title="Koşul doğruysa">
        <Checkbox label="Statik metin" checked={config.thenIsStatic} onChange={(v) => update({ thenIsStatic: v })} />
        {config.thenIsStatic ? (
          <TextInput ariaLabel="Koşul doğruysa metin" value={config.thenContent} onChange={(v) => update({ thenContent: v })} placeholder="Metin..." />
        ) : (
          <XPathInput ariaLabel="Koşul doğruysa XPath" value={config.thenContent} onChange={(v) => update({ thenContent: v })} placeholder="XPath..." />
        )}
      </PanelSection>
      <PanelSection title="Koşul yanlışsa">
        <Checkbox label="Statik metin" checked={config.elseIsStatic} onChange={(v) => update({ elseIsStatic: v })} />
        {config.elseIsStatic ? (
          <TextInput ariaLabel="Koşul yanlışsa metin" value={config.elseContent} onChange={(v) => update({ elseContent: v })} placeholder="Metin..." />
        ) : (
          <XPathInput ariaLabel="Koşul yanlışsa XPath" value={config.elseContent} onChange={(v) => update({ elseContent: v })} placeholder="XPath..." />
        )}
      </PanelSection>
      <PanelSection title="Biçim">
        <FontSizeField config={config} update={update} />
      </PanelSection>
    </>
  )
}

// ── InvoiceHeader — Alan düzenleyici ─────────────────────────────────────────

function InvoiceHeaderPanel({ config, update }: { config: Config<'InvoiceHeader'>; update: UpdateFn }) {
  const customFields = config.fields.filter((f) => f.isCustom)

  function updateCustomField(index: number, patch: Partial<InvoiceHeaderField>) {
    const fields = config.fields.map((f, i) => (i === index ? { ...f, ...patch } : f))
    update({ fields })
  }

  function removeCustomField(index: number) {
    const fields = config.fields.filter((_, i) => i !== index)
    fields.forEach((f, i) => (f.order = i))
    update({ fields })
  }

  function addCustomField() {
    update({
      fields: [
        ...config.fields,
        {
          key: `custom_${Date.now()}`,
          label: 'Yeni Alan',
          xpath: '//',
          visible: true,
          order: config.fields.length,
          isCustom: true,
        },
      ],
    })
  }

  return (
    <>
      <PanelSection title="Özel alanlar" meta={customFields.length}>
        {customFields.map((field) => {
          const absIdx = config.fields.indexOf(field)
          return (
            <ItemCard
              key={field.key}
              title={field.label || 'Etiketsiz'}
              actions={
                <IconButton danger label="Alanı sil" onClick={() => removeCustomField(absIdx)}>
                  <X size={13} aria-hidden="true" />
                </IconButton>
              }
            >
              <TextInput ariaLabel="Alan etiketi" value={field.label} onChange={(v) => updateCustomField(absIdx, { label: v })} placeholder="Etiket" />
              <XPathInput ariaLabel="Alan XPath" value={field.xpath} onChange={(v) => updateCustomField(absIdx, { xpath: v })} placeholder="//cbc:..." />
            </ItemCard>
          )
        })}
        <AddButton onClick={addCustomField}>+ Özel alan ekle</AddButton>
      </PanelSection>
      <PanelSection title="Biçim">
        <Checkbox label="Kenarlıklı" checked={config.bordered} onChange={(v) => update({ bordered: v })} />
        {config.bordered && <BorderStyleField value={config.borderStyle} update={update} />}
        <FontSizeField config={config} update={update} placeholder="11px" />
      </PanelSection>
    </>
  )
}

// ── InvoiceTotals — Dip toplamlar düzenleyici ─────────────────────────────────

function InvoiceTotalsPanel({ config, update }: { config: Config<'InvoiceTotals'>; update: UpdateFn }) {
  const [expandedKey, setExpandedKey] = useState<string | null>(null)

  function updateField(index: number, patch: Partial<InvoiceTotalsField>) {
    const fields = config.fields.map((f, i) => (i === index ? { ...f, ...patch } : f))
    update({ fields })
  }

  function moveField(index: number, direction: -1 | 1) {
    const target = index + direction
    if (target < 0 || target >= config.fields.length) return
    const fields = [...config.fields]
    ;[fields[index], fields[target]] = [fields[target], fields[index]]
    fields.forEach((f, i) => (f.order = i))
    update({ fields })
  }

  function addCustomField() {
    update({
      fields: [
        ...config.fields,
        {
          key: `custom_${Date.now()}`,
          label: 'Yeni Satır',
          xpath: '//',
          visible: true,
          highlight: false,
          bold: false,
          order: config.fields.length,
          isCustom: true,
        },
      ],
    })
  }

  function removeField(index: number) {
    const fields = config.fields.filter((_, i) => i !== index)
    fields.forEach((f, i) => (f.order = i))
    update({ fields })
  }

  const visibleCount = config.fields.filter((f) => f.visible).length
  const isDynamic = (key: string) => key === 'taxSubtotalGroups' || key === 'taxBaseGroups'

  return (
    <>
      <PanelSection title="Satırlar" meta={`${visibleCount} / ${config.fields.length} görünür`}>
        <ListBox>
          {config.fields.map((field, i) => {
            const open = expandedKey === field.key
            return (
              <div key={field.key} className={field.visible ? '' : 'opacity-60'}>
                <div className="flex min-w-0 items-center gap-1.5 px-2 py-1.5">
                  <CheckToggle
                    checked={field.visible}
                    onToggle={() => updateField(i, { visible: !field.visible })}
                    label={`${field.label} satırını ${field.visible ? 'gizle' : 'göster'}`}
                  />
                  <button
                    type="button"
                    aria-expanded={open}
                    onClick={() => setExpandedKey(open ? null : field.key)}
                    className="xc-focus flex min-w-0 flex-1 items-center gap-2 rounded px-1 py-0.5 text-left"
                  >
                    <span className={`min-w-0 flex-1 truncate text-[12px] text-tx ${field.bold ? 'font-semibold' : ''}`}>{field.label || 'Etiketsiz'}</span>
                    {isDynamic(field.key)
                      ? <RefreshCw size={11} aria-label="Orana göre dinamik" className="flex-none text-tx-muted" />
                      : <MonoPath value={field.xpath.replace('//cac:LegalMonetaryTotal/', '')} />}
                  </button>
                  <IconButton label={field.highlight ? 'Vurguyu kaldır' : 'Vurgula (arka plan rengi)'} pressed={field.highlight} onClick={() => updateField(i, { highlight: !field.highlight })}>
                    <Star size={12} aria-hidden="true" />
                  </IconButton>
                  <IconButton label={field.bold ? 'Kalın yazıyı kaldır' : 'Kalın yaz'} pressed={field.bold} onClick={() => updateField(i, { bold: !field.bold })}>
                    <Bold size={12} aria-hidden="true" />
                  </IconButton>
                </div>
                {open && (
                  <div className="flex flex-col gap-2 border-t border-ui-line bg-ui-side px-2.5 py-2.5">
                    <div className="flex items-end gap-1.5">
                      <Field label="Etiket">
                        <TextInput value={field.label} onChange={(v) => updateField(i, { label: v })} placeholder="Etiket" />
                      </Field>
                      <IconButton label="Yukarı taşı" disabled={i === 0} onClick={() => moveField(i, -1)}>
                        <ChevronUp size={13} aria-hidden="true" />
                      </IconButton>
                      <IconButton label="Aşağı taşı" disabled={i === config.fields.length - 1} onClick={() => moveField(i, 1)}>
                        <ChevronDown size={13} aria-hidden="true" />
                      </IconButton>
                      {field.isCustom && (
                        <IconButton danger label="Satırı sil" onClick={() => { removeField(i); setExpandedKey(null) }}>
                          <X size={13} aria-hidden="true" />
                        </IconButton>
                      )}
                    </div>
                    {isDynamic(field.key) ? (
                      <InfoNote>
                        {field.key === 'taxBaseGroups'
                          ? 'TaxSubtotal/TaxableAmount — KDV oranına göre, ÖTV toplam'
                          : 'TaxSubtotal/TaxAmount — KDV oranına göre, ÖTV toplam'}
                      </InfoNote>
                    ) : field.isCustom ? (
                      <XPathInput ariaLabel="Satır XPath" value={field.xpath} onChange={(v) => updateField(i, { xpath: v })} placeholder="//cac:LegalMonetaryTotal/cbc:..." />
                    ) : (
                      <>
                        <div className="truncate rounded-md border border-ui-line bg-ui-card px-2.5 py-1.5 font-mono text-[11px] text-tx-muted" title={field.xpath}>
                          {field.xpath}
                        </div>
                        <MatchInfo xpath={field.xpath} />
                      </>
                    )}
                  </div>
                )}
              </div>
            )
          })}
        </ListBox>
        <div className="flex items-center gap-2">
          <AddButton onClick={addCustomField}>+ Özel satır ekle</AddButton>
          <button
            type="button"
            onClick={() => update({ fields: DEFAULT_INVOICE_TOTALS_FIELDS.map((f) => ({ ...f })) })}
            className="xc-focus ml-auto rounded px-1 py-1 text-[12px] text-tx-muted hover:text-tx hover:underline"
          >
            Sıfırla
          </button>
        </div>
      </PanelSection>

      <PanelSection title="Biçim">
        <Checkbox label="Para birimi göster" checked={config.showCurrency} onChange={(v) => update({ showCurrency: v })} />
        {config.showCurrency && (
          <Field label="Para birimi XPath">
            <XPathInput value={config.currencyXpath} onChange={(v) => update({ currencyXpath: v })} placeholder="//cbc:DocumentCurrencyCode" />
          </Field>
        )}
        <FieldRow>
          <Field label="Etiket genişliği">
            <TextInput value={config.labelWidth ?? ''} onChange={(v) => update({ labelWidth: v })} placeholder="60%" />
          </Field>
          <FontSizeField config={config} update={update} placeholder="9pt" />
        </FieldRow>
      </PanelSection>
    </>
  )
}

function GibLogoPanel({ config, update }: { config: Config<'GibLogo'>; update: UpdateFn }) {
  return (
    <>
      <PanelSection title="Logo">
        {/* Sabit logo önizlemesi */}
        <div className="flex rounded-md border border-ui-line bg-ui-side p-2" style={{ justifyContent: config.alignment }}>
          <img
            src={GIB_LOGO_BASE64}
            alt="GİB Logo"
            style={{ width: config.width ?? '80px', height: config.height ?? '80px', objectFit: 'contain' }}
          />
        </div>
      </PanelSection>
      <PanelSection title="Biçim">
        <FieldRow>
          <Field label="Genişlik">
            <TextInput value={config.width ?? ''} onChange={(v) => update({ width: v })} placeholder="80px" />
          </Field>
          <Field label="Yükseklik">
            <TextInput value={config.height ?? ''} onChange={(v) => update({ height: v })} placeholder="80px" />
          </Field>
        </FieldRow>
        <Field label="Hizalama">
          <Select value={config.alignment} onChange={(v) => update({ alignment: v as 'left' | 'center' | 'right' })} options={ALIGNMENTS} />
        </Field>
        <FieldRow>
          <Field label="Yazı boyutu">
            <TextInput value={config.fontSize ?? '11px'} onChange={(v) => update({ fontSize: v })} placeholder="11px" />
          </Field>
          <OpacityField value={config.opacity} update={update} />
        </FieldRow>
      </PanelSection>
    </>
  )
}

function GibKarekodPanel({ config, update }: { config: Config<'GibKarekod'>; update: UpdateFn }) {
  return (
    <>
      <InfoNote>GİB standart karekodu (e-Fatura veya e-İrsaliye, şablon türüne göre). XPath değerleri sabittir.</InfoNote>
      <PanelSection title="Biçim">
        <FieldRow>
          <Field label="Genişlik (px)">
            <TextInput value={String(config.qrWidth ?? 150)} onChange={(v) => update({ qrWidth: parseInt(v) || 150 })} placeholder="150" />
          </Field>
          <Field label="Yükseklik (px)">
            <TextInput value={String(config.qrHeight ?? 150)} onChange={(v) => update({ qrHeight: parseInt(v) || 150 })} placeholder="150" />
          </Field>
        </FieldRow>
        <FieldRow>
          <Field label="Hizalama">
            <Select value={config.qrAlignment ?? 'right'} onChange={(v) => update({ qrAlignment: v })} options={ALIGNMENTS} />
          </Field>
          <OpacityField value={config.opacity} update={update} />
        </FieldRow>
      </PanelSection>
    </>
  )
}

function TaxSummaryPanel({ config, update }: { config: Config<'TaxSummary'>; update: UpdateFn }) {
  return (
    <>
      <PanelSection title="Satır kaynağı (XPath)">
        <XPathInput
          ariaLabel="KDV alt toplamları XPath"
          match="nodes"
          value={config.taxTotalXpath}
          onChange={(v) => update({ taxTotalXpath: v })}
          placeholder="//cac:TaxTotal/cac:TaxSubtotal"
        />
      </PanelSection>
      <PanelSection title="Kolonlar">
        <Checkbox label="KDV Oranı sütunu" checked={config.showPercent} onChange={(v) => update({ showPercent: v })} />
        {config.showPercent && (
          <Field label="Oran XPath">
            <XPathInput value={config.percentXpath} onChange={(v) => update({ percentXpath: v })} placeholder="cac:TaxCategory/cbc:Percent" contextXPath={config.taxTotalXpath} />
          </Field>
        )}
        <Field label="Matrah XPath">
          <XPathInput value={config.taxableAmountXpath} onChange={(v) => update({ taxableAmountXpath: v })} placeholder="cbc:TaxableAmount" contextXPath={config.taxTotalXpath} />
        </Field>
        <Field label="KDV tutarı XPath">
          <XPathInput value={config.taxAmountXpath} onChange={(v) => update({ taxAmountXpath: v })} placeholder="cbc:TaxAmount" contextXPath={config.taxTotalXpath} />
        </Field>
      </PanelSection>
      <PanelSection title="Biçim">
        <Field label="Başlık arka planı">
          <ColorInput value={config.headerBackgroundColor ?? '#E0E0E0'} onChange={(v) => update({ headerBackgroundColor: v })} placeholder="#E0E0E0" />
        </Field>
      </PanelSection>
    </>
  )
}

function PartyInfoPanel({ config, update }: { config: Config<'PartyInfo'>; update: UpdateFn }) {
  const [expandedKey, setExpandedKey] = useState<string | null>(null)

  const PARTY_TYPES: { value: PartyType; label: string }[] = [
    { value: 'SupplierParty', label: 'Satıcı' },
    { value: 'CustomerParty', label: 'Alıcı' },
    { value: 'DespatchSupplierParty', label: 'Gönderici' },
    { value: 'DeliveryCustomerParty', label: 'Teslim Alıcı' },
    { value: 'BuyerCustomerParty', label: 'Alıcı Müşteri' },
  ]

  const LABEL_STYLES: { value: PartyLabelStyle; label: string }[] = [
    { value: 'table', label: 'Tablo (iki sütun)' },
    { value: 'inline', label: 'Satır içi (etiket: değer)' },
    { value: 'hidden', label: 'Gizli (sadece değer)' },
  ]

  function handlePartyTypeChange(newType: PartyType) {
    const currentAutoTitle = PARTY_TYPE_LABELS[config.partyType]
    const patch: Record<string, unknown> = { partyType: newType }
    if (config.title === currentAutoTitle) {
      patch.title = PARTY_TYPE_LABELS[newType]
    }
    update(patch)
  }

  function updateField(index: number, patch: Record<string, unknown>) {
    const fields = config.fields.map((f, i) => (i === index ? { ...f, ...patch } : f))
    update({ fields })
  }

  function moveField(index: number, direction: -1 | 1) {
    const target = index + direction
    if (target < 0 || target >= config.fields.length) return
    const fields = [...config.fields]
    ;[fields[index], fields[target]] = [fields[target], fields[index]]
    fields.forEach((f, i) => (f.order = i))
    update({ fields })
  }

  function addCustomField() {
    const fields = [
      ...config.fields,
      {
        key: `custom_${Date.now()}`,
        label: 'Yeni Alan',
        relativeXpath: '',
        visible: true,
        order: config.fields.length,
        isCustom: true,
      },
    ]
    update({ fields })
  }

  function removeField(index: number) {
    const fields = config.fields.filter((_, i) => i !== index)
    fields.forEach((f, i) => (f.order = i))
    update({ fields })
  }

  const rootXPath = PARTY_TYPE_ROOT_XPATH[config.partyType]
  const visibleCount = config.fields.filter((f) => f.visible).length

  return (
    <>
      <PanelSection title="Veri kaynağı">
        <Field label="Taraf tipi">
          <Select value={config.partyType} onChange={(v) => handlePartyTypeChange(v as PartyType)} options={PARTY_TYPES} />
        </Field>
        <div className="truncate rounded-md border border-ui-line bg-ui-side px-2.5 py-1.5 font-mono text-[11px] text-tx-muted" title={rootXPath}>
          {rootXPath}
        </div>
        <MatchInfo xpath={rootXPath} mode="nodes" noun="kayıt" />
      </PanelSection>

      <PanelSection title="Alanlar" meta={`${visibleCount} / ${config.fields.length} görünür`}>
        <ListBox>
          {config.fields.map((field, i) => {
            const open = expandedKey === field.key
            return (
              <div key={field.key} className={field.visible ? '' : 'opacity-60'}>
                <div className="flex min-w-0 items-center gap-1.5 px-2 py-1.5">
                  <CheckToggle
                    checked={field.visible}
                    onToggle={() => updateField(i, { visible: !field.visible })}
                    label={`${field.label} alanını ${field.visible ? 'gizle' : 'göster'}`}
                  />
                  <button
                    type="button"
                    aria-expanded={open}
                    onClick={() => setExpandedKey(open ? null : field.key)}
                    className="xc-focus flex min-w-0 flex-1 items-center gap-2 rounded px-1 py-0.5 text-left"
                  >
                    <span className="min-w-0 flex-1 truncate text-[12px] text-tx">{field.label || 'Etiketsiz'}</span>
                    <MonoPath value={field.relativeXpath} />
                  </button>
                </div>
                {open && (
                  <div className="flex flex-col gap-2 border-t border-ui-line bg-ui-side px-2.5 py-2.5">
                    <div className="flex items-end gap-1.5">
                      <Field label="Etiket">
                        <TextInput value={field.label} onChange={(v) => updateField(i, { label: v })} placeholder="Etiket" />
                      </Field>
                      <IconButton label="Yukarı taşı" disabled={i === 0} onClick={() => moveField(i, -1)}>
                        <ChevronUp size={13} aria-hidden="true" />
                      </IconButton>
                      <IconButton label="Aşağı taşı" disabled={i === config.fields.length - 1} onClick={() => moveField(i, 1)}>
                        <ChevronDown size={13} aria-hidden="true" />
                      </IconButton>
                      {field.isCustom && (
                        <IconButton danger label="Alanı sil" onClick={() => { removeField(i); setExpandedKey(null) }}>
                          <X size={13} aria-hidden="true" />
                        </IconButton>
                      )}
                    </div>
                    {field.isCustom ? (
                      <XPathInput
                        ariaLabel="Alan XPath (taraf köküne göre)"
                        value={field.relativeXpath}
                        onChange={(v) => updateField(i, { relativeXpath: v })}
                        placeholder="cac:.../cbc:..."
                        contextXPath={rootXPath}
                      />
                    ) : (
                      <MatchInfo xpath={field.relativeXpath} contextXPath={rootXPath} />
                    )}
                  </div>
                )}
              </div>
            )
          })}
        </ListBox>
        <AddButton onClick={addCustomField}>+ Özel alan ekle</AddButton>
      </PanelSection>

      <PanelSection title="Biçim">
        <Field label="Başlık">
          <TextInput value={config.title} onChange={(v) => update({ title: v })} placeholder="SATICI" />
        </Field>
        <Checkbox label="Başlığı göster" checked={config.showTitle} onChange={(v) => update({ showTitle: v })} />
        <Field label="Etiket stili">
          <Select value={config.labelStyle} onChange={(v) => update({ labelStyle: v })} options={LABEL_STYLES} />
        </Field>
        <Checkbox label="Kenarlıklı" checked={config.bordered} onChange={(v) => update({ bordered: v })} />
        {config.bordered && <BorderStyleField value={config.borderStyle} update={update} />}
        <FontSizeField config={config} update={update} placeholder="9pt" />
      </PanelSection>
    </>
  )
}

// ── InvoiceLineTable — Kolon düzenleyici ─────────────────────────────────────

function InvoiceLineColumnEditor({
  config,
  updateColumn,
  moveColumn,
  addCustomColumn,
  removeColumn,
  formats,
}: {
  config: Config<'InvoiceLineTable'>
  updateColumn: (index: number, patch: Record<string, unknown>) => void
  moveColumn: (index: number, direction: -1 | 1) => void
  addCustomColumn: () => void
  removeColumn: (index: number) => void
  formats: { value: ColumnFormat_ILT; label: string }[]
}) {
  const [expandedIndex, setExpandedIndex] = useState<number | null>(null)
  const visibleCount = config.columns.filter((c) => c.visible).length

  return (
    <PanelSection title="Kolonlar" meta={`${visibleCount} / ${config.columns.length} görünür`}>
      <ListBox>
        {config.columns.map((col, i) => {
          const isExpanded = expandedIndex === i
          return (
            <div key={col.key} className={isExpanded ? 'bg-ui-soft/30' : ''}>
              {/* Satır: onay kutusu + kolon adı + soluk mono XPath */}
              <div className="flex min-w-0 items-center gap-1.5 px-2 py-1.5">
                <CheckToggle
                  checked={col.visible}
                  onToggle={() => updateColumn(i, { visible: !col.visible })}
                  label={`${col.header || 'Başlıksız'} kolonunu ${col.visible ? 'gizle' : 'göster'}`}
                />
                <button
                  type="button"
                  aria-expanded={isExpanded}
                  aria-label={`${col.header || 'Başlıksız'} kolon ayrıntıları`}
                  onClick={() => setExpandedIndex(isExpanded ? null : i)}
                  className="xc-focus flex min-w-0 flex-1 items-center gap-2 rounded px-1 py-0.5 text-left"
                >
                  <span className={`min-w-0 flex-1 truncate text-[12px] ${col.visible ? 'text-tx' : 'text-tx-muted'}`}>
                    {col.header || 'Başlıksız'}
                  </span>
                  <MonoPath value={col.relativeXpath} />
                </button>
              </div>

              {/* Ayrıntılar */}
              {isExpanded && (
                <div className="flex flex-col gap-2.5 border-t border-ui-line bg-ui-side px-2.5 py-2.5">
                  <div className="flex items-end gap-1.5">
                    <Field label="Başlık">
                      <TextInput value={col.header} onChange={(v) => updateColumn(i, { header: v })} placeholder="Başlık" />
                    </Field>
                    <IconButton label="Yukarı taşı" disabled={i === 0} onClick={() => moveColumn(i, -1)}>
                      <ChevronUp size={13} aria-hidden="true" />
                    </IconButton>
                    <IconButton label="Aşağı taşı" disabled={i === config.columns.length - 1} onClick={() => moveColumn(i, 1)}>
                      <ChevronDown size={13} aria-hidden="true" />
                    </IconButton>
                  </div>
                  <FieldRow>
                    <Field label="Genişlik">
                      <TextInput value={col.width ?? ''} onChange={(v) => updateColumn(i, { width: v })} placeholder="10%" />
                    </Field>
                    <Field label="Format">
                      <Select value={col.format ?? 'text'} onChange={(v) => updateColumn(i, { format: v })} options={formats} />
                    </Field>
                  </FieldRow>
                  <Field label="XPath (satıra göre)">
                    {col.isCustom ? (
                      <XPathInput
                        value={col.relativeXpath}
                        onChange={(v) => updateColumn(i, { relativeXpath: v })}
                        placeholder="cac:.../cbc:..."
                        contextXPath={config.iterateOver}
                      />
                    ) : (
                      <div className="flex flex-col gap-1.5">
                        <div className="truncate rounded-md border border-ui-line bg-ui-card px-2.5 py-1.5 font-mono text-[11px] text-tx-muted" title={col.relativeXpath}>
                          {col.relativeXpath}
                        </div>
                        <MatchInfo xpath={col.relativeXpath} contextXPath={config.iterateOver} />
                      </div>
                    )}
                  </Field>
                  {col.isCustom && (
                    <button
                      type="button"
                      onClick={() => { removeColumn(i); setExpandedIndex(null) }}
                      className="xc-focus flex items-center gap-1 self-end rounded px-1 py-0.5 text-[12px] font-medium text-fn-red-ink hover:underline"
                    >
                      <Trash2 size={13} aria-hidden="true" />
                      Kolonu sil
                    </button>
                  )}
                </div>
              )}
            </div>
          )
        })}
      </ListBox>
      <AddButton onClick={addCustomColumn}>+ Kolon ekle</AddButton>
    </PanelSection>
  )
}

function InvoiceLineTablePanel({ config, update }: { config: Config<'InvoiceLineTable'>; update: UpdateFn }) {
  const FORMATS: { value: ColumnFormat_ILT; label: string }[] = [
    { value: 'text', label: 'Metin' },
    { value: 'currency', label: 'Para' },
    { value: 'number', label: 'Sayı' },
    { value: 'percent', label: 'Yüzde (×100)' },
    { value: 'percentDirect', label: 'Yüzde (Doğrudan)' },
    { value: 'quantityWithUnit', label: 'Miktar + Birim' },
    { value: 'unitCode', label: 'Birim Kodu' },
  ]

  function updateColumn(index: number, patch: Record<string, unknown>) {
    const columns = config.columns.map((c, i) => (i === index ? { ...c, ...patch } : c))
    update({ columns })
  }

  function moveColumn(index: number, direction: -1 | 1) {
    const target = index + direction
    if (target < 0 || target >= config.columns.length) return
    const columns = [...config.columns]
    ;[columns[index], columns[target]] = [columns[target], columns[index]]
    columns.forEach((c, i) => (c.order = i))
    update({ columns })
  }

  function addCustomColumn() {
    const columns = [
      ...config.columns,
      {
        key: `custom_${Date.now()}`,
        header: 'Yeni Kolon',
        relativeXpath: '',
        width: '10%',
        format: 'text' as ColumnFormat_ILT,
        visible: true,
        order: config.columns.length,
        isCustom: true,
      },
    ]
    update({ columns })
  }

  function removeColumn(index: number) {
    const columns = config.columns.filter((_, i) => i !== index)
    columns.forEach((c, i) => (c.order = i))
    update({ columns })
  }

  return (
    <>
      <PanelSection title="Satır kaynağı (XPath)">
        <XPathInput
          ariaLabel="Satır kaynağı XPath"
          match="nodes"
          value={config.iterateOver}
          onChange={(v) => update({ iterateOver: v })}
          placeholder="//cac:InvoiceLine"
        />
      </PanelSection>

      <InvoiceLineColumnEditor config={config} updateColumn={updateColumn} moveColumn={moveColumn} addCustomColumn={addCustomColumn} removeColumn={removeColumn} formats={FORMATS} />

      <PanelSection title="Biçim">
        <Field label="Başlık">
          <TextInput value={config.title} onChange={(v) => update({ title: v })} placeholder="MAL HİZMET TABLOSU" />
        </Field>
        <div className="grid grid-cols-2 gap-x-3 gap-y-2">
          <Checkbox label="Başlığı göster" checked={config.showTitle} onChange={(v) => update({ showTitle: v })} />
          <Checkbox label="Başlık satırı" checked={config.showHeader} onChange={(v) => update({ showHeader: v })} />
          <Checkbox label="Sıra No göster" checked={config.showRowNumber} onChange={(v) => update({ showRowNumber: v })} />
          <Checkbox label="Para birimi" checked={config.showCurrency ?? true} onChange={(v) => update({ showCurrency: v })} />
          <Checkbox label="Kenarlıklı" checked={config.bordered} onChange={(v) => update({ bordered: v })} />
        </div>
        {config.bordered && <BorderStyleField value={config.borderStyle} update={update} />}
        <Field label="Başlık arka planı">
          <ColorInput value={config.headerBackgroundColor ?? '#E0E0E0'} onChange={(v) => update({ headerBackgroundColor: v })} placeholder="#E0E0E0" />
        </Field>
        <Field label="Alternatif satır rengi">
          <ColorInput value={config.alternateRowColor ?? '#F9F9F9'} onChange={(v) => update({ alternateRowColor: v })} placeholder="#F9F9F9" />
        </Field>
        <FontSizeField config={config} update={update} />
      </PanelSection>
    </>
  )
}

// ── ShipmentInfo — e-İrsaliye taşıyıcı / araç / şoför bilgileri ─────────────

function ShipmentInfoPanel({ config, update }: { config: Config<'ShipmentInfo'>; update: UpdateFn }) {
  return (
    <>
      <InfoNote>Taşıyıcı firma, araç/dorse plakası, şoför ve teslim eden bilgileri XML'de varsa otomatik gösterilir.</InfoNote>
      <PanelSection title="Başlık">
        <TextInput ariaLabel="Başlık" value={config.title} onChange={(v) => update({ title: v })} placeholder="TAŞIYICI BİLGİLERİ" />
        <Checkbox label="Başlığı göster" checked={config.showTitle} onChange={(v) => update({ showTitle: v })} />
      </PanelSection>
      <InvoiceHeaderPanel config={config} update={update} />
    </>
  )
}

// ── Properties content ────────────────────────────────────────────────────────

type PropTab = 'data' | 'position'

const PROP_TABS = [
  { id: 'data', label: 'Veri' },
  { id: 'position', label: 'Konum' },
] as const

/** Seçili bloğun tipine özel veri panelleri (mantık değişmez). */
function BlockDataPanel({ block }: { block: GridBlock }) {
  const updateBlockConfig = useEditorStore((s) => s.updateBlockConfig)
  const update: UpdateFn = (patch) => updateBlockConfig(block.id, patch)
  const cfg = block.config as never

  return (
    <>
      {block.type === 'Text' && <TextPanel config={cfg} update={update} />}
      {block.type === 'Heading' && <HeadingPanel config={cfg} update={update} />}
      {block.type === 'Paragraph' && <ParagraphPanel config={cfg} update={update} />}
      {block.type === 'Table' && <TablePanel config={cfg} update={update} />}
      {block.type === 'Image' && <ImagePanel config={cfg} update={update} />}
      {block.type === 'DocumentInfo' && <DocumentInfoPanel config={cfg} update={update} />}
      {block.type === 'Totals' && <TotalsPanel config={cfg} update={update} />}
      {block.type === 'Notes' && <NotesPanel config={cfg} update={update} />}
      {block.type === 'BankInfo' && <BankInfoPanel config={cfg} update={update} />}
      {block.type === 'ETTN' && <ETTNPanel config={cfg} update={update} />}
      {block.type === 'Divider' && <DividerPanel config={cfg} update={update} />}
      {block.type === 'Spacer' && <SpacerPanel config={cfg} update={update} />}
      {block.type === 'ConditionalText' && <ConditionalTextPanel config={cfg} update={update} />}
      {block.type === 'TaxSummary' && <TaxSummaryPanel config={cfg} update={update} />}
      {block.type === 'GibKarekod' && <GibKarekodPanel config={cfg} update={update} />}
      {block.type === 'GibLogo' && <GibLogoPanel config={cfg} update={update} />}
      {block.type === 'PartyInfo' && <PartyInfoPanel config={cfg} update={update} />}
      {block.type === 'InvoiceLineTable' && <InvoiceLineTablePanel config={cfg} update={update} />}
      {block.type === 'InvoiceHeader' && <InvoiceHeaderPanel config={cfg} update={update} />}
      {block.type === 'InvoiceTotals' && <InvoiceTotalsPanel config={cfg} update={update} />}
      {block.type === 'ShipmentInfo' && <ShipmentInfoPanel config={cfg} update={update} />}
    </>
  )
}

// ── Root PropertyPanel ────────────────────────────────────────────────────────

/**
 * Tasarım Alanı sağ paneli: "Seçili blok" başlığı + Veri | Konum sekmeleri.
 * XML ağacı artık sol panelde (EditorLeftPanel); XPath düğmesi oraya yönlendirir.
 */
export default function PropertyPanel() {
  const selectedBlockId = useEditorStore((s) => s.selectedBlockId)
  const blocks = useEditorStore((s) => s.blocks)
  const documentType = useEditorStore((s) => s.documentType)
  const [tab, setTab] = useState<PropTab>('data')

  const block = selectedBlockId ? blocks[selectedBlockId] : undefined

  return (
    <aside
      aria-label="Blok özellikleri"
      className="flex w-[304px] flex-none flex-col overflow-hidden border-l border-ui-line bg-ui-card"
    >
      {!block ? (
        <div className="flex flex-1 items-center justify-center px-6">
          <p className="text-center text-[12px] leading-relaxed text-tx-subtle">
            Düzenlemek için sayfadaki bir bloğu seç.
          </p>
        </div>
      ) : (
        <>
          <div className="flex flex-none flex-col gap-1 px-4 pt-3.5">
            <span className="text-[11px] font-semibold uppercase tracking-[0.07em] text-tx-muted">Seçili blok</span>
            <div className="flex min-w-0 items-center gap-2">
              <BlockTypeIcon type={block.type} size={16} strokeWidth={1.8} aria-hidden="true" className="flex-none text-ui-accent" />
              <h2 className="m-0 min-w-0 truncate text-[15px] font-semibold text-tx">
                {blockLabel(block.type, documentType)}
              </h2>
              <span className="ml-auto flex-none font-mono text-[11px] text-tx-subtle">{block.type}</span>
            </div>
            <SegmentedTabs<PropTab>
              items={PROP_TABS}
              value={tab}
              onChange={setTab}
              ariaLabel="Blok özellikleri"
              variant="underline"
              idPrefix="design-props"
              className="mt-1.5"
            />
          </div>
          <div
            role="tabpanel"
            id={`design-props-panel-${tab}`}
            aria-labelledby={`design-props-tab-${tab}`}
            className="flex min-h-0 flex-1 flex-col gap-[18px] overflow-y-auto p-4"
          >
            {tab === 'data' ? <BlockDataPanel key={block.id} block={block} /> : <GridPositionControls block={block} />}
          </div>
        </>
      )}
    </aside>
  )
}
