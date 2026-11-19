using System.Buffers.Binary;
using System.Text;

namespace UnionFlight.XPlane.Udp;

public readonly record struct RrefRequest(int Frequency, int Id, string Dataref);

public readonly record struct RrefUpdate(int Id, float Value);

/// <summary>
/// X-Plane RREF wire format. A 413-byte datagram starts with "RREF\0"
/// and contains one or more (frequency, id, dataref\0) entries.
/// Responses are "RREF\0" followed by (id, float) records.
/// </summary>
internal static class XPlaneRrefProtocol
{
    public const int PacketSize = 413;
    private const int HeaderSize = 5;

    public static IReadOnlyList<byte[]> BuildPackets(IEnumerable<RrefRequest> requests)
    {
        var packets = new List<byte[]>();
        var current = NewPacket();

        foreach (var request in requests)
        {
            var entry = BuildEntry(request);
            if (current.Count + entry.Length > PacketSize)
            {
                Flush(current, packets);
            }

            current.AddRange(entry);
        }

        Flush(current, packets);
        return packets;
    }

    public static int Parse(ReadOnlySpan<byte> packet, Span<RrefUpdate> destination)
    {
        if (packet.Length < HeaderSize + 8 ||
            !packet[..4].SequenceEqual("RREF"u8) ||
            packet[4] != 0)
        {
            return 0;
        }

        var count = 0;
        var position = HeaderSize;

        while (position + 8 <= packet.Length && count < destination.Length)
        {
            var id = BinaryPrimitives.ReadInt32LittleEndian(packet.Slice(position, 4));
            var value = BinaryPrimitives.ReadSingleLittleEndian(packet.Slice(position + 4, 4));
            destination[count++] = new RrefUpdate(id, value);
            position += 8;
        }

        return count;
    }

    private static List<byte> NewPacket()
    {
        var packet = new List<byte>(PacketSize);
        packet.AddRange("RREF"u8.ToArray());
        packet.Add(0);
        return packet;
    }

    private static byte[] BuildEntry(RrefRequest request)
    {
        var dataref = Encoding.ASCII.GetBytes(request.Dataref);
        var entry = new byte[8 + dataref.Length + 1];

        BinaryPrimitives.WriteInt32LittleEndian(entry.AsSpan(0, 4), request.Frequency);
        BinaryPrimitives.WriteInt32LittleEndian(entry.AsSpan(4, 4), request.Id);
        dataref.CopyTo(entry.AsSpan(8));
        entry[^1] = 0;

        return entry;
    }

    private static void Flush(List<byte> current, List<byte[]> packets)
    {
        if (current.Count <= HeaderSize)
        {
            return;
        }

        while (current.Count < PacketSize)
        {
            current.Add(0);
        }

        packets.Add(current.ToArray());
        current.Clear();
        current.AddRange("RREF"u8.ToArray());
        current.Add(0);
    }
}
