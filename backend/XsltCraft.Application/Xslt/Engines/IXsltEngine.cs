namespace XsltCraft.Application.Xslt.Engines;

/// <summary>
/// Motor-bağımsız XSLT derleyici. Hatalar yalnız <see cref="XsltProcessingException"/> olarak
/// fırlatılır. Motora özgü tipler (Saxon vb.) bu sözleşmeye sızmaz.
/// </summary>
public interface IXsltEngine
{
    XsltEngineKind Kind { get; }

    ICompiledXslt Compile(string xslt, XsltExecutionLimits limits);
}

/// <summary>Derlenmiş, tekrar kullanılabilir (iş parçacığı güvenli) stylesheet.</summary>
public interface ICompiledXslt
{
    XsltEngineKind Kind { get; }

    /// <summary>
    /// <paramref name="xml"/>'i dönüştürüp <paramref name="output"/>'a yazar. Derleme süresi
    /// çağıranın ölçümüdür; dönen <see cref="XsltTransformTimings.CompileMs"/> 0'dır.
    /// </summary>
    XsltTransformTimings Transform(string xml, TextWriter output, XsltExecutionLimits limits);
}
