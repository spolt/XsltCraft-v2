import api from './apiService'

export interface XPathResultItem {
  kind: 'element' | 'attribute' | 'text' | 'comment' | 'pi' | 'atomic' | 'node'
  name: string
  value: string
  line: number | null
  column: number | null
}

export interface XPathEvaluateResponse {
  kind: 'node-set' | 'atomic' | 'empty' | 'error'
  items: XPathResultItem[]
  executionMs: number
  error: string | null
}

export async function evaluateXPath(
  xpathExpression: string,
  xmlContent: string,
): Promise<XPathEvaluateResponse> {
  const { data } = await api.post<XPathEvaluateResponse>('/api/xpath/evaluate', {
    xpathExpression,
    xmlContent,
  })
  return data
}
