import { CheckCircle2, AlertCircle, AlertTriangle, Info, X } from 'lucide-react'
import { useToastStore, type ToastVariant } from '../../store/toastStore'

const VARIANT_STYLES: Record<ToastVariant, { bg: string; border: string; text: string; Icon: typeof Info }> = {
  info: { bg: 'bg-gray-900/95', border: 'border-blue-700', text: 'text-blue-300', Icon: Info },
  success: { bg: 'bg-gray-900/95', border: 'border-emerald-700', text: 'text-emerald-300', Icon: CheckCircle2 },
  warning: { bg: 'bg-gray-900/95', border: 'border-amber-700', text: 'text-amber-300', Icon: AlertTriangle },
  error: { bg: 'bg-gray-900/95', border: 'border-red-700', text: 'text-red-300', Icon: AlertCircle },
}

export default function ToastContainer() {
  const toasts = useToastStore(s => s.toasts)
  const dismiss = useToastStore(s => s.dismiss)

  if (toasts.length === 0) return null

  return (
    <div className="fixed bottom-4 right-4 z-[100] flex flex-col gap-2 w-[360px] max-w-[calc(100vw-2rem)] pointer-events-none">
      {toasts.map(t => {
        const style = VARIANT_STYLES[t.variant]
        return (
          <div
            key={t.id}
            role="status"
            aria-live="polite"
            className={`pointer-events-auto rounded-lg border ${style.border} ${style.bg} shadow-xl backdrop-blur-sm px-3 py-2.5 flex items-start gap-2 text-gray-100 animate-in slide-in-from-right-4`}
          >
            <style.Icon size={16} className={`flex-shrink-0 mt-0.5 ${style.text}`} />
            <div className="flex-1 min-w-0">
              {t.title && (
                <div className={`text-xs font-semibold ${style.text}`}>{t.title}</div>
              )}
              <div className="text-xs text-gray-200 break-words whitespace-pre-wrap">{t.message}</div>
            </div>
            <button
              onClick={() => dismiss(t.id)}
              className="text-gray-500 hover:text-white flex-shrink-0"
              aria-label="Bildirimi kapat"
            >
              <X size={14} />
            </button>
          </div>
        )
      })}
    </div>
  )
}
