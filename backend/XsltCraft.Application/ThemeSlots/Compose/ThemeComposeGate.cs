namespace XsltCraft.Application.ThemeSlots.Compose;

/// <summary>
/// İstekle gelen <c>xslt</c> ile yapılan birleştirmenin süreç geneli eşzamanlılık kapısı: birleştirme senkron ve iptal
/// edilemez, her yuva bir thread-pool thread'i tutar.
/// </summary>
public interface IThemeComposeGate
{
    /// <summary>Beklemeden yuva alır; kapı doluysa <c>null</c>. Dönen nesne atılınca yuva geri verilir.</summary>
    IDisposable? TryEnter();
}

public sealed class ThemeComposeGate(int maxConcurrent) : IThemeComposeGate
{
    private readonly SemaphoreSlim _slots = new(maxConcurrent, maxConcurrent);

    public IDisposable? TryEnter() => _slots.Wait(0) ? new Slot(_slots) : null;

    private sealed class Slot(SemaphoreSlim slots) : IDisposable
    {
        private int _released;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _released, 1) == 0)
                slots.Release();
        }
    }
}
