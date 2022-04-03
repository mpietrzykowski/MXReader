/*
Author: Marcin Pietrzykowski
*/

using System.Text;

namespace MXReader {
    //Differenet Utils Methods
    public static class Utils {
        
        public static string DecodeDomainName(byte[] data, ref ushort offset) {
            StringBuilder builder = new();

            byte length = data[offset++];
            ushort globalOffset = 0;

            while (length > 0) {
                if ((length & 0b1100_0000) == 0b1100_0000) {
                    if (globalOffset == 0) {
                        globalOffset = (ushort)(offset + 1);
                    }
                    offset = GetPointer(data, (ushort)(offset - 1));
                    length = data[offset++];
                } else {
                    builder.Append(Encoding.ASCII.GetChars(data, offset, length));
                    offset += length;
                    length = data[offset++];

                    if (length > 0) {
                        builder.Append('.');
                    }
                }
            }

            if (globalOffset > 0) {
                offset = globalOffset;
            }

            return builder.ToString();
        }

        private static ushort GetPointer(byte[] data, ushort offset) {
            return (ushort)(((data[offset] & 0b0011_1111) << 8) + data[offset + 1]);
        }

        public static short ToInt16BigEndian(byte[] data, ushort offset) {
            return (short)((data[offset] << 8) + data[offset + 1]);
        }

        public static ushort ToUInt16BigEndian(byte[] data, ushort offset) {
            return (ushort)((data[offset] << 8) + data[offset + 1]);
        }
    }
}