/*
Author: Marcin Pietrzykowski
*/

namespace MXReader {
    //Implementation of messege
    public class Message {

        #region Fields

        private const int HEADER_LENGTH = 12;

        private const int QUERY_LENGTH = 512;

        private static ushort nextID = 1;

        private static readonly object idLock = new ();
        
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

        private Section[] questions;

        private ResourceRecord[] answers;

        private ResourceRecord[] authority;

        private ResourceRecord[] additional;

        #endregion

        #endregion

        #region Properties 

        public ResourceRecord[] Additional {
            get { return this.additional; }
        }

        public ResourceRecord[] Answers {
            get { return this.answers; }
        }

        public int ID {
            get { return this.id; }
        }

        public byte RCode {
            get { return this.rcode; }
        }

        #endregion

        #region Constructors

        public Message() {
        }

        public Message(QType type, string domain) {
            this.questions = new Section[1] { new Section(){ Type = type, Name = domain } };
        }

        #endregion

        #region Methods

        private void Docode(byte[] data) {
            //Header
            this.id = Utils.ToUInt16BigEndian(data, 0);
            this.opcode = (byte)((data[2] & 0b0111_1000) >> 3);
            this.qr = (data[2] & 0b1000_0000) > 0;
            this.aa = (data[2] & 0b0000_0100) > 0;
            this.tc = (data[2] & 0b0000_0010) > 0;
            this.rcode = (byte)(data[3] & 0b0001_1111);
            this.qdCount = Utils.ToUInt16BigEndian(data, 4);
            this.anCount = Utils.ToUInt16BigEndian(data, 6);
            this.nsCount = Utils.ToUInt16BigEndian(data, 8);
            this.arCount = Utils.ToUInt16BigEndian(data, 10);

            ushort offset = HEADER_LENGTH;

            //Sections
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

        public byte[] Encode() {
            byte[] data = new byte[QUERY_LENGTH];

            lock (idLock) {
                this.id = nextID++;
            }

            this.qdCount = (ushort)this.questions.Length;

            data[0] = (byte)(this.id >> 8); data[1] = (byte)this.id;
            data[2] = 0b00000000; data[3] = 0b00000000;
            data[4] = (byte)(this.qdCount >> 8); data[5] = (byte)this.qdCount;
            data[6] = 0; data[7] = 0;
            data[8] = 0; data[9] = 0;
            data[10] = 0; data[11] = 0;

            ushort offset = HEADER_LENGTH;

            foreach (var item in this.questions) {
                if (!item.Encode(data, ref offset)){
                    //Messege is truncated
                    data[2] = 0b00000010;
                    break;
                }
            }

            return data;
        }

        public static Message FromData(byte[] data) {
            Message message = new();
            message.Docode(data);
            return message;
        }

        public static string ResponseCodeMessageToStringShort(int rcode) {
            switch (rcode) {
                case 0:
                    return "No error condition";
                case 1:
                    return "Format error (1)";
                case 2:
                    return "Server failure (2)";
                case 3:
                    return "Name Error (3)";
                case 4:
                    return "Not Implemented (4)";
                case 5:
                    return "Refused (5)";
                default:
                    return "Reserved for future use (6-15)";
            }
        }
 
        #endregion
    }
}