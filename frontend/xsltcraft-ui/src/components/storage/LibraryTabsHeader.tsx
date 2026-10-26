import type { ReactNode } from 'react'
import { useNavigate } from 'react-router-dom'
import PageHeader from '../ui/PageHeader'
import SegmentedTabs from '../ui/SegmentedTabs'

type LibraryTab = 'drafts' | 'xslt'

const TABS = [
  { id: 'drafts', label: 'Taslaklarım' },
  { id: 'xslt', label: 'Şablonlarım (XSLT)' },
] as const satisfies readonly { id: LibraryTab; label: string }[]

const ROUTE: Record<LibraryTab, string> = { drafts: '/drafts', xslt: '/my-xslt-templates' }

const COPY: Record<LibraryTab, { title: string; description: string }> = {
  drafts: { title: 'Taslaklarım', description: 'Tasarım alanında ve temalardan oluşturduğun şablonlar.' },
  xslt: { title: 'Şablonlarım', description: 'XSLT Editör’de kaydettiğin şablonlar.' },
}

interface Props {
  active: LibraryTab
  /** Başlığın sağındaki eylemler (ör. yeni şablon bağlantıları). */
  actions?: ReactNode
}

/** Taslaklarım | Şablonlarım (XSLT) ortak başlığı. Sekmeler ayrı rotalara gider; sayfa mantıkları ayrı kalır. */
export default function LibraryTabsHeader({ active, actions }: Props) {
  const navigate = useNavigate()
  const copy = COPY[active]
  return (
    <PageHeader
      title={copy.title}
      description={copy.description}
      actions={
        <>
          {actions}
          <SegmentedTabs
            items={TABS}
            value={active}
            onChange={(id) => { if (id !== active) navigate(ROUTE[id]) }}
            ariaLabel="Kayıt listesi"
          />
        </>
      }
    />
  )
}
