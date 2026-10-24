namespace XsltCraft.Xslt.Saxon;

/// <summary>
/// Süreç içi (Api) Saxon için Java standart akışları. IKVM'in <c>java.lang.System.out/err</c>'ü fd 1/2'ye DOĞRUDAN
/// yazar (<see cref="Console.SetOut"/> etkilemez — worker <c>JavaStdio</c> ile aynı bulgu). Saxon zaten sessiz
/// logger/sınırlı raporlayıcı kullanır; bu, gözden kaçan her Java yazımının (stylesheet içeriği taşıyabilir) Api
/// konteyner loguna düşmemesi için derinlemesine savunmadır. Api'de Java tanısı ILogger'a gider; ikisi de yutulur.
/// Saxon ilk kurulmadan önce bir kez çağrılır (idempotent). Worker kendi <c>JavaStdio</c>'sunu kullanır.
/// </summary>
public static class SaxonProcessStdio
{
    private static int _redirected;

    public static void DiscardJavaStdio()
    {
        if (Interlocked.Exchange(ref _redirected, 1) != 0)
            return;
        java.lang.System.setOut(new java.io.PrintStream(new DiscardingJavaStream(), false));
        java.lang.System.setErr(new java.io.PrintStream(new DiscardingJavaStream(), false));
    }

    private sealed class DiscardingJavaStream : java.io.OutputStream
    {
        public override void write(int b)
        {
        }

        public override void write(byte[] b, int off, int len)
        {
        }
    }
}
