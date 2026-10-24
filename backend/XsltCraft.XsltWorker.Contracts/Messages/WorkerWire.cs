using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using XsltCraft.XsltWorker.Contracts.Framing;

namespace XsltCraft.XsltWorker.Contracts.Messages;

/// <summary>Tipli mesaj katmanı: başlık (de)serileştirme + yön başına çerçeve sınırları.</summary>
public static class WorkerWire
{
    private static readonly UTF8Encoding StrictUtf8 = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);
    private static readonly IReadOnlyList<ReadOnlyMemory<byte>> NoBodies = [];

    public static ValueTask WriteHelloAsync(Stream stream, HelloHeader hello, CancellationToken ct = default) =>
        Write(stream, FrameType.Hello, hello, WorkerJsonContext.Default.HelloHeader, NoBodies, FrameLimits.MaxResponseFrameBytes, ct);

    public static ValueTask WriteRequestAsync(Stream stream, RequestHeader request, IReadOnlyList<ReadOnlyMemory<byte>> bodies, CancellationToken ct = default) =>
        Write(stream, FrameType.Request, request, WorkerJsonContext.Default.RequestHeader, bodies, FrameLimits.MaxRequestFrameBytes, ct);

    public static ValueTask WriteResponseAsync(Stream stream, ResponseHeader response, IReadOnlyList<ReadOnlyMemory<byte>> bodies, CancellationToken ct = default) =>
        Write(stream, FrameType.Response, response, WorkerJsonContext.Default.ResponseHeader, bodies, FrameLimits.MaxResponseFrameBytes, ct);

    public static ValueTask WriteShutdownAsync(Stream stream, ShutdownHeader shutdown, CancellationToken ct = default) =>
        Write(stream, FrameType.Shutdown, shutdown, WorkerJsonContext.Default.ShutdownHeader, NoBodies, FrameLimits.MaxRequestFrameBytes, ct);

    /// <summary>Başlığı çözer; geçersiz JSON → <see cref="FrameError.InvalidHeader"/>.</summary>
    public static T DecodeHeader<T>(Frame frame, JsonTypeInfo<T> typeInfo)
    {
        ArgumentNullException.ThrowIfNull(frame);
        try
        {
            return JsonSerializer.Deserialize(frame.Header, typeInfo)
                ?? throw new FrameProtocolException(FrameError.InvalidHeader, "Başlık boş.");
        }
        catch (JsonException ex)
        {
            throw new FrameProtocolException(FrameError.InvalidHeader, $"Başlık JSON'u çözülemedi: {ex.Message}");
        }
    }

    /// <summary>Gövdeyi katı UTF-8 ile çözer; geçersiz bayt → <see cref="FrameError.InvalidBody"/>.</summary>
    public static string DecodeBody(byte[] body)
    {
        try
        {
            return StrictUtf8.GetString(body);
        }
        catch (DecoderFallbackException)
        {
            throw new FrameProtocolException(FrameError.InvalidBody, "Gövde geçerli UTF-8 değil.");
        }
    }

    /// <summary>Metni BOM'suz UTF-8 gövdeye çevirir (eşleşmemiş vekil karakter → hata).</summary>
    public static byte[] EncodeBody(string text) => StrictUtf8.GetBytes(text);

    private static ValueTask Write<T>(
        Stream stream, FrameType type, T header, JsonTypeInfo<T> typeInfo,
        IReadOnlyList<ReadOnlyMemory<byte>> bodies, int maxFrameBytes, CancellationToken ct)
    {
        var headerBytes = JsonSerializer.SerializeToUtf8Bytes(header, typeInfo);
        return FrameCodec.WriteAsync(stream, type, headerBytes, bodies, maxFrameBytes, ct);
    }
}
