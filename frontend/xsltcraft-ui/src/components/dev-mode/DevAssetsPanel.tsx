import { useState } from 'react'
import { CircleX, Image as ImageIcon, Landmark, ScanEye, Signature, StickyNote, TriangleAlert } from 'lucide-react'
import type { ThemeSlotOption } from '../../services/templateService'
import type { AssetKind, ComposeWarning, FixedNotePosition } from '../../services/themeComposeService'
import { useDraftField } from '../../store/themeDraftStore'
import { toast } from '../../store/toastStore'
import BankInfoPanel from '../theme-assets/BankInfoPanel'
import DraggableAssetChip from '../theme-assets/DraggableAssetChip'
import FixedNotesPanel from '../theme-assets/FixedNotesPanel'
import ImagePanel from '../theme-assets/ImagePanel'
import { countNotes, noteHasText, type FixedNoteState } from '../theme-assets/fixedNoteState'
import {
  effectiveSlot,
  filledBankRows,
  readAsDataUrl,
  themeImageRemovable,
  validateImageFile,
} from '../theme-assets/imageState'
import AssetAccordionSection from '../theme-use/AssetAccordionSection'

interface DevAssetsPanelProps {
  slots: ThemeSlotOption[]
  fixedNotesSupported: boolean | null
  composeError: string | null
  composeErrorField: string | null
  warnings: ComposeWarning[]
  /** XSLT şu an bozuk: değişiklikler düzeltilince uygulanacak. */
  pendingInvalid: boolean
  /** Stil değişimi ve temizle için beklemeden yeniden birleştir. */
  composeNow: () => void
  /** "Alanları göster" açık: görseller önizlemedeki yuvalara sürüklenebilir. */
  showSlots: boolean
  onToggleSlots: () => void
}

/** Geliştirici Modu sol paneli: tema varlıkları. Değerler taslak store'dan okunur ve yazılır. */
export default function DevAssetsPanel({
  slots,
  fixedNotesSupported,
  composeError,
  composeErrorField,
  warnings,
  pendingInvalid,
  composeNow,
  showSlots,
  onToggleSlots,
}: DevAssetsPanelProps) {
  const [logo, setLogo] = useDraftField('logo')
  const [signature, setSignature] = useDraftField('signature')
  const [bankInfo, setBankInfo] = useDraftField('bankInfo')
  const [fixedNotes, setFixedNotes] = useDraftField('fixedNotes')
  const [uploading, setUploading] = useState<AssetKind | null>(null)
  const [openSections, setOpenSections] = useState({ logo: true, signature: false, bank: false, notes: false })
  const toggleSection = (key: keyof typeof openSections) => setOpenSections((o) => ({ ...o, [key]: !o[key] }))

  const bankCount = filledBankRows(bankInfo).length
  const noteCount = countNotes(fixedNotes)
  const setterFor = (kind: AssetKind) => (kind === 'logo' ? setLogo : setSignature)

  /** Metin değişimi debounce ile; stil/temizle (dolu notta) beklemeden birleştirir. */
  function handleNoteChange(position: FixedNotePosition, patch: Partial<FixedNoteState>, immediate: boolean) {
    const prev = fixedNotes[position]
    const next = { ...prev, ...patch }
    setFixedNotes((s) => ({ ...s, [position]: { ...s[position], ...patch } }))
    if (immediate && (noteHasText(prev) || noteHasText(next))) composeNow()
  }

  async function handleImageUpload(kind: AssetKind, e: React.ChangeEvent<HTMLInputElement>) {
    const file = e.target.files?.[0]
    e.target.value = ''
    if (!file) return
    const invalid = validateImageFile(file)
    if (invalid) {
      toast.error(invalid, { title: kind === 'logo' ? 'Logo yüklenemedi' : 'İmza yüklenemedi' })
      return
    }
    setUploading(kind)
    try {
      const dataUrl = await readAsDataUrl(file)
      setterFor(kind)((prev) => ({ ...prev, dataUrl }))
    } catch {
      toast.error('Görsel okunamadı.')
    } finally {
      setUploading(null)
    }
  }

  const renderImagePanel = (kind: AssetKind) => {
    const state = kind === 'logo' ? logo : signature
    return (
      <ImagePanel
        hideTitle
        label={kind === 'logo' ? 'Logo' : 'İmza / Kaşe'}
        state={state}
        uploading={uploading === kind}
        slots={slots}
        slotValue={effectiveSlot(kind, state, slots)}
        onUpload={(e) => handleImageUpload(kind, e)}
        onChange={(patch) => setterFor(kind)((p) => ({ ...p, ...patch }))}
        onRemove={() => setterFor(kind)((p) => ({ ...p, dataUrl: '' }))}
        themeImageRemovable={themeImageRemovable(kind, slots)}
        dragHandle={showSlots && state.dataUrl
          ? <DraggableAssetChip kind={kind} dataUrl={state.dataUrl} />
          : undefined}
      />
    )
  }

  return (
    <>
      {slots.length > 0 && (
        <div className="flex flex-none items-center border-b border-ui-line px-4 py-2">
          <button
            type="button"
            aria-pressed={showSlots}
            onClick={onToggleSlots}
            title="Görselleri temanın konumlarına sürükleyin"
            className={`xc-focus inline-flex h-[30px] items-center gap-1.5 rounded-md border px-2.5 text-[12px] transition-colors ${
              showSlots
                ? 'border-ui-soft-line bg-ui-soft text-ui-ink'
                : 'border-field-line bg-ui-card text-tx-2 hover:bg-ui-hover'
            }`}
          >
            <ScanEye size={13} aria-hidden="true" />
            Alanları göster
          </button>
        </div>
      )}
      {pendingInvalid && (
        <div role="status" className="flex items-start gap-1.5 border-b border-fn-orange-line bg-fn-orange-tint px-4 py-2 text-[12px] leading-normal text-fn-orange-ink">
          <TriangleAlert size={13} aria-hidden="true" className="mt-0.5 flex-none" />
          XSLT düzeltilince uygulanacak.
        </div>
      )}
      {composeError && (
        <div role="alert" className="flex items-start gap-1.5 border-b border-fn-red-line bg-fn-red-tint px-4 py-2 text-[12px] leading-normal text-fn-red-ink">
          <CircleX size={13} aria-hidden="true" className="mt-0.5 flex-none" />
          {composeError}
        </div>
      )}
      {warnings.length > 0 && (
        <div className="flex flex-col gap-0.5 border-b border-fn-orange-line bg-fn-orange-tint px-4 py-2 text-[12px] leading-normal text-fn-orange-ink">
          {warnings.map((w) => (
            <span key={w.code + (w.kind ?? '')} className="flex items-start gap-1.5">
              <TriangleAlert size={12} aria-hidden="true" className="mt-0.5 flex-none" />
              {w.message}
            </span>
          ))}
        </div>
      )}

      <AssetAccordionSection
        title="Logo"
        icon={ImageIcon}
        status={logo.dataUrl ? 'Eklendi' : 'Boş'}
        done={!!logo.dataUrl}
        open={openSections.logo}
        onToggle={() => toggleSection('logo')}
      >
        {renderImagePanel('logo')}
      </AssetAccordionSection>
      <AssetAccordionSection
        title="İmza / Kaşe"
        icon={Signature}
        status={signature.dataUrl ? 'Eklendi' : 'Boş'}
        done={!!signature.dataUrl}
        open={openSections.signature}
        onToggle={() => toggleSection('signature')}
      >
        {renderImagePanel('signature')}
      </AssetAccordionSection>
      <AssetAccordionSection
        title="Banka / IBAN"
        icon={Landmark}
        status={bankCount > 0 ? `${bankCount} hesap` : 'Boş'}
        done={bankCount > 0}
        open={openSections.bank}
        onToggle={() => toggleSection('bank')}
      >
        <BankInfoPanel hideTitle rows={bankInfo} onChange={setBankInfo} />
      </AssetAccordionSection>
      <AssetAccordionSection
        title="Sabit notlar"
        icon={StickyNote}
        status={fixedNotesSupported === false ? 'Uygun değil' : noteCount > 0 ? `${noteCount} not` : 'Boş'}
        done={fixedNotesSupported !== false && noteCount > 0}
        open={openSections.notes}
        onToggle={() => toggleSection('notes')}
      >
        <FixedNotesPanel
          notes={fixedNotes}
          onChange={handleNoteChange}
          supported={fixedNotesSupported}
          error={composeErrorField === 'fixedNotes' ? composeError : null}
        />
      </AssetAccordionSection>

      <p className="m-0 border-b border-ui-line px-4 py-3 text-[12px] leading-normal text-tx-muted">
        Değişiklikler editördeki XSLT'ye otomatik işlenir. Görsel/banka bloğu ve panelden yazılan sabit notlar her değişiklikte yeniden yazılır; diğer düzenlemeleriniz korunur.
      </p>
    </>
  )
}
