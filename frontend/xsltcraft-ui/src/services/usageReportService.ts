import api from './apiService'

export interface UsageReportRow {
  userId: string
  username: string | null
  email: string
  tokensUsed: number
  aiRequests: number
  templateExports: number
  saveCount: number
  downloadCount: number
}

export interface UsageReportTotals {
  tokensUsed: number
  aiRequests: number
  templateExports: number
  saveCount: number
  downloadCount: number
  userCount: number
}

export interface UsageReport {
  from: string
  to: string
  rows: UsageReportRow[]
  totals: UsageReportTotals
}

export interface UsageDailyPoint {
  date: string
  tokensUsed: number
  aiRequests: number
  templateExports: number
  saveCount: number
  downloadCount: number
}

export interface UsageDaily {
  from: string
  to: string
  points: UsageDailyPoint[]
}

export async function getUsageReport(from: string, to: string): Promise<UsageReport> {
  const { data } = await api.get<UsageReport>('/api/admin/usage/report', { params: { from, to } })
  return data
}

export async function getUsageDaily(from: string, to: string): Promise<UsageDaily> {
  const { data } = await api.get<UsageDaily>('/api/admin/usage/daily', { params: { from, to } })
  return data
}
