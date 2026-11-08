import axios from 'axios'
import type { ComposeWarning } from '../../services/themeComposeService'
import { ASSET_LABEL } from '../theme-assets/imageState'
import { FREE_IMAGE_LABEL } from '../theme-assets/freeImageState'

/** Birleştirilmiş metin istek sınırını aştığında sunucunun 400 yanıtındaki kod. */
export const XSLT_TOO_LARGE_CODE = 'xslt-too-large'
export const XSLT_TOO_LARGE_MESSAGE =
  'Şablon izin verilen boyutu aşıyor. Görselleri küçült.'

/** Compose hata yanıtındaki `code` (yoksa null). */
export function composeErrorCode(err: unknown): string | null {
  if (!axios.isAxiosError(err)) return null
  const code = (err.response?.data as { code?: unknown } | undefined)?.code
  return typeof code === 'string' ? code : null
}

const TEXT: Record<string, (subject: string) => string> = {
  'slot-occupied': (s) => `${s}: seçtiğin konumda dosyadaki bir görsel duruyor; yeni görsel oraya konmadı.`,
  'static-image-not-editable': (s) =>
    `${s}: dosyadaki bu görselin yeri değiştirilemiyor. Boyutunu, hizasını değiştirebilir ya da kaldırabilirsin.`,
  'static-image-too-large': (s) => `${s}: bu konuma taşımak şablonu çok büyütürdü; görsel yerinde bırakıldı.`,
  'static-image-ignored': (s) =>
    `${s}: yeni görsel yüklediğin için dosyadaki görselde yaptığın değişiklik uygulanmadı; yüklediğin görsel onun yerini alıyor.`,
  'static-image-conflict': (s) => `${s}: başka bir değişiklikle çakıştığı için görsel yerinde bırakıldı.`,
  'static-image-not-found': (s) => `${s}: dosyadaki görsel bulunamadı; değişiklik uygulanmadı.`,
  'static-images-skipped': () => 'Dosyadaki görsellerde yaptığın değişiklikler uygulanamadı; görseller yerinde bırakıldı.',
  'slot-not-found': (s) => `${s}: seçilen konum bu belgede yok; görsel yerinde bırakıldı.`,
  'slot-conditional': (s) => `${s}: seçtiğin konum bazı belgelerde görünmeyebilir.`,
  'free-slot-not-found': (s) => `${s}: seçilen konum bu belgede yok; görsel eklenmedi.`,
  // Belge IBAN uyarıları `kind` taşımaz; metin konudan bağımsızdır.
  'document-bank-not-found': () => 'Belgedeki banka bilgileri bulunamadı; banka değişiklikleri uygulanmadı.',
  'document-bank-row-not-found': () => 'Belgedeki bir banka satırı bulunamadı; o satırdaki değişiklik uygulanmadı.',
  'document-bank-row-locked': () => 'Belgedeki bir banka satırı düzenlenemiyor ya da kaldırılamıyor; satır olduğu gibi bırakıldı.',
  'document-bank-add-unsupported': () => 'Belgedeki banka tablosuna yeni satır eklenemiyor; eklediğin hesap uygulanmadı.',
  'document-bank-limit': () => 'Banka hesabı sınırına ulaşıldı; fazla satırlar eklenmedi.',
  'document-bank-too-large': () => 'Banka değişiklikleri şablonu çok büyütürdü; değişiklikler uygulanmadı.',
  'document-bank-skipped': () => 'Belgedeki banka bilgilerinde yaptığın değişiklikler uygulanamadı; satırlar olduğu gibi bırakıldı.',
}

/** Serbest görsel uyarıları `kind` taşımaz; bu kodlarda `kind` null → serbest görsel. */
const FREE_IMAGE_CODES = new Set(['free-slot-not-found', 'slot-occupied', 'slot-conditional'])

/** Kolay düzenle (doc) uyarısının kullanıcıya gösterilen metni; bilinmeyen kodda sunucunun mesajı. */
export function composeWarningText(w: ComposeWarning): string {
  const text = TEXT[w.code]
  if (!text) return w.message
  const subject = w.kind ? ASSET_LABEL[w.kind] : FREE_IMAGE_CODES.has(w.code) ? FREE_IMAGE_LABEL : 'Görsel'
  return text(subject)
}
