import type { ReactNode } from 'react'
import type { Resolved } from './xmlEval'

/** Çözülen değer; çözülemeyen ifade soluk mono XPath olarak gösterilir. */
export function Val({ r, format }: { r: Resolved; format?: (text: string) => string }) {
  if (r.kind === 'value') return <>{format ? format(r.text) : r.text}</>
  if (r.kind === 'unresolved') return <Unresolved xpath={r.xpath} />
  return null
}

export function Unresolved({ xpath }: { xpath: string }) {
  return (
    <span
      className="font-mono"
      title="Örnek XML'de tarayıcıda değerlendirilemedi"
      style={{ fontSize: '0.85em', color: 'var(--color-tx-subtle)', wordBreak: 'break-all' }}
    >
      {xpath}
    </span>
  )
}

/** İçeriği boş kalan blokta soluk ipucu (editöre özgü; çıktıda yok). */
export function EmptyHint({ children }: { children: ReactNode }) {
  return (
    <div
      style={{
        height: '100%',
        minHeight: 18,
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'center',
        fontFamily: 'var(--font-sans, inherit)',
        fontSize: 10,
        color: 'var(--color-tx-subtle)',
        textAlign: 'center',
        padding: 2,
      }}
    >
      {children}
    </div>
  )
}
