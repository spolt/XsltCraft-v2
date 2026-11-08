using XsltCraft.Application.ThemeSlots.Compose;

namespace XsltCraft.Application.EasyEdit;

/// <summary>Kolay düzenle analizinin süreç geneli eşzamanlılık kapısı (compose kapısından ayrı yuva havuzu).</summary>
public interface IEasyEditAnalyzeGate
{
    /// <summary>Beklemeden yuva alır; kapı doluysa <c>null</c>. Dönen nesne atılınca yuva geri verilir.</summary>
    IDisposable? TryEnter();
}

public sealed class EasyEditAnalyzeGate(int maxConcurrent) : IEasyEditAnalyzeGate
{
    private readonly ThemeComposeGate _inner = new(maxConcurrent);

    public IDisposable? TryEnter() => _inner.TryEnter();
}
