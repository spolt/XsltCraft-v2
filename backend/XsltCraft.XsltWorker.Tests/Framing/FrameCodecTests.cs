using System.Buffers.Binary;
using System.Text;
using XsltCraft.XsltWorker.Contracts;
using XsltCraft.XsltWorker.Contracts.Framing;
using XsltCraft.XsltWorker.Contracts.Messages;

namespace XsltCraft.XsltWorker.Tests.Framing;

public class FrameCodecTests
{
    [Fact]
    public async Task RoundTrip_LargeBodies()
    {
        var xslt = new string('x', 2 * 1024 * 1024) + "ğüşİöç";
        var xml = "<a>" + new string('y', 5 * 1024 * 1024) + "</a>";
        var request = new RequestHeader
        {
            Id = "42",
            Op = WorkerOps.Render,
            Engine = WorkerEngines.Saxon,
            XsltSha256 = new string('a', 64),
            Limits = new RequestLimits { DeadlineMs = 9000, MaxOutputChars = 8_000_000, MaxInputDepth = 512, MaxInputNodes = 1_000_000, MaxStylesheetDepth = 256 },
        };

        using var stream = new MemoryStream();
        await WorkerWire.WriteRequestAsync(stream, request, [WorkerWire.EncodeBody(xslt), WorkerWire.EncodeBody(xml)]);
        stream.Position = 0;

        var frame = await FrameCodec.ReadAsync(stream, FrameLimits.MaxRequestFrameBytes);

        Assert.NotNull(frame);
        Assert.Equal(FrameType.Request, frame.Type);
        Assert.Equal(request, WorkerWire.DecodeHeader(frame, WorkerJsonContext.Default.RequestHeader));
        Assert.Equal(2, frame.Bodies.Count);
        Assert.Equal(xslt, WorkerWire.DecodeBody(frame.Bodies[0]));
        Assert.Equal(xml, WorkerWire.DecodeBody(frame.Bodies[1]));

        // Gövdeler JSON'a gömülmez: başlık küçük kalır.
        Assert.True(frame.Header.Length < 1024);
        Assert.Null(await FrameCodec.ReadAsync(stream, FrameLimits.MaxRequestFrameBytes));
    }

    [Fact]
    public async Task OversizeFrame_Rejected()
    {
        // Yazma: sınırı aşan çerçeve akışa HİÇ bayt yazmadan reddedilir.
        using var output = new MemoryStream();
        var huge = new byte[FrameLimits.MaxRequestFrameBytes];
        var ex = await Assert.ThrowsAsync<FrameProtocolException>(async () =>
            await WorkerWire.WriteRequestAsync(output, new RequestHeader { Id = "1", Op = WorkerOps.Render }, [huge]));
        Assert.Equal(FrameError.FrameTooLarge, ex.Error);
        Assert.Equal(0, output.Length);

        // Okuma: başlık 64KB'ı aşıyor.
        var bigHeader = Prefix(FrameType.Request, FrameLimits.MaxHeaderBytes + 1);
        var headerEx = await Assert.ThrowsAsync<FrameProtocolException>(async () =>
            await FrameCodec.ReadAsync(new MemoryStream(bigHeader), FrameLimits.MaxRequestFrameBytes));
        Assert.Equal(FrameError.HeaderTooLarge, headerEx.Error);

        // Okuma: gövde uzunluk alanı sınırı aşıyor → gövde ayrılmadan/okunmadan reddedilir
        // (akışta gövde baytı yok; ayırmaya kalksaydı Truncated olurdu).
        var lying = new MemoryStream();
        lying.Write(Prefix(FrameType.Response, 2));
        lying.Write("{}"u8);
        lying.WriteByte(1);
        var len = new byte[4];
        BinaryPrimitives.WriteUInt32LittleEndian(len, uint.MaxValue);
        lying.Write(len);
        lying.Position = 0;
        var bodyEx = await Assert.ThrowsAsync<FrameProtocolException>(async () =>
            await FrameCodec.ReadAsync(lying, FrameLimits.MaxResponseFrameBytes));
        Assert.Equal(FrameError.FrameTooLarge, bodyEx.Error);
    }

    [Fact]
    public async Task BadMagic_Rejected()
    {
        var garbage = new MemoryStream(Encoding.ASCII.GetBytes("garbage: not a protocol frame\n"));
        var ex = await Assert.ThrowsAsync<FrameProtocolException>(async () =>
            await FrameCodec.ReadAsync(garbage, FrameLimits.MaxResponseFrameBytes));
        Assert.Equal(FrameError.BadMagic, ex.Error);

        var wrongVersion = Prefix(FrameType.Hello, 0);
        wrongVersion[2] = ProtocolVersion.Current + 1;
        var versionEx = await Assert.ThrowsAsync<FrameProtocolException>(async () =>
            await FrameCodec.ReadAsync(new MemoryStream(wrongVersion), FrameLimits.MaxResponseFrameBytes));
        Assert.Equal(FrameError.UnsupportedVersion, versionEx.Error);

        var unknownType = Prefix(FrameType.Hello, 0);
        unknownType[3] = 99;
        var typeEx = await Assert.ThrowsAsync<FrameProtocolException>(async () =>
            await FrameCodec.ReadAsync(new MemoryStream(unknownType), FrameLimits.MaxResponseFrameBytes));
        Assert.Equal(FrameError.UnknownType, typeEx.Error);
    }

    [Fact]
    public async Task PartialReads()
    {
        using var buffer = new MemoryStream();
        var response = new ResponseHeader { Id = "7", Ok = true, Engine = WorkerEngines.DotNet, WorkingSetBytes = 123 };
        var html = "<html>" + new string('ş', 20_000) + "</html>";
        await WorkerWire.WriteResponseAsync(buffer, response, [WorkerWire.EncodeBody(html)]);
        await WorkerWire.WriteShutdownAsync(buffer, new ShutdownHeader { Reason = "test" });
        var bytes = buffer.ToArray();

        // Her okuma en çok 1..7 bayt döndürür (boru davranışı).
        var trickle = new TrickleStream(bytes);
        var first = await FrameCodec.ReadAsync(trickle, FrameLimits.MaxResponseFrameBytes);
        var second = await FrameCodec.ReadAsync(trickle, FrameLimits.MaxResponseFrameBytes);
        var end = await FrameCodec.ReadAsync(trickle, FrameLimits.MaxResponseFrameBytes);

        Assert.NotNull(first);
        Assert.Equal(response, WorkerWire.DecodeHeader(first, WorkerJsonContext.Default.ResponseHeader));
        Assert.Equal(html, WorkerWire.DecodeBody(first.Bodies[0]));
        Assert.NotNull(second);
        Assert.Equal(FrameType.Shutdown, second.Type);
        Assert.Null(end);

        // Çerçeve ortasında EOF → Truncated (sessiz null değil).
        for (var cut = 1; cut < 40; cut++)
        {
            var truncated = new TrickleStream(bytes[..cut]);
            var ex = await Assert.ThrowsAsync<FrameProtocolException>(async () =>
                await FrameCodec.ReadAsync(truncated, FrameLimits.MaxResponseFrameBytes));
            Assert.Equal(FrameError.Truncated, ex.Error);
        }
    }

    [Fact]
    public void DecodeBody_InvalidUtf8_Rejected()
    {
        var ex = Assert.Throws<FrameProtocolException>(() => WorkerWire.DecodeBody([0xC3, 0x28]));
        Assert.Equal(FrameError.InvalidBody, ex.Error);
    }

    private static byte[] Prefix(FrameType type, int headerLength)
    {
        var prefix = new byte[FrameCodec.PrefixLength];
        prefix[0] = FrameCodec.Magic0;
        prefix[1] = FrameCodec.Magic1;
        prefix[2] = ProtocolVersion.Current;
        prefix[3] = (byte)type;
        BinaryPrimitives.WriteUInt32LittleEndian(prefix.AsSpan(4), (uint)headerLength);
        return prefix;
    }

    /// <summary>Her ReadAsync çağrısında az sayıda bayt döndüren akış.</summary>
    private sealed class TrickleStream(byte[] data) : Stream
    {
        private int _position;
        private int _turn;

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => data.Length;
        public override long Position { get => _position; set => throw new NotSupportedException(); }

        public override int Read(byte[] buffer, int offset, int count) => Read(buffer.AsSpan(offset, count));

        public override int Read(Span<byte> buffer)
        {
            var n = Math.Min(Math.Min(buffer.Length, (_turn++ % 7) + 1), data.Length - _position);
            data.AsSpan(_position, n).CopyTo(buffer);
            _position += n;
            return n;
        }

        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(Read(buffer.Span));

        public override void Flush() { }
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
