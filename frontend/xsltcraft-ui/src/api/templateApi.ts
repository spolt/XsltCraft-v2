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
export async function validateXslt(xslt: string) {
  const res = await fetch(`${BASE}/api/preview/validate-xslt`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ xslt }),
  })
  return await res.json()
}

export type XsltValidationResult =
  | { valid: true }
  | { valid: false; error: string; line: number; column: number }

export type TransformResult =
  | { ok: true; html: string }
  | { ok: false; error: string; line: number; column: number }

export async function transformPreview(xml: string, xslt: string): Promise<TransformResult> {
  const res = await fetch(`${BASE}/api/preview`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ xml, xslt }),
  })

  if (res.ok) {
    const html = await res.text()
    return { ok: true, html }
  }

  const err = await res.json()
  return { ok: false, error: err.error, line: err.line, column: err.column }
}
