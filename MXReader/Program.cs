using System;
using System.Collections;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace MXReader {
    class Program {

        private const int HEADER_LENGTH = 12;
        private const int DNS_PORT = 53;

        private static string DNSIPAddress = "8.8.8.8";

        private static string Domain = "gmail.com";
        // private static string Domain = "hotmail.com";
        

        private const int QUERY_LENGTH = 512;

        private static ushort id = 0;

        private static byte[] CreateQuery() {
            byte[] data = new byte[QUERY_LENGTH];

            ++id;
            data[0] = (byte)(id >> 8); data[1] = (byte)id;
            data[2] = 0b00000000; data[3] = 0b00000000;
            data[4] = 0; data[5] = 1;
            data[6] = 0; data[7] = 0;
            data[8] = 0; data[9] = 0;
            data[10] = 0; data[11] = 0;

            int offset = HEADER_LENGTH;

            foreach (string label in Domain.Split('.')) {
                data[offset++] = (byte)label.Length;

                byte[] asciiLabel = Encoding.ASCII.GetBytes(label);

                Array.Copy(asciiLabel, 0, data, offset, asciiLabel.Length);
                offset += asciiLabel.Length;
            }
            data[offset++] = 0;
            data[offset++] = 0; data[offset++] = 15;
            data[offset++] = 0; data[offset++] = 1;

            return data;
        }

        private static ushort ToUInt16BigEndian(byte[] data, int offset) {
            return (ushort)((data[offset] << 8) + data[offset + 1]);
        }

        private static ushort DecodeName(byte[] data, ushort offset){
            StringBuilder builder = new();
            ushort length = data[offset++];

            while (length > 0) {
                builder.Append(Encoding.ASCII.GetChars(data, offset, length));
                offset += length;
                length = data[offset++];
                if (length > 0) {
                    builder.Append('.');
                }
            }

            return offset;
        }
        

        private static void Decode (byte[] data) {
            //Header
            bool b = BitConverter.IsLittleEndian;
            ushort id = ToUInt16BigEndian(data, 0);
            byte opcode = (byte)((data[2] & 0b0111_1000) >> 3);
            bool QR = (data[2] & 0b1000_0000) > 0;
            bool AA = (data[2] & 0b0000_0100) > 0;
            bool TC = (data[2] & 0b0000_0010) > 0;
            byte rcode = (byte)(data[3] & 0b0001_1111);
            ushort qdCount = ToUInt16BigEndian(data, 4);
            ushort anCount = ToUInt16BigEndian(data, 6);
            ushort nsCount = ToUInt16BigEndian(data, 8);
            ushort arCount = ToUInt16BigEndian(data, 10);


            //Question
            // if qdCount
            ushort offset = DecodeName(data, HEADER_LENGTH);

            ushort type = ToUInt16BigEndian(data, offset);
            offset += 2;

            ushort class_ = ToUInt16BigEndian(data, offset);
            offset += 2;

            //Answer
            //offset = DecodeName(data, offset);
            offset += 2;

            ushort type2 = ToUInt16BigEndian(data, offset);
            offset += 2;

            ushort class2_ = ToUInt16BigEndian(data, offset);
            offset += 2;

            uint ttl = System.Buffers.Binary.BinaryPrimitives.ReadUInt32BigEndian(new ReadOnlySpan<byte>(data, offset, 4));
            // ushort ttl = Convert. ToUInt16BigEndian(data, offset);
            uint ttl2 = BitConverter.ToUInt32(data, offset);
            offset += 4;

            TimeSpan ts = TimeSpan.FromSeconds(ttl);
            TimeSpan ts2 = TimeSpan.FromSeconds(ttl2);

            ushort rdlength = ToUInt16BigEndian(data, offset);
            offset += 2;

            string rdata = Encoding.ASCII.GetString(data, offset, rdlength);
            offset += rdlength;
            


        }

        private static void Query(){
            UdpClient udp = new (DNSIPAddress, DNS_PORT);
            byte[] data = CreateQuery();
            int i = udp.Send(data, data.Length);

            IPEndPoint endpoint = null;
			data = udp.Receive(ref endpoint);

            char[] messege = Encoding.ASCII.GetChars(data);
            Decode(data);
        }

        static void Main(string[] args) {
            Query();



        }
    }
}
