using System;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Collections.Generic;
using System.Linq;

class Packet
{
    public string Symbol { get; set; }
    public string BuySell { get; set; }
    public int Quantity { get; set; }
    public int Price { get; set; }
    public int Sequence { get; set; }
}

class Program
{
    const int Port = 3000;
    const string Host = "127.0.0.1";

    static void Main()
    {
        var allPackets = RequestAllPackets();

        // Find missing sequence numbers
        var expectedSeq = Enumerable.Range(1, allPackets.Max(p => p.Sequence));
        var missing = expectedSeq.Except(allPackets.Select(p => p.Sequence)).ToList();

        foreach (var seq in missing)
        {
            var packet = RequestResendPacket(seq);
            if (packet != null)
                allPackets.Add(packet);
        }

        // Sort packets by sequence
        allPackets = allPackets.OrderBy(p => p.Sequence).ToList();

        // Write to JSON
        File.WriteAllText("packets.json", JsonSerializer.Serialize(allPackets, new JsonSerializerOptions { WriteIndented = true }));

        Console.WriteLine("All packets written to packets.json");
    }

    static List<Packet> RequestAllPackets()
    {
        using var client = new TcpClient(Host, Port);
        var stream = client.GetStream();

        // Send CallType 1
        stream.Write(new byte[] { 1, 0 }, 0, 2);

        var packets = new List<Packet>();
        var buffer = new byte[17];

        while (true)
        {
            try
            {
                int read = stream.Read(buffer, 0, buffer.Length);
                if (read == 0) break; // connection closed
                if (read < 17) continue;

                packets.Add(ParsePacket(buffer));
            }
            catch
            {
                break;
            }
        }

        return packets;
    }

    static Packet RequestResendPacket(int sequence)
    {
        using var client = new TcpClient(Host, Port);
        var stream = client.GetStream();

        stream.Write(new byte[] { 2, (byte)sequence }, 0, 2);  // Note: resendSeq is 1 byte; only supports up to 255

        var buffer = new byte[17];
        int read = stream.Read(buffer, 0, buffer.Length);

        if (read == 17)
            return ParsePacket(buffer);

        return null;
    }

    static Packet ParsePacket(byte[] data)
    {
        return new Packet
        {
            Symbol = Encoding.ASCII.GetString(data, 0, 4),
            BuySell = Encoding.ASCII.GetString(data, 4, 1),
            Quantity = ReadInt32BigEndian(data, 5),
            Price = ReadInt32BigEndian(data, 9),
            Sequence = ReadInt32BigEndian(data, 13),
        };
    }

    static int ReadInt32BigEndian(byte[] data, int offset)
    {
        return (data[offset] << 24) | (data[offset + 1] << 16) |
               (data[offset + 2] << 8) | data[offset + 3];
    }
}
