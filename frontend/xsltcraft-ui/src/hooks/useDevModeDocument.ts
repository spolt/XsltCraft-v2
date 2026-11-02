import { useEffect, useState } from 'react'
import { useNavigate } from 'react-router-dom'
import axios from 'axios'
import defaultDespatchXml from '../assets/default-despatch.xml?raw'
import defaultInvoiceXml from '../assets/default-invoice.xml?raw'
import { documentTypeFromXslt, draftComposeFields, draftComposeKey, emptyDraftComposeKey } from '../components/dev-mode/draftCompose'
import { parseGateError } from '../services/entitlementService'
import { fetchThemeXslt } from '../services/previewService'
import { getTemplate, type ThemeSlotOption } from '../services/templateService'
import { composeTheme, type ComposeWarning } from '../services/themeComposeService'
import { useDraftField, useThemeDraftStore, type ThemeDraftXml } from '../store/themeDraftStore'
import { openUpgradeModal } from '../store/upgradeModalStore'

const NO_SLOTS: ThemeSlotOption[] = []
const LOAD_ERROR = 'XSLT dosyası yüklenemedi.'
const PAID_THEME_ERROR = 'Bu temayı kullanmak için XsltCraft Pro üyeliği gerekir.'

export interface DevModeDocument {
  /** XSLT ve belge türü hazır; bir kez true olunca geri dönmez. */
  ready: boolean
  loadError: string | null
  templateName: string
  slots: ThemeSlotOption[]
  documentType: 'Invoice' | 'Despatch' | null
  xmlContent: string | null
  xmlName: string
  setXml: (xml: ThemeDraftXml) => void
  /** İlk yükleme compose'unun uyarıları. */
  initialWarnings: ComposeWarning[] | null
}

/**
 * Geliştirici Modu belgesi: tema bilgisi ve çalışma XSLT'si taslakta yoksa yüklenir. Taslaktaki metin
 * asla yeniden yüklenmez. Anahtar `loadXslt` ile eşitlendiğinden açılışta ikinci compose gitmez.
 */
export function useDevModeDocument(templateId: string | undefined): DevModeDocument {
  const navigate = useNavigate()
  const [meta, setMeta] = useDraftField('meta')
  const [xml, setXml] = useDraftField('xml')
  const [, setFixedNotesSupported] = useDraftField('fixedNotesSupported')
  const workingXslt = useThemeDraftStore((s) => s.workingXslt)
  const baselineXslt = useThemeDraftStore((s) => s.baselineXslt)
  const [metaFailed, setMetaFailed] = useState(false)
  const [loadError, setLoadError] = useState<string | null>(null)
  const [initialWarnings, setInitialWarnings] = useState<ComposeWarning[] | null>(null)

  useEffect(() => {
    if (!templateId) return
    const controller = new AbortController()
    const { signal } = controller
    const { session } = useThemeDraftStore.getState()

    /** 402 ise yükseltme akışını başlatır. */
    async function handledAsGate(err: unknown): Promise<boolean> {
      const gate = await parseGateError(err)
      if (gate.status !== 402) return false
      if (signal.aborted) return true
      setLoadError(gate.message ?? PAID_THEME_ERROR)
      openUpgradeModal({ title: 'Ücretli tema', message: gate.message ?? PAID_THEME_ERROR })
      navigate('/templates')
      return true
    }

    async function loadFromTheme(id: string, slots: ThemeSlotOption[]) {
      const fields = draftComposeFields(useThemeDraftStore.getState(), slots)
      try {
        const result = await composeTheme({ themeId: id, ...fields, includeXslt: true, includeHtml: false }, signal)
        if (signal.aborted) return
        if (result.xslt === null) throw new Error('Yanıtta XSLT yok.')
        useThemeDraftStore.getState().loadXslt(result.xslt, draftComposeKey(fields), session)
        setInitialWarnings(result.warnings)
        if (typeof result.fixedNotesSupported === 'boolean') setFixedNotesSupported(result.fixedNotesSupported)
      } catch (err) {
        if (axios.isCancel(err) || signal.aborted || await handledAsGate(err) || signal.aborted) return
        try {
          const content = await fetchThemeXslt(id)
          if (signal.aborted) return
          // Ham metinde taslak varlıkları yok: taslak doluysa anahtar farkı compose'u kendiliğinden tetikler.
          useThemeDraftStore.getState().loadXslt(content, emptyDraftComposeKey(), session)
        } catch (fallbackErr) {
          if (signal.aborted || await handledAsGate(fallbackErr) || signal.aborted) return
          setLoadError(LOAD_ERROR)
        }
      }
    }

    async function load(id: string) {
      let slots = useThemeDraftStore.getState().meta?.slots
      if (!slots) {
        try {
          const t = await getTemplate(id)
          if (signal.aborted) return
          slots = t.slots ?? []
          setMeta({ name: t.name, slots, documentType: t.documentType })
        } catch {
          if (signal.aborted) return
          slots = NO_SLOTS
          setMetaFailed(true)
        }
      }
      if (useThemeDraftStore.getState().workingXslt === null) await loadFromTheme(id, slots)
    }

    void load(templateId)
    return () => controller.abort()
  }, [templateId, setMeta, setFixedNotesSupported, navigate])

  // Tema bilgisi alınamadıysa belge türü ilk metindeki ad alanından anlaşılır.
  const documentType = meta?.documentType
    ?? (metaFailed && baselineXslt !== null ? documentTypeFromXslt(baselineXslt) : null)

  const sampleXml = documentType === 'Despatch' ? defaultDespatchXml : defaultInvoiceXml

  return {
    ready: workingXslt !== null && documentType !== null,
    loadError,
    templateName: meta?.name ?? 'Geliştirici Modu',
    slots: meta?.slots ?? NO_SLOTS,
    documentType,
    xmlContent: xml?.content ?? (documentType ? sampleXml : null),
    xmlName: xml?.fileName ?? (documentType === 'Despatch' ? 'Örnek irsaliye XML' : 'Örnek fatura XML'),
    setXml,
    initialWarnings,
  }
}
