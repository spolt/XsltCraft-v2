import type { GridBlock } from '../../../types/blocks'
import { PARTY_TYPE_ROOT_XPATH, type PartyType } from '../../../types/blocks'

/** Seçili blok çipinde gösterilen birincil bağlı XPath (yoksa null). */
export function primaryXPath(block: GridBlock): string | null {
  const c = block.config as unknown as Record<string, unknown>
  const s = (v: unknown) => (typeof v === 'string' && v.trim() ? v.trim() : null)
  switch (block.type) {
    case 'InvoiceLineTable':
    case 'Table':
    case 'Notes':
      return s(c.iterateOver)
    case 'PartyInfo':
      return PARTY_TYPE_ROOT_XPATH[c.partyType as PartyType] ?? null
    case 'Text':
    case 'Heading':
      return c.isStatic ? null : s((c.binding as { xpath?: string } | undefined)?.xpath)
    case 'ETTN':
      return s(c.ettnXpath)
    case 'TaxSummary':
      return s(c.taxTotalXpath)
    case 'ConditionalText':
      return s((c.condition as { xpath?: string } | undefined)?.xpath)
    case 'InvoiceTotals':
      return '//cac:LegalMonetaryTotal'
    default:
      return null
  }
}
