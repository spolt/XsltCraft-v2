import { sparkPaths } from './usageChartMath'

/** Metrik kartındaki küçük alan grafiği (dekoratif; değerler kartta metin olarak var). */
export default function Sparkline({ values, color }: { values: readonly number[]; color: string }) {
  const { line, area } = sparkPaths(values)
  return (
    <svg aria-hidden="true" viewBox="0 0 100 32" preserveAspectRatio="none" className="mt-0.5 block h-[38px] w-full">
      {area && <path d={area} fill={color} fillOpacity={0.16} />}
      {line && (
        <path
          d={line}
          fill="none"
          stroke={color}
          strokeWidth={2}
          strokeLinejoin="round"
          strokeLinecap="round"
          vectorEffect="non-scaling-stroke"
        />
      )}
    </svg>
  )
}
