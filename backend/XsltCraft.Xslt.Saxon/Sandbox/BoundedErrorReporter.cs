using net.sf.saxon.lib;
using net.sf.saxon.s9api;

namespace XsltCraft.Xslt.Saxon.Sandbox;

/// <summary>Saxon hata/uyarı kaydının .NET kopyası (Saxon nesnesine referans tutmaz).</summary>
internal sealed record SaxonErrorRecord(string Message, string? Code, int Line, int Column, bool IsWarning, Exception? Cause);

/// <summary>
/// Derleyici / transformer için sınırlı <see cref="ErrorReporter"/>: varsayılan raporlayıcı System.err'e yazar
/// (worker protokol kanalı, Api log'u). Uyarılar sayılır ama saklanmaz; hatalardan en fazla
/// <c>capacity</c> adet saklanır. <c>setErrorList</c>'in sınırsız listesi yerine kullanılır.
/// </summary>
internal sealed class BoundedErrorReporter(int capacity, int maxMessageChars) : ErrorReporter
{
    private readonly List<SaxonErrorRecord> _errors = [];

    public IReadOnlyList<SaxonErrorRecord> Errors => _errors;

    public int WarningCount { get; private set; }

    public int DroppedErrorCount { get; private set; }

    public void report(XmlProcessingError error)
    {
        if (error is null)
            return;

        if (error.isWarning())
        {
            WarningCount++;
            return;
        }

        if (_errors.Count >= capacity)
        {
            DroppedErrorCount++;
            return;
        }

        var location = error.getLocation();
        _errors.Add(new SaxonErrorRecord(
            Truncate(error.getMessage() ?? string.Empty, maxMessageChars),
            error.getErrorCode()?.getLocalName(),
            Math.Max(0, location?.getLineNumber() ?? 0),
            Math.Max(0, location?.getColumnNumber() ?? 0),
            IsWarning: false,
            error.getCause()));
    }

    internal static string Truncate(string value, int max) =>
        value.Length <= max ? value : value[..max] + "…";
}
