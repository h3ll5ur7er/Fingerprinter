using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Fingerprinter;

public static class ByteExtensions
{
    public static string Hex(this byte value) => value.ToString("X2");
    public static string Hex(this IEnumerable<byte> values) => string.Join(" ", values.Select(value => value.ToString("X2")));
    public static ArraySegment<byte>[] Partition(this byte[] values, params int[] partLengths) {
        /*
            2     4           1  2     1 -2           2
            00 11 22 33 44 55 66 77 88 99 AA BB CC DD EE FF
            00 11          0   2  2
            22 33 44 55    2   4  4
            66             6   1  1
            77 88          7   2  2
            99             9   1  1
            AA BB CC DD   10  -2  4 = 16-10 + -2    4 = length - offset + partLengths[i]
            EE FF         14   2  2
        */
        var parts = new ArraySegment<byte>[partLengths.Length+1];
        var offset = 0;
        for (var i = 0; i < partLengths.Length; i++) {
            var length = partLengths[i];
            if (length < 0)
                length = values.Length - offset + partLengths[i];
            parts[i] = new ArraySegment<byte>(values, offset, length);
            offset += length;
        }
        parts[partLengths.Length] = new ArraySegment<byte>(values, offset, values.Length - offset);
        return parts;
    }
    public static void ArrayPartitioningTest() {
        var values = Enumerable.Range(0, 16).Select(i => (byte)i).ToArray();
        var parts = values.Partition(2, 4, 1, 2, 1, -2, 2);
        foreach (var part in parts)
            Logger.Trace(part.Hex());
    }

    public static ushort ReadUShort(this IEnumerable<byte> values) => BitConverter.ToUInt16(values.Reverse().ToArray(), 0);
    public static uint ReadUInt(this IEnumerable<byte> values) => BitConverter.ToUInt32(values.Reverse().ToArray(), 0);
    public static string ReadString(this IEnumerable<byte> values) => Encoding.UTF8.GetString(values.ToArray());
}