using System.Buffers.Binary;
using SeedField.Runtime.Js.Worker;

namespace SeedField.Runtime.Js.Tests;

public sealed class IpcFuzzingTests
{
    [Fact]
    public void RandomFramesOnlyProduceValidTextOrProtocolErrors()
    {
        var random = new Random(20261117);
        Span<byte> header = stackalloc byte[4];

        for (var iteration = 0; iteration < 300; iteration++)
        {
            var stream = new MemoryStream();
            var declaredLength = random.Next(-16, 512);
            BinaryPrimitives.WriteInt32LittleEndian(header, declaredLength);
            stream.Write(header);

            var payloadLength = random.Next(0, 64);
            var payload = new byte[payloadLength];
            random.NextBytes(payload);
            stream.Write(payload);
            stream.Position = 0;

            try
            {
                var text = IpcFraming.Read(stream);
                Assert.False(string.IsNullOrEmpty(text));
            }
            catch (IpcProtocolException)
            {
                // Expected for malformed/short/oversized frames.
            }
        }
    }

    [Fact]
    public void RandomEnvelopesOnlyProduceProtocolErrors()
    {
        var random = new Random(99);
        const string alphabet = "{}[]\":,\\abcdefghijklmnopqrstuvwxyz0123456789 \n\t";

        for (var iteration = 0; iteration < 300; iteration++)
        {
            var length = random.Next(0, 256);
            var chars = new char[length];
            for (var index = 0; index < chars.Length; index++)
            {
                chars[index] = alphabet[random.Next(alphabet.Length)];
            }

            try
            {
                WorkerProtocol.Parse(new string(chars));
            }
            catch (IpcProtocolException)
            {
                // Expected for malformed JSON / missing kind.
            }
        }
    }

    [Fact]
    public void ValidEnvelopeSurvivesFuzzRoundTrip()
    {
        var envelope = new WorkerEnvelope(
            Kind: WorkerProtocol.Observe,
            Id: 42,
            ObservationJson: "{\"tick\":1}");
        var text = WorkerProtocol.Serialize(envelope);

        var parsed = WorkerProtocol.Parse(text);

        Assert.Equal(WorkerProtocol.Observe, parsed.Kind);
        Assert.Equal(42, parsed.Id);
    }
}
