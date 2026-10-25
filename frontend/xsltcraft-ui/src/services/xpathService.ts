import axios from 'axios'
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
  /** Hata sınıfı (XPATH_TOO_LONG, XPATH_TOO_COMPLEX, XPATH_TIMEOUT, XPATH_BUSY, XML_INVALID…). */
  code?: string | null
}

export async function evaluateXPath(
  xpathExpression: string,
  xmlContent: string,
): Promise<XPathEvaluateResponse> {
  try {
    const { data } = await api.post<XPathEvaluateResponse>('/api/xpath/evaluate', {
      xpathExpression,
      xmlContent,
    })
    return data
  } catch (err) {
    // 400 (maliyet kapısı) / 503 (meşgul) gövdesi aynı sözleşmededir — mesajı kullanıcıya göster.
    const body = axios.isAxiosError(err) ? (err.response?.data as XPathEvaluateResponse | undefined) : undefined
    if (body?.kind === 'error' && body.error) return body
    throw err
  }
}
