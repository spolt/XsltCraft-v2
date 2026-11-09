import { useEffect, useRef, useState } from 'react'
import { ArrowRight, Sparkles, X } from 'lucide-react'

/** Band bu süre görünür kaldıktan sonra kendiliğinden kapanır (üzerindeyken sayaç durur). */
const AUTO_HIDE_MS = 10_000
/** Açılış/kapanış animasyon süresi — `duration-300` ile aynı olmalı. */
const ANIMATION_MS = 300

interface EasyEditHintBandProps {
  onSwitch: () => void
  /** X ile kapatma — kalıcı tercih. */
  onDismiss: () => void
  /** Süre dolunca kapanma — yalnız bu açılış için. */
  onAutoHide: () => void
  /** Geçiş hazırlanıyor (analiz sürüyor). */
  busy?: boolean
}

/** XSLT Editör'de, Kolay düzenle ile yapılabilecek işler için öneri bandı; aşağı kayarak açılır, 10 sn sonra yukarı kayarak kapanır. */
export default function EasyEditHintBand({ onSwitch, onDismiss, onAutoHide, busy = false }: EasyEditHintBandProps) {
  const [open, setOpen] = useState(false)
  const [paused, setPaused] = useState(false)
  const remainingRef = useRef(AUTO_HIDE_MS)
  const closingRef = useRef(false)
  const closeTimerRef = useRef<number | undefined>(undefined)

  // İlk karede kapalı çizilir, sonraki karede açılır → geçiş tetiklenir.
  useEffect(() => {
    const raf = requestAnimationFrame(() => setOpen(true))
    return () => {
      cancelAnimationFrame(raf)
      window.clearTimeout(closeTimerRef.current)
    }
  }, [])

  const close = (after: () => void) => {
    if (closingRef.current) return
    closingRef.current = true
    setOpen(false)
    closeTimerRef.current = window.setTimeout(after, ANIMATION_MS)
  }

  const onAutoHideRef = useRef(onAutoHide)
  useEffect(() => { onAutoHideRef.current = onAutoHide })

  useEffect(() => {
    if (!open || paused || busy) return
    const startedAt = Date.now()
    const timer = window.setTimeout(() => close(() => onAutoHideRef.current()), remainingRef.current)
    return () => {
      window.clearTimeout(timer)
      remainingRef.current = Math.max(0, remainingRef.current - (Date.now() - startedAt))
    }
  }, [open, paused, busy])

  return (
    <div
      role="note"
      onMouseEnter={() => setPaused(true)}
      onMouseLeave={() => setPaused(false)}
      onFocus={() => setPaused(true)}
      onBlur={(e) => { if (!e.currentTarget.contains(e.relatedTarget as Node | null)) setPaused(false) }}
      className={`grid flex-none transition-[grid-template-rows,opacity] duration-300 ease-out motion-reduce:transition-none ${open ? 'grid-rows-[1fr] opacity-100' : 'grid-rows-[0fr] opacity-0'}`}
    >
      <div className="min-h-0 overflow-hidden">
        <div
          className={`flex items-center gap-2.5 border-b border-fn-blue-line bg-fn-blue-tint px-4 py-2 text-[12.5px] text-fn-blue-ink transition-transform duration-300 ease-out motion-reduce:transition-none ${open ? 'translate-y-0' : '-translate-y-full'}`}
        >
          <Sparkles size={14} aria-hidden="true" className="flex-none" />
          <p className="m-0 min-w-0 flex-1">
            Bu dosyada logo, imza, IBAN ve not alanları var. Bunlar için kod yazman gerekmez.
          </p>
          <button
            type="button"
            onClick={onSwitch}
            disabled={busy}
            className="xc-focus inline-flex h-[28px] flex-none items-center gap-1.5 rounded-md bg-ui-accent px-3 text-[12px] font-semibold text-ui-accent-fg hover:brightness-110 disabled:cursor-wait disabled:opacity-60"
          >
            {busy ? 'Hazırlanıyor…' : 'Kolay düzenle\'ye geç'}
            {!busy && <ArrowRight size={13} aria-hidden="true" />}
          </button>
          <button
            type="button"
            onClick={() => close(onDismiss)}
            aria-label="Öneriyi kapat"
            title="Öneriyi kapat"
            className="xc-focus inline-flex h-[28px] w-[28px] flex-none items-center justify-center rounded-md hover:bg-ui-hover"
          >
            <X size={14} aria-hidden="true" />
          </button>
        </div>
      </div>
    </div>
  )
}
