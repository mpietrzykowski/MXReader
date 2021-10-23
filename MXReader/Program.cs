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

        private static string DNSIPAddress = "8.8.4.4";

        private static string Domain = "gmail.com";
        // private static string Domain = "aol.com";
        

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


        private static void Query(){
            UdpClient udp = new (DNSIPAddress, DNS_PORT);
            byte[] data = CreateQuery();
            int i = udp.Send(data, data.Length);

            IPEndPoint endpoint = null;
			data = udp.Receive(ref endpoint);

            char[] messege = Encoding.ASCII.GetChars(data);
            Message m = Message.FromRawData(data);
        }

        static void Main(string[] args) {
            Query();



        }
    }
}
