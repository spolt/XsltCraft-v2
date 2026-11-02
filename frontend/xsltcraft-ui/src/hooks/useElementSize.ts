import { useEffect, useState, type RefObject } from 'react'
import type { Size } from '../utils/floatingRect'

/** Elemanın içerik kutusu boyutunu izler; ilk ölçüme kadar null döner. */
export function useElementSize<T extends HTMLElement>(ref: RefObject<T | null>): Size | null {
  const [size, setSize] = useState<Size | null>(null)

  useEffect(() => {
    const el = ref.current
    if (!el) return
    const observer = new ResizeObserver(entries => {
      const { width, height } = entries[0].contentRect
      setSize({ width: Math.round(width), height: Math.round(height) })
    })
    observer.observe(el)
    return () => observer.disconnect()
  }, [ref])

  return size
}
