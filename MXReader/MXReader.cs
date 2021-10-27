using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace MXReader {
    public sealed class MXReader {
        private const int DNS_PORT = 53;

        private readonly IPAddress ip;

        private readonly string dns;

        private readonly object reportLock = new ();

        private readonly StringBuilder report;

        public MXReader(IPAddress ip) {
            this.ip = ip;
            this.dns = ip.ToString();
            this.report = new StringBuilder();
        }

        private void QueryWorker(object o) {
            using (UdpClient udpClient = new()) {
                StringBuilder builder = new();

                string domain = (string)o;
                Message questionMx = new(QType.MX, domain);
                byte[] data = questionMx.Encode();

                IPEndPoint endpoint = null;
                udpClient.Connect(ip, DNS_PORT);
                int i = udpClient.Send(data, data.Length);
                data = udpClient.Receive(ref endpoint);

                Message answerMX = Message.FromData(data);

                foreach (var answer in answerMX.Answers) {
                    if (answer.Type == QType.MX) {

                        MXRData mx = (MXRData)answer.RData;

                        Message questionA = new(QType.A, mx.Exchange);
                        data = questionA.Encode();

                        udpClient.Send(data, data.Length);
                        data = udpClient.Receive(ref endpoint);

                        Message answerA = Message.FromData(data);

                        if (answerA.RCode == 0 && answerA.Answers.Length > 0) {
                            foreach (var item in answerA.Answers) {
                                if (item.Type == QType.A) {
                                    AData aData = (AData)item.RData;

                                    foreach (var ip in aData.IPs) {
                                        builder.AppendFormat(
                                            "{0}, {1}, {2}, {3}{4}",
                                            ip.ToString(), mx.Exchange, mx.Preference, this.dns, Environment.NewLine
                                        );
                                    }
                                }
                            }
                        }
                    }
                }

                lock (this.reportLock) {
                    this.report.Append(builder);
                }

                udpClient.Close();
            }
        }

        public string Query(IList<string> domains) {
            this.report.Clear();

            foreach (var item in domains) {
                QueryWorker((string)item);
            }

            return this.report.ToString();
        }

        public string QueryAsync(IList<string> domains) {
            this.report.Clear();

            Thread[] pool = new Thread[domains.Count];
            for (int i = 0; i < domains.Count; i++) {
                Thread thread = new (new ParameterizedThreadStart(QueryWorker));
                thread.Start(domains[i]);
                pool[i] = thread;
            }

            foreach (var thread in pool) {
                thread.Join();
            }

            return this.report.ToString();
        }
    }
}