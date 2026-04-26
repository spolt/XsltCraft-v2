import { create } from 'zustand'

export type ToastVariant = 'info' | 'success' | 'warning' | 'error'

export interface Toast {
  id: string
  variant: ToastVariant
  title?: string
  message: string
  /** Otomatik kapanma süresi (ms). null → manuel kapatılana dek kalır. */
  durationMs: number | null
}

interface ToastState {
  toasts: Toast[]
  push: (t: Omit<Toast, 'id'>) => string
  dismiss: (id: string) => void
  clear: () => void
}

let counter = 0
const nextId = () => `t${Date.now()}_${++counter}`

const DEFAULT_DURATION_MS: Record<ToastVariant, number | null> = {
  info: 4000,
  success: 3500,
  warning: 6000,
  error: null, // hatalar manuel kapatılana dek dursun
}

export const useToastStore = create<ToastState>((set, get) => ({
  toasts: [],
  push: (t) => {
    const id = nextId()
    const durationMs = t.durationMs === undefined ? DEFAULT_DURATION_MS[t.variant] : t.durationMs
    const toast: Toast = { ...t, id, durationMs }
    set((s) => ({ toasts: [...s.toasts, toast] }))
    if (durationMs !== null) {
      window.setTimeout(() => get().dismiss(id), durationMs)
    }
    return id
  },
  dismiss: (id) => set((s) => ({ toasts: s.toasts.filter((t) => t.id !== id) })),
  clear: () => set({ toasts: [] }),
}))

// Kolaylık helper'ları — store dışından kolay tetiklemek için.
export const toast = {
  info: (message: string, opts?: { title?: string; durationMs?: number | null }) =>
    useToastStore.getState().push({ variant: 'info', message, durationMs: opts?.durationMs ?? null, title: opts?.title }),
  success: (message: string, opts?: { title?: string; durationMs?: number | null }) =>
    useToastStore.getState().push({ variant: 'success', message, durationMs: opts?.durationMs ?? null, title: opts?.title }),
  warning: (message: string, opts?: { title?: string; durationMs?: number | null }) =>
    useToastStore.getState().push({ variant: 'warning', message, durationMs: opts?.durationMs ?? null, title: opts?.title }),
  error: (message: string, opts?: { title?: string; durationMs?: number | null }) =>
    useToastStore.getState().push({ variant: 'error', message, durationMs: opts?.durationMs ?? null, title: opts?.title }),
}
