import { create } from 'zustand'

export interface UpgradeContext {
  /** Modal başlığı (varsayılan: "XsltCraft Pro'ya Geçin"). */
  title?: string
  /** Bağlama özel açıklama (ör. indirme / kaydetme / AI limiti). */
  message?: string
}

interface UpgradeModalState {
  isOpen: boolean
  context: UpgradeContext | null
  open: (ctx?: UpgradeContext) => void
  close: () => void
}

export const useUpgradeModalStore = create<UpgradeModalState>((set) => ({
  isOpen: false,
  context: null,
  open: (ctx) => set({ isOpen: true, context: ctx ?? null }),
  close: () => set({ isOpen: false, context: null }),
}))

/** Bileşen dışından (servis/handler) kolay tetikleme. */
export const openUpgradeModal = (ctx?: UpgradeContext) => useUpgradeModalStore.getState().open(ctx)
