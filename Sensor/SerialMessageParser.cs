using System.Collections.Generic;
using System.Threading.Tasks;

namespace Fingerprinter;

public class SerialMessageParser {
    internal const ushort FINGERPRINT_STARTCODE = 0xEF01;
    const ushort RETRY_TIME_MS = 10;
    const ushort DEFAULT_TIMEOUT_MS = RETRY_TIME_MS * 100;
    public static void SendPacket(Message packet, SafeSerialPort serial) {
        var buffer = packet.Serialize();
        try {
            serial.Write(buffer, 0, buffer.Length);
        } catch (System.Exception e) {
            Logger.Exception("Failed to write to serial port", e);
        }
    }
    public static async Task<(ErrorCode error, Message packet)> ReceivePacket(SafeSerialPort serial) {
        /*
        The packet format is as follows:
        a    |b          |c |d    |e       |f    
        EF 01 XX XX XX XX YY ZZ ZZ DD .. DD SS SS

        a [2]: Header (0xEF01)
        b [4]: Sensor address (0xFFFFFFFF)
        c [1]: Packet type (0x01: cmd, 0x02: dat, 0x07: ack, 0x08: end)
        d [2]: Packet length (|e|+|f|, max=256)
        e [d-2]: Data ()
        f [2]: Checksum (sum of all bytes from |c| to |e|, mod 2^16-1)
        */
        var raw = new List<byte>();
        var packet = new Message();
        byte b;
        ushort idx = 0, timer = 0;
        if (!serial.Open()) {
            Logger.Error("<<<: Sensor not found");
            return (ErrorCode.PortClosed, packet);
        }
        while (true)
        {
            try {
                while (serial.BytesToRead <= 0) {
                    await Task.Delay(RETRY_TIME_MS);
                    timer++;
                    if (timer >= DEFAULT_TIMEOUT_MS) {
                        Logger.Error("<<<: Timeout");
                        return (ErrorCode.Timeout, packet);
                    }
                }
                b = serial.ReadByte() ?? throw new System.Exception("Failed to read from serial port");
            } catch (System.Exception) {
                return (ErrorCode.Timeout, packet);
            }
            raw.Add(b);

            switch (idx) {
                case 0:
                    if (b != (FINGERPRINT_STARTCODE >> 8)) {
                        continue;
                    }
                    packet.startCode = (ushort)(b << 8);
                    break;
                case 1:
                    packet.startCode |= b;
                    if (packet.startCode != FINGERPRINT_STARTCODE) {
                        Logger.Error("<<<: Bad Packet: Wrong Header");
                        return (ErrorCode.BadPacket, packet);
                    }
                    break;
                case 2:
                case 3:
                case 4:
                case 5:
                    packet.address[idx - 2] = b;
                    break;
                case 6:
                    packet.type = (PacketType)b;
                    break;
                case 7:
                    packet.length = (ushort)((ushort)b << 8);
                    break;
                case 8:
                    packet.length |= b;
                    if (packet.length == 0) {
                        return (ErrorCode.Ok, packet);
                    }
                    break;
                default:
                    var dataIndex = idx - 9;
                    packet.data[dataIndex] = b;
                    if ((dataIndex + 1) == packet.length) {
                        return (ErrorCode.Ok, packet);
                    }
                    break;
            }
            idx++;
            if ((idx + 9) >= Message.MAX_DATA_SIZE) {
                Logger.Error("<<<: Bad Packet: Overflow");
                return (ErrorCode.BadPacket, packet);
            }
        }
    }
}
