import { create } from 'zustand'
import { getAiStatus } from '../services/aiAssistantService'

interface AiState {
  enabled: boolean | null  // null => henüz sorgulanmadı
  /** Sohbete ekran görüntüsü eklenebilir mi (admin vision açık + uygun sağlayıcı var). */
  vision: boolean
  loading: boolean
  refresh: () => Promise<void>
  setEnabled: (v: boolean) => void
}

export const useAiStore = create<AiState>((set, get) => ({
  enabled: null,
  vision: false,
  loading: false,
  refresh: async () => {
    if (get().loading) return
    set({ loading: true })
    try {
      const { enabled, vision } = await getAiStatus()
      set({ enabled, vision: !!vision, loading: false })
    } catch {
      set({ enabled: false, vision: false, loading: false })
    }
  },
  setEnabled: (v) => set({ enabled: v }),
}))
