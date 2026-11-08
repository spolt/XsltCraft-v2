/** Dosyadaki statik görselin rolü (admin tema yükleme ve Kolay düzenle aynı kümeyi kullanır). */
export type ImageRoleValue = 'logo' | 'signature' | 'design'

export interface RoleOption {
  value: ImageRoleValue
  /** Admin tema yükleme diyaloğundaki etiket. */
  label: string
  /** Kolay düzenle'de kullanıcıya gösterilen etiket (kendi belgesi için). */
  userLabel: string
}

export const ROLE_OPTIONS: readonly RoleOption[] = [
  { value: 'logo', label: 'Firma logosu', userLabel: 'Firma logom' },
  { value: 'signature', label: 'İmza / kaşe', userLabel: 'İmza / Kaşe' },
  { value: 'design', label: 'Tasarımın parçası', userLabel: 'Tasarımın parçası' },
]

export const userRoleLabel = (role: ImageRoleValue) => ROLE_OPTIONS.find((o) => o.value === role)?.userLabel ?? role
