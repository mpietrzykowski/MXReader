using System;
using System.Collections.Generic;
using System.Net;
using System.Text;

namespace MXReader {
    public class Message {

        private const int HEADER_LENGTH = 12;

        private const int QUERY_LENGTH = 512;

        private static ushort nextID = 0;

        private static object myLock = new object();

        public static string ResponseCodeMessageShort(int rcode) {
            switch (rcode) {
                case 0:
                    return "No error condition";
                case 1:
                    return "Format error";
                case 2:
                    return "Server failure";
                case 3:
                    return "Name Error";
                case 4:
                    return "Not Implemented";
                case 5:
                    return "Refused";
                default:
                    return "Not implemented";
            }
        }

        public interface IRData {
            void Decode(byte[] data, ref ushort offset, ushort rdlength);
        }

        public class AData : IRData {

            private IPAddress[] ips;

            public IPAddress[] IPs{
                get { return this.ips; }
            }

            public void Decode(byte[] data, ref ushort offset, ushort rdlength) {
                int count = rdlength / 4;
                ips = new IPAddress[count];

                for (int i = 0; i < ips.Length; i++) {
                    ips[i] = new IPAddress(new ReadOnlySpan<byte>(data, offset, 4));
                    offset += 4;
                }
            }

            public override string ToString() {
                return this.ips.ToString();
            }
        }

        public class NSRData : IRData {
            private string nsdname;
            public void Decode(byte[] data, ref ushort offset, ushort rdlength) {
                this.nsdname = DecodeDomainName(data, ref offset);
            }

            public override string ToString() {
                return nsdname;
            }
        }

        public class MXRData : IRData {

            private string exchange;

            private short preference;

            public string Exchange {
                get { return this.exchange; }
            }

            public short Preference{
                get { return this.preference; }
            }

            public void Decode(byte[] data, ref ushort offset, ushort rdlength) {
                this.preference = ToInt16BigEndian(data, offset);
                offset += 2;

                // this.exchange = Encoding.ASCII.GetString(data, offset, rdlength - 2);
                this.exchange = DecodeDomainName(data, ref offset);
            }

            public override string ToString() {
                return string.Format("Preference: {0}, Exchange: {1}", preference, exchange);
            }
        }

        public class NotImplementedRData : IRData {
            public void Decode(byte[] data, ref ushort offset, ushort rdlength) {
                offset += rdlength;
            }

            public override string ToString() {
                return "Not Implemented record type";
            }
        }

        public class Section {
            protected string name;

            protected QType type;

            protected ushort class_ = 1;

            public string Name {
                get => this.name;
                set => this.name = value;
            }

            public QType Type {
                get => this.type;
                set => this.type = value;
            }

            public bool Encode(byte[] data, ref ushort offset) {

                //+6 bo jeszcze 6 bajtów prócz samej wiadomości
                if (this.name.Length + 6 > data.Length - offset - 1) {
                    return false;
                }

                foreach (string label in this.name.Split('.')) {
                    data[offset++] = (byte)label.Length;

                    byte[] asciiLabel = Encoding.ASCII.GetBytes(label);

                    Array.Copy(asciiLabel, 0, data, offset, asciiLabel.Length);
                    offset += (ushort)asciiLabel.Length;
                }

                data[offset++] = 0;

                data[offset++] = 0; data[offset++] = (byte)this.type;
                data[offset++] = 0; data[offset++] = (byte)this.class_;


                return true;
            }

            public virtual void Decode(byte[] data, ref ushort offset) {
                this.name = DecodeDomainName(data, ref offset);
                this.type = (QType)ToUInt16BigEndian(data, offset);
                offset += 2;
                this.class_ = ToUInt16BigEndian(data, offset);
                offset += 2;
            }

            public override string ToString() {
                return string.Format("Domain: {0}, Type: {1}", name, type);
            }
        }

        public class ResourceRecord : Section {
            protected uint ttl;

            protected ushort rdlength;

            protected IRData rdata;

            public IRData RData {
                get { 
                    return rdata; 
                }
            }

            public override void Decode(byte[] data, ref ushort offset) {
                base.Decode(data, ref offset);

                this.ttl = System.Buffers.Binary.BinaryPrimitives.ReadUInt32BigEndian(new ReadOnlySpan<byte>(data, offset, 4));
                offset += 4;

                this.rdlength = ToUInt16BigEndian(data, offset);
                offset += 2;

                switch (this.type) {
                    case QType.MX:
                        this.rdata = new MXRData();
                        break;
                    case QType.A:
                        this.rdata = new AData();
                        break;
                    case QType.NS:
                        this.rdata = new NSRData();
                        break;
                    default:
                        this.rdata = new NotImplementedRData();
                        break;
                }

                this.rdata.Decode(data, ref offset, this.rdlength);
            }

            public override string ToString() {
                return string.Format("{0}, TTL: {1}, {2}", base.ToString(), ttl, rdata.ToString());
            }
        }

        #region Header

        ushort id;
        bool qr;
        byte opcode;
        bool aa;
        bool tc;
        byte rcode;
        ushort qdCount;
        ushort anCount;
        ushort nsCount;
        ushort arCount;

        #endregion

        #region Sections

        private IList<Section> questions;

        private ResourceRecord[] answers;

        private ResourceRecord[] authority;

        private ResourceRecord[] additional;

        #endregion

        public ResourceRecord[] Answers {
            get { return this.answers; }
        }

        public int ID {
            get { return this.id; }
        }

        public byte RCode {
            get { return this.rcode; }
        }

        public Message() {
            this.questions = new List<Section>();
        }

        private void Docode(byte[] data) {
            //Header
            this.id = ToUInt16BigEndian(data, 0);
            this.opcode = (byte)((data[2] & 0b0111_1000) >> 3);
            this.qr = (data[2] & 0b1000_0000) > 0;
            this.aa = (data[2] & 0b0000_0100) > 0;
            this.tc = (data[2] & 0b0000_0010) > 0;
            this.rcode = (byte)(data[3] & 0b0001_1111);
            this.qdCount = ToUInt16BigEndian(data, 4);
            this.anCount = ToUInt16BigEndian(data, 6);
            this.nsCount = ToUInt16BigEndian(data, 8);
            this.arCount = ToUInt16BigEndian(data, 10);

            ushort offset = HEADER_LENGTH;

            this.questions = DecodeRecords<Section>(data, ref offset, this.qdCount);
            this.answers = DecodeRecords<ResourceRecord>(data, ref offset, this.anCount);
            this.authority = DecodeRecords<ResourceRecord>(data, ref offset, this.nsCount);
            this.additional = DecodeRecords<ResourceRecord>(data, ref offset, this.arCount);
        }

        private static T[] DecodeRecords<T>(byte[] data, ref ushort offset, int recordsCount) where T : Section, new() {
            T[] records = new T[recordsCount];

            for (int i = 0; i < records.Length; i++) {
                T r = new();
                r.Decode(data, ref offset);
                records[i] = r;
            }

            return records;
        }

        public void Add(QType type, string question) {
            this.questions.Add(new Section() { Name = question, Type = type });
        }

        public byte[] Encode() {
            byte[] data = new byte[QUERY_LENGTH];

            lock (myLock) {
                this.id = nextID++;
            }

            this.qdCount = (ushort)this.questions.Count;

            data[0] = (byte)(this.id >> 8); data[1] = (byte)this.id;
            data[2] = 0b00000000; data[3] = 0b00000000;
            data[4] = (byte)(this.qdCount >> 8); data[5] = (byte)this.qdCount;
            data[6] = 0; data[7] = 0;
            data[8] = 0; data[9] = 0;
            data[10] = 0; data[11] = 0;

            ushort offset = HEADER_LENGTH;

            foreach (var item in this.questions) {
                item.Encode(data, ref offset);
            }

            return data;
        }

        public void Print() {
            switch (this.rcode) {
                case 0:
                    Console.WriteLine("Answers:");
                    if (this.anCount == 0) {
                        Console.WriteLine("Empty");
                    } else {
                        foreach (var item in this.answers) {
                            Console.WriteLine(item);
                        }
                    }

                    Console.WriteLine("Authority Section:");
                    if (this.arCount == 0) {
                        Console.WriteLine("Empty");
                    } else {
                        foreach (var item in this.authority) {
                            Console.WriteLine(item);
                        }
                    }

                    Console.WriteLine("Additional Section:");
                    if (this.nsCount == 0) {
                        Console.WriteLine("Empty");
                    } else {
                        foreach (var item in this.additional) {
                            Console.WriteLine(item);
                        }
                    }
                    break;
                case 1:
                    Console.WriteLine("Format error (1)- The name server was unable to interpret the query.");
                    break;
                case 2:
                    Console.WriteLine("Server failure (2)- The name server was " +
                                "unable to process this query due to a " +
                                "problem with the name server.");
                    break;
                case 3:
                    Console.WriteLine("Name Error (3) - Meaningful only for " +
                                "responses from an authoritative name " +
                                "server, this code signifies that the " +
                                "domain name referenced in the query does " +
                                "not exist.");
                    break;
                case 4:
                    Console.WriteLine("Not Implemented (4) - The name server does "+
                                "not support the requested kind of query.");
                    break;
                case 5:
                    Console.WriteLine("Refused (5) - The name server refuses to perform the specified operation for " +
                                "policy reasons.  For example, a name server may not wish to provide the " +
                                "information to the particular requester, or a name server may not wish to perform " +
                                "a particular operation (e.g., zone  transfer)for particular data.");
                    break;
                default:
                    break;
            }

        }


        public static Message FromRawData(byte[] data) {
            Message message = new();
            message.Docode(data);
            return message;
        }

        //TODO: chyba na rekursje
        private static string DecodeDomainName(byte[] data, ref ushort offset) {
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

        private static short ToInt16BigEndian(byte[] data, ushort offset) {
            return (short)((data[offset] << 8) + data[offset + 1]);
        }

        private static ushort ToUInt16BigEndian(byte[] data, ushort offset) {
            return (ushort)((data[offset] << 8) + data[offset + 1]);
        }
    }
}