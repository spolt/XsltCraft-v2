import api from './apiService'

export type BusinessRuleSeverity = 'error' | 'warning' | 'info'

export interface BusinessRuleResult {
  ruleId: string
  ruleName: string
  severity: BusinessRuleSeverity
  message: string
  line?: number | null
  column?: number | null
  xpath?: string | null
}

export interface BusinessRuleSummary {
  total: number
  errors: number
  warnings: number
  infos: number
}

export interface ValidateBusinessRulesResponse {
  results: BusinessRuleResult[]
  summary: BusinessRuleSummary
}

/** UBL-TR iş kurallarına göre fatura XML'ini doğrular. */
export async function validateBusinessRules(
  xmlContent: string,
): Promise<ValidateBusinessRulesResponse> {
  const { data } = await api.post<ValidateBusinessRulesResponse>(
    '/api/ubl-tr/validate-business-rules',
    { xmlContent },
  )
  return data
}
