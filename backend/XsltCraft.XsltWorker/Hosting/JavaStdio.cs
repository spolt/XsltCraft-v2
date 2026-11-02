namespace XsltCraft.XsltWorker.Hosting;

/// <summary>
/// Java standart akışlarının (IKVM) yönlendirilmesi. DOĞRULANDI (IKVM 8.x / Saxon-HE 12.10, T2.2 probu):
/// IKVM'in <c>java.lang.System.out</c>'u fd 1'e DOĞRUDAN yazar — <see cref="Console.SetOut"/> onu etkilemez,
/// yani <see cref="StdioGuard"/> tek başına protokol akışını Java'ya karşı korumaz. Bu yüzden:
/// <list type="bullet">
/// <item><c>System.out</c> → her şeyi yutan akış (stdout yalnız protokol çerçevelerinindir).</item>
/// <item><c>System.err</c> → gerçek stderr'e süreç ömrü boyunca en çok <see cref="MaxStderrBytes"/> bayt yazan
/// sınırlı akış (Api stderr'i 8 KB halka tampona boşaltır, içeriği loglamaz).</item>
/// </list>
/// Saxon zaten sessiz Logger / sınırlı ErrorReporter-MessageHandler / null trace hedefi kullanır; bu katman
/// gözden kaçan her Java yazımına karşı derinlemesine savunmadır. Herhangi bir Java kodu çalışmadan ÖNCE çağrılır.
/// </summary>
internal static class JavaStdio
{
    public const int MaxStderrBytes = 16 * 1024;

    public static void Redirect(Stream stderr)
    {
        ArgumentNullException.ThrowIfNull(stderr);
        java.lang.System.setOut(new java.io.PrintStream(new DiscardingJavaStream(), false));
        java.lang.System.setErr(new java.io.PrintStream(new BoundedJavaStream(stderr, MaxStderrBytes), true));
    }

    /// <summary>Her şeyi yutar.</summary>
    private sealed class DiscardingJavaStream : java.io.OutputStream
    {
        public override void write(int b)
        {
        }

        public override void write(byte[] b, int off, int len)
        {
        }
    }

    /// <summary>İlk <c>limit</c> baytı iç akışa aktarır, gerisini sessizce atar; G/Ç hatası yutulur.</summary>
    private sealed class BoundedJavaStream(Stream inner, int limit) : java.io.OutputStream
    {
        private readonly object _gate = new();
        private int _written;

        public override void write(int b)
        {
            var one = new[] { (byte)b };
            write(one, 0, 1);
        }

        public override void write(byte[] b, int off, int len)
        {
            if (b is null || len <= 0)
                return;

            lock (_gate)
            {
                var allowed = Math.Min(len, limit - _written);
                if (allowed <= 0)
                    return;
                _written += allowed;
                try
                {
                    inner.Write(b, off, allowed);
                }
                catch (Exception ex) when (ex is IOException or ObjectDisposedException)
                {
                    // Api stderr'i kapattıysa yazacak yer yok.
                }
            }
        }

        public override void flush()
        {
            lock (_gate)
            {
                try
                {
                    inner.Flush();
                }
                catch (Exception ex) when (ex is IOException or ObjectDisposedException)
                {
                    // Yutulur.
                }
            }
        }
    }
}
