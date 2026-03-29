import {
  ArrowLeft,
  Upload,
  Download,
  Save,
  Printer,
  CheckCircle2,
  TriangleAlert,
  FileCode2,
  FileText,
} from 'lucide-react'

interface Props {
  templateName: string
  xsltValid: boolean | null
  xmlValid: boolean | null
  previewLoading: boolean
  lastMs: number | null
  hasXslt: boolean
  hasXml: boolean
  isDirty: boolean
  onBack: () => void
  onUploadXslt: (e: React.ChangeEvent<HTMLInputElement>) => void
  onUploadXml: (e: React.ChangeEvent<HTMLInputElement>) => void
  onDownload: () => void
  onSave: () => void
  onPrint: () => void
}

export default function XsltEditorToolbar({
  templateName,
  xsltValid,
  xmlValid,
  previewLoading,
  lastMs,
  hasXslt,
  hasXml,
  isDirty,
  onBack,
  onUploadXslt,
  onUploadXml,
  onDownload,
  onSave,
  onPrint,
}: Props) {
  const btnBase = 'flex items-center gap-1.5 text-sm border border-gray-600 rounded px-3 py-1.5 hover:bg-gray-800 transition-colors'

  return (
    <div className="h-12 border-b border-gray-700 bg-gray-900 flex items-center gap-2 px-4 flex-shrink-0">
      <button onClick={onBack} className="flex items-center gap-1.5 text-sm text-gray-400 hover:text-gray-200 transition-colors mr-1" title="Geri dön">
        <ArrowLeft size={15} />
      </button>

      <span className="text-base font-medium text-gray-200 flex-1 truncate">
        {templateName || 'XSLT Editör'}
        {isDirty && <span className="text-gray-500 ml-1">*</span>}
      </span>

      {/* Loading / timing */}
      {previewLoading && (
        <span className="text-sm text-blue-400 flex items-center gap-1">
          <span className="inline-block w-4 h-4 border-2 border-blue-400 border-t-transparent rounded-full animate-spin" />
          Derleniyor
        </span>
      )}
      {!previewLoading && lastMs !== null && <span className="text-sm text-gray-500">{lastMs} ms</span>}

      {/* Validation status */}
      {xsltValid !== null && (
        <span className={`flex items-center gap-1 text-sm ${xsltValid ? 'text-green-400' : 'text-red-400'}`} title={xsltValid ? 'XSLT geçerli' : 'XSLT hatası'}>
          {xsltValid ? <CheckCircle2 size={15} /> : <TriangleAlert size={15} />}
          <span className="hidden sm:inline">XSLT</span>
        </span>
      )}
      {xmlValid !== null && (
        <span className={`flex items-center gap-1 text-sm ${xmlValid ? 'text-green-400' : 'text-red-400'}`} title={xmlValid ? 'XML geçerli' : 'XML hatası'}>
          {xmlValid ? <CheckCircle2 size={15} /> : <TriangleAlert size={15} />}
          <span className="hidden sm:inline">XML</span>
        </span>
      )}

      {/* Upload buttons */}
      <label className={`${btnBase} text-gray-400 cursor-pointer`} title="XSLT dosyası yükle">
        <FileCode2 size={15} />
        <span className="hidden sm:inline">XSLT</span>
        <Upload size={13} />
        <input type="file" accept=".xsl,.xslt" className="hidden" onChange={onUploadXslt} />
      </label>

      <label className={`${btnBase} text-gray-400 cursor-pointer`} title="XML dosyası yükle">
        <FileText size={15} />
        <span className="hidden sm:inline">XML</span>
        <Upload size={13} />
        <input type="file" accept=".xml,text/xml,application/xml" className="hidden" onChange={onUploadXml} />
      </label>

      {/* Download */}
      <button onClick={onDownload} disabled={!hasXslt} className={`${btnBase} text-gray-400 disabled:opacity-30`} title="XSLT indir">
        <Download size={15} />
        <span className="hidden sm:inline">İndir</span>
      </button>

      {/* Save */}
      <button onClick={onSave} disabled={!hasXslt} className="flex items-center gap-1.5 text-sm font-medium text-white bg-blue-600 hover:bg-blue-700 rounded px-3 py-1.5 disabled:opacity-30 transition-colors" title="Kaydet">
        <Save size={15} />
        <span className="hidden sm:inline">Kaydet</span>
      </button>

      {/* Print */}
      <button onClick={onPrint} disabled={!hasXml || !hasXslt} className={`${btnBase} text-gray-400 disabled:opacity-30`} title="Yazdır">
        <Printer size={16} />
      </button>
    </div>
  )
}
