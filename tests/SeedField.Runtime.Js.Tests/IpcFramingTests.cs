using System.Buffers.Binary;
using SeedField.Runtime.Js.Worker;

namespace SeedField.Runtime.Js.Tests;

public sealed class IpcFramingTests
{
    [Fact]
    public void FrameRoundTripsUtf8()
    {
        var stream = new MemoryStream();
        IpcFraming.Write(stream, "{\"kind\":\"observe\",\"message\":\"种子域\"}");
        stream.Position = 0;

        Assert.Equal(
            "{\"kind\":\"observe\",\"message\":\"种子域\"}",
            IpcFraming.Read(stream));
    }

    [Fact]
    public void EmptyStreamMeansCleanEof()
    {
        Assert.Null(IpcFraming.Read(new MemoryStream()));
    }

    [Fact]
    public void OversizedWriteIsRejected()
    {
        var stream = new MemoryStream();
        var exception = Assert.Throws<IpcProtocolException>(() =>
            IpcFraming.Write(stream, new string('a', IpcFraming.MaxFrameBytes + 1)));

        Assert.Equal("FRAME_SIZE", exception.Code);
    }

    [Fact]
    public void OversizedHeaderIsRejected()
    {
        var stream = new MemoryStream();
        Span<byte> header = stackalloc byte[4];
        BinaryPrimitives.WriteInt32LittleEndian(header, IpcFraming.MaxFrameBytes + 1);
        stream.Write(header);
        stream.Position = 0;

        var exception = Assert.Throws<IpcProtocolException>(() => IpcFraming.Read(stream));
        Assert.Equal("FRAME_SIZE", exception.Code);
    }

    [Fact]
    public void TruncatedHeaderIsRejected()
    {
        var stream = new MemoryStream(new byte[] { 1, 0 });
        var exception = Assert.Throws<IpcProtocolException>(() => IpcFraming.Read(stream));
        Assert.Equal("TRUNCATED_HEADER", exception.Code);
    }

    [Fact]
    public void TruncatedPayloadIsRejected()
    {
        var stream = new MemoryStream();
        Span<byte> header = stackalloc byte[4];
        BinaryPrimitives.WriteInt32LittleEndian(header, 5);
        stream.Write(header);
        stream.Write(new byte[] { (byte)'{', (byte)'"' });
        stream.Position = 0;

        var exception = Assert.Throws<IpcProtocolException>(() => IpcFraming.Read(stream));
        Assert.Equal("TRUNCATED_FRAME", exception.Code);
    }

    [Fact]
    public void InvalidUtf8IsRejected()
    {
        var stream = new MemoryStream();
        Span<byte> header = stackalloc byte[4];
        BinaryPrimitives.WriteInt32LittleEndian(header, 1);
        stream.Write(header);
        stream.WriteByte(0xFF);
        stream.Position = 0;

        var exception = Assert.Throws<IpcProtocolException>(() => IpcFraming.Read(stream));
        Assert.Equal("INVALID_UTF8", exception.Code);
    }

    [Fact]
    public void EnvelopeRoundTripsHello()
    {
        var envelope = new WorkerEnvelope(
            Kind: WorkerProtocol.Hello,
            CivilizationId: "B",
            Script: "function decide(){return [];}",
            MaxStatements: 50_000,
            MemoryLimitBytes: 8_000_000,
            TimeoutMs: 50);

        var parsed = WorkerProtocol.Parse(WorkerProtocol.Serialize(envelope));

        Assert.Equal(WorkerProtocol.Hello, parsed.Kind);
        Assert.Equal("B", parsed.CivilizationId);
        Assert.Equal(50_000, parsed.MaxStatements);
    }

    [Fact]
    public void EnvelopeWithoutKindIsRejected()
    {
        Assert.Throws<IpcProtocolException>(() =>
            WorkerProtocol.Parse("{\"protocolVersion\":1}"));
    }
}
