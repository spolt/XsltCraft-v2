import { Link } from 'react-router-dom'
import { TriangleAlert } from 'lucide-react'

/** Tema XSLT'si alınamadığında çalışma alanının yerine gösterilir. */
export default function DevModeLoadError({ message }: { message: string }) {
  return (
    <main className="flex flex-1 flex-col items-center justify-center gap-4 px-4">
      <div role="alert" className="flex items-center gap-2 rounded-lg border border-fn-red-line bg-fn-red-tint px-4 py-3 text-sm text-fn-red-ink">
        <TriangleAlert size={15} aria-hidden="true" className="flex-none" />
        {message}
      </div>
      <Link to="/templates" className="xc-focus rounded text-sm text-ui-ink underline hover:no-underline">
        Tema Kütüphanesi'ne dön
      </Link>
    </main>
  )
}
