/**
 * Öğeleri sınırlı eşzamanlılıkla işler ve her tamamlanan öğede ilerleme bildirir.
 * Bir öğenin hatası diğerlerini durdurmaz; başarısız olanlar ayrı döner ki çağıran
 * yalnız başaranları yerel duruma yansıtabilsin.
 */
export async function runPool<T>(
  items: readonly T[],
  worker: (item: T) => Promise<void>,
  onProgress: (done: number) => void,
  concurrency = 6,
): Promise<{ succeeded: T[]; failed: T[] }> {
  const succeeded: T[] = []
  const failed: T[] = []
  let next = 0
  let done = 0

  async function lane() {
    while (next < items.length) {
      const item = items[next++]
      try {
        await worker(item)
        succeeded.push(item)
      } catch {
        failed.push(item)
      }
      onProgress(++done)
    }
  }

  await Promise.all(Array.from({ length: Math.min(concurrency, items.length) }, lane))
  return { succeeded, failed }
}

/** Diziyi en fazla `size` elemanlık parçalara böler. */
export function chunk<T>(items: readonly T[], size: number): T[][] {
  const out: T[][] = []
  for (let i = 0; i < items.length; i += size) out.push(items.slice(i, i + size))
  return out
}
