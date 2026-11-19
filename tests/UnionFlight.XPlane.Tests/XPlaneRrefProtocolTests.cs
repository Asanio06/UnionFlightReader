using System.Buffers.Binary;
using System.Text;
using UnionFlight.XPlane.Udp;

namespace UnionFlight.XPlane.Tests;

public sealed class XPlaneRrefProtocolTests
{
    [Fact]
    public void BuildPackets_Batches_And_Pads_To_413_Bytes()
    {
        var requests = Enumerable.Range(0, 100)
            .Select(index => new RrefRequest(10, index, $"sim/test/value_{index}"))
            .ToArray();

        var packets = XPlaneRrefProtocol.BuildPackets(requests);

        Assert.True(packets.Count > 1);
        Assert.All(packets, packet => Assert.Equal(413, packet.Length));
        Assert.All(packets, packet =>
        {
            Assert.Equal("RREF", Encoding.ASCII.GetString(packet, 0, 4));
            Assert.Equal(0, packet[4]);
        });
    }

    [Fact]
    public void BuildPackets_Encodes_Frequency_Id_And_Dataref()
    {
        var packet = XPlaneRrefProtocol.BuildPackets(
            new[] { new RrefRequest(15, 42, "sim/test/value") }).Single();

        Assert.Equal(15, BinaryPrimitives.ReadInt32LittleEndian(packet.AsSpan(5, 4)));
        Assert.Equal(42, BinaryPrimitives.ReadInt32LittleEndian(packet.AsSpan(9, 4)));

        var datarefLength = Array.IndexOf(packet, (byte)0, 13) - 13;
        Assert.Equal("sim/test/value", Encoding.ASCII.GetString(packet, 13, datarefLength));
    }

    [Fact]
    public void Parse_Reads_Id_And_Value()
    {
        var packet = Response(7, 123.5f);
        Span<RrefUpdate> updates = stackalloc RrefUpdate[2];

        var count = XPlaneRrefProtocol.Parse(packet, updates);

        Assert.Equal(1, count);
        Assert.Equal(7, updates[0].Id);
        Assert.Equal(123.5f, updates[0].Value);
    }

    [Fact]
    public void Parse_Reads_Multiple_Records()
    {
        var packet = new byte[21];
        "RREF"u8.CopyTo(packet);
        packet[4] = 0;
        BinaryPrimitives.WriteInt32LittleEndian(packet.AsSpan(5, 4), 1);
        BinaryPrimitives.WriteSingleLittleEndian(packet.AsSpan(9, 4), 10f);
        BinaryPrimitives.WriteInt32LittleEndian(packet.AsSpan(13, 4), 2);
        BinaryPrimitives.WriteSingleLittleEndian(packet.AsSpan(17, 4), 20f);

        Span<RrefUpdate> updates = stackalloc RrefUpdate[4];
        var count = XPlaneRrefProtocol.Parse(packet, updates);

        Assert.Equal(2, count);
        Assert.Equal(1, updates[0].Id);
        Assert.Equal(10f, updates[0].Value);
        Assert.Equal(2, updates[1].Id);
        Assert.Equal(20f, updates[1].Value);
    }

    [Fact]
    public void Parse_Ignores_Unknown_Header_And_Truncated_Packets()
    {
        Span<RrefUpdate> updates = stackalloc RrefUpdate[2];

        Assert.Equal(0, XPlaneRrefProtocol.Parse(new byte[] { 1, 2, 3 }, updates));

        var badHeader = Response(1, 1f);
        badHeader[0] = (byte)'X';
        Assert.Equal(0, XPlaneRrefProtocol.Parse(badHeader, updates));

        var truncated = Response(1, 1f)[..12];
        Assert.Equal(0, XPlaneRrefProtocol.Parse(truncated, updates));
    }

    private static byte[] Response(int id, float value)
    {
        var packet = new byte[13];
        "RREF"u8.CopyTo(packet);
        packet[4] = 0;
        BinaryPrimitives.WriteInt32LittleEndian(packet.AsSpan(5, 4), id);
        BinaryPrimitives.WriteSingleLittleEndian(packet.AsSpan(9, 4), value);
        return packet;
    }
}
