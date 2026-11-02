import { useMemo } from 'react'
import { useXmlStore } from '../../../store/xmlStore'
import { getXmlContext, type XmlContext } from './xmlEval'

/** Etkin örnek XML'in (xmlStore) önbellekli ayrıştırma bağlamı; tüm bloklar aynı nesneyi paylaşır. */
export function useXmlContext(): XmlContext | null {
  const content = useXmlStore((s) => s.xmlFiles.find((f) => f.id === s.activeXmlId)?.content)
  return useMemo(() => getXmlContext(content), [content])
}
