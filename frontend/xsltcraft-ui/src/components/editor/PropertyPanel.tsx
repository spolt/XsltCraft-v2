import { useEffect, useRef, useState } from 'react'
import { useEditorStore } from '../../store/editorStore'
import { useXmlStore } from '../../store/xmlStore'
import { uploadAsset, getAssetUrl, deleteAsset } from '../../services/assetService'
import { evaluateXPathValue } from '../../utils/xmlUtils'
import XmlTreeExplorer from './XmlTreeExplorer'
import type { Block, BlockConfig, BlockAlignment, BlockWidth, PartyType, PartyLabelStyle, ColumnFormat_ILT, InvoiceHeaderField, InvoiceTotalsField } from '../../types/blocks'
import { PARTY_TYPE_LABELS, DEFAULT_INVOICE_TOTALS_FIELDS } from '../../types/blocks'
import { GIB_LOGO_BASE64 } from '../../assets/gibLogo'

// ── Generic field helpers ─────────────────────────────────────────────────────

function Field({ label, children }: { label: string; children: React.ReactNode }) {
  return (
    <div className="flex flex-col" style={{ gap: 3 }}>
      <label style={{ fontSize: 10, fontWeight: 500, color: 'var(--color-text-secondary)' }}>{label}</label>
      {children}
    </div>
  )
}

function TextInput({
  value,
  onChange,
  placeholder,
}: {
  value: string
  onChange: (v: string) => void
  placeholder?: string
}) {
  return (
    <input
      type="text"
      value={value}
      onChange={(e) => onChange(e.target.value)}
      placeholder={placeholder}
      style={{
        width: '100%',
        height: 28,
        fontSize: 11,
        padding: '0 8px',
        boxSizing: 'border-box',
        border: '0.5px solid var(--color-border-subtle)',
        borderRadius: 5,
        background: 'var(--color-surface-secondary)',
        color: 'var(--color-text-primary)',
        outline: 'none',
        fontFamily: 'inherit',
      }}
      onFocus={e => { e.currentTarget.style.borderColor = 'var(--color-brand-primary)'; e.currentTarget.style.background = 'var(--color-surface-card)' }}
      onBlur={e => { e.currentTarget.style.borderColor = 'var(--color-border-subtle)'; e.currentTarget.style.background = 'var(--color-surface-secondary)' }}
    />
  )
}

/** XPath input with "pick from tree" button + resolved value display */
function XPathInput({
  value,
  onChange,
  placeholder,
}: {
  value: string
  onChange: (v: string) => void
  placeholder?: string
}) {
  const xmlFiles = useXmlStore((s) => s.xmlFiles)
  const activeXmlId = useXmlStore((s) => s.activeXmlId)
  const isSelecting = useXmlStore((s) => s.isSelecting)
  const startXPathSelection = useXmlStore((s) => s.startXPathSelection)
  const activeXml = xmlFiles.find((f) => f.id === activeXmlId)

  const [resolved, setResolved] = useState('')

  useEffect(() => {
    if (!activeXml || !value.trim()) {
      setResolved('')
      return
    }
    const t = setTimeout(() => {
      setResolved(evaluateXPathValue(activeXml.content, value))
    }, 300)
    return () => clearTimeout(t)
  }, [activeXml?.content, value]) // eslint-disable-line react-hooks/exhaustive-deps

  return (
    <div className="flex flex-col" style={{ gap: 2 }}>
      <div className="flex">
        <input
          type="text"
          value={value}
          onChange={(e) => onChange(e.target.value)}
          placeholder={placeholder}
          style={{
            flex: 1,
            minWidth: 0,
            height: 28,
            fontSize: 11,
            fontFamily: 'monospace',
            padding: '0 8px',
            border: '0.5px solid var(--color-xpath-border)',
            borderRight: activeXml ? 'none' : '0.5px solid var(--color-xpath-border)',
            borderRadius: activeXml ? '5px 0 0 5px' : 5,
            background: 'var(--color-xpath-bg)',
            color: 'var(--color-xpath-text)',
            outline: 'none',
            boxSizing: 'border-box',
          }}
          onFocus={e => (e.currentTarget.style.borderColor = 'var(--color-brand-primary)')}
          onBlur={e => (e.currentTarget.style.borderColor = 'var(--color-xpath-border)')}
        />
        {activeXml && (
          <button
            type="button"
            title="XML Ağacından seç"
            onClick={() => startXPathSelection(onChange)}
            style={{
              padding: '0 7px',
              border: '0.5px solid var(--color-xpath-border)',
              borderLeft: 'none',
              borderRadius: '0 5px 5px 0',
              background: isSelecting ? 'var(--color-brand-light)' : 'var(--color-xpath-bg)',
              color: isSelecting ? 'var(--color-brand-primary)' : 'var(--color-xpath-text)',
              fontSize: 11,
              cursor: 'pointer',
            }}
          >
            ⊕
          </button>
        )}
      </div>
      {resolved && (
        <span
          style={{ fontSize: 11, color: 'var(--color-xpath-text)', padding: '0 2px', overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }}
          title={resolved}
        >
          ↳ {resolved}
        </span>
      )}
    </div>
  )
}

function Select<T extends string>({
  value,
  onChange,
  options,
}: {
  value: T
  onChange: (v: T) => void
  options: { value: T; label: string }[]
}) {
  return (
    <select
      value={value}
      onChange={(e) => onChange(e.target.value as T)}
      style={{
        width: '100%',
        height: 28,
        fontSize: 11,
        padding: '0 8px',
        boxSizing: 'border-box',
        border: '0.5px solid var(--color-border-subtle)',
        borderRadius: 5,
        background: 'var(--color-surface-secondary)',
        color: 'var(--color-text-primary)',
        outline: 'none',
        fontFamily: 'inherit',
        cursor: 'pointer',
      }}
    >
      {options.map((o) => (
        <option key={o.value} value={o.value}>
          {o.label}
        </option>
      ))}
    </select>
  )
}

function ColorInput({
  value,
  onChange,
  placeholder,
}: {
  value: string
  onChange: (v: string) => void
  placeholder?: string
}) {
  // Normalize: color picker requires a valid 6-digit hex; fall back to #000000
  const pickerValue = /^#[0-9a-fA-F]{6}$/.test(value) ? value : '#000000'

  return (
    <div className="flex items-center" style={{ gap: 6 }}>
      <label className="relative flex-shrink-0 cursor-pointer">
        <input
          type="color"
          value={pickerValue}
          onChange={(e) => onChange(e.target.value)}
          className="absolute inset-0 opacity-0 w-full h-full cursor-pointer"
        />
        <span
          style={{ display: 'block', width: 24, height: 24, borderRadius: 4, border: '0.5px solid var(--color-border-subtle)' }}
          className="flex-shrink-0"
        />
        <span
          style={{ display: 'block', width: 24, height: 24, borderRadius: 4, background: pickerValue, position: 'absolute', inset: 0 }}
        />
      </label>
      <input
        type="text"
        value={value}
        onChange={(e) => onChange(e.target.value)}
        placeholder={placeholder ?? '#000000'}
        style={{
          flex: 1,
          height: 28,
          fontSize: 11,
          fontFamily: 'monospace',
          padding: '0 8px',
          border: '0.5px solid var(--color-border-subtle)',
          borderRadius: 5,
          background: 'var(--color-surface-secondary)',
          color: 'var(--color-text-primary)',
          outline: 'none',
          boxSizing: 'border-box',
        }}
      />
    </div>
  )
}

function Checkbox({
  label,
  checked,
  onChange,
}: {
  label: string
  checked: boolean
  onChange: (v: boolean) => void
}) {
  return (
    <label className="flex items-center cursor-pointer" style={{ gap: 6 }}>
      <span
        style={{
          display: 'inline-flex',
          alignItems: 'center',
          justifyContent: 'center',
          width: 14,
          height: 14,
          borderRadius: 2,
          border: checked ? '1.5px solid var(--color-brand-primary)' : '1.5px solid #A0A09A',
          background: checked ? 'var(--color-brand-primary)' : 'var(--color-surface-card)',
          flexShrink: 0,
          fontSize: 9,
          color: '#fff',
          fontWeight: 700,
          cursor: 'pointer',
        }}
      >
        {checked ? '✓' : ''}
      </span>
      <input
        type="checkbox"
        checked={checked}
        onChange={(e) => onChange(e.target.checked)}
        style={{ position: 'absolute', opacity: 0, width: 0, height: 0 }}
      />
      <span style={{ fontSize: 11, color: 'var(--color-text-secondary)' }}>{label}</span>
    </label>
  )
}

// ── Layout controls ───────────────────────────────────────────────────────────

function LayoutControls({ block }: { block: Block }) {
  const updateBlockLayout = useEditorStore((s) => s.updateBlockLayout)

  const widths: { value: BlockWidth; label: string }[] = [
    { value: 'full', label: 'Tam' },
    { value: '1/2', label: '1/2' },
    { value: '1/3', label: '1/3' },
    { value: '2/3', label: '2/3' },
  ]

  const alignments: { value: BlockAlignment; label: string }[] = [
    { value: 'left', label: '◀' },
    { value: 'center', label: '▐▌' },
    { value: 'right', label: '▶' },
  ]

  const currentWidth = block.layout?.width ?? 'full'
  const currentAlignment = block.layout?.alignment ?? 'left'

  function segBtnStyle(active: boolean) {
    return {
      flex: 1,
      height: 28,
      fontSize: 10,
      fontFamily: 'inherit',
      border: active ? '0.5px solid var(--color-brand-border)' : '0.5px solid var(--color-border-subtle)',
      borderRadius: 5,
      background: active ? '#EBF3FC' : 'transparent',
      color: active ? 'var(--color-brand-primary)' : 'var(--color-text-secondary)',
      fontWeight: active ? 500 : 400,
      cursor: 'pointer',
      transition: 'all 120ms',
    } as React.CSSProperties
  }

  return (
    <div className="flex flex-col" style={{ gap: 6, paddingBottom: 8, borderBottom: '0.5px solid var(--color-border-default)' }}>
      <div>
        <p style={{ fontSize: 9, fontWeight: 600, letterSpacing: '0.08em', color: 'var(--color-text-muted)', marginBottom: 4 }}>GENİŞLİK</p>
        <div className="flex" style={{ gap: 3 }}>
          {widths.map(({ value, label }) => (
            <button
              key={value}
              onClick={() => updateBlockLayout(block.id, { width: value })}
              style={segBtnStyle(currentWidth === value)}
            >
              {label}
            </button>
          ))}
        </div>
      </div>
      <div>
        <p style={{ fontSize: 9, fontWeight: 600, letterSpacing: '0.08em', color: 'var(--color-text-muted)', marginBottom: 4 }}>HİZALAMA</p>
        <div className="flex" style={{ gap: 3 }}>
          {alignments.map(({ value, label }) => (
            <button
              key={value}
              onClick={() => updateBlockLayout(block.id, { alignment: value })}
              style={segBtnStyle(currentAlignment === value)}
            >
              {label}
            </button>
          ))}
        </div>
      </div>
    </div>
  )
}

// ── Block-type-specific sub-panels ────────────────────────────────────────────

type Config<T extends Block['type']> = Extract<BlockConfig, { type: T }>['config']
type UpdateFn = (patch: Partial<BlockConfig['config']>) => void

function TextStyleFields({ config, update }: { config: Config<'Text'> | Config<'Heading'>; update: UpdateFn }) {
  return (
    <>
      <div className="flex gap-2">
        <Field label="Yazı boyutu">
          <TextInput value={config.fontSize ?? ''} onChange={(v) => update({ fontSize: v })} placeholder="10pt" />
        </Field>
        <Field label="Renk">
          <ColorInput value={config.color ?? '#111111'} onChange={(v) => update({ color: v })} placeholder="#111111" />
        </Field>
      </div>
      <div className="flex gap-3">
        <Checkbox label="Kalın" checked={config.fontWeight === 'bold'} onChange={(v) => update({ fontWeight: v ? 'bold' : 'normal' })} />
        <Checkbox label="İtalik" checked={config.fontStyle === 'italic'} onChange={(v) => update({ fontStyle: v ? 'italic' : 'normal' })} />
      </div>
    </>
  )
}

function TextPanel({ config, update }: { config: Config<'Text'>; update: UpdateFn }) {
  return (
    <>
      <Field label="İçerik">
        <TextInput value={config.content ?? ''} onChange={(v) => update({ content: v })} placeholder="Statik metin..." />
      </Field>
      <Checkbox label="Statik metin" checked={config.isStatic} onChange={(v) => update({ isStatic: v })} />
      {!config.isStatic && (
        <>
          <Field label="XPath">
            <XPathInput
              value={config.binding?.xpath ?? ''}
              onChange={(v) => update({ binding: { ...config.binding, xpath: v, fallback: config.binding?.fallback } })}
              placeholder="//cbc:Name"
            />
          </Field>
          <Field label="Fallback">
            <TextInput
              value={config.binding?.fallback ?? ''}
              onChange={(v) => update({ binding: { ...config.binding, xpath: config.binding?.xpath ?? '', fallback: v } })}
              placeholder="—"
            />
          </Field>
        </>
      )}
      <TextStyleFields config={config} update={update} />
    </>
  )
}

function HeadingPanel({ config, update }: { config: Config<'Heading'>; update: UpdateFn }) {
  return (
    <>
      <Field label="Seviye">
        <Select value={config.level} onChange={(v) => update({ level: v })} options={[
          { value: 'H1', label: 'H1' }, { value: 'H2', label: 'H2' },
          { value: 'H3', label: 'H3' }, { value: 'H4', label: 'H4' },
        ]} />
      </Field>
      <Field label="İçerik">
        <TextInput value={config.content ?? ''} onChange={(v) => update({ content: v })} placeholder="Başlık metni..." />
      </Field>
      <Checkbox label="Statik metin" checked={config.isStatic} onChange={(v) => update({ isStatic: v })} />
      {!config.isStatic && (
        <Field label="XPath">
          <XPathInput
            value={config.binding?.xpath ?? ''}
            onChange={(v) => update({ binding: { xpath: v } })}
            placeholder="//cbc:..."
          />
        </Field>
      )}
      <TextStyleFields config={config} update={update} />
    </>
  )
}

function FontSizeField({ config, update }: { config: { fontSize?: string }; update: UpdateFn }) {
  return (
    <Field label="Yazı boyutu">
      <TextInput value={config.fontSize ?? ''} onChange={(v) => update({ fontSize: v })} placeholder="9.5pt" />
    </Field>
  )
}

function ParagraphPanel({ config, update }: { config: Config<'Paragraph'>; update: UpdateFn }) {
  function updateLine(index: number, patch: object) {
    const lines = config.lines.map((l, i) => (i === index ? { ...l, ...patch } : l))
    update({ lines })
  }

  return (
    <div className="flex flex-col gap-2">
      {config.lines.map((line, i) => (
        <div key={i} className="border border-gray-100 rounded-md p-2 flex flex-col gap-1 bg-gray-50">
          <div className="flex items-center justify-between">
            <span className="text-xs text-gray-400">Satır {i + 1}</span>
            <button
              onClick={() => update({ lines: config.lines.filter((_, idx) => idx !== i) })}
              className="text-xs text-red-400 hover:text-red-600"
            >
              ✕
            </button>
          </div>
          <Checkbox label="Statik" checked={line.isStatic} onChange={(v) => updateLine(i, { isStatic: v })} />
          {line.isStatic ? (
            <TextInput value={line.content ?? ''} onChange={(v) => updateLine(i, { content: v })} placeholder="Metin..." />
          ) : (
            <XPathInput
              value={line.xpath ?? ''}
              onChange={(v) => updateLine(i, { xpath: v })}
              placeholder="XPath..."
            />
          )}
        </div>
      ))}
      <button
        onClick={() => update({ lines: [...config.lines, { isStatic: true, content: '' }] })}
        className="text-xs text-blue-600 hover:text-blue-800 text-left"
      >
        + Satır ekle
      </button>
      <FontSizeField config={config} update={update} />
    </div>
  )
}

function TablePanel({ config, update }: { config: Config<'Table'>; update: UpdateFn }) {
  function updateColumn(index: number, patch: object) {
    const columns = config.columns.map((c, i) => (i === index ? { ...c, ...patch } : c))
    update({ columns })
  }

  return (
    <>
      <Field label="Her satır için (XPath)">
        <XPathInput
          value={config.iterateOver}
          onChange={(v) => update({ iterateOver: v })}
          placeholder="//cac:InvoiceLine"
        />
      </Field>
      <Checkbox label="Başlık satırı göster" checked={config.showHeader} onChange={(v) => update({ showHeader: v })} />
      <Field label="Başlık arkaplanı">
        <ColorInput value={config.headerBackgroundColor ?? '#E0E0E0'} onChange={(v) => update({ headerBackgroundColor: v })} placeholder="#E0E0E0" />
      </Field>
      <Field label="Sütunlar">
        <div className="flex flex-col gap-2 mt-1">
          {config.columns.map((col, i) => (
            <div key={i} className="border border-gray-100 rounded p-2 bg-gray-50 flex flex-col gap-1">
              <div className="flex justify-between items-center">
                <span className="text-xs text-gray-400">Sütun {i + 1}</span>
                <button
                  onClick={() => update({ columns: config.columns.filter((_, idx) => idx !== i) })}
                  className="text-xs text-red-400 hover:text-red-600"
                >
                  ✕
                </button>
              </div>
              <TextInput value={col.header} onChange={(v) => updateColumn(i, { header: v })} placeholder="Başlık" />
              <XPathInput
                value={col.xpath}
                onChange={(v) => updateColumn(i, { xpath: v })}
                placeholder="XPath (cbc:ID)"
              />
              <div className="flex gap-1.5">
                <div className="flex-1">
                  <TextInput value={col.width ?? ''} onChange={(v) => updateColumn(i, { width: v })} placeholder="Genişlik (10%)" />
                </div>
                <div className="flex-1">
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
                </div>
              </div>
            </div>
          ))}
          <button
            onClick={() => update({ columns: [...config.columns, { header: '', xpath: '', width: '' }] })}
            className="text-xs text-blue-600 hover:text-blue-800 text-left"
          >
            + Sütun ekle
          </button>
        </div>
      </Field>
    </>
  )
}

function ForEachPanel({ config, update }: { config: Config<'ForEach'>; update: UpdateFn }) {
  return (
    <Field label="Her eleman için (XPath)">
      <XPathInput
        value={config.iterateOver}
        onChange={(v) => update({ iterateOver: v })}
        placeholder="//cac:InvoiceLine"
      />
    </Field>
  )
}

function ConditionalPanel({ config, update }: { config: Config<'Conditional'>; update: UpdateFn }) {
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
      <Field label="XPath">
        <XPathInput
          value={config.condition.xpath}
          onChange={(v) => update({ condition: { ...config.condition, xpath: v } })}
          placeholder="//cbc:InvoiceTypeCode"
        />
      </Field>
      <Field label="Operatör">
        <Select
          value={config.condition.operator}
          onChange={(v) => update({ condition: { ...config.condition, operator: v } })}
          options={[...OPERATORS]}
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
    } catch {
      setUploadError('Yükleme başarısız.')
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
      <Field label="Varlık Tipi">
        <Select value={config.assetType} onChange={(v) => update({ assetType: v })} options={[
          { value: 'logo', label: 'Logo' },
          { value: 'signature', label: 'İmza' },
          { value: 'other', label: 'Diğer' },
        ]} />
      </Field>

      {/* Asset yükleme */}
      <Field label="Görsel">
        <input ref={fileInputRef} type="file" accept=".png,.jpg,.jpeg,.svg" className="hidden" onChange={handleFileChange} />
        {config.assetId ? (
          <div className="flex flex-col gap-1">
            <img
              src={getAssetUrl(config.assetId)}
              alt="önizleme"
              className="w-full max-h-20 object-contain border border-gray-200 rounded bg-gray-50"
            />
            <button
              onClick={handleRemoveAsset}
              className="text-xs text-red-500 hover:text-red-700 text-left"
            >
              ✕ Görseli kaldır
            </button>
          </div>
        ) : (
          <button
            onClick={() => fileInputRef.current?.click()}
            disabled={uploading}
            className="w-full py-2 text-xs border-2 border-dashed border-gray-200 rounded text-gray-500 hover:border-blue-300 hover:text-blue-600 disabled:opacity-50 transition-colors"
          >
            {uploading ? 'Yükleniyor…' : '+ PNG / JPG / SVG yükle'}
          </button>
        )}
        {uploadError && <span className="text-xs text-red-500">{uploadError}</span>}
      </Field>

      <Field label="Hizalama">
        <Select value={config.alignment} onChange={(v) => update({ alignment: v })} options={[
          { value: 'left', label: 'Sol' },
          { value: 'center', label: 'Orta' },
          { value: 'right', label: 'Sağ' },
        ]} />
      </Field>
      <Field label="Genişlik"><TextInput value={config.width ?? ''} onChange={(v) => update({ width: v })} placeholder="150px" /></Field>
      <Field label="Yükseklik"><TextInput value={config.height ?? ''} onChange={(v) => update({ height: v })} placeholder="80px" /></Field>
      <Field label="Alt metni"><TextInput value={config.altText ?? ''} onChange={(v) => update({ altText: v })} placeholder="Logo" /></Field>
      <Checkbox label="Ücretsiz temada düzenlenebilir" checked={config.editableOnFreeTheme} onChange={(v) => update({ editableOnFreeTheme: v })} />
    </>
  )
}

function DocumentInfoPanel({ config, update }: { config: Config<'DocumentInfo'>; update: UpdateFn }) {
  function updateRow(i: number, patch: object) {
    update({ rows: config.rows.map((r, idx) => (idx === i ? { ...r, ...patch } : r)) })
  }
  return (
    <>
    <Checkbox label="Kenarlıklı" checked={config.bordered ?? true} onChange={(v) => update({ bordered: v })} />
    {(config.bordered ?? true) && (
      <Field label="Kenarlık Stili">
        <Select value={config.borderStyle ?? 'solid'} onChange={(v) => update({ borderStyle: v })} options={[
          { value: 'solid', label: 'Düz' }, { value: 'dashed', label: 'Kesik' }, { value: 'dotted', label: 'Noktalı' },
        ]} />
      </Field>
    )}
    <FontSizeField config={config} update={update} />
    <Field label="Satırlar">
      <div className="flex flex-col gap-2">
        {config.rows.map((row, i) => (
          <div key={i} className="border border-gray-100 rounded p-2 bg-gray-50 flex flex-col gap-1">
            <div className="flex justify-between">
              <span className="text-xs text-gray-400">Satır {i + 1}</span>
              <button
                onClick={() => update({ rows: config.rows.filter((_, idx) => idx !== i) })}
                className="text-xs text-red-400"
              >
                ✕
              </button>
            </div>
            <TextInput value={row.label} onChange={(v) => updateRow(i, { label: v })} placeholder="Etiket" />
            <XPathInput value={row.xpath} onChange={(v) => updateRow(i, { xpath: v })} placeholder="XPath" />
          </div>
        ))}
        <button
          onClick={() => update({ rows: [...config.rows, { label: '', xpath: '' }] })}
          className="text-xs text-blue-600"
        >
          + Satır ekle
        </button>
      </div>
    </Field>
    </>
  )
}

function TotalsPanel({ config, update }: { config: Config<'Totals'>; update: UpdateFn }) {
  function updateRow(i: number, patch: object) {
    update({ rows: config.rows.map((r, idx) => (idx === i ? { ...r, ...patch } : r)) })
  }
  return (
    <>
      <FontSizeField config={config} update={update} />
      <Field label="Hizalama">
        <Select value={config.alignment} onChange={(v) => update({ alignment: v })} options={[
          { value: 'left', label: 'Sol' }, { value: 'center', label: 'Orta' }, { value: 'right', label: 'Sağ' },
        ]} />
      </Field>
      <Field label="Etiket sütun genişliği">
        <TextInput value={config.labelWidth ?? ''} onChange={(v) => update({ labelWidth: v })} placeholder="180px" />
      </Field>
      <Field label="Satırlar">
        <div className="flex flex-col gap-2">
          {config.rows.map((row, i) => (
            <div key={i} className="border border-gray-100 rounded p-2 bg-gray-50 flex flex-col gap-1">
              <div className="flex justify-between">
                <span className="text-xs text-gray-400">Satır {i + 1}</span>
                <button
                  onClick={() => update({ rows: config.rows.filter((_, idx) => idx !== i) })}
                  className="text-xs text-red-400"
                >
                  ✕
                </button>
              </div>
              <TextInput value={row.label} onChange={(v) => updateRow(i, { label: v })} placeholder="Etiket" />
              <XPathInput value={row.xpath} onChange={(v) => updateRow(i, { xpath: v })} placeholder="XPath" />
              <Checkbox label="Vurgula" checked={row.highlight ?? false} onChange={(v) => updateRow(i, { highlight: v })} />
            </div>
          ))}
          <button
            onClick={() => update({ rows: [...config.rows, { label: '', xpath: '' }] })}
            className="text-xs text-blue-600"
          >
            + Satır ekle
          </button>
        </div>
      </Field>
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
      <Field label="Her not için (XPath)">
        <XPathInput value={config.iterateOver} onChange={(v) => update({ iterateOver: v })} placeholder="//cbc:Note" />
      </Field>
      <Field label="Önek">
        <TextInput value={config.prefix ?? ''} onChange={(v) => update({ prefix: v })} placeholder="Not: " />
      </Field>

      {/* Sabit Notlar */}
      <div style={{ borderTop: '0.5px solid var(--color-border-subtle)', paddingTop: 10, marginTop: 4 }}>
        <div className="flex items-center justify-between" style={{ marginBottom: 6 }}>
          <span style={{ fontSize: 11, fontWeight: 500, color: 'var(--color-text-secondary)' }}>Sabit Notlar</span>
          <button
            onClick={addLine}
            style={{
              fontSize: 10, padding: '2px 8px', borderRadius: 4,
              border: '0.5px solid var(--color-brand-border)',
              background: 'var(--color-brand-light)', color: 'var(--color-brand-primary)',
              cursor: 'pointer', fontFamily: 'inherit',
            }}
          >
            + Not Ekle
          </button>
        </div>

        {lines.length === 0 && (
          <p style={{ fontSize: 10, color: 'var(--color-text-muted)', textAlign: 'center', padding: '6px 0' }}>
            Henüz sabit not yok
          </p>
        )}

        {lines.map((line, i) => (
          <div key={i} className="flex items-center" style={{ gap: 4, marginBottom: 4 }}>
            <input
              value={line}
              onChange={(e) => updateLine(i, e.target.value)}
              placeholder={`Sabit not ${i + 1}`}
              style={{
                flex: 1, fontSize: 11, padding: '4px 6px',
                border: '0.5px solid var(--color-border-default)', borderRadius: 4,
                background: 'var(--color-surface-card)', color: 'var(--color-text-primary)',
                outline: 'none', fontFamily: 'inherit',
              }}
              onFocus={e => (e.currentTarget.style.borderColor = 'var(--color-brand-primary)')}
              onBlur={e => (e.currentTarget.style.borderColor = 'var(--color-border-default)')}
            />
            <button
              onClick={() => removeLine(i)}
              title="Sil"
              style={{
                width: 20, height: 20, flexShrink: 0, border: 'none', background: 'none',
                cursor: 'pointer', fontSize: 11, color: 'var(--color-text-muted)',
                display: 'flex', alignItems: 'center', justifyContent: 'center',
              }}
              onMouseEnter={e => (e.currentTarget.style.color = 'var(--color-danger)')}
              onMouseLeave={e => (e.currentTarget.style.color = 'var(--color-text-muted)')}
            >
              ✕
            </button>
          </div>
        ))}

        {lines.length > 0 && (
          <Field label="Sabit notların yeri">
            <select
              value={position}
              onChange={(e) => update({ staticPosition: e.target.value as 'before' | 'after' })}
              style={{
                width: '100%', fontSize: 11, padding: '4px 6px',
                border: '0.5px solid var(--color-border-default)', borderRadius: 4,
                background: 'var(--color-surface-card)', color: 'var(--color-text-primary)',
                outline: 'none', fontFamily: 'inherit', cursor: 'pointer',
              }}
            >
              <option value="before">XPath notlarından önce</option>
              <option value="after">XPath notlarından sonra</option>
            </select>
          </Field>
        )}
      </div>

      <Checkbox label="Dış kenarlık" checked={config.bordered ?? false} onChange={(v) => update({ bordered: v })} />
      {config.bordered && (
        <>
          <Field label="Kenarlık Stili">
            <Select value={config.borderStyle ?? 'solid'} onChange={(v) => update({ borderStyle: v })} options={[
              { value: 'solid', label: 'Düz' }, { value: 'dashed', label: 'Kesik' }, { value: 'dotted', label: 'Noktalı' },
            ]} />
          </Field>
          <Field label="Kenarlık Rengi">
            <ColorInput value={config.borderColor ?? '#555555'} onChange={(v) => update({ borderColor: v })} />
          </Field>
        </>
      )}
      <FontSizeField config={config} update={update} />
    </>
  )
}

function BankInfoPanel({ config, update }: { config: Config<'BankInfo'>; update: UpdateFn }) {
  return (
    <>
      <Field label="Banka Adı">
        <TextInput value={config.bankName} onChange={(v) => update({ bankName: v })} placeholder="ör. YAPIKREDI" />
      </Field>
      <Field label="IBAN">
        <TextInput value={config.iban} onChange={(v) => update({ iban: v })} placeholder="ör. TR66 0006 7010 0000 0020 1934 37" />
      </Field>
      <Field label="IBAN Etiketi">
        <TextInput value={config.ibanLabel ?? 'IBAN: '} onChange={(v) => update({ ibanLabel: v })} placeholder="IBAN: " />
      </Field>
      <Checkbox label="Dış kenarlık" checked={config.bordered ?? true} onChange={(v) => update({ bordered: v })} />
      {config.bordered && (
        <>
          <Field label="Kenarlık Stili">
            <Select value={config.borderStyle ?? 'solid'} onChange={(v) => update({ borderStyle: v })} options={[
              { value: 'solid', label: 'Düz' }, { value: 'dashed', label: 'Kesik' }, { value: 'dotted', label: 'Noktalı' },
            ]} />
          </Field>
          <Field label="Kenarlık Rengi">
            <ColorInput value={config.borderColor ?? '#555555'} onChange={(v) => update({ borderColor: v })} />
          </Field>
        </>
      )}
      <FontSizeField config={config} update={update} />
    </>
  )
}

function ETTNPanel({ config, update }: { config: Config<'ETTN'>; update: UpdateFn }) {
  return (
    <>
      <Field label="ETTN XPath">
        <XPathInput value={config.ettnXpath} onChange={(v) => update({ ettnXpath: v })} placeholder="//cbc:UUID" />
      </Field>
      <Checkbox
        label="ETTN metnini göster"
        checked={config.showEttn ?? true}
        onChange={(v) => update({ showEttn: v })}
      />
      <Checkbox
        label="QR Kodu göster"
        checked={config.showQR}
        onChange={(v) => update({ showQR: v })}
      />
      {config.showQR && (
        <>
          <div className="flex gap-2">
            <Field label="QR Genişlik (px)">
              <TextInput
                value={String(config.qrWidth ?? 80)}
                onChange={(v) => update({ qrWidth: parseInt(v) || 80 })}
                placeholder="80"
              />
            </Field>
            <Field label="QR Yükseklik (px)">
              <TextInput
                value={String(config.qrHeight ?? 80)}
                onChange={(v) => update({ qrHeight: parseInt(v) || 80 })}
                placeholder="80"
              />
            </Field>
          </div>
          <Field label="QR Hizalama">
            <Select
              value={config.qrAlignment ?? 'right'}
              onChange={(v) => update({ qrAlignment: v })}
              options={[
                { value: 'left', label: 'Sol' },
                { value: 'center', label: 'Orta' },
                { value: 'right', label: 'Sağ' },
              ]}
            />
          </Field>
        </>
      )}
    </>
  )
}

function DividerPanel({ config, update }: { config: Config<'Divider'>; update: UpdateFn }) {
  return (
    <>
      <Field label="Stil">
        <Select value={config.style} onChange={(v) => update({ style: v })} options={[
          { value: 'solid', label: 'Düz' }, { value: 'dashed', label: 'Kesik' }, { value: 'dotted', label: 'Noktalı' },
        ]} />
      </Field>
      <Field label="Renk"><ColorInput value={config.color ?? '#CCCCCC'} onChange={(v) => update({ color: v })} placeholder="#CCCCCC" /></Field>
      <Field label="Kalınlık"><TextInput value={config.thickness ?? '1px'} onChange={(v) => update({ thickness: v })} placeholder="1px" /></Field>
      <Field label="Üst boşluk"><TextInput value={config.marginTop ?? '8px'} onChange={(v) => update({ marginTop: v })} placeholder="8px" /></Field>
      <Field label="Alt boşluk"><TextInput value={config.marginBottom ?? '8px'} onChange={(v) => update({ marginBottom: v })} placeholder="8px" /></Field>
    </>
  )
}

function SpacerPanel({ config, update }: { config: Config<'Spacer'>; update: UpdateFn }) {
  return (
    <Field label="Yükseklik">
      <TextInput value={config.height} onChange={(v) => update({ height: v })} placeholder="24px" />
    </Field>
  )
}

function VariablePanel({ config, update }: { config: Config<'Variable'>; update: UpdateFn }) {
  return (
    <>
      <p className="text-xs text-gray-400 bg-gray-50 rounded p-2">
        Görünür çıktı üretmez. Diğer bloklarda <code className="font-mono">$isim</code> ile kullanılır.
      </p>
      <Field label="Değişken adı">
        <TextInput value={config.name} onChange={(v) => update({ name: v })} placeholder="currency" />
      </Field>
      <Field label="XPath / Değer">
        <XPathInput value={config.xpath} onChange={(v) => update({ xpath: v })} placeholder="//cbc:DocumentCurrencyCode" />
      </Field>
    </>
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
      <Field label="Koşul XPath">
        <XPathInput
          value={config.condition.xpath}
          onChange={(v) => update({ condition: { ...config.condition, xpath: v } })}
          placeholder="//cbc:InvoiceTypeCode"
        />
      </Field>
      <Field label="Operatör">
        <Select
          value={config.condition.operator}
          onChange={(v) => update({ condition: { ...config.condition, operator: v } })}
          options={[...OPERATORS]}
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
      <div className="border-t border-gray-100 pt-2 flex flex-col gap-2">
        <p className="text-xs font-medium text-green-600">Koşul doğruysa (THEN)</p>
        <Checkbox label="Statik metin" checked={config.thenIsStatic} onChange={(v) => update({ thenIsStatic: v })} />
        {config.thenIsStatic ? (
          <TextInput value={config.thenContent} onChange={(v) => update({ thenContent: v })} placeholder="Metin..." />
        ) : (
          <XPathInput value={config.thenContent} onChange={(v) => update({ thenContent: v })} placeholder="XPath..." />
        )}
      </div>
      <div className="border-t border-gray-100 pt-2 flex flex-col gap-2">
        <p className="text-xs font-medium text-orange-500">Koşul yanlışsa (ELSE)</p>
        <Checkbox label="Statik metin" checked={config.elseIsStatic} onChange={(v) => update({ elseIsStatic: v })} />
        {config.elseIsStatic ? (
          <TextInput value={config.elseContent} onChange={(v) => update({ elseContent: v })} placeholder="Metin..." />
        ) : (
          <XPathInput value={config.elseContent} onChange={(v) => update({ elseContent: v })} placeholder="XPath..." />
        )}
      </div>
      <FontSizeField config={config} update={update} />
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

  const sectionLabelStyle: React.CSSProperties = {
    fontSize: 9,
    fontWeight: 600,
    letterSpacing: '0.08em',
    color: 'var(--color-text-muted)',
    marginBottom: 6,
    paddingBottom: 4,
    borderBottom: '0.5px solid var(--color-border-default)',
  }

  return (
    <>
      <label className="flex items-center cursor-pointer" style={{ gap: 6 }}>
        <span
          style={{
            display: 'inline-flex', alignItems: 'center', justifyContent: 'center',
            width: 14, height: 14, borderRadius: 2,
            border: config.bordered ? '1.5px solid var(--color-brand-primary)' : '1.5px solid #A0A09A',
            background: config.bordered ? 'var(--color-brand-primary)' : 'var(--color-surface-card)',
            fontSize: 9, color: '#fff', fontWeight: 700, cursor: 'pointer', flexShrink: 0,
          }}
        >{config.bordered ? '✓' : ''}</span>
        <input type="checkbox" checked={config.bordered} onChange={(e) => update({ bordered: e.target.checked })} style={{ position: 'absolute', opacity: 0, width: 0, height: 0 }} />
        <span style={{ fontSize: 11, color: 'var(--color-text-secondary)' }}>Kenarlıklı</span>
      </label>
      {config.bordered && (
        <Field label="Kenarlık Stili">
          <Select value={config.borderStyle ?? 'solid'} onChange={(v) => update({ borderStyle: v })} options={[
            { value: 'solid', label: 'Düz' }, { value: 'dashed', label: 'Kesik' }, { value: 'dotted', label: 'Noktalı' },
          ]} />
        </Field>
      )}
      <Field label="Yazı Boyutu">
        <TextInput value={config.fontSize ?? ''} onChange={(v) => update({ fontSize: v })} placeholder="11px" />
      </Field>

      {/* Özel alanlar */}
      <div style={{ borderTop: '0.5px solid var(--color-border-default)', paddingTop: 8 }}>
        <p style={sectionLabelStyle}>ÖZEL ALANLAR</p>
        <div className="flex flex-col" style={{ gap: 3 }}>
          {customFields.map((field) => {
            const absIdx = config.fields.indexOf(field)
            return (
              <div
                key={field.key}
                style={{ border: '0.5px solid var(--color-border-default)', borderRadius: 5, overflow: 'hidden' }}
              >
                <div className="flex items-center" style={{ padding: '5px 8px', gap: 5, background: 'var(--color-surface-secondary)' }}>
                  <input
                    type="text"
                    value={field.label}
                    onChange={(e) => updateCustomField(absIdx, { label: e.target.value })}
                    style={{
                      flex: 1, minWidth: 0, fontSize: 11, fontFamily: 'inherit',
                      border: 'none', background: 'transparent', outline: 'none',
                      color: 'var(--color-text-primary)', fontWeight: 500,
                    }}
                    placeholder="Etiket"
                  />
                  <button type="button" onClick={() => removeCustomField(absIdx)}
                    style={{ width: 18, height: 18, border: 'none', background: 'none', fontSize: 10, color: 'var(--color-danger)', cursor: 'pointer' }}>✕</button>
                </div>
                <div style={{ padding: '4px 8px', background: 'var(--color-surface-card)' }}>
                  <XPathInput value={field.xpath} onChange={(v) => updateCustomField(absIdx, { xpath: v })} placeholder="//cbc:..." />
                </div>
              </div>
            )
          })}
        </div>
        <div style={{ marginTop: 8 }}>
          <button
            onClick={addCustomField}
            style={{ fontSize: 11, color: 'var(--color-brand-primary)', background: 'none', border: 'none', cursor: 'pointer', fontFamily: 'inherit', padding: 0 }}
          >
            + Özel alan ekle
          </button>
        </div>
      </div>
    </>
  )
}

// ── InvoiceTotals — Dip toplamlar düzenleyici ─────────────────────────────────

function InvoiceTotalsPanel({ config, update }: { config: Config<'InvoiceTotals'>; update: UpdateFn }) {
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

  const sectionLabelStyle: React.CSSProperties = {
    fontSize: 9,
    fontWeight: 600,
    letterSpacing: '0.08em',
    color: 'var(--color-text-muted)',
    marginBottom: 6,
    paddingBottom: 4,
    borderBottom: '0.5px solid var(--color-border-default)',
  }

  return (
    <>
      <div className="flex" style={{ gap: 12 }}>
        <label className="flex items-center cursor-pointer" style={{ gap: 6 }}>
          <span
            style={{
              display: 'inline-flex', alignItems: 'center', justifyContent: 'center',
              width: 14, height: 14, borderRadius: 2,
              border: config.showCurrency ? '1.5px solid var(--color-brand-primary)' : '1.5px solid #A0A09A',
              background: config.showCurrency ? 'var(--color-brand-primary)' : 'var(--color-surface-card)',
              fontSize: 9, color: '#fff', fontWeight: 700, cursor: 'pointer', flexShrink: 0,
            }}
          >{config.showCurrency ? '✓' : ''}</span>
          <input type="checkbox" checked={config.showCurrency} onChange={(e) => update({ showCurrency: e.target.checked })} style={{ position: 'absolute', opacity: 0, width: 0, height: 0 }} />
          <span style={{ fontSize: 11, color: 'var(--color-text-secondary)' }}>Para birimi göster</span>
        </label>
      </div>

      {config.showCurrency && (
        <Field label="Para Birimi XPath">
          <XPathInput
            value={config.currencyXpath}
            onChange={(v) => update({ currencyXpath: v })}
            placeholder="//cbc:DocumentCurrencyCode"
          />
        </Field>
      )}

      <div className="flex" style={{ gap: 8 }}>
        <Field label="Etiket Genişliği">
          <TextInput value={config.labelWidth ?? ''} onChange={(v) => update({ labelWidth: v })} placeholder="60%" />
        </Field>
        <Field label="Yazı Boyutu">
          <TextInput value={config.fontSize ?? ''} onChange={(v) => update({ fontSize: v })} placeholder="9pt" />
        </Field>
      </div>

      {/* Alan listesi */}
      <div style={{ borderTop: '0.5px solid var(--color-border-default)', paddingTop: 8 }}>
        <p style={sectionLabelStyle}>SATIRLAR</p>
        <div className="flex flex-col" style={{ gap: 3 }}>
          {config.fields.map((field, i) => (
            <div
              key={field.key}
              style={{
                border: `0.5px solid ${field.visible ? 'var(--color-border-default)' : 'var(--color-border-subtle)'}`,
                borderRadius: 5,
                overflow: 'hidden',
                opacity: field.visible ? 1 : 0.55,
              }}
            >
              {/* Header satırı */}
              <div className="flex items-center" style={{ padding: '5px 8px', gap: 5, background: 'var(--color-surface-secondary)' }}>
                {/* Görünürlük toggle */}
                <button
                  type="button"
                  onClick={() => updateField(i, { visible: !field.visible })}
                  style={{
                    width: 14, height: 14, borderRadius: 2, flexShrink: 0,
                    border: field.visible ? '1.5px solid var(--color-brand-primary)' : '1.5px solid #A0A09A',
                    background: field.visible ? 'var(--color-brand-primary)' : 'var(--color-surface-card)',
                    display: 'flex', alignItems: 'center', justifyContent: 'center',
                    fontSize: 9, color: '#fff', fontWeight: 700, cursor: 'pointer',
                  }}
                  title={field.visible ? 'Gizle' : 'Göster'}
                >
                  {field.visible ? '✓' : ''}
                </button>

                {/* Vurgula toggle */}
                <button
                  type="button"
                  onClick={() => updateField(i, { highlight: !field.highlight })}
                  title={field.highlight ? 'Vurguyu kaldır' : 'Vurgula'}
                  style={{
                    width: 14, height: 14, borderRadius: 2, flexShrink: 0,
                    border: field.highlight ? '1.5px solid #D4A017' : '1.5px solid #A0A09A',
                    background: field.highlight ? '#FFF3CD' : 'var(--color-surface-card)',
                    display: 'flex', alignItems: 'center', justifyContent: 'center',
                    fontSize: 8, color: '#D4A017', fontWeight: 700, cursor: 'pointer',
                  }}
                >
                  ★
                </button>

                {/* Etiket */}
                <input
                  type="text"
                  value={field.label}
                  onChange={(e) => updateField(i, { label: e.target.value })}
                  style={{
                    flex: 1, minWidth: 0, fontSize: 11, fontFamily: 'inherit',
                    border: 'none', background: 'transparent', outline: 'none',
                    color: 'var(--color-text-primary)', fontWeight: 500,
                  }}
                  placeholder="Etiket"
                />

                {/* Sırala */}
                <div className="flex" style={{ gap: 2 }}>
                  <button type="button" onClick={() => moveField(i, -1)} disabled={i === 0}
                    style={{ width: 18, height: 18, border: 'none', background: 'none', fontSize: 9, color: 'var(--color-text-muted)', cursor: 'pointer', opacity: i === 0 ? 0.3 : 1 }}>▲</button>
                  <button type="button" onClick={() => moveField(i, 1)} disabled={i === config.fields.length - 1}
                    style={{ width: 18, height: 18, border: 'none', background: 'none', fontSize: 9, color: 'var(--color-text-muted)', cursor: 'pointer', opacity: i === config.fields.length - 1 ? 0.3 : 1 }}>▼</button>
                </div>

                {/* Sil (sadece custom) */}
                {field.isCustom && (
                  <button type="button" onClick={() => removeField(i)}
                    style={{ width: 18, height: 18, border: 'none', background: 'none', fontSize: 10, color: 'var(--color-danger)', cursor: 'pointer' }}>✕</button>
                )}
              </div>

              {/* XPath */}
              {field.isCustom ? (
                <div style={{ padding: '4px 8px', background: 'var(--color-surface-card)' }}>
                  <XPathInput
                    value={field.xpath}
                    onChange={(v) => updateField(i, { xpath: v })}
                    placeholder="//cac:LegalMonetaryTotal/cbc:..."
                  />
                </div>
              ) : (
                <div style={{
                  padding: '3px 8px', background: 'var(--color-surface-card)',
                  fontSize: 10, fontFamily: 'monospace', color: 'var(--color-text-muted)',
                  overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap',
                }} title={field.xpath}>
                  {field.xpath}
                </div>
              )}
            </div>
          ))}
        </div>

        {/* Reset + Özel ekle */}
        <div className="flex items-center" style={{ marginTop: 8, gap: 8 }}>
          <button
            onClick={addCustomField}
            style={{ fontSize: 11, color: 'var(--color-brand-primary)', background: 'none', border: 'none', cursor: 'pointer', fontFamily: 'inherit', padding: 0 }}
          >
            + Özel satır ekle
          </button>
          <button
            onClick={() => update({ fields: DEFAULT_INVOICE_TOTALS_FIELDS.map((f) => ({ ...f })) })}
            style={{ fontSize: 11, color: 'var(--color-text-muted)', background: 'none', border: 'none', cursor: 'pointer', fontFamily: 'inherit', padding: 0, marginLeft: 'auto' }}
          >
            Sıfırla
          </button>
        </div>
      </div>
    </>
  )
}

function GibLogoPanel({ config, update }: { config: Config<'GibLogo'>; update: UpdateFn }) {
  return (
    <>
      {/* Sabit logo önizlemesi */}
      <div style={{
        display: 'flex', justifyContent: config.alignment,
        padding: 8, background: 'var(--color-surface-secondary)',
        borderRadius: 5, border: '0.5px solid var(--color-border-subtle)',
      }}>
        <img
          src={GIB_LOGO_BASE64}
          alt="GİB Logo"
          style={{ width: config.width ?? '80px', height: config.height ?? '80px', objectFit: 'contain' }}
        />
      </div>

      <div className="flex" style={{ gap: 8 }}>
        <Field label="Genişlik">
          <TextInput value={config.width ?? ''} onChange={(v) => update({ width: v })} placeholder="80px" />
        </Field>
        <Field label="Yükseklik">
          <TextInput value={config.height ?? ''} onChange={(v) => update({ height: v })} placeholder="80px" />
        </Field>
      </div>
      <Field label="Hizalama">
        <Select
          value={config.alignment}
          onChange={(v) => update({ alignment: v as 'left' | 'center' | 'right' })}
          options={[
            { value: 'left', label: 'Sol' },
            { value: 'center', label: 'Orta' },
            { value: 'right', label: 'Sağ' },
          ]}
        />
      </Field>
    </>
  )
}

function GibKarekodPanel({ config, update }: { config: Config<'GibKarekod'>; update: UpdateFn }) {
  return (
    <>
      <p className="text-xs text-gray-400 bg-gray-50 rounded p-2">
        GİB standart e-Fatura karekodu. XPath değerleri sabittir.
      </p>
      <div className="flex gap-2">
        <Field label="Genişlik (px)">
          <TextInput
            value={String(config.qrWidth ?? 150)}
            onChange={(v) => update({ qrWidth: parseInt(v) || 150 })}
            placeholder="150"
          />
        </Field>
        <Field label="Yükseklik (px)">
          <TextInput
            value={String(config.qrHeight ?? 150)}
            onChange={(v) => update({ qrHeight: parseInt(v) || 150 })}
            placeholder="150"
          />
        </Field>
      </div>
      <Field label="Hizalama">
        <Select
          value={config.qrAlignment ?? 'right'}
          onChange={(v) => update({ qrAlignment: v })}
          options={[
            { value: 'left', label: 'Sol' },
            { value: 'center', label: 'Orta' },
            { value: 'right', label: 'Sağ' },
          ]}
        />
      </Field>
    </>
  )
}

function TaxSummaryPanel({ config, update }: { config: Config<'TaxSummary'>; update: UpdateFn }) {
  return (
    <>
      <Field label="KDV alt toplamları (XPath)">
        <XPathInput
          value={config.taxTotalXpath}
          onChange={(v) => update({ taxTotalXpath: v })}
          placeholder="//cac:TaxTotal/cac:TaxSubtotal"
        />
      </Field>
      <Checkbox label="KDV Oranı sütunu" checked={config.showPercent} onChange={(v) => update({ showPercent: v })} />
      {config.showPercent && (
        <Field label="Oran XPath">
          <XPathInput value={config.percentXpath} onChange={(v) => update({ percentXpath: v })} placeholder="cac:TaxCategory/cbc:Percent" />
        </Field>
      )}
      <Field label="Matrah XPath">
        <XPathInput value={config.taxableAmountXpath} onChange={(v) => update({ taxableAmountXpath: v })} placeholder="cbc:TaxableAmount" />
      </Field>
      <Field label="KDV Tutarı XPath">
        <XPathInput value={config.taxAmountXpath} onChange={(v) => update({ taxAmountXpath: v })} placeholder="cbc:TaxAmount" />
      </Field>
      <Field label="Başlık arkaplanı">
        <ColorInput value={config.headerBackgroundColor ?? '#E0E0E0'} onChange={(v) => update({ headerBackgroundColor: v })} placeholder="#E0E0E0" />
      </Field>
    </>
  )
}

function PartyInfoPanel({ config, update }: { config: Config<'PartyInfo'>; update: UpdateFn }) {
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

  return (
    <>
      <Field label="Taraf Tipi">
        <Select
          value={config.partyType}
          onChange={(v) => handlePartyTypeChange(v as PartyType)}
          options={PARTY_TYPES}
        />
      </Field>
      <Field label="Başlık">
        <TextInput value={config.title} onChange={(v) => update({ title: v })} placeholder="SATICI" />
      </Field>
      <Checkbox label="Başlığı göster" checked={config.showTitle} onChange={(v) => update({ showTitle: v })} />
      <Checkbox label="Kenarlıklı" checked={config.bordered} onChange={(v) => update({ bordered: v })} />
      {config.bordered && (
        <Field label="Kenarlık Stili">
          <Select value={config.borderStyle ?? 'solid'} onChange={(v) => update({ borderStyle: v })} options={[
            { value: 'solid', label: 'Düz' }, { value: 'dashed', label: 'Kesik' }, { value: 'dotted', label: 'Noktalı' },
          ]} />
        </Field>
      )}
      <Field label="Etiket Stili">
        <Select
          value={config.labelStyle}
          onChange={(v) => update({ labelStyle: v })}
          options={LABEL_STYLES}
        />
      </Field>
      <Field label="Yazı Boyutu">
        <TextInput value={config.fontSize ?? ''} onChange={(v) => update({ fontSize: v })} placeholder="9pt" />
      </Field>

      <div className="border-t border-gray-100 pt-2">
        <p className="text-xs font-medium text-gray-500 uppercase tracking-wide mb-2">Alanlar</p>
        <div className="flex flex-col gap-1.5">
          {config.fields.map((field, i) => (
            <div
              key={field.key}
              className={`border rounded p-2 flex flex-col gap-1 ${
                field.visible ? 'border-gray-200 bg-white' : 'border-gray-100 bg-gray-50 opacity-60'
              }`}
            >
              <div className="flex items-center gap-1">
                <button
                  type="button"
                  title={field.visible ? 'Gizle' : 'Göster'}
                  onClick={() => updateField(i, { visible: !field.visible })}
                  className={`w-5 h-5 flex items-center justify-center rounded text-xs ${
                    field.visible ? 'text-blue-600' : 'text-gray-500'
                  }`}
                >
                  {field.visible ? '◉' : '○'}
                </button>
                <input
                  type="text"
                  value={field.label}
                  onChange={(e) => updateField(i, { label: e.target.value })}
                  className="flex-1 min-w-0 text-xs border border-gray-200 rounded px-1.5 py-1 outline-none focus:border-blue-400"
                  placeholder="Etiket"
                />
                <div className="flex items-center gap-0.5">
                  <button
                    type="button"
                    onClick={() => moveField(i, -1)}
                    disabled={i === 0}
                    className="w-5 h-5 flex items-center justify-center text-xs text-gray-400 hover:text-gray-700 disabled:opacity-30"
                  >
                    ▲
                  </button>
                  <button
                    type="button"
                    onClick={() => moveField(i, 1)}
                    disabled={i === config.fields.length - 1}
                    className="w-5 h-5 flex items-center justify-center text-xs text-gray-400 hover:text-gray-700 disabled:opacity-30"
                  >
                    ▼
                  </button>
                  {field.isCustom && (
                    <button
                      type="button"
                      onClick={() => removeField(i)}
                      className="w-5 h-5 flex items-center justify-center text-xs text-red-400 hover:text-red-600"
                    >
                      ✕
                    </button>
                  )}
                </div>
              </div>
              {field.isCustom ? (
                <XPathInput
                  value={field.relativeXpath}
                  onChange={(v) => updateField(i, { relativeXpath: v })}
                  placeholder="cac:.../cbc:..."
                />
              ) : (
                <span className="text-[10px] text-gray-400 px-1 truncate" title={field.relativeXpath}>
                  {field.relativeXpath}
                </span>
              )}
            </div>
          ))}
        </div>
        <button
          onClick={addCustomField}
          className="mt-2 text-xs text-blue-600 hover:text-blue-800"
        >
          + Özel alan ekle
        </button>
      </div>
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

  return (
    <div className="border-t border-gray-100 pt-2">
      <p className="text-xs font-medium text-gray-500 uppercase tracking-wide mb-2">Kolonlar</p>
      <div className="flex flex-col gap-0.5">
        {config.columns.map((col, i) => {
          const isExpanded = expandedIndex === i
          return (
            <div
              key={col.key}
              className={`border rounded transition-colors ${
                col.visible
                  ? isExpanded ? 'border-blue-300 bg-blue-50/30' : 'border-gray-200 bg-white'
                  : 'border-gray-100 bg-gray-50 opacity-50'
              }`}
            >
              {/* Compact header row — always visible */}
              <div
                className="flex items-center gap-1 px-2 py-1.5 cursor-pointer select-none"
                onClick={() => setExpandedIndex(isExpanded ? null : i)}
              >
                <button
                  type="button"
                  title={col.visible ? 'Gizle' : 'Göster'}
                  onClick={(e) => { e.stopPropagation(); updateColumn(i, { visible: !col.visible }) }}
                  className={`w-4 h-4 flex items-center justify-center rounded-full border transition-colors ${
                    col.visible
                      ? 'border-blue-500 bg-blue-500 text-white'
                      : 'border-gray-400 bg-white text-transparent'
                  }`}
                >
                  <svg className="w-2.5 h-2.5" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={3}><path strokeLinecap="round" strokeLinejoin="round" d="M5 13l4 4L19 7" /></svg>
                </button>
                <span className="flex-1 min-w-0 text-xs font-medium text-gray-700 truncate">{col.header || 'Başlıksız'}</span>
                <span className="text-[10px] text-gray-400 shrink-0">{col.width}</span>
                <svg
                  className={`w-3 h-3 text-gray-400 transition-transform ${isExpanded ? 'rotate-180' : ''}`}
                  fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2}
                >
                  <path strokeLinecap="round" strokeLinejoin="round" d="M19 9l-7 7-7-7" />
                </svg>
              </div>

              {/* Expanded detail section */}
              {isExpanded && (
                <div className="px-2.5 pb-2.5 pt-1 border-t border-gray-100 flex flex-col gap-2.5">
                  {/* Header & Sıralama */}
                  <div className="flex items-center gap-1.5">
                    <div className="flex-1">
                      <label className="text-[11px] text-gray-500 mb-1 block">Başlık</label>
                      <input
                        type="text"
                        value={col.header}
                        onChange={(e) => updateColumn(i, { header: e.target.value })}
                        className="w-full text-[13px] border border-gray-200 rounded-md px-2.5 py-1.5 outline-none focus:border-blue-400 focus:ring-1 focus:ring-blue-100"
                        placeholder="Başlık"
                      />
                    </div>
                    <div className="flex flex-col gap-0.5 pt-4">
                      <button
                        type="button"
                        onClick={() => moveColumn(i, -1)}
                        disabled={i === 0}
                        className="w-6 h-6 flex items-center justify-center rounded border border-gray-200 text-[11px] text-gray-500 hover:bg-gray-100 disabled:opacity-30"
                      >
                        ▲
                      </button>
                      <button
                        type="button"
                        onClick={() => moveColumn(i, 1)}
                        disabled={i === config.columns.length - 1}
                        className="w-6 h-6 flex items-center justify-center rounded border border-gray-200 text-[11px] text-gray-500 hover:bg-gray-100 disabled:opacity-30"
                      >
                        ▼
                      </button>
                    </div>
                  </div>

                  {/* Genişlik & Format yan yana */}
                  <div className="grid grid-cols-2 gap-2">
                    <div>
                      <label className="text-[11px] text-gray-500 mb-1 block">Genişlik</label>
                      <input
                        type="text"
                        value={col.width ?? ''}
                        onChange={(e) => updateColumn(i, { width: e.target.value })}
                        className="w-full text-[13px] border border-gray-200 rounded-md px-2.5 py-1.5 outline-none focus:border-blue-400 focus:ring-1 focus:ring-blue-100"
                        placeholder="10%"
                      />
                    </div>
                    <div>
                      <label className="text-[11px] text-gray-500 mb-1 block">Format</label>
                      <select
                        value={col.format ?? 'text'}
                        onChange={(e) => updateColumn(i, { format: e.target.value })}
                        className="w-full text-[13px] border border-gray-200 rounded-md px-2 py-1.5 outline-none focus:border-blue-400 focus:ring-1 focus:ring-blue-100 bg-white"
                      >
                        {formats.map((f) => (
                          <option key={f.value} value={f.value}>{f.label}</option>
                        ))}
                      </select>
                    </div>
                  </div>

                  {/* XPath */}
                  <div>
                    <label className="text-[11px] text-gray-500 mb-1 block">XPath</label>
                    {col.isCustom ? (
                      <XPathInput
                        value={col.relativeXpath}
                        onChange={(v) => updateColumn(i, { relativeXpath: v })}
                        placeholder="cac:.../cbc:..."
                      />
                    ) : (
                      <div className="text-[11px] text-gray-500 bg-gray-50 rounded-md px-2.5 py-1.5 font-mono truncate border border-gray-100" title={col.relativeXpath}>
                        {col.relativeXpath}
                      </div>
                    )}
                  </div>

                  {/* Sil butonu (sadece custom) */}
                  {col.isCustom && (
                    <button
                      type="button"
                      onClick={() => { removeColumn(i); setExpandedIndex(null) }}
                      className="self-end text-[10px] text-red-500 hover:text-red-700 flex items-center gap-0.5"
                    >
                      <svg className="w-3 h-3" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2}><path strokeLinecap="round" strokeLinejoin="round" d="M19 7l-.867 12.142A2 2 0 0116.138 21H7.862a2 2 0 01-1.995-1.858L5 7m5 4v6m4-6v6m1-10V4a1 1 0 00-1-1h-4a1 1 0 00-1 1v3M4 7h16" /></svg>
                      Kolonu sil
                    </button>
                  )}
                </div>
              )}
            </div>
          )
        })}
      </div>
      <button
        onClick={addCustomColumn}
        className="mt-2 text-xs text-blue-600 hover:text-blue-800 flex items-center gap-1"
      >
        <svg className="w-3.5 h-3.5" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2}><path strokeLinecap="round" strokeLinejoin="round" d="M12 4v16m8-8H4" /></svg>
        Özel kolon ekle
      </button>
    </div>
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
      <Field label="Tekrar XPath">
        <XPathInput
          value={config.iterateOver}
          onChange={(v) => update({ iterateOver: v })}
          placeholder="//cac:InvoiceLine"
        />
      </Field>
      <Field label="Başlık">
        <TextInput value={config.title} onChange={(v) => update({ title: v })} placeholder="MAL HİZMET TABLOSU" />
      </Field>
      <Checkbox label="Başlığı göster" checked={config.showTitle} onChange={(v) => update({ showTitle: v })} />
      <Checkbox label="Başlık satırı göster" checked={config.showHeader} onChange={(v) => update({ showHeader: v })} />
      <Checkbox label="Sıra No göster" checked={config.showRowNumber} onChange={(v) => update({ showRowNumber: v })} />
      <Checkbox label="Kenarlıklı" checked={config.bordered} onChange={(v) => update({ bordered: v })} />
      {config.bordered && (
        <Field label="Kenarlık Stili">
          <Select value={config.borderStyle ?? 'solid'} onChange={(v) => update({ borderStyle: v })} options={[
            { value: 'solid', label: 'Düz' }, { value: 'dashed', label: 'Kesik' }, { value: 'dotted', label: 'Noktalı' },
          ]} />
        </Field>
      )}
      <Field label="Başlık arkaplanı">
        <ColorInput value={config.headerBackgroundColor ?? '#E0E0E0'} onChange={(v) => update({ headerBackgroundColor: v })} placeholder="#E0E0E0" />
      </Field>
      <Field label="Alternatif satır rengi">
        <ColorInput value={config.alternateRowColor ?? '#F9F9F9'} onChange={(v) => update({ alternateRowColor: v })} placeholder="#F9F9F9" />
      </Field>
      <FontSizeField config={config} update={update} />

      <InvoiceLineColumnEditor config={config} updateColumn={updateColumn} moveColumn={moveColumn} addCustomColumn={addCustomColumn} removeColumn={removeColumn} formats={FORMATS} />
    </>
  )
}

// ── Properties content ────────────────────────────────────────────────────────

function PropertiesContent() {
  const selectedBlockId = useEditorStore((s) => s.selectedBlockId)
  const blocks = useEditorStore((s) => s.blocks)
  const updateBlockConfig = useEditorStore((s) => s.updateBlockConfig)

  if (!selectedBlockId) {
    return (
      <div className="flex-1 flex items-center justify-center">
        <p style={{ fontSize: 11, color: 'var(--color-text-muted)', textAlign: 'center', padding: '0 16px' }}>Düzenlemek için bir blok seç</p>
      </div>
    )
  }

  const block = blocks[selectedBlockId]
  if (!block) return null

  const update: UpdateFn = (patch) => updateBlockConfig(selectedBlockId, patch)
  const cfg = block.config as never

  return (
    <div className="flex-1 overflow-y-auto flex flex-col">
      <div style={{ padding: '8px 12px', borderBottom: '0.5px solid var(--color-border-default)', flexShrink: 0 }}>
        <p style={{ fontSize: 13, fontWeight: 600, color: 'var(--color-text-primary)' }}>{block.type}</p>
      </div>
      <div style={{ padding: 12, display: 'flex', flexDirection: 'column', gap: 8, flex: 1 }}>
        <LayoutControls block={block} />
        {block.type === 'Text' && <TextPanel config={cfg} update={update} />}
        {block.type === 'Heading' && <HeadingPanel config={cfg} update={update} />}
        {block.type === 'Paragraph' && <ParagraphPanel config={cfg} update={update} />}
        {block.type === 'Table' && <TablePanel config={cfg} update={update} />}
        {block.type === 'ForEach' && <ForEachPanel config={cfg} update={update} />}
        {block.type === 'Conditional' && <ConditionalPanel config={cfg} update={update} />}
        {block.type === 'Image' && <ImagePanel config={cfg} update={update} />}
        {block.type === 'DocumentInfo' && <DocumentInfoPanel config={cfg} update={update} />}
        {block.type === 'Totals' && <TotalsPanel config={cfg} update={update} />}
        {block.type === 'Notes' && <NotesPanel config={cfg} update={update} />}
        {block.type === 'BankInfo' && <BankInfoPanel config={cfg} update={update} />}
        {block.type === 'ETTN' && <ETTNPanel config={cfg} update={update} />}
        {block.type === 'Divider' && <DividerPanel config={cfg} update={update} />}
        {block.type === 'Spacer' && <SpacerPanel config={cfg} update={update} />}
        {block.type === 'Variable' && <VariablePanel config={cfg} update={update} />}
        {block.type === 'ConditionalText' && <ConditionalTextPanel config={cfg} update={update} />}
        {block.type === 'TaxSummary' && <TaxSummaryPanel config={cfg} update={update} />}
        {block.type === 'GibKarekod' && <GibKarekodPanel config={cfg} update={update} />}
        {block.type === 'GibLogo' && <GibLogoPanel config={cfg} update={update} />}
        {block.type === 'PartyInfo' && <PartyInfoPanel config={cfg} update={update} />}
        {block.type === 'InvoiceLineTable' && <InvoiceLineTablePanel config={cfg} update={update} />}
        {block.type === 'InvoiceHeader' && <InvoiceHeaderPanel config={cfg} update={update} />}
        {block.type === 'InvoiceTotals' && <InvoiceTotalsPanel config={cfg} update={update} />}
      </div>
    </div>
  )
}

// ── Root PropertyPanel ────────────────────────────────────────────────────────

export default function PropertyPanel() {
  const rightPanelTab = useXmlStore((s) => s.rightPanelTab)
  const setRightPanelTab = useXmlStore((s) => s.setRightPanelTab)

  const isXmlTab = rightPanelTab === 'xml'

  return (
    <aside
      className="flex-shrink-0 flex flex-col overflow-hidden"
      style={{
        width: isXmlTab ? 400 : 220,
        background: 'var(--color-surface-card)',
        borderLeft: '0.5px solid var(--color-border-default)',
        transition: 'width 200ms ease',
      }}
    >
      {/* Tab bar */}
      <div
        className="flex flex-shrink-0"
        style={{ height: 36, borderBottom: '0.5px solid var(--color-border-default)' }}
      >
        {(['properties', 'xml'] as const).map((tab) => {
          const active = rightPanelTab === tab
          return (
            <button
              key={tab}
              onClick={() => setRightPanelTab(tab)}
              style={{
                flex: 1,
                fontSize: 11,
                fontWeight: 500,
                fontFamily: 'inherit',
                color: active ? 'var(--color-brand-primary)' : 'var(--color-text-secondary)',
                background: 'transparent',
                border: 'none',
                borderBottom: active ? '2px solid var(--color-brand-primary)' : '2px solid transparent',
                cursor: 'pointer',
                transition: 'color 120ms',
              }}
              onMouseEnter={e => { if (!active) { e.currentTarget.style.color = 'var(--color-text-primary)'; e.currentTarget.style.background = 'var(--color-surface-secondary)' } }}
              onMouseLeave={e => { if (!active) { e.currentTarget.style.color = 'var(--color-text-secondary)'; e.currentTarget.style.background = 'transparent' } }}
            >
              {tab === 'properties' ? 'Özellikler' : 'XML Ağacı'}
            </button>
          )
        })}
      </div>

      {/* Tab content */}
      {rightPanelTab === 'properties' ? <PropertiesContent /> : <XmlTreeExplorer />}
    </aside>
  )
}
