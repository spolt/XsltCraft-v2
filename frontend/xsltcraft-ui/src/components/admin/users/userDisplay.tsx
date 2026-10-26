import { Crown } from 'lucide-react'
import type { UserListItem } from '../../../services/adminUserService'
import { fnStyle } from '../../../theme/uiThemes'
import { Pill } from '../adminUi'
import { avatarTone, ROLE_TONE, userInitials } from './userFormat'

/** Renkli baş harf avatarı (dekoratif; ad yanında metin olarak var). */
export function UserAvatar({ user, size = 'md' }: { user: UserListItem; size?: 'md' | 'lg' }) {
  const box = size === 'lg' ? 'h-12 w-12 text-[16px]' : 'h-8 w-8 text-[12px]'
  return (
    <span
      aria-hidden="true"
      style={fnStyle(avatarTone(user.email))}
      className={`flex flex-none items-center justify-center rounded-full bg-(--fn-tint) font-bold text-(--fn-ink) ${box}`}
    >
      {userInitials(user)}
    </span>
  )
}

export function RolePill({ role }: { role: UserListItem['role'] }) {
  return <Pill tone={ROLE_TONE[role] ?? 'gray'}>{role}</Pill>
}

export function PlanPill({ plan }: { plan: UserListItem['plan'] }) {
  return plan === 'Pro'
    ? <Pill tone="purple"><Crown size={11} aria-hidden="true" />Pro</Pill>
    : <Pill tone="gray">Free</Pill>
}
