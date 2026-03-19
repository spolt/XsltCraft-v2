import { useEditorStore } from '../../store/editorStore'
import type { Block, BlockConfig } from '../../types/blocks'

// ---- generic field helpers ----

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

// ---- Block-type-specific sub-panels ----

type Config<T extends Block['type']> = Extract<BlockConfig, { type: T }>['config']
type UpdateFn = (patch: Partial<BlockConfig['config']>) => void

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
            <TextInput value={config.binding?.xpath ?? ''} onChange={(v) => update({ binding: { ...config.binding, xpath: v, fallback: config.binding?.fallback } })} placeholder="//cbc:Name" />
          </Field>
          <Field label="Fallback">
            <TextInput value={config.binding?.fallback ?? ''} onChange={(v) => update({ binding: { ...config.binding, xpath: config.binding?.xpath ?? '', fallback: v } })} placeholder="—" />
          </Field>
        </>
      )}
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
          <TextInput value={config.binding?.xpath ?? ''} onChange={(v) => update({ binding: { xpath: v } })} placeholder="//cbc:..." />
        </Field>
      )}
    </>
  )
}

function ParagraphPanel({ config, update }: { config: Config<'Paragraph'>; update: UpdateFn }) {
  function updateLine(index: number, patch: object) {
    const lines = config.lines.map((l, i) => (i === index ? { ...l, ...patch } : l))
    update({ lines })
  }

  function addLine() {
    update({ lines: [...config.lines, { isStatic: true, content: '' }] })
  }

  function removeLine(index: number) {
    update({ lines: config.lines.filter((_, i) => i !== index) })
  }

  return (
    <div className="flex flex-col gap-2">
      {config.lines.map((line, i) => (
        <div key={i} className="border border-gray-100 rounded-md p-2 flex flex-col gap-1 bg-gray-50">
          <div className="flex items-center justify-between">
            <span className="text-xs text-gray-400">Satır {i + 1}</span>
            <button onClick={() => removeLine(i)} className="text-xs text-red-400 hover:text-red-600">✕</button>
          </div>
          <Checkbox label="Statik" checked={line.isStatic} onChange={(v) => updateLine(i, { isStatic: v })} />
          {line.isStatic ? (
            <TextInput value={line.content ?? ''} onChange={(v) => updateLine(i, { content: v })} placeholder="Metin..." />
          ) : (
            <TextInput value={line.xpath ?? ''} onChange={(v) => updateLine(i, { xpath: v })} placeholder="XPath..." />
          )}
        </div>
      ))}
      <button onClick={addLine} className="text-xs text-blue-600 hover:text-blue-800 text-left">+ Satır ekle</button>
    </div>
  )
}

function TablePanel({ config, update }: { config: Config<'Table'>; update: UpdateFn }) {
  function updateColumn(index: number, patch: object) {
    const columns = config.columns.map((c, i) => (i === index ? { ...c, ...patch } : c))
    update({ columns })
  }

  function addColumn() {
    update({ columns: [...config.columns, { header: '', xpath: '', width: '' }] })
  }

  function removeColumn(index: number) {
    update({ columns: config.columns.filter((_, i) => i !== index) })
  }

  return (
    <>
      <Field label="Her satır için (XPath)">
        <TextInput value={config.iterateOver} onChange={(v) => update({ iterateOver: v })} placeholder="//cac:InvoiceLine" />
      </Field>
      <Checkbox label="Başlık satırı göster" checked={config.showHeader} onChange={(v) => update({ showHeader: v })} />
      <Field label="Başlık arkaplanı">
        <TextInput value={config.headerBackgroundColor ?? '#E0E0E0'} onChange={(v) => update({ headerBackgroundColor: v })} placeholder="#E0E0E0" />
      </Field>
      <Field label="Sütunlar">
        <div className="flex flex-col gap-2 mt-1">
          {config.columns.map((col, i) => (
            <div key={i} className="border border-gray-100 rounded p-2 bg-gray-50 flex flex-col gap-1">
              <div className="flex justify-between items-center">
                <span className="text-xs text-gray-400">Sütun {i + 1}</span>
                <button onClick={() => removeColumn(i)} className="text-xs text-red-400 hover:text-red-600">✕</button>
              </div>
              <TextInput value={col.header} onChange={(v) => updateColumn(i, { header: v })} placeholder="Başlık" />
              <TextInput value={col.xpath} onChange={(v) => updateColumn(i, { xpath: v })} placeholder="XPath (cbc:ID)" />
              <TextInput value={col.width ?? ''} onChange={(v) => updateColumn(i, { width: v })} placeholder="Genişlik (10%)" />
            </div>
          ))}
          <button onClick={addColumn} className="text-xs text-blue-600 hover:text-blue-800 text-left">+ Sütun ekle</button>
        </div>
      </Field>
    </>
  )
}

function ForEachPanel({ config, update }: { config: Config<'ForEach'>; update: UpdateFn }) {
  return (
    <Field label="Her eleman için (XPath)">
      <TextInput value={config.iterateOver} onChange={(v) => update({ iterateOver: v })} placeholder="//cac:InvoiceLine" />
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
        <TextInput value={config.condition.xpath} onChange={(v) => update({ condition: { ...config.condition, xpath: v } })} placeholder="//cbc:InvoiceTypeCode" />
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
          <TextInput value={config.condition.value ?? ''} onChange={(v) => update({ condition: { ...config.condition, value: v } })} placeholder="TEMELFATURA" />
        </Field>
      )}
    </>
  )
}

function ImagePanel({ config, update }: { config: Config<'Image'>; update: UpdateFn }) {
  return (
    <>
      <Field label="Varlık Tipi">
        <Select value={config.assetType} onChange={(v) => update({ assetType: v })} options={[
          { value: 'logo', label: 'Logo' },
          { value: 'signature', label: 'İmza' },
          { value: 'other', label: 'Diğer' },
        ]} />
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
    <Field label="Satırlar">
      <div className="flex flex-col gap-2">
        {config.rows.map((row, i) => (
          <div key={i} className="border border-gray-100 rounded p-2 bg-gray-50 flex flex-col gap-1">
            <div className="flex justify-between">
              <span className="text-xs text-gray-400">Satır {i + 1}</span>
              <button onClick={() => update({ rows: config.rows.filter((_, idx) => idx !== i) })} className="text-xs text-red-400">✕</button>
            </div>
            <TextInput value={row.label} onChange={(v) => updateRow(i, { label: v })} placeholder="Etiket" />
            <TextInput value={row.xpath} onChange={(v) => updateRow(i, { xpath: v })} placeholder="XPath" />
          </div>
        ))}
        <button onClick={() => update({ rows: [...config.rows, { label: '', xpath: '' }] })} className="text-xs text-blue-600">+ Satır ekle</button>
      </div>
    </Field>
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
      <Field label="Satırlar">
        <div className="flex flex-col gap-2">
          {config.rows.map((row, i) => (
            <div key={i} className="border border-gray-100 rounded p-2 bg-gray-50 flex flex-col gap-1">
              <div className="flex justify-between">
                <span className="text-xs text-gray-400">Satır {i + 1}</span>
                <button onClick={() => update({ rows: config.rows.filter((_, idx) => idx !== i) })} className="text-xs text-red-400">✕</button>
              </div>
              <TextInput value={row.label} onChange={(v) => updateRow(i, { label: v })} placeholder="Etiket" />
              <TextInput value={row.xpath} onChange={(v) => updateRow(i, { xpath: v })} placeholder="XPath" />
              <Checkbox label="Vurgula" checked={row.highlight ?? false} onChange={(v) => updateRow(i, { highlight: v })} />
            </div>
          ))}
          <button onClick={() => update({ rows: [...config.rows, { label: '', xpath: '' }] })} className="text-xs text-blue-600">+ Satır ekle</button>
        </div>
      </Field>
    </>
  )
}

function NotesPanel({ config, update }: { config: Config<'Notes'>; update: UpdateFn }) {
  return (
    <>
      <Field label="Her not için (XPath)">
        <TextInput value={config.iterateOver} onChange={(v) => update({ iterateOver: v })} placeholder="//cbc:Note" />
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
      <Field label="Banka Adı XPath"><TextInput value={config.bankNameXpath ?? ''} onChange={(v) => update({ bankNameXpath: v })} placeholder="//cbc:Name" /></Field>
      <Field label="IBAN XPath"><TextInput value={config.ibanXpath ?? ''} onChange={(v) => update({ ibanXpath: v })} placeholder="//cbc:ID" /></Field>
      <Field label="Ödeme Koşulları XPath"><TextInput value={config.paymentTermsXpath ?? ''} onChange={(v) => update({ paymentTermsXpath: v })} placeholder="//cac:PaymentTerms/cbc:Note" /></Field>
    </>
  )
}

function ETTNPanel({ config, update }: { config: Config<'ETTN'>; update: UpdateFn }) {
  return (
    <>
      <Field label="ETTN XPath"><TextInput value={config.ettnXpath} onChange={(v) => update({ ettnXpath: v })} placeholder="//cbc:UUID" /></Field>
      <Checkbox label="QR Kodu göster" checked={config.showQR} onChange={(v) => update({ showQR: v })} />
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
      <Field label="Renk"><TextInput value={config.color ?? '#CCCCCC'} onChange={(v) => update({ color: v })} placeholder="#CCCCCC" /></Field>
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

// ---- Root PropertyPanel ----

export default function PropertyPanel() {
  const selectedBlockId = useEditorStore((s) => s.selectedBlockId)
  const blocks = useEditorStore((s) => s.blocks)
  const updateBlockConfig = useEditorStore((s) => s.updateBlockConfig)

  if (!selectedBlockId) {
    return (
      <aside className="w-64 flex-shrink-0 border-l border-gray-200 bg-white flex items-center justify-center">
        <p className="text-xs text-gray-400 text-center px-4">Düzenlemek için bir blok seç</p>
      </aside>
    )
  }

  const block = blocks[selectedBlockId]
  if (!block) return null

  const update: UpdateFn = (patch) => updateBlockConfig(selectedBlockId, patch)
  const cfg = block.config as never

  return (
    <aside className="w-64 flex-shrink-0 border-l border-gray-200 bg-white overflow-y-auto flex flex-col">
      <div className="px-3 py-3 border-b border-gray-100">
        <h2 className="text-xs font-semibold text-gray-500 uppercase tracking-wide">Özellikler</h2>
        <p className="text-sm font-medium text-gray-700 mt-0.5">{block.type}</p>
      </div>
      <div className="p-3 flex flex-col gap-3">
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
      </div>
    </aside>
  )
}
