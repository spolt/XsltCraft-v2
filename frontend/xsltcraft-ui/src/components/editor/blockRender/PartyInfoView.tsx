import { useMemo } from 'react'
import type { PartyInfoBlockConfig } from '../../../types/blocks'
import { PARTY_TYPE_ROOT_XPATH } from '../../../types/blocks'
import { evalNodes, evalString, type Resolved, type XmlContext } from './xmlEval'
import { Val } from './parts'
import { borderOf, P_RESET } from './invoiceStyle'

interface Row { key: string; label: string; value: Resolved }

/** Satıcı/alıcı: başlık + (satır içi | iki sütun) alanlar — generator'daki for-each(party) ile aynı. */
export default function PartyInfoView({ config, ctx }: { config: PartyInfoBlockConfig; ctx: XmlContext | null }) {
  const fontSize = config.fontSize || '9.5pt'

  const parties = useMemo(() => {
    const fields = [...(config.fields ?? [])].filter((f) => f.visible).sort((a, b) => a.order - b.order)
    const rootXpath = PARTY_TYPE_ROOT_XPATH[config.partyType] ?? PARTY_TYPE_ROOT_XPATH.SupplierParty
    const nodes = evalNodes(ctx, rootXpath)
    // XML yoksa ya da kök çözülemezse alanlar XPath'leriyle gösterilir; taraf XML'de yoksa (çıktıdaki gibi) yalnız başlık
    const contexts: (Node | null)[] = nodes === null ? [null] : nodes.slice(0, 3)
    return contexts.map((node): Row[] =>
      fields.map((f) => ({
        key: f.key,
        label: f.label,
        value: node
          ? evalString(ctx, f.relativeXpath, node)
          : ({ kind: 'unresolved', xpath: f.relativeXpath } as Resolved),
      })),
    )
  }, [config.fields, config.partyType, ctx])

  return (
    <div style={config.bordered ? { border: borderOf(config.borderStyle), padding: '4px 8px' } : undefined}>
      {config.showTitle && config.title && (
        <p style={{ ...P_RESET, fontWeight: 'bold', marginBottom: 4, fontSize }}>{config.title}</p>
      )}
      {parties.map((rows, i) =>
        config.labelStyle === 'inline' ? (
          <div key={i}>
            {rows.map((r) => (
              <p key={r.key} style={{ ...P_RESET, fontSize }}>
                {r.label && <strong>{r.label}: </strong>}
                <Val r={r.value} />
              </p>
            ))}
          </div>
        ) : (
          <table key={i} style={{ width: '100%', borderCollapse: 'collapse', fontSize }}>
            <tbody>
              {rows.map((r) => (
                <tr key={r.key}>
                  {config.labelStyle !== 'hidden' && (
                    <td style={{ fontWeight: 'bold', padding: '1px 4px', whiteSpace: 'nowrap', width: '35%', verticalAlign: 'top' }}>
                      {r.label}
                    </td>
                  )}
                  <td style={{ padding: '1px 4px' }}><Val r={r.value} /></td>
                </tr>
              ))}
            </tbody>
          </table>
        ),
      )}
    </div>
  )
}
