# Kolay düzenle — sahip geri bildirimi #1 (2026-11-07)

> Durum: **not alındı, planner sahip onayından sonra çalışacak.** Bağlam: `easy-edit-uploaded-xslt.md` (W1–W5 tamamlandı, commit edilmedi). Test dosyası: `09.04e-fatura.xslt` (SEYMEN logosu, imza/kaşe görseli, YAPIKREDİ IBAN satırı).

## Maddeler

| # | Sahibin gözlemi | İlk teknik okuma (doğrulanacak) | Netleştirilecek |
|---|---|---|---|
| 1 | "Buraya da koy" gereksiz | Kaldırılacak: sürükle-bırak menüsündeki "Buraya da koy", sol paneldeki "Buraya da koy · en fazla 3 ek konum" alanı ve önizleme kopya çipleri. Backend `extraSlots`/`staticImages.extraSlots` desteği kalabilir ya da sadeleştirilebilir. | Yalnız UI'dan mı kalksın, backend'den de mi? Bırakınca seçim menüsü yerine doğrudan "taşı" mı yapılsın? |
| 2 | Karekodun altındaki logoyu "Satıcı bilgilerinin üstü"ne taşıyınca mavi kesikli alana yerleşmesi beklenirdi; yeni alan açıldı | Statik taşıyıcı işaret kutusuna (`data-xc-inserted`) görseli **içine değil ardına** koyuyor (kayıtlı dosyada boş kutu kalmasın diye). Önizlemede kesikli kutu boş kalıyor, görsel altında yeni blok olarak görünüyor. Runtime (yeni yüklenen görsel) ise kutunun içine koyuyor. | Hedef: görsel kesikli alanın yerini alsın (kutuyu doldursun). |
| 3 | Şablondaki imza/kaşe taşınamıyor, görsel olarak tanınmamış | Panelde "İmza / Kaşe: Boş"; önizlemede "Temadaki imza/kaşenin yeri" yuvası var. Olasılıklar: görsel `movable=false` (PNG/JPEG dışı, >500 KB ya da >4000 px), rol önerisi "tasarım", aynı türde ikinci görsel ya da AVT/dinamik nitelik. Bu dosyayla analiz yanıtı incelenecek. | — |
| 4 | Logo ve imza yüklüyken 3. bir görsel yüklenemiyor | Model yalnız iki tür biliyor (`logo`, `signature`). 3. görsel yeni bir kavram gerektirir (ör. "ek görsel" türü ya da tür başına çoklu görsel). | 3. görsel ne olacak (ek logo, kaşe ayrı, serbest görsel)? Kaç adet? Hangi konumlara konabilir? |
| 5 | Şablondaki mevcut IBAN tablosu okunmuyor; yeni IBAN eklemek ya da mevcut IBAN'ı değiştirmek mümkün değil (ekleme ayrı yeni tablo açıyor) | Banka geri okuyucu yalnız XsltCraft'ın kendi yazdığı bloğu tanıyor. Kullanıcının kendi XSLT'sindeki banka/IBAN satırı (literal metin ya da XPath) tanınmıyor; Kolay düzenle kendi tablosunu ekliyor. Mevcut yapıyı tanıyıp satır ekleme/düzenleme için yeni bir analiz kuralı gerekir (literal "IBAN" metni + TR IBAN deseni). | Mevcut tablo bulunursa: o tabloya satır mı eklensin, kendi tablomuz yerine mi geçsin? XPath ile üretilen (belgeden gelen) IBAN'lar düzenlenebilir sayılsın mı? |

## Sahip cevapları (2026-11-07)
1. "Buraya da koy" kalkar. Görsel bırakılınca seçim menüsü açılmaz, **doğrudan taşınır**.
2. Üçüncü görsel **serbest görsel** olur: kullanıcı isterse herhangi bir görseli yükler.
3. Dosyada IBAN tablosu bulunursa yeni satır **o tabloya eklenir**. Bulunan IBAN satırları da **düzenlenebilir**.

## Sonraki adım
Sahip onay verince: planner bu notu ve yukarıdaki "netleştirilecek" soruların cevaplarını alıp uygulama planı çıkarır. Önce 3. madde için örnek dosyayla analiz yanıtı incelenir.

## Sahip kararları — plan (2026-11-07)
- Serbest görsel: en fazla 1.
- XML'den gelen IBAN desteklenmiyor; yalnız XSLT'de gömülü literal TR IBAN'ı aranır. Döngüyle üretilen tablolar desteklenmiyor. Bulunamazsa kendi tablomuz kullanılır.
- Son IBAN satırı da silinebilir. TR IBAN biçimi zorunlu.
- "Buraya da koy" sunucudan da kaldırılır (eski kayıtlar okunmaya devam eder). Büyük ya da PNG/JPEG dışı dosya görselleri taşınabilir (SVG hariç). Tanınmayan görseller bu turda taşınmaz.
