using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace extracTOR.Helper
{
    public class HashLittle2
    {
        private const uint Init = 0xDEADBEEF;
        private static uint RotL(uint x, int n) => (x << n) | (x >> (32 - n));

        private static uint ReadUInt32LE(byte[] data, int offset)
        {
            return (uint)(data[offset] |
                (data[offset + 1] << 8) |
                (data[offset + 2] << 16) |
                (data[offset + 3] << 24));
        }

        public static (uint C, uint B) HashLittle2ToArray(string key)
        {
            byte[] data = Encoding.UTF8.GetBytes(key);

            int length = data.Length;
            int offset = 0;

            unchecked
            {
                uint a = Init + (uint)length;
                uint b = a;
                uint c = a;

                while (length > 12)
                {
                    a += ReadUInt32LE(data, offset);
                    b += ReadUInt32LE(data, offset + 4);
                    c += ReadUInt32LE(data, offset + 8);

                    // Mix
                    a -= c;
                    a ^= RotL(c, 4);
                    c += b;

                    b -= a;
                    b ^= RotL(a, 6);
                    a += c;

                    c -= b;
                    c ^= RotL(b, 8);
                    b += a;

                    a -= c;
                    a ^= RotL(c, 16);
                    c += b;

                    b -= a;
                    b ^= RotL(a, 19);
                    a += c;

                    c -= b;
                    c ^= RotL(b, 4);
                    b += a;

                    length -= 12;
                    offset += 12;
                }

                switch (length)
                {
                    case 12:
                        c += ReadUInt32LE(data, offset + 8);
                        b += ReadUInt32LE(data, offset + 4);
                        a += ReadUInt32LE(data, offset);
                        break;

                    case 11:
                        c += (uint)(
                            data[offset + 8] |
                            (data[offset + 9] << 8) |
                            (data[offset + 10] << 16));

                        b += ReadUInt32LE(data, offset + 4);
                        a += ReadUInt32LE(data, offset);
                        break;

                    case 10:
                        c += (uint)(
                            data[offset + 8] |
                            (data[offset + 9] << 8));

                        b += ReadUInt32LE(data, offset + 4);
                        a += ReadUInt32LE(data, offset);
                        break;

                    case 9:
                        c += data[offset + 8];
                        b += ReadUInt32LE(data, offset + 4);
                        a += ReadUInt32LE(data, offset);
                        break;

                    case 8:
                        b += ReadUInt32LE(data, offset + 4);
                        a += ReadUInt32LE(data, offset);
                        break;

                    case 7:
                        b += (uint)(
                            data[offset + 4] |
                            (data[offset + 5] << 8) |
                            (data[offset + 6] << 16));

                        a += ReadUInt32LE(data, offset);
                        break;

                    case 6:
                        b += (uint)(
                            data[offset + 4] |
                            (data[offset + 5] << 8));

                        a += ReadUInt32LE(data, offset);
                        break;

                    case 5:
                        b += data[offset + 4];
                        a += ReadUInt32LE(data, offset);
                        break;

                    case 4:
                        a += ReadUInt32LE(data, offset);
                        break;

                    case 3:
                        a += (uint)(
                            data[offset] |
                            (data[offset + 1] << 8) |
                            (data[offset + 2] << 16));
                        break;

                    case 2:
                        a += (uint)(
                            data[offset] |
                            (data[offset + 1] << 8));
                        break;

                    case 1:
                        a += data[offset];
                        break;

                    default:
                        return (c, b);
                }

                // Final
                c ^= b;
                c -= RotL(b, 14);

                a ^= c;
                a -= RotL(c, 11);

                b ^= a;
                b -= RotL(a, 25);

                c ^= b;
                c -= RotL(b, 16);

                a ^= c;
                a -= RotL(c, 4);

                b ^= a;
                b -= RotL(a, 14);

                c ^= b;
                c -= RotL(b, 24);

                return (c, b);
            }
        }

        public static ulong GetHash(string key)
        {
            var (c, b) = HashLittle2ToArray(key);

            return ((ulong)b << 32) | c;
        }
    }
}
