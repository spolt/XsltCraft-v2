import { useState, useRef, useEffect } from 'react'
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
  Timer,
  Keyboard,
} from 'lucide-react'
import type { PreviewTimings } from '../../services/previewService'

interface Props {
  templateName: string
  xsltValid: boolean | null
  xmlValid: boolean | null
  previewLoading: boolean
  lastMs: number | null
  lastTimings: PreviewTimings | null
  hasXslt: boolean
  hasXml: boolean
  isDirty: boolean
  onBack: () => void
  onUploadXslt: (e: React.ChangeEvent<HTMLInputElement>) => void
  onUploadXml: (e: React.ChangeEvent<HTMLInputElement>) => void
  onDownload: () => void
  onSave: () => void
  onPrint: () => void
  onShowShortcuts: () => void
}

function TimingPopover({ timings, onClose }: { timings: PreviewTimings; onClose: () => void }) {
  const ref = useRef<HTMLDivElement>(null)

  useEffect(() => {
    function handleClick(e: MouseEvent) {
      if (ref.current && !ref.current.contains(e.target as Node)) onClose()
    }
    document.addEventListener('mousedown', handleClick)
    return () => document.removeEventListener('mousedown', handleClick)
  }, [onClose])

  const rows: [string, number][] = [
    ['XSLT Derleme', timings.compileMs],
    ['XML Ayrıştırma', timings.parseMs],
    ['Dönüşüm', timings.transformMs],
    ['Serileştirme', timings.serializeMs],
  ]
  const total = rows.reduce((s, [, v]) => s + v, 0)

  return (
    <div
      ref={ref}
      className="absolute top-full mt-1 right-0 z-50 bg-gray-800 border border-gray-600 rounded-lg shadow-xl p-3 min-w-[200px]"
    >
      <p className="text-[10px] uppercase tracking-wide text-gray-400 mb-2 font-medium">Önizleme Süresi</p>
      <table className="w-full text-xs">
        <tbody>
          {rows.map(([label, ms]) => (
            <tr key={label}>
              <td className="py-0.5 pr-4 text-gray-300">{label}</td>
              <td className="py-0.5 text-right font-mono text-gray-200">{ms} ms</td>
            </tr>
          ))}
          <tr className="border-t border-gray-600 mt-1">
            <td className="pt-1.5 text-gray-400 font-medium">Toplam</td>
            <td className="pt-1.5 text-right font-mono text-white font-semibold">{total} ms</td>
          </tr>
        </tbody>
      </table>
    </div>
  )
}

export default function XsltEditorToolbar({
  templateName,
  xsltValid,
  xmlValid,
  previewLoading,
  lastMs,
  lastTimings,
  hasXslt,
  hasXml,
  isDirty,
  onBack,
  onUploadXslt,
  onUploadXml,
  onDownload,
  onSave,
  onPrint,
  onShowShortcuts,
}: Props) {
  const btnBase = 'flex items-center gap-1.5 text-sm border border-gray-600 rounded px-3 py-1.5 hover:bg-gray-800 transition-colors'
  const [showTimingPopover, setShowTimingPopover] = useState(false)

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
      {!previewLoading && lastMs !== null && (
        <div className="relative">
          <button
            onClick={() => lastTimings && setShowTimingPopover(s => !s)}
            className={`flex items-center gap-1 text-sm text-gray-500 rounded px-1.5 py-0.5 transition-colors ${
              lastTimings ? 'hover:bg-gray-800 hover:text-gray-300 cursor-pointer' : 'cursor-default'
            }`}
            title={lastTimings ? 'Ayrıntılı süre kırılımı' : undefined}
          >
            {lastTimings && <Timer size={13} />}
            {lastMs} ms
          </button>
          {showTimingPopover && lastTimings && (
            <TimingPopover timings={lastTimings} onClose={() => setShowTimingPopover(false)} />
          )}
        </div>
      )}

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

      {/* Shortcuts */}
      <button onClick={onShowShortcuts} className={`${btnBase} text-gray-400`} title="Klavye Kısayolları (F1)">
        <Keyboard size={15} />
      </button>
    </div>
  )
}
