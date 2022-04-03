/*
Author: Marcin Pietrzykowski
*/

using System;
using System.Text;

namespace MXReader {
    //Implementation of sections from message
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

            data[offset++] = (byte)((ushort)this.type >> 8); data[offset++] = (byte)this.type;
            data[offset++] = 0; data[offset++] = (byte)this.class_;

            return true;
        }

        public virtual void Decode(byte[] data, ref ushort offset) {
            this.name = Utils.DecodeDomainName(data, ref offset);
            this.type = (QType)Utils.ToUInt16BigEndian(data, offset);
            offset += 2;
            this.class_ = Utils.ToUInt16BigEndian(data, offset);
            offset += 2;
        }

        public override string ToString() {
            return string.Format("Domain: {0}, Type: {1}", name, type);
        }
    }

    public class ResourceRecord : Section {
        private uint ttl;

        private ushort rdlength;

        private IRData rdata;

        public IRData RData {
            get {
                return rdata;
            }
        }

        public override void Decode(byte[] data, ref ushort offset) {
            base.Decode(data, ref offset);

            //TODO: własna i metoda do umieszczenia w Utils
            this.ttl = System.Buffers.Binary.BinaryPrimitives.ReadUInt32BigEndian(new ReadOnlySpan<byte>(data, offset, 4));
            offset += 4;

            this.rdlength = Utils.ToUInt16BigEndian(data, offset);
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
}