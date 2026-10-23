import axios from "axios"
import api from "../services/apiService"

const BASE = "https://localhost:44314"

export async function getTemplates() {
  const res = await fetch(`${BASE}/api/templates`)
  return await res.json()
}

// Returns { id, files: string[] }
export async function getTemplateFiles(id: string) {
  const res = await fetch(`${BASE}/api/templates/${id}`)
  return await res.json()
}

// Returns { id, fileName, content }
export async function getTemplateFile(id: string, fileName: string) {
  const res = await fetch(`${BASE}/api/templates/${id}/${fileName}`)
  return await res.json()
}

// Returns { valid: true } or { valid: false, error: string, line: number, column: number }
export async function validateXslt(xslt: string): Promise<XsltValidationResult> {
  const { data } = await api.post<XsltValidationResult>("/api/preview/validate-xslt", { xslt })
  return data
}

export type XsltValidationResult =
  | { valid: true }
  | { valid: false; error: string; line: number; column: number }

export type TransformResult =
  | { ok: true; html: string }
  | { ok: false; error: string; line: number; column: number }

export async function transformPreview(xml: string, xslt: string): Promise<TransformResult> {
  try {
    // Ham gövde metni korunur (eski fetch + res.text() davranışı).
    const { data: html } = await api.post<string>("/api/preview", { xml, xslt }, { responseType: "text" })
    return { ok: true, html }
  } catch (e) {
    // HTTP hata yanıtı (4xx/5xx) → { error, line, column } gövdesi okunur; ağ hatası yukarı fırlatılır.
    if (!axios.isAxiosError(e) || !e.response) throw e
    const err = parseErrorBody(e.response.data)
    return { ok: false, error: err.error ?? "", line: err.line ?? 0, column: err.column ?? 0 }
  }
}

function parseErrorBody(body: unknown): { error?: string; line?: number; column?: number } {
  if (typeof body !== "string") return (body ?? {}) as { error?: string; line?: number; column?: number }
  try {
    return JSON.parse(body)
  } catch {
    return { error: body }
  }
}
