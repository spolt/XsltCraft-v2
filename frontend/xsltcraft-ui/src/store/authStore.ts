import { create } from 'zustand'
import { persist } from 'zustand/middleware'

interface User {
  id: string
  email: string
  displayName: string | null
  role: string
}

interface AuthState {
  user: User | null
  /** Yalnız bellekte: localStorage'a yazılmaz (XSS ile okunamasın, tarayıcı kapanınca kalmasın). */
  accessToken: string | null
  login: (token: string, user: User) => void
  logout: () => void
  setAccessToken: (token: string) => void
  updateUser: (patch: Partial<User>) => void
}

export const useAuthStore = create<AuthState>()(
  persist(
    (set) => ({
      user: null,
      accessToken: null,

      login: (token, user) => set({ accessToken: token, user }),

      logout: () => set({ accessToken: null, user: null }),

      setAccessToken: (token) => set({ accessToken: token }),

      updateUser: (patch) =>
        set((state) => ({ user: state.user ? { ...state.user, ...patch } : null })),
    }),
    {
      name: 'xsltcraft-auth',
      version: 1,
      partialize: (state) => ({ user: state.user }),
      // v0 access token'ı da saklıyordu; yükseltmede at.
      migrate: (persisted) => ({ user: (persisted as { user?: User | null } | null)?.user ?? null }),
    }
  )
)
