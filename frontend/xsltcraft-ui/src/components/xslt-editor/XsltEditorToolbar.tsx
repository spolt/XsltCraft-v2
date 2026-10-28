import type { CSSProperties, ReactNode } from 'react'
import {
  AlignLeft,
  MessageSquareCode,
  TriangleAlert,
  ShieldCheck,
  Maximize2,
  Minimize2,
  Printer,
  Keyboard,
  Sparkles,
  Eye,
} from 'lucide-react'
import LayoutSwitcher from './LayoutSwitcher'
import type { EditorLayoutId } from '../../store/uiPrefsStore'

interface Props {
  onFormat: () => void
  onToggleComment: () => void
  /** Problemler paneli */
  showProblems: boolean
  onToggleProblems: () => void
  errorCount: number
  warningCount: number
  /** UBL-TR iş kuralları */
  onValidateUblTr: () => void
  ublTrDisabled: boolean
  ublTrErrorCount: number | null
  /** Düzen */
  layout: EditorLayoutId
  onLayoutChange: (id: EditorLayoutId) => void
  maximized: boolean
  onToggleMaximize: () => void
  /** Yalnız editör modunda mini önizlemeyi açar. */
  onShowMini?: () => void
  onPrint: () => void
  onShowShortcuts: () => void
  /** AI Asistan — verilmezse buton gösterilmez. */
  aiOpen?: boolean
  onToggleAi?: () => void
}

// Araç çubuğu editör bölmesinin genişliğine göre (container query) sıkışır: dar bölmede
// etiketler gizlenir (ekran okuyucu adı kalır), çubuk iki satıra kırılmaz.
const btn =
  'xc-focus h-[26px] px-2 flex flex-none items-center gap-1.5 whitespace-nowrap rounded-md text-[12px] text-ed-text transition-colors hover:bg-ed-active disabled:opacity-40 disabled:hover:bg-transparent'
const iconBtn =
  'xc-focus h-[26px] w-[26px] flex flex-none items-center justify-center rounded-md text-ed-muted transition-colors hover:bg-ed-active hover:text-ed-text disabled:opacity-40'
/** Geniş bölmede görünen, darda yalnız ekran okuyucuya kalan etiket. */
const wideLabel = 'sr-only @min-[760px]:not-sr-only'

function Badge({ tone, children }: { tone: 'error' | 'warn'; children: ReactNode }) {
  return (
    <span
      className={`min-w-[18px] rounded-full px-1.5 text-center text-[11px] font-bold leading-[17px] ${
        tone === 'error' ? 'bg-fn-red text-on-fn' : 'bg-ed-warn text-ed-bg'
      }`}
    >
      {children}
    </span>
  )
}

/** Editör alanının araç çubuğu (--ed-* paleti). Tam ekranda da görünür (editör katmanının içinde). */
export default function XsltEditorToolbar({
  onFormat,
  onToggleComment,
  showProblems,
  onToggleProblems,
  errorCount,
  warningCount,
  onValidateUblTr,
  ublTrDisabled,
  ublTrErrorCount,
  layout,
  onLayoutChange,
  maximized,
  onToggleMaximize,
  onShowMini,
  onPrint,
  onShowShortcuts,
  aiOpen = false,
  onToggleAi,
}: Props) {
  return (
    <div
      role="toolbar"
      aria-label="Editör araçları"
      style={{ '--xc-focus-ring': 'var(--ed-text)' } as CSSProperties}
      className="@container flex-none border-b border-ed-border bg-ed-panel"
    >
      <div className="flex h-[36px] min-w-0 items-center gap-0.5 overflow-hidden px-2">
      <button type="button" onClick={onFormat} className={btn} title="Belgeyi Biçimlendir (Shift+Alt+F)">
        <AlignLeft size={14} aria-hidden="true" />
        <span className={wideLabel}>Biçimlendir</span>
      </button>
      <button type="button" onClick={onToggleComment} className={btn} title="Açıklama Satırı Yap / Kaldır (Ctrl+Shift+C)">
        <MessageSquareCode size={14} aria-hidden="true" />
        <span className={wideLabel}>Açıklama</span>
      </button>
      <button
        type="button"
        onClick={onToggleProblems}
        aria-pressed={showProblems}
        className={`${btn} ${showProblems ? 'bg-ed-active' : ''}`}
        title="Problemler panelini aç/kapat"
      >
        <TriangleAlert size={14} aria-hidden="true" />
        <span className="sr-only @min-[560px]:not-sr-only">Problemler</span>
        {errorCount > 0 && <Badge tone="error"><span className="sr-only">hata: </span>{errorCount}</Badge>}
        {warningCount > 0 && <Badge tone="warn"><span className="sr-only">uyarı: </span>{warningCount}</Badge>}
      </button>

      <span aria-hidden="true" className="mx-1 h-4 w-px flex-none bg-ed-border" />

      <button
        type="button"
        onClick={onValidateUblTr}
        disabled={ublTrDisabled}
        className={`${btn} border border-ed-border font-semibold`}
        title="UBL-TR iş kurallarını kontrol et"
      >
        <ShieldCheck size={14} aria-hidden="true" />
        UBL-TR<span className="sr-only @min-[620px]:not-sr-only"> kontrol</span>
        {ublTrErrorCount != null && ublTrErrorCount > 0 && (
          <Badge tone="error"><span className="sr-only">hata: </span>{ublTrErrorCount}</Badge>
        )}
      </button>

      <div className="min-w-2 flex-1" />

      <LayoutSwitcher value={layout} onChange={onLayoutChange} />

      <button
        type="button"
        onClick={onToggleMaximize}
        aria-pressed={maximized}
        className={iconBtn}
        title={maximized ? 'Tam ekrandan çık (Ctrl+Shift+F)' : 'Tam ekran editör (Ctrl+Shift+F)'}
        aria-label={maximized ? 'Tam ekrandan çık' : 'Tam ekran editör'}
      >
        {maximized ? <Minimize2 size={14} aria-hidden="true" /> : <Maximize2 size={14} aria-hidden="true" />}
      </button>

      {onShowMini && (
        <button
          type="button"
          onClick={onShowMini}
          className={btn}
          title="Önizlemeyi mini pencerede aç"
          aria-label="Mini önizlemeyi göster"
        >
          <Eye size={14} aria-hidden="true" />
          Önizle
        </button>
      )}

      <button type="button" onClick={onPrint} className={iconBtn} title="Yazdır" aria-label="Yazdır">
        <Printer size={14} aria-hidden="true" />
      </button>
      <button
        type="button"
        onClick={onShowShortcuts}
        className={iconBtn}
        title="Klavye Kısayolları (F1)"
        aria-label="Klavye kısayolları"
      >
        <Keyboard size={14} aria-hidden="true" />
      </button>

      {onToggleAi && (
        <button
          type="button"
          onClick={onToggleAi}
          aria-pressed={aiOpen}
          className={`xc-focus ml-1 flex h-[26px] flex-none items-center gap-1.5 whitespace-nowrap rounded-md border px-2 text-[12px] font-semibold transition-colors ${
            aiOpen
              ? 'border-ui-accent bg-ui-accent text-ui-accent-fg'
              : 'border-ed-border text-ed-text hover:bg-ed-active'
          }`}
        >
          <Sparkles size={14} aria-hidden="true" />
          AI Asistan
        </button>
      )}
      </div>
    </div>
  )
}
