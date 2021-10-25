using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace MXReader {
    public class MXReader {
        private const int DNS_PORT = 53;

        private string dns;

        private IPAddress ip;

        private UdpClient udpClient;

        private StringBuilder globalBuilder;

        public MXReader(string dns) {
            this.ip = IPAddress.Parse(dns);
            this.udpClient = new UdpClient();
            globalBuilder = new StringBuilder();
        }

        public void Connect() {
            this.udpClient.Connect(this.ip, DNS_PORT);
        }

        public string Query(string[] domains) {
            IPEndPoint endpoint = null;
            StringBuilder builder = new();
            builder.AppendFormat("DNS: {0}\n\n", this.ip.ToString());

            byte[] data;

            foreach (var domain in domains) {
                builder.Append(domain);
                builder.Append('\n');

                Message questionMx = new();
                questionMx.Add(QType.MX, domain);

                data = questionMx.Encode();
                int i = this.udpClient.Send(data, data.Length);

                data = this.udpClient.Receive(ref endpoint);
                Message answerMX = Message.FromRawData(data);

                if (answerMX.RCode > 0) {
                    builder.Append(Message.ResponseCodeMessageShort(answerMX.RCode));
                    continue;
                }

                foreach (var answer in answerMX.Answers) {
                    builder.Append('\t');

                    if (answer.Type == QType.MX) {
                        Message.MXRData mx = (Message.MXRData)answer.RData;

                        builder.AppendFormat("Preferences: {0}, Exchanger: {1}, IP: ", mx.Preference, mx.Exchange);

                        Message questionA = new();
                        questionA.Add(QType.A, mx.Exchange);

                        data = questionA.Encode();
                        udpClient.Send(data, data.Length);

                        data = this.udpClient.Receive(ref endpoint);
                        Message answerA = Message.FromRawData(data);

                        if (answerA.RCode > 0) {
                            builder.Append(Message.ResponseCodeMessageShort(answerA.RCode));
                            builder.Append('\n');
                            continue;
                        }

                        if (answerA.Answers.Length > 0) {
                            foreach (var item in answerA.Answers) {
                                if (item.Type == QType.A) {
                                    Message.AData aData = (Message.AData)item.RData;

                                    foreach (var ip in aData.IPs) {
                                        builder.AppendFormat("{0}, ", ip.ToString());
                                    }
                                }
                            }
                            builder.Length -= 2;
                        } else {
                            builder.Append("empty answer section");
                        }
                    }
                    builder.Append('\n');
                }
            }

            return builder.ToString();
        }

        public void QueryDomainWorker(object o) {
            UdpClient udpClient = new UdpClient();
            udpClient.Connect(ip, DNS_PORT);

            IPEndPoint endpoint = null;
            StringBuilder builder = new();
            byte[] data;
            string domain = (string)o;

            builder.Append(domain);
            builder.Append('\n');

            Message questionMx = new();
            questionMx.Add(QType.MX, domain);

            data = questionMx.Encode();
            // lock (this.udpClient) {
            //     int i = this.udpClient.Send(data, data.Length);
            //     data = this.udpClient.Receive(ref endpoint);
            // }
            int i = udpClient.Send(data, data.Length);
            data = udpClient.Receive(ref endpoint);

            Message answerMX = Message.FromRawData(data);

            if (answerMX.RCode > 0) {
                builder.Append(Message.ResponseCodeMessageShort(answerMX.RCode));
            }

            foreach (var answer in answerMX.Answers) {
                builder.Append('\t');

                if (answer.Type == QType.MX) {
                    Message.MXRData mx = (Message.MXRData)answer.RData;

                    builder.AppendFormat("Preferences: {0}, Exchanger: {1}, IP: ", mx.Preference, mx.Exchange);

                    Message questionA = new();
                    questionA.Add(QType.A, mx.Exchange);

                    data = questionA.Encode();
                    // lock (this.udpClient) {
                    //     udpClient.Send(data, data.Length);

                    //     data = this.udpClient.Receive(ref endpoint);
                    // }
                    
                    udpClient.Send(data, data.Length);
                    data = udpClient.Receive(ref endpoint);
                    
                    Message answerA = Message.FromRawData(data);

                    if (answerA.RCode > 0) {
                        builder.Append(Message.ResponseCodeMessageShort(answerA.RCode));
                        builder.Append('\n');
                        continue;
                    }

                    if (answerA.Answers.Length > 0) {
                        foreach (var item in answerA.Answers) {
                            if (item.Type == QType.A) {
                                Message.AData aData = (Message.AData)item.RData;

                                foreach (var ip in aData.IPs) {
                                    builder.AppendFormat("{0}, ", ip.ToString());
                                }
                            }
                        }
                        builder.Length -= 2;
                    } else {
                        builder.Append("empty answer section");
                    }
                }
                builder.Append('\n');
            }

            lock (this.globalBuilder) {
                this.globalBuilder.Append(builder);
            }
        }


        public string QueryAsync(string[] domains) {
            Thread[] pool = new Thread[domains.Length];
            for (int i = 0; i < domains.Length; i++) {
                Thread thread = new Thread(new ParameterizedThreadStart(QueryDomainWorker));
                thread.Start(domains[i]);
                pool[i] = thread;
            }

            foreach (var thread in pool)
            {
                thread.Join();
            }

            return this.globalBuilder.ToString();
        }
    }
}