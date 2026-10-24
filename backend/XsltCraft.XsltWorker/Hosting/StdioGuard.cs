namespace XsltCraft.XsltWorker.Hosting;

/// <summary>
/// stdout koruması: protokol akışı ham <see cref="Console.OpenStandardOutput()"/> ile alınır, ardından
/// <see cref="Console.Out"/> <see cref="TextWriter.Null"/>'a yönlendirilir. Böylece kütüphanelerin
/// <c>Console.Write*</c> çağrıları çerçeve akışını bozamaz. (fd 1'e doğrudan yazan yerel kod hâlâ
/// bozabilir — Api bunu protokol ihlali sayıp worker'ı öldürür.) stderr dokunulmadan kalır; Api
/// tarafından sınırlı halka tampona boşaltılır.
/// </summary>
internal static class StdioGuard
{
    private const int BufferSize = 64 * 1024;

    public static (Stream Input, Stream Output) Acquire()
    {
        var output = new BufferedStream(Console.OpenStandardOutput(), BufferSize);
        var input = new BufferedStream(Console.OpenStandardInput(), BufferSize);

        Console.SetOut(TextWriter.Null);
        Console.SetIn(TextReader.Null);

        return (input, output);
    }
}
