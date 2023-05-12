namespace Fingerprinter;

public enum PacketType : byte {
    Command = 0x1,
    Data = 0x2,
    Ack = 0x7,
    EndData = 0x8
}
