using System;
using System.Collections.Generic;
using System.Text;

namespace MXReader {
    class Message {

        private const int HEADER_LENGTH = 12;

        private interface IRData {
            void Decode(byte[] data, ref ushort offset, ushort rdlength);
        }

        private class MXRData : IRData {
            private short preference;

            private string exchange;

            public void Decode(byte[] data, ref ushort offset, ushort rdlength) {
                this.preference = ToInt16BigEndian(data, offset);
                offset += 2;

                // this.exchange = Encoding.ASCII.GetString(data, offset, rdlength - 2);
                this.exchange = DecodeDomainName(data, ref offset);
            }
        }

        private class Section {
            protected string name;

            protected ushort type;

            protected ushort class_;

            public virtual void Decode(byte[] data, ref ushort offset) {
                this.name = DecodeDomainName(data, ref offset);
                this.type = ToUInt16BigEndian(data, offset);
                offset += 2;
                this.class_ = ToUInt16BigEndian(data, offset);
                offset += 2;
            }
        }

        private class ResourceRecord : Section {
            protected uint ttl;

            protected ushort rdlength;

            protected IRData rdata;

            public override void Decode(byte[] data, ref ushort offset) {
                base.Decode(data, ref offset);

                this.ttl = System.Buffers.Binary.BinaryPrimitives.ReadUInt32BigEndian(new ReadOnlySpan<byte>(data, offset, 4));
                offset += 4;

                this.rdlength = ToUInt16BigEndian(data, offset);
                offset += 2;

                switch (this.type) {
                    case 15:
                        this.rdata = new MXRData();
                        break;
                    default:
                        throw new NotImplementedException("Resource record type not implemented or invalid.");
                }

                this.rdata.Decode(data, ref offset, this.rdlength);
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

        private Section[] questions;

        private ResourceRecord[] answers;

        private ResourceRecord[] authority;

        private ResourceRecord[] additional;

        #endregion

        public Message() { }

        private void Docode(byte[] data){
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

        public static Message FromRawData(byte[] data){
            Message message = new();
            message.Docode(data);
            return message;
        }

        //TODO: chyba na rekursje
        private static string DecodeDomainName(byte[] data, ref ushort offset){
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

            if (globalOffset > 0){
                offset = globalOffset;
            }

            return builder.ToString();
        }

        private static ushort GetPointer(byte[]data, ushort offset){
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
