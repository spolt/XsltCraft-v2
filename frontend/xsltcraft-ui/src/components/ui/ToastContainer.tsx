import { CheckCircle2, AlertCircle, AlertTriangle, Info, X } from 'lucide-react'
import { useToastStore, type ToastVariant } from '../../store/toastStore'
import { fnStyle, type FnColor } from '../../theme/uiThemes'

const VARIANT_STYLES: Record<ToastVariant, { fn: FnColor; Icon: typeof Info }> = {
  info: { fn: 'blue', Icon: Info },
  success: { fn: 'green', Icon: CheckCircle2 },
  warning: { fn: 'orange', Icon: AlertTriangle },
  error: { fn: 'red', Icon: AlertCircle },
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
            style={fnStyle(style.fn)}
            className="pointer-events-auto rounded-lg border border-ui-line border-l-4 border-l-(--fn) bg-ui-card shadow-[var(--ui-shadow-pop)] px-3 py-2.5 flex items-start gap-2 text-tx"
          >
            <style.Icon size={16} className="flex-shrink-0 mt-0.5 text-(--fn)" aria-hidden="true" />
            <div className="flex-1 min-w-0">
              {t.title && (
                <div className="text-xs font-semibold text-(--fn-ink)">{t.title}</div>
              )}
              <div className="text-xs text-tx-2 break-words whitespace-pre-wrap">{t.message}</div>
            </div>
            <button
              onClick={() => dismiss(t.id)}
              className="xc-focus rounded text-tx-subtle hover:text-tx flex-shrink-0"
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
