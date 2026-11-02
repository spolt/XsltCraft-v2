using net.sf.saxon.s9api;

namespace XsltCraft.Xslt.Saxon.Sandbox;

/// <summary>
/// <c>xsl:message</c> işleyicisi: varsayılanı System.err'e yazar. Burada mesajlar sayılır, yalnız ilk
/// <c>capacity</c> tanesi kısaltılarak saklanır — bellek ve log taşması yok.
/// </summary>
internal sealed class BoundedMessageHandler(int capacity, int maxMessageChars) : java.util.function.Consumer
{
    private readonly List<string> _messages = [];

    public IReadOnlyList<string> Messages => _messages;

    public long Count { get; private set; }

    public void accept(object message)
    {
        Count++;
        if (_messages.Count >= capacity || message is not Message m)
            return;

        // getStringValue tüm içeriği birleştirir; önce kısalt, sakla.
        var text = m.getStringValue() ?? string.Empty;
        _messages.Add(BoundedErrorReporter.Truncate(text, maxMessageChars));
    }

    public java.util.function.Consumer andThen(java.util.function.Consumer after) => throw new NotSupportedException();
}
