import { useEffect, useRef, useState } from 'react'
import { useEditorStore } from '../../store/editorStore'
import { useXmlStore } from '../../store/xmlStore'
import { uploadAsset, getAssetUrl, deleteAsset } from '../../services/assetService'
import { evaluateXPathValue } from '../../utils/xmlUtils'
import XmlTreeExplorer from './XmlTreeExplorer'
import type { Block, BlockConfig, BlockAlignment, BlockWidth } from '../../types/blocks'

// ── Generic field helpers ─────────────────────────────────────────────────────

function Field({ label, children }: { label: string; children: React.ReactNode }) {
  return (
    <div className="flex flex-col gap-1">
      <label className="text-xs font-medium text-gray-500 uppercase tracking-wide">{label}</label>
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
      className="w-full text-sm border border-gray-200 rounded px-2 py-1.5 outline-none focus:border-blue-400 focus:ring-1 focus:ring-blue-100"
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
    <div className="flex flex-col gap-0.5">
      <div className="flex">
        <input
          type="text"
          value={value}
          onChange={(e) => onChange(e.target.value)}
          placeholder={placeholder}
          className="flex-1 min-w-0 text-sm border border-gray-200 rounded-l px-2 py-1.5 outline-none focus:border-blue-400 focus:ring-1 focus:ring-blue-100"
        />
        {activeXml && (
          <button
            type="button"
            title="XML Ağacından seç"
            onClick={() => startXPathSelection(onChange)}
            className={`px-2 border border-l-0 border-gray-200 rounded-r text-xs transition-colors ${
              isSelecting
                ? 'bg-blue-100 border-blue-400 text-blue-600'
                : 'text-gray-400 hover:bg-gray-50 hover:text-blue-600'
            }`}
          >
            ⊕
          </button>
        )}
      </div>
      {resolved && (
        <span
          className="text-xs text-emerald-600 px-1 truncate"
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
      className="w-full text-sm border border-gray-200 rounded px-2 py-1.5 outline-none focus:border-blue-400 bg-white"
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
    <div className="flex items-center gap-1.5">
      <label className="relative flex-shrink-0 cursor-pointer">
        <input
          type="color"
          value={pickerValue}
          onChange={(e) => onChange(e.target.value)}
          className="absolute inset-0 opacity-0 w-full h-full cursor-pointer"
        />
        <span
          className="block w-7 h-7 rounded border border-gray-300 shadow-sm"
          style={{ background: pickerValue }}
        />
      </label>
      <input
        type="text"
        value={value}
        onChange={(e) => onChange(e.target.value)}
        placeholder={placeholder ?? '#000000'}
        className="flex-1 text-sm border border-gray-200 rounded px-2 py-1.5 outline-none focus:border-blue-400 focus:ring-1 focus:ring-blue-100 font-mono"
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
    <label className="flex items-center gap-2 cursor-pointer">
      <input
        type="checkbox"
        checked={checked}
        onChange={(e) => onChange(e.target.checked)}
        className="w-4 h-4 rounded border-gray-300 text-blue-600"
      />
      <span className="text-sm text-gray-700">{label}</span>
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

  return (
    <div className="flex flex-col gap-2 pb-3 border-b border-gray-100">
      <div>
        <p className="text-xs font-medium text-gray-500 uppercase tracking-wide mb-1">Genişlik</p>
        <div className="flex gap-1">
          {widths.map(({ value, label }) => (
            <button
              key={value}
              onClick={() => updateBlockLayout(block.id, { width: value })}
              className={`flex-1 text-xs py-1 rounded border transition-colors ${
                currentWidth === value
                  ? 'border-blue-500 bg-blue-50 text-blue-700 font-medium'
                  : 'border-gray-200 text-gray-600 hover:border-gray-300 hover:bg-gray-50'
              }`}
            >
              {label}
            </button>
          ))}
        </div>
      </div>
      <div>
        <p className="text-xs font-medium text-gray-500 uppercase tracking-wide mb-1">Hizalama</p>
        <div className="flex gap-1">
          {alignments.map(({ value, label }) => (
            <button
              key={value}
              onClick={() => updateBlockLayout(block.id, { alignment: value })}
              className={`flex-1 text-sm py-1 rounded border transition-colors ${
                currentAlignment === value
                  ? 'border-blue-500 bg-blue-50 text-blue-700'
                  : 'border-gray-200 text-gray-600 hover:border-gray-300 hover:bg-gray-50'
              }`}
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
  return (
    <>
      <Field label="Her not için (XPath)">
        <XPathInput value={config.iterateOver} onChange={(v) => update({ iterateOver: v })} placeholder="//cbc:Note" />
      </Field>
      <Field label="Önek">
        <TextInput value={config.prefix ?? ''} onChange={(v) => update({ prefix: v })} placeholder="Not: " />
      </Field>
    </>
  )
}

function BankInfoPanel({ config, update }: { config: Config<'BankInfo'>; update: UpdateFn }) {
  return (
    <>
      <Field label="Banka Adı XPath">
        <XPathInput value={config.bankNameXpath ?? ''} onChange={(v) => update({ bankNameXpath: v })} placeholder="//cbc:Name" />
      </Field>
      <Field label="IBAN XPath">
        <XPathInput value={config.ibanXpath ?? ''} onChange={(v) => update({ ibanXpath: v })} placeholder="//cbc:ID" />
      </Field>
      <Field label="Ödeme Koşulları XPath">
        <XPathInput value={config.paymentTermsXpath ?? ''} onChange={(v) => update({ paymentTermsXpath: v })} placeholder="//cac:PaymentTerms/cbc:Note" />
      </Field>
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

// ── Properties content ────────────────────────────────────────────────────────

function PropertiesContent() {
  const selectedBlockId = useEditorStore((s) => s.selectedBlockId)
  const blocks = useEditorStore((s) => s.blocks)
  const updateBlockConfig = useEditorStore((s) => s.updateBlockConfig)

  if (!selectedBlockId) {
    return (
      <div className="flex-1 flex items-center justify-center">
        <p className="text-xs text-gray-400 text-center px-4">Düzenlemek için bir blok seç</p>
      </div>
    )
  }

  const block = blocks[selectedBlockId]
  if (!block) return null

  const update: UpdateFn = (patch) => updateBlockConfig(selectedBlockId, patch)
  const cfg = block.config as never

  return (
    <div className="flex-1 overflow-y-auto flex flex-col">
      <div className="px-3 py-2 border-b border-gray-100 flex-shrink-0">
        <p className="text-sm font-medium text-gray-700">{block.type}</p>
      </div>
      <div className="p-3 flex flex-col gap-3 flex-1">
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
      className="flex-shrink-0 border-l border-gray-200 bg-white flex flex-col overflow-hidden transition-all duration-200"
      style={{ width: isXmlTab ? 420 : 256 }}
    >
      {/* Tab bar */}
      <div className="flex border-b border-gray-200 flex-shrink-0">
        {(['properties', 'xml'] as const).map((tab) => (
          <button
            key={tab}
            onClick={() => setRightPanelTab(tab)}
            className={`flex-1 py-2 text-xs font-medium transition-colors ${
              rightPanelTab === tab
                ? 'text-blue-700 border-b-2 border-blue-600 bg-blue-50'
                : 'text-gray-500 hover:text-gray-700 hover:bg-gray-50'
            }`}
          >
            {tab === 'properties' ? 'Özellikler' : 'XML Ağacı'}
          </button>
        ))}
      </div>

      {/* Tab content */}
      {rightPanelTab === 'properties' ? <PropertiesContent /> : <XmlTreeExplorer />}
    </aside>
  )
}
