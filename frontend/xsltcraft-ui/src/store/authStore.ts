import { create } from 'zustand'

interface User {
  id: string
  email: string
  displayName: string | null
  role: string
}

interface AuthState {
  user: User | null
  accessToken: string | null
  login: (token: string, user: User) => void
  logout: () => void
  setAccessToken: (token: string) => void
}

export const useAuthStore = create<AuthState>((set) => ({
  user: null,
  accessToken: null,

  login: (token, user) => set({ accessToken: token, user }),

  logout: () => set({ accessToken: null, user: null }),

  setAccessToken: (token) => set({ accessToken: token }),
}))
