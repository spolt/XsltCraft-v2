import api from './apiService'

export interface UserXsltTemplateSummary {
  id: string
  name: string
  createdAt: string
  updatedAt: string
}

export interface UserXsltTemplateDetail extends UserXsltTemplateSummary {
  xsltContent: string
  xmlContent: string | null
}

export async function getUserXsltTemplates(): Promise<UserXsltTemplateSummary[]> {
  const { data } = await api.get<UserXsltTemplateSummary[]>('/api/user-xslt-templates')
  return data
}

export async function getUserXsltTemplate(id: string): Promise<UserXsltTemplateDetail> {
  const { data } = await api.get<UserXsltTemplateDetail>(`/api/user-xslt-templates/${id}`)
  return data
}

export async function createUserXsltTemplate(params: {
  name: string
  xsltContent: string
  xmlContent?: string
}): Promise<UserXsltTemplateDetail> {
  const { data } = await api.post<UserXsltTemplateDetail>('/api/user-xslt-templates', params)
  return data
}

export async function updateUserXsltTemplate(
  id: string,
  params: { name?: string; xsltContent?: string; xmlContent?: string },
): Promise<UserXsltTemplateDetail> {
  const { data } = await api.put<UserXsltTemplateDetail>(`/api/user-xslt-templates/${id}`, params)
  return data
}

export async function deleteUserXsltTemplate(id: string): Promise<void> {
  await api.delete(`/api/user-xslt-templates/${id}`)
}
