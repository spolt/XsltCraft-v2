interface UserLike {
  displayName: string | null
  email: string
}

/** Görünen ad: displayName yoksa e-posta (eski Navbar ile aynı). */
export function userDisplayName(user: UserLike | null | undefined): string {
  return user?.displayName || user?.email || ''
}

/** Avatar baş harfleri: "Semih Polat" → "SP", e-posta → ilk harf. */
export function userInitials(user: UserLike | null | undefined): string {
  const name = user?.displayName?.trim()
  if (name) {
    const parts = name.split(/\s+/).filter(Boolean)
    const first = parts[0]?.[0] ?? ''
    const last = parts.length > 1 ? parts[parts.length - 1][0] : ''
    return (first + last).toLocaleUpperCase('tr-TR')
  }
  return (user?.email?.[0] ?? '?').toLocaleUpperCase('tr-TR')
}
