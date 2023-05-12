using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Fingerprinter;

public struct Message {
    public const int MAX_DATA_SIZE = 512;
    public ushort startCode = FingerprintSensor.FINGERPRINT_STARTCODE;
    public byte[] address = new byte[4];
    public Instruction instruction;
    public PacketType type;
    public ushort length;
    public byte[] data = new byte[MAX_DATA_SIZE];
    public ushort Checksum => (ushort)((byte)type + (length + 2) + (byte)instruction + data.Take(length).Sum(x => x));

    public Message() {
        address = new byte[4];
        data = new byte[MAX_DATA_SIZE];

    }
    public Message(PacketType type, Instruction instruction, byte[] data) {
        this.type = type;
        this.length = (ushort)(data.Length + 1);
        this.address[0] = 0xFF;
        this.address[1] = 0xFF;
        this.address[2] = 0xFF;
        this.address[3] = 0xFF;
        this.instruction = instruction;
        Array.Copy(data, this.data, data.Length);
    }

    public override string ToString() {
        var sb = new StringBuilder();
        sb.Append($"Address: {address[0]:X2} {address[1]:X2} {address[2]:X2} {address[3]:X2} ");
        sb.Append($"Type: {type} ");
        sb.Append($"Length: {length} ");
        sb.Append($"Instruction: {instruction} ");
        sb.Append($"Data: ");
        for (int i = 0; i < length - 1; i++)
        {
            sb.Append($"{data[i]:X2} ");
        }
        return sb.ToString();
    }

    public byte[] Serialize() {
        List<byte> buffer = new List<byte>();
        ushort wire_length = (ushort)(this.length + 2);
        // Debugger.Break();
        
        buffer.Add((byte)(this.startCode >> 8));
        buffer.Add((byte)(this.startCode & 0xFF));
        buffer.Add(this.address[0]);
        buffer.Add(this.address[1]);
        buffer.Add(this.address[2]);
        buffer.Add(this.address[3]);
        buffer.Add((byte)this.type);
        buffer.Add((byte)(wire_length >> 8));
        buffer.Add((byte)(wire_length & 0xFF));
        buffer.Add((byte)this.instruction);
        buffer.AddRange(this.data.Take(length-1));
        var sum = Checksum;
        buffer.Add((byte)(sum >> 8));
        buffer.Add((byte)(sum & 0xFF));
        return buffer.ToArray();
    }

    public static Message MakeCommand(Instruction instruction, byte[] data) => new Message(PacketType.Command, instruction, data);
}