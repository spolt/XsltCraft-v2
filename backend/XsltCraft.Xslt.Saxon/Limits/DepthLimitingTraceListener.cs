using net.sf.saxon;
using net.sf.saxon.expr;
using net.sf.saxon.lib;
using net.sf.saxon.om;
using net.sf.saxon.trace;
using XsltCraft.Application.Xslt.Engines;

namespace XsltCraft.Xslt.Saxon.Limits;

/// <summary>
/// Template / <c>xsl:function</c> / komut girişlerinin iç içe derinliğini sayar (stylesheet
/// <c>setCompileWithTracing(true)</c> ile derlenmiş olmalı). Sınır aşılınca ya da süre/iptal bütçesi
/// bitince <see cref="SaxonLimitError"/> (java.lang.Error) fırlatır.
/// <list type="bullet">
/// <item>TRANSFORM BAŞINA örnek; <c>[ThreadStatic]</c> YOK (spike'taki paylaşım hatası).</item>
/// <item>YAPIŞKAN: bir kez aşılınca sonraki HER <c>enter()</c> yeniden fırlatır; <c>xsl:catch</c>
/// (zaten yakalayamaz) bir şekilde devam ettirse bile ilk çağrıda tekrar patlar.</item>
/// <item>Satır içi fonksiyon özyinelemesi <c>enter()</c> üretmez; XPATH_VERSION_FOR_XSLT=20 o sözdizimini
/// derlemede reddeder. Kalan durdurulamaz döngülerde asıl garanti worker kill'idir (ADR 008).</item>
/// </list>
/// </summary>
internal sealed class DepthLimitingTraceListener : TraceListener
{
    private readonly int _maxDepth;
    private readonly ExecutionBudget _budget;
    private int _depth;

    public DepthLimitingTraceListener(int maxDepth, ExecutionBudget budget)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(maxDepth, 1);
        _maxDepth = maxDepth;
        _budget = budget ?? throw new ArgumentNullException(nameof(budget));
    }

    /// <summary>Görülen en büyük derinlik (ölçüm/test).</summary>
    public int MaxSeen { get; private set; }

    /// <summary>Toplam <c>enter()</c> çağrısı (ölçüm/test).</summary>
    public long EnterCount { get; private set; }

    public void enter(Traceable instruction, java.util.Map properties, XPathContext context)
    {
        EnterCount++;
        _budget.Check();

        var depth = ++_depth;
        if (depth > MaxSeen)
            MaxSeen = depth;

        if (depth > _maxDepth)
        {
            throw _budget.Trip(new SaxonLimitError(XsltFailureKind.LimitExceeded, LimitCodes.Depth,
                $"Özyineleme / iç içe çağrı derinliği sınırı aşıldı (en fazla {_maxDepth})."));
        }
    }

    public void leave(Traceable instruction)
    {
        if (_depth > 0)
            _depth--;
    }

    public void setOutputDestination(Logger stream)
    {
    }

    public void open(Controller controller)
    {
    }

    public void close()
    {
    }

    public void startCurrentItem(Item currentItem)
    {
    }

    public void endCurrentItem(Item currentItem)
    {
    }

    public void startRuleSearch()
    {
    }

    public void endRuleSearch(object rule, net.sf.saxon.trans.Mode mode, Item item)
    {
    }

    public object? checkpoint() => null;

    public void recover(object instruction, net.sf.saxon.trans.XPathException exception)
    {
    }
}
