import type { UserListItem } from '../../../services/adminUserService'
import type { FnColor } from '../../../theme/uiThemes'

export const ROLE_TONE: Record<UserListItem['role'], FnColor> = { Admin: 'slate', Editor: 'violet', User: 'gray' }
const AVATAR_TONES: readonly FnColor[] = ['purple', 'blue', 'green', 'orange', 'violet', 'slate']

export function formatDate(iso: string | null | undefined): string {
  if (!iso) return '—'
  return new Date(iso).toLocaleString('tr-TR', {
    day: '2-digit', month: 'short', year: 'numeric',
    hour: '2-digit', minute: '2-digit',
  })
}

export function formatDateShort(iso: string | null | undefined): string {
  if (!iso) return '—'
  return new Date(iso).toLocaleDateString('tr-TR', { day: '2-digit', month: 'short', year: 'numeric' })
}

export function userInitials(user: UserListItem): string {
  if (user.displayName) {
    const parts = user.displayName.trim().split(' ')
    if (parts.length >= 2) return (parts[0][0] + parts[parts.length - 1][0]).toUpperCase()
    return parts[0].slice(0, 2).toUpperCase()
  }
  return user.username.slice(0, 2).toUpperCase()
}

/** E-postadan kararlı avatar rengi. */
export function avatarTone(email: string): FnColor {
  let hash = 0
  for (const c of email) hash = (hash * 31 + c.charCodeAt(0)) & 0xffff
  return AVATAR_TONES[hash % AVATAR_TONES.length]
}

export function apiMessage(err: unknown): string | undefined {
  return (err as { response?: { data?: { message?: string } } })?.response?.data?.message
}
