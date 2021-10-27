/*
Author: Marcin Pietrzykowski
*/

using System;
using System.Net;
using System.Text;

namespace MXReader {

    public interface IRData {
        void Decode(byte[] data, ref ushort offset, ushort rdlength);
    }

    public class AData : IRData {

        private IPAddress[] ips;

        public IPAddress[] IPs {
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
            StringBuilder builder = new();
            foreach (var item in this.ips) {
                builder.AppendFormat("{0}, ", item.ToString());
            }
            builder.Length -= 2;
            return builder.ToString();
        }
    }

    public class MXRData : IRData {

        private string exchange;

        private short preference;

        public string Exchange {
            get { return this.exchange; }
        }

        public short Preference {
            get { return this.preference; }
        }

        public void Decode(byte[] data, ref ushort offset, ushort rdlength) {
            this.preference = Utils.ToInt16BigEndian(data, offset);
            offset += 2;

            this.exchange = Utils.DecodeDomainName(data, ref offset);
        }

        public override string ToString() {
            return string.Format("Preference: {0}, Exchange: {1}", preference, exchange);
        }
    }

    public class NSRData : IRData {

        private string nsdname;

        public void Decode(byte[] data, ref ushort offset, ushort rdlength) {
            this.nsdname = Utils.DecodeDomainName(data, ref offset);
        }

        public override string ToString() {
            return nsdname;
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
}