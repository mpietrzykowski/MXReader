using System;

namespace MXReader {
    class Message {
        class Header{
            ushort id;
            byte opcode;
            bool qr;
            bool aa;
            bool tc;
            byte rcode;
            ushort qdCount;
            ushort anCount;
            ushort nsCount;
            ushort arCount;
        }
    }
}
