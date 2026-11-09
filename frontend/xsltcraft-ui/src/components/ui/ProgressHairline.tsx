interface Props {
  className?: string
  /** Ray rengi; koyu çalışma alanında `bg-ui-workspace-line`. */
  trackClassName?: string
}

/** Belirsiz süreli işlemler için 2px'lik sakin ilerleme çizgisi. Dekoratiftir; durum metni ayrıca verilmeli. */
export default function ProgressHairline({ className = '', trackClassName = 'bg-ui-line' }: Props) {
  return (
    <div aria-hidden="true" className={`h-[2px] overflow-hidden rounded-full ${trackClassName} ${className}`}>
      <div className="xc-hairline-bar h-full w-1/3 rounded-full bg-ui-accent" />
    </div>
  )
}
