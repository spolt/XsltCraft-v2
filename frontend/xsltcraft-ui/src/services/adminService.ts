import axios from 'axios'
import api from './apiService'
import type { FreeTheme } from './templateService'

export async function getAdminThemes(): Promise<FreeTheme[]> {
  const { data } = await api.get<FreeTheme[]>('/api/templates')
  return data
}

// ─── Tema konum analizi (yükleme sonrası rapor) ──────────────────────────────

export interface ThemeSlotReport {
  status: 'ok' | 'partial' | 'none' | 'failed' | 'skipped'
  analyzedAt: string
  anchors: { id: string; label: string; found: boolean; line: number | null }[]
  slots: {
    id: string
    label: string
    group: string
    defaultFor: 'logo' | 'signature' | null
    order: number
    conditional: boolean
    legacyId: string | null
    inserted: boolean
    line: number
    removable: boolean
  }[]
  occupiedHolders: string[]
  overrides: { found: boolean; decision: 'strip' | 'keep' | null; imagesKept: number; bankRowsKept: number } | null
  warnings: { code: string; message: string; slotId: string | null }[]
  /** Temadaki statik görseller ve admin'in verdiği rol (yuvası doğrulanamadıysa slotId null). */
  images: { index: number; role: ImageRole; line: number; slotId: string | null; removable: boolean; stripped: boolean }[]
}

export interface AdminTheme extends FreeTheme {
  /** Eski (analiz edilmemiş) temalarda null. */
  slotAnalysis: ThemeSlotReport | null
}

export async function getAdminTheme(id: string): Promise<AdminTheme> {
  const { data } = await api.get<AdminTheme>(`/api/admin/themes/${id}`)
  return data
}

// ─── Yükleme kararı (HTTP 409): önceden gömülü görsel bloğu + temadaki görsellerin rolü ─

export type OverridesDecision = 'strip' | 'keep'

/** Temadaki statik görselin rolü: logo/imza → kullanıcının görseli onun yerini alır; design → dokunulmaz. */
export type ImageRole = 'logo' | 'signature' | 'design'

export interface DetectedThemeImage {
  index: number
  suggested: ImageRole
  /** Önerinin gerekçesi. */
  reason: string
  line: number
  /** Yalnız data:image kaynaklarında. */
  previewDataUrl: string | null
}

export interface OverridesInspection {
  present: boolean
  blockCount: number
  danglingMarkers: number
  version: number
  keepable: boolean
  reason: string | null
  images: {
    kind: 'logo' | 'signature' | null
    targets: string[]
    dataUrl: string | null
    width: number | null
    height: number | null
    alignment: string
    valid: boolean
    problem: string | null
  }[]
  bankRows: number
}

export interface UploadDecisionRequest {
  /** null → temada önceden gömülü görsel bloğu yok. */
  overrides: OverridesInspection | null
  images: DetectedThemeImage[]
}

/** Temadaki bir görsel için admin kararı. */
/**
 * Temadaki görselin akıbeti: keep → görünür; removable → görünür, kullanıcı görsel eklemeden kaldırabilir (yalnız
 * logo/imza); strip → admin yüklemede temadan kaldırır (logo/imzanın yeri kullanıcının görseli için konum olarak kalır,
 * tasarım görseli tamamen silinir).
 */
export type ImageVisibility = 'keep' | 'removable' | 'strip'

export interface ImageDecision {
  role: ImageRole
  visibility: ImageVisibility
}

export interface UploadDecision {
  existingOverrides?: OverridesDecision
  /** 409 yanıtındaki görsel sırasıyla. */
  images?: ImageDecision[]
}

/** Admin kararı gerekli: önceden gömülü görsel bloğu (kaldır/koru) ve/veya temadaki görsellerin rolü. Aynı istek kararla yinelenir. */
export class UploadDecisionRequiredError extends Error {
  readonly request: UploadDecisionRequest
  constructor(message: string, request: UploadDecisionRequest) {
    super(message)
    this.name = 'UploadDecisionRequiredError'
    this.request = request
  }
}

function rethrowDecision(err: unknown): never {
  if (axios.isAxiosError(err) && err.response?.status === 409 && err.response.data?.code === 'upload_decision_required') {
    const data = err.response.data
    throw new UploadDecisionRequiredError(data.message, { overrides: data.overrides ?? null, images: data.images ?? [] })
  }
  throw err
}

function appendDecision(body: FormData, decision: UploadDecision | undefined) {
  if (decision?.existingOverrides) body.append('existingOverrides', decision.existingOverrides)
  if (decision?.images) {
    body.append('existingImages', decision.images
      .map((d) => (d.visibility === 'strip' ? `${d.role}:strip`
        : d.visibility === 'removable' && d.role !== 'design' ? `${d.role}:removable`
          : d.role)).join(','))
  }
}

export async function createTheme(form: {
  name: string
  documentType: string
  file: File
  isPremium?: boolean
  decision?: UploadDecision
}): Promise<AdminTheme> {
  const body = new FormData()
  body.append('name', form.name)
  body.append('documentType', form.documentType)
  body.append('file', form.file)
  body.append('isPremium', String(form.isPremium ?? false))
  appendDecision(body, form.decision)
  try {
    const { data } = await api.post<AdminTheme>('/api/admin/themes', body)
    return data
  } catch (err) {
    rethrowDecision(err)
  }
}

export async function updateTheme(
  id: string,
  form: { name?: string; documentType?: string; file?: File; isPremium?: boolean; decision?: UploadDecision },
): Promise<AdminTheme> {
  const body = new FormData()
  if (form.name) body.append('name', form.name)
  if (form.documentType) body.append('documentType', form.documentType)
  if (form.file) body.append('file', form.file)
  if (form.isPremium !== undefined) body.append('isPremium', String(form.isPremium))
  appendDecision(body, form.decision)
  try {
    const { data } = await api.put<AdminTheme>(`/api/admin/themes/${id}`, body)
    return data
  } catch (err) {
    rethrowDecision(err)
  }
}

export async function deleteTheme(id: string): Promise<void> {
  await api.delete(`/api/admin/themes/${id}`)
}
