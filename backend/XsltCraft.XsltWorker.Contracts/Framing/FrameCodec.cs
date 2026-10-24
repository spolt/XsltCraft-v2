using System.Buffers.Binary;

namespace XsltCraft.XsltWorker.Contracts.Framing;

/// <summary>
/// Uzunluk önekli ikili çerçeve kodlayıcısı.
/// <code>
/// "XC"(2B) ver(1B) type(1B) headerLen(u32 LE) header(JSON) bodyCount(1B) { len(u32 LE) body(ham UTF-8) }*
/// </code>
/// Okuma kısmi okumalara karşı güvenlidir (<see cref="Stream.ReadExactlyAsync(Memory{byte}, CancellationToken)"/>),
/// sınırlar gövde ayrılmadan ÖNCE denetlenir (uzunluk alanıyla bellek şişirme yapılamaz).
/// </summary>
public static class FrameCodec
{
    public const byte Magic0 = (byte)'X';
    public const byte Magic1 = (byte)'C';

    /// <summary>magic(2) + ver(1) + type(1) + headerLen(4).</summary>
    public const int PrefixLength = 8;

    /// <summary>Bir çerçevenin tel üzerindeki toplam bayt uzunluğu.</summary>
    public static long ComputeFrameLength(int headerLength, IReadOnlyList<ReadOnlyMemory<byte>> bodies)
    {
        long total = PrefixLength + (long)headerLength + 1;
        foreach (var body in bodies)
            total += 4 + (long)body.Length;
        return total;
    }

    /// <summary>
    /// Çerçeveyi yazar ve akışı boşaltır. Sınır ihlalinde akışa HİÇBİR bayt yazılmadan
    /// <see cref="FrameProtocolException"/> fırlatılır (akış kullanılabilir kalır).
    /// </summary>
    public static async ValueTask WriteAsync(
        Stream stream,
        FrameType type,
        ReadOnlyMemory<byte> header,
        IReadOnlyList<ReadOnlyMemory<byte>> bodies,
        int maxFrameBytes,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentNullException.ThrowIfNull(bodies);

        if (!Enum.IsDefined(type))
            throw new FrameProtocolException(FrameError.UnknownType, $"Bilinmeyen çerçeve türü: {(byte)type}.");
        if (header.Length > FrameLimits.MaxHeaderBytes)
            throw new FrameProtocolException(FrameError.HeaderTooLarge, $"Başlık {header.Length} bayt; sınır {FrameLimits.MaxHeaderBytes}.");
        if (bodies.Count > FrameLimits.MaxBodyCount)
            throw new FrameProtocolException(FrameError.TooManyBodies, $"Gövde sayısı {bodies.Count}; sınır {FrameLimits.MaxBodyCount}.");

        var total = ComputeFrameLength(header.Length, bodies);
        if (total > maxFrameBytes)
            throw new FrameProtocolException(FrameError.FrameTooLarge, $"Çerçeve {total} bayt; sınır {maxFrameBytes}.");

        var prefix = new byte[PrefixLength];
        prefix[0] = Magic0;
        prefix[1] = Magic1;
        prefix[2] = ProtocolVersion.Current;
        prefix[3] = (byte)type;
        BinaryPrimitives.WriteUInt32LittleEndian(prefix.AsSpan(4), (uint)header.Length);

        await stream.WriteAsync(prefix, cancellationToken).ConfigureAwait(false);
        if (!header.IsEmpty)
            await stream.WriteAsync(header, cancellationToken).ConfigureAwait(false);

        var scratch = new byte[4];
        scratch[0] = (byte)bodies.Count;
        await stream.WriteAsync(scratch.AsMemory(0, 1), cancellationToken).ConfigureAwait(false);

        foreach (var body in bodies)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(scratch, (uint)body.Length);
            await stream.WriteAsync(scratch, cancellationToken).ConfigureAwait(false);
            if (!body.IsEmpty)
                await stream.WriteAsync(body, cancellationToken).ConfigureAwait(false);
        }

        await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Bir çerçeve okur. Çerçeve sınırında temiz EOF → <c>null</c>.
    /// Bozuk/kesik/sınır aşan çerçeve → <see cref="FrameProtocolException"/> (akış artık güvenilmez).
    /// </summary>
    public static async ValueTask<Frame?> ReadAsync(
        Stream stream,
        int maxFrameBytes,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(stream);

        var prefix = new byte[PrefixLength];

        // Önce yalnız magic: protokol dışı bayt mümkün olan en erken anda yakalanır.
        var got = await stream.ReadAtLeastAsync(prefix.AsMemory(0, 2), 2, throwOnEndOfStream: false, cancellationToken)
            .ConfigureAwait(false);
        if (got == 0)
            return null;
        if (got < 2)
            throw Truncated();
        if (prefix[0] != Magic0 || prefix[1] != Magic1)
            throw new FrameProtocolException(FrameError.BadMagic, "Çerçeve imzası (\"XC\") bulunamadı.");

        await ReadExactly(stream, prefix.AsMemory(2), cancellationToken).ConfigureAwait(false);

        if (prefix[2] != ProtocolVersion.Current)
            throw new FrameProtocolException(FrameError.UnsupportedVersion, $"Desteklenmeyen çerçeve sürümü: {prefix[2]}.");

        var type = (FrameType)prefix[3];
        if (!Enum.IsDefined(type))
            throw new FrameProtocolException(FrameError.UnknownType, $"Bilinmeyen çerçeve türü: {prefix[3]}.");

        var headerLength = BinaryPrimitives.ReadUInt32LittleEndian(prefix.AsSpan(4));
        if (headerLength > FrameLimits.MaxHeaderBytes)
            throw new FrameProtocolException(FrameError.HeaderTooLarge, $"Başlık {headerLength} bayt; sınır {FrameLimits.MaxHeaderBytes}.");

        long total = PrefixLength + (long)headerLength + 1;
        EnsureWithin(total, maxFrameBytes);

        var header = new byte[headerLength];
        await ReadExactly(stream, header, cancellationToken).ConfigureAwait(false);

        var scratch = new byte[4];
        await ReadExactly(stream, scratch.AsMemory(0, 1), cancellationToken).ConfigureAwait(false);
        int bodyCount = scratch[0];
        if (bodyCount > FrameLimits.MaxBodyCount)
            throw new FrameProtocolException(FrameError.TooManyBodies, $"Gövde sayısı {bodyCount}; sınır {FrameLimits.MaxBodyCount}.");

        var bodies = new byte[bodyCount][];
        for (var i = 0; i < bodyCount; i++)
        {
            await ReadExactly(stream, scratch, cancellationToken).ConfigureAwait(false);
            var length = BinaryPrimitives.ReadUInt32LittleEndian(scratch);
            total += 4 + (long)length;
            EnsureWithin(total, maxFrameBytes);

            var body = new byte[length];
            await ReadExactly(stream, body, cancellationToken).ConfigureAwait(false);
            bodies[i] = body;
        }

        return new Frame(type, header, bodies);
    }

    private static void EnsureWithin(long total, int maxFrameBytes)
    {
        if (total > maxFrameBytes)
            throw new FrameProtocolException(FrameError.FrameTooLarge, $"Çerçeve en az {total} bayt; sınır {maxFrameBytes}.");
    }

    private static async ValueTask ReadExactly(Stream stream, Memory<byte> buffer, CancellationToken cancellationToken)
    {
        try
        {
            await stream.ReadExactlyAsync(buffer, cancellationToken).ConfigureAwait(false);
        }
        catch (EndOfStreamException)
        {
            throw Truncated();
        }
    }

    private static FrameProtocolException Truncated() =>
        new(FrameError.Truncated, "Akış çerçeve ortasında kapandı.");
}
