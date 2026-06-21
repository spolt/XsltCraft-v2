import { create } from 'zustand'
import { getEntitlements, type Entitlements } from '../services/entitlementService'

interface EntitlementState {
  entitlements: Entitlements | null
  loading: boolean
  loaded: boolean
  /** /api/me/entitlements'i çeker. Gate'li bir işlem (indirme vb.) sonrası rozetleri tazelemek için tekrar çağrılabilir. */
  refresh: () => Promise<void>
  clear: () => void
}

export const useEntitlementStore = create<EntitlementState>((set) => ({
  entitlements: null,
  loading: false,
  loaded: false,

  refresh: async () => {
    set({ loading: true })
    try {
      const e = await getEntitlements()
      set({ entitlements: e, loaded: true })
    } catch {
      // Sessizce geç — gate'ler güvenli tarafta (entitlements yoksa kısıtlı) varsayar.
    } finally {
      set({ loading: false })
    }
  },

  clear: () => set({ entitlements: null, loaded: false }),
}))
