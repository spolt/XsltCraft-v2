namespace XsltCraft.Application.Xslt.Execution;

/// <summary>İstenen işlem: dönüşüm (HTML üretir) veya yalnız derleme (doğrulama).</summary>
public enum XsltOperation
{
    /// <summary>Stylesheet'i derleyip XML'e uygular; başarıda HTML döner.</summary>
    Render,

    /// <summary>Yalnız derler (<c>validate-xslt</c>); XML kullanılmaz, HTML dönmez.</summary>
    Validate,
}

/// <summary>Bir isteğin fiilen çalıştığı yer (kullanıcı kapısı kötüye kullanım alanı da buna göredir).</summary>
public enum XsltExecutionPlace
{
    /// <summary>İzole worker süreci (<see cref="IXsltIsolatedHost"/>).</summary>
    Isolated,

    /// <summary>Api süreci içi (<see cref="IXsltInProcessHost"/>).</summary>
    InProcess,
}
