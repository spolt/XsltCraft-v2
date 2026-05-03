import { create } from 'zustand'
import { getAiStatus } from '../services/aiAssistantService'

interface AiState {
  enabled: boolean | null  // null => henüz sorgulanmadı
  loading: boolean
  refresh: () => Promise<void>
  setEnabled: (v: boolean) => void
}

export const useAiStore = create<AiState>((set, get) => ({
  enabled: null,
  loading: false,
  refresh: async () => {
    if (get().loading) return
    set({ loading: true })
    try {
      const { enabled } = await getAiStatus()
      set({ enabled, loading: false })
    } catch {
      set({ enabled: false, loading: false })
    }
  },
  setEnabled: (v) => set({ enabled: v }),
}))
