import { X } from 'lucide-react'

interface TemplatePreviewPanelProps {
  name: string
  html: string
  loading: boolean
  actionLabel: string
  onAction: () => void
  onClose: () => void
}

export default function TemplatePreviewPanel({
  name,
  html,
  loading,
  actionLabel,
  onAction,
  onClose,
}: TemplatePreviewPanelProps) {
  return (
    <div className="flex-1 flex flex-col overflow-hidden">
      <div className="h-11 flex items-center gap-3 px-4 bg-white border-b border-gray-200 flex-shrink-0">
        <div className="flex-1 min-w-0">
          <span className="text-sm font-medium text-gray-800 truncate block">{name}</span>
        </div>
        {loading && (
          <span className="flex items-center gap-1.5 text-xs text-blue-500 flex-shrink-0">
            <span className="inline-block w-3 h-3 border-2 border-blue-400 border-t-transparent rounded-full animate-spin" />
            Yükleniyor
          </span>
        )}
        <button
          onClick={onAction}
          disabled={loading}
          className="flex-shrink-0 text-sm font-medium text-white bg-blue-600 hover:bg-blue-700 disabled:opacity-50 disabled:cursor-not-allowed rounded-lg px-4 py-1.5 transition-colors"
        >
          {actionLabel}
        </button>
        <button
          onClick={onClose}
          className="flex-shrink-0 text-gray-400 hover:text-gray-600 transition-colors"
          title="Kapat"
        >
          <X size={16} />
        </button>
      </div>
      <div className="flex-1 overflow-auto bg-gray-300 flex justify-center">
        <iframe
          srcDoc={html || '<html><body style="background:#d1d5db;margin:0"></body></html>'}
          sandbox="allow-same-origin allow-scripts allow-modals"
          className="border-none bg-white shadow-lg"
          style={{ width: 860, minHeight: '100%', height: 'max(100%, 1200px)' }}
          title="Şablon Önizleme"
        />
      </div>
    </div>
  )
}
