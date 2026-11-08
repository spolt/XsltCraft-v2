import { useState, type ReactNode } from 'react'
import { Link } from 'react-router-dom'
import { Code2, Image as ImageIcon, ImagePlus, Landmark, Signature, StickyNote } from 'lucide-react'
import AssetAccordionSection from './AssetAccordionSection'
import type { EasyEditBreadcrumb } from './easyEditSource'

interface Section {
  status: string
  done: boolean
  content: ReactNode
}

interface Props {
  breadcrumb: EasyEditBreadcrumb
  templateName: string
  isDoc: boolean
  hasSlots: boolean
  confirmLeave: () => boolean
  logo: Section
  signature: Section
  /** Yalnız doc: serbest görsel. */
  free?: Section
  bank: Section
  notes: Section
  /** Yalnız doc: "Daha fazlası mı lazım?" kutusundan XSLT Editör'e geçiş. */
  onOpenEditor?: () => void
  editorDisabled?: boolean
}

/** Kolay düzenle sol paneli: konum kırıntısı, başlık, varlık akordeonları (doc'ta editöre geçiş kutusu). */
export default function EasyEditAssetsPanel({
  breadcrumb, templateName, isDoc, hasSlots, confirmLeave, logo, signature, free, bank, notes, onOpenEditor, editorDisabled,
}: Props) {
  const [openSections, setOpenSections] = useState({ logo: true, signature: false, free: false, bank: false, notes: false })
  const toggleSection = (key: keyof typeof openSections) => setOpenSections((o) => ({ ...o, [key]: !o[key] }))

  return (
    <aside
      aria-label="Tema düzenleme"
      className="flex flex-col border-r border-ui-line bg-ui-card lg:w-80 lg:flex-none lg:overflow-y-auto"
    >
      <div className="border-b border-ui-line px-4 pt-3.5 pb-3">
        <nav aria-label="Konum">
          <ol className="m-0 flex list-none gap-1.5 p-0 text-[12px] text-tx-muted">
            <li>
              <Link
                to={breadcrumb.rootTo}
                onClick={(e) => {
                  // Ctrl/⌘/orta tık yeni sekme açar; bu sayfadan ayrılınmaz.
                  if (e.button !== 0 || e.metaKey || e.ctrlKey || e.shiftKey || e.altKey) return
                  if (!confirmLeave()) e.preventDefault()
                }}
                className="xc-focus rounded text-ui-ink no-underline hover:underline"
              >
                {breadcrumb.rootLabel}
              </Link>
            </li>
            <li aria-hidden="true">/</li>
            <li>{breadcrumb.current}</li>
          </ol>
        </nav>
        <h1 className="mt-1 mb-0 text-[17px] font-semibold text-tx">{templateName}</h1>
        {isDoc && (
          <p className="mt-2 mb-0 text-[13px] font-semibold text-tx">Neleri değiştirmek istersin?</p>
        )}
        <p className="mt-1 mb-0 text-[12px] leading-normal text-tx-muted">
          {isDoc
            ? 'Logonuzu, imzanızı, banka bilgilerinizi ve sabit notlarınızı ekleyin; belgenin düzeni korunur.'
            : 'Logonuzu, imzanızı, banka bilgilerinizi ve sabit notlarınızı ekleyin; tema düzeni korunur.'}
        </p>
        <p className="mt-2.5 mb-0 rounded-md border border-ui-soft-line bg-ui-soft px-3 py-2 text-[12px] leading-normal text-tx-2">
          {hasSlots
            ? isDoc
              ? 'Görseli önizlemedeki mavi kesikli alanlardan birine sürükleyin; bıraktığınız yere taşınır.'
              : 'Görseli önizlemedeki mavi kesikli alanlardan birine sürükleyin ya da “Konum” listesinden seçin.'
            : isDoc
              ? 'Bu belgede konum seçimi yok; görseller belgenin logo/imza alanına yerleşir.'
              : 'Bu tema için konum seçimi yok; görseller temanın logo/imza alanına yerleşir.'}
        </p>
      </div>

      <AssetAccordionSection
        title="Logo"
        icon={ImageIcon}
        status={logo.status}
        done={logo.done}
        open={openSections.logo}
        onToggle={() => toggleSection('logo')}
      >
        {logo.content}
      </AssetAccordionSection>
      <AssetAccordionSection
        title="İmza / Kaşe"
        icon={Signature}
        status={signature.status}
        done={signature.done}
        open={openSections.signature}
        onToggle={() => toggleSection('signature')}
      >
        {signature.content}
      </AssetAccordionSection>
      {free && (
        <AssetAccordionSection
          title="Serbest görsel"
          icon={ImagePlus}
          status={free.status}
          done={free.done}
          open={openSections.free}
          onToggle={() => toggleSection('free')}
        >
          {free.content}
        </AssetAccordionSection>
      )}
      <AssetAccordionSection
        title="Banka / IBAN"
        icon={Landmark}
        status={bank.status}
        done={bank.done}
        open={openSections.bank}
        onToggle={() => toggleSection('bank')}
      >
        {bank.content}
      </AssetAccordionSection>
      <AssetAccordionSection
        title="Sabit notlar"
        icon={StickyNote}
        status={notes.status}
        done={notes.done}
        open={openSections.notes}
        onToggle={() => toggleSection('notes')}
      >
        {notes.content}
      </AssetAccordionSection>

      {isDoc && onOpenEditor && (
        <div className="m-4 rounded-lg border-[1.5px] border-dashed border-field-line p-3 text-[12px] text-tx-2">
          <p className="m-0 font-semibold text-tx">Daha fazlası mı lazım?</p>
          <p className="mt-0.5 mb-2 leading-normal text-tx-muted">
            Tablo, yazı tipi ya da alan düzeni gibi değişiklikler için XSLT Editör'ü kullan.
          </p>
          <button
            type="button"
            onClick={onOpenEditor}
            disabled={editorDisabled}
            data-testid="open-xslt-editor"
            className="xc-focus inline-flex items-center gap-1.5 rounded-md font-semibold text-ui-ink hover:underline disabled:opacity-50"
          >
            <Code2 size={13} aria-hidden="true" />
            XSLT Editör'e geç
          </button>
        </div>
      )}
    </aside>
  )
}
