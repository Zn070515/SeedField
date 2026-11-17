using System.Buffers.Binary;
using System.Text;

namespace SeedField.Runtime.Js.Worker;

public sealed class IpcProtocolException : Exception
{
    public IpcProtocolException(string code, string message)
        : base(message)
    {
        Code = code;
    }

    public string Code { get; }
}

/// <summary>
/// Length-prefixed UTF-8 frames over stdin/stdout. The 4-byte little-endian prefix
/// makes each message self-delimiting; oversized/truncated/invalid frames are rejected
/// before any JSON parsing.
/// </summary>
public static class IpcFraming
{
    public const int MaxFrameBytes = 4 * 1024 * 1024;
    private const int HeaderBytes = 4;

    private static readonly UTF8Encoding Utf8 = new(
        encoderShouldEmitUTF8Identifier: false,
        throwOnInvalidBytes: true);

    public static void Write(Stream stream, string text)
    {
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentNullException.ThrowIfNull(text);

        var bytes = Utf8.GetBytes(text);
        if (bytes.Length == 0 || bytes.Length > MaxFrameBytes)
        {
            throw new IpcProtocolException(
                "FRAME_SIZE",
                $"Frame must be 1..{MaxFrameBytes} bytes; got {bytes.Length}.");
        }

        Span<byte> header = stackalloc byte[HeaderBytes];
        BinaryPrimitives.WriteInt32LittleEndian(header, bytes.Length);
        stream.Write(header);
        stream.Write(bytes);
        stream.Flush();
    }

    /// <summary>Reads one frame; null means the peer closed the stream cleanly.</summary>
    public static string? Read(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);

        Span<byte> header = stackalloc byte[HeaderBytes];
        var headerRead = ReadAtMost(stream, header);
        if (headerRead == 0)
        {
            return null;
        }

        if (headerRead != HeaderBytes)
        {
            throw new IpcProtocolException("TRUNCATED_HEADER", "Frame header was truncated.");
        }

        var length = BinaryPrimitives.ReadInt32LittleEndian(header);
        if (length <= 0 || length > MaxFrameBytes)
        {
            throw new IpcProtocolException(
                "FRAME_SIZE",
                $"Frame length {length} is outside 1..{MaxFrameBytes}.");
        }

        var payload = new byte[length];
        var payloadRead = ReadAtMost(stream, payload);
        if (payloadRead != length)
        {
            throw new IpcProtocolException("TRUNCATED_FRAME", "Frame payload was truncated.");
        }

        try
        {
            return Utf8.GetString(payload);
        }
        catch (DecoderFallbackException exception)
        {
            throw new IpcProtocolException("INVALID_UTF8", exception.Message);
        }
    }

    private static int ReadAtMost(Stream stream, Span<byte> buffer)
    {
        var total = 0;
        while (total < buffer.Length)
        {
            var read = stream.Read(buffer[total..]);
            if (read <= 0)
            {
                break;
            }

            total += read;
        }

        return total;
    }
}
