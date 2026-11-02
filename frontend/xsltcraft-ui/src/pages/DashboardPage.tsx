import { useRecentWork } from '../hooks/useRecentWork'
import CreateActions from '../components/dashboard/CreateActions'
import XsltWorkspace from '../components/dashboard/XsltWorkspace'
import RecentList from '../components/dashboard/RecentList'

/**
 * Çalışma alanı: oluşturma aksiyonları · XSLT Editör (ana iş akışı, ~%65) · son düzenlenenler (~%35).
 * xl ve üstünde iki kolon görünür yüksekliği doldurur (üst sınırlı); liste kendi içinde kayar.
 */
export default function DashboardPage() {
  const { items, loading, error } = useRecentWork()

  return (
    <div className="mx-auto flex min-h-full w-full max-w-[1600px] flex-col gap-6 px-4 pt-6 pb-8 sm:px-8">
      <header className="flex flex-col gap-3.5">
        <h1 className="m-0 text-[22px] font-semibold leading-tight text-tx">Çalışma alanı</h1>
        <CreateActions />
      </header>

      <div className="grid grid-cols-1 gap-6 xl:max-h-[880px] xl:min-h-[540px] xl:flex-1 xl:grid-cols-[minmax(0,65fr)_minmax(0,35fr)]">
        <XsltWorkspace />
        <div className="relative min-w-0">
          <RecentList items={items} loading={loading} error={error} className="xl:absolute xl:inset-0" />
        </div>
      </div>
    </div>
  )
}
