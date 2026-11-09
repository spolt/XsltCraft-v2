/**
 * Benzersiz istemci kimliği. `crypto.randomUUID` yalnız güvenli bağlamda (HTTPS / localhost) vardır; düz HTTP'de
 * çağırmak TypeError fırlatır — bu yüzden doğrudan çağırma, bunu kullan.
 */
export function newId(): string {
  return typeof crypto !== 'undefined' && typeof crypto.randomUUID === 'function'
    ? crypto.randomUUID()
    : `${Date.now().toString(36)}-${Math.random().toString(36).slice(2, 10)}${Math.random().toString(36).slice(2, 10)}`
}
