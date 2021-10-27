using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace MXReader {
    public class MXReader {
        private const int DNS_PORT = 53;

        private string dns;

        private object threadLock;

        private int threadID = 0;

        private IPAddress ip;

        private UdpClient udpClient;

        private StringBuilder globalBuilder;

        public MXReader(IPAddress ip) {
            this.ip = ip;
            this.udpClient = new UdpClient();
            globalBuilder = new StringBuilder();

            threadLock = new object();
        }

        public void Connect() {
            this.udpClient.Connect(this.ip, DNS_PORT);
        }

        public string Query(List<string> domains) {
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
                    builder.AppendFormat("\t{0}", Message.ResponseCodeMessageShort(answerMX.RCode));
                    continue;
                }

                if (answerMX.Answers.Length == 0) {
                    builder.AppendFormat("\tEmpty answer section\n");
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
            int id;
            lock(this.threadLock) {
                id = threadID++;
            }

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

            // Console.WriteLine("q {0} t {1}", questionMx.ID, id);

            // lock (this.udpClient) {
            //     int i = this.udpClient.Send(data, data.Length);
            //     data = this.udpClient.Receive(ref endpoint);
            // }
            int i = udpClient.Send(data, data.Length);
            data = udpClient.Receive(ref endpoint);

            // Console.WriteLine(id + " - " + endpoint.Address.ToString() + ":" + endpoint.Port);

            Message answerMX = Message.FromRawData(data);

            Console.WriteLine("mx {0} {1}", id, questionMx.ID == answerMX.ID);

            if (answerMX.RCode > 0) {
                builder.AppendFormat("\t{0}\n", Message.ResponseCodeMessageShort(answerMX.RCode));
            } else {

                if (answerMX.Answers.Length == 0) {
                    builder.AppendFormat("\tEmpty answer section\n");
                }

                foreach (var answer in answerMX.Answers) {
                    builder.Append('\t');

                    if (answer.Type == QType.MX) {
                        Message.MXRData mx = (Message.MXRData)answer.RData;

                        builder.AppendFormat("Preferences: {0}, Exchanger: {1}, IP: ", mx.Preference, mx.Exchange);

                        Message questionA = new();
                        questionA.Add(QType.A, mx.Exchange);

                        data = questionA.Encode();

                        // Console.WriteLine("q {0} t {1}", questionA.ID, id);
                        // lock (this.udpClient) {
                        //     udpClient.Send(data, data.Length);

                        //     data = this.udpClient.Receive(ref endpoint);
                        // }

                        udpClient.Send(data, data.Length);
                        data = udpClient.Receive(ref endpoint);


                        //Console.WriteLine(id + " - " + endpoint.Address.ToString() + ":" + endpoint.Port);

                        Message answerA = Message.FromRawData(data);

                        Console.WriteLine("a {0} {1}", id, questionA.ID == answerA.ID);


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
            lock (this.globalBuilder) {
                this.globalBuilder.Append(builder);
            }
        }

        public void QueryDomainWorkerOld(object o) {
            int id;
            lock(this.threadLock) {
                id = threadID++;
            }

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

            // Console.WriteLine("q {0} t {1}", questionMx.ID, id);

            // lock (this.udpClient) {
            //     int i = this.udpClient.Send(data, data.Length);
            //     data = this.udpClient.Receive(ref endpoint);
            // }
            int i = udpClient.Send(data, data.Length);
            data = udpClient.Receive(ref endpoint);

            // Console.WriteLine(id + " - " + endpoint.Address.ToString() + ":" + endpoint.Port);

            Message answerMX = Message.FromRawData(data);

            Console.WriteLine("mx {0} {1}", id, questionMx.ID == answerMX.ID);

            if (answerMX.RCode > 0) {
                builder.AppendFormat("\t{0}\n", Message.ResponseCodeMessageShort(answerMX.RCode));
            } else {

                if (answerMX.Answers.Length == 0) {
                    builder.AppendFormat("\tEmpty answer section\n");
                }

                foreach (var answer in answerMX.Answers) {
                    builder.Append('\t');

                    if (answer.Type == QType.MX) {
                        Message.MXRData mx = (Message.MXRData)answer.RData;

                        builder.AppendFormat("Preferences: {0}, Exchanger: {1}, IP: ", mx.Preference, mx.Exchange);

                        Message questionA = new();
                        questionA.Add(QType.A, mx.Exchange);

                        data = questionA.Encode();

                        // Console.WriteLine("q {0} t {1}", questionA.ID, id);
                        // lock (this.udpClient) {
                        //     udpClient.Send(data, data.Length);

                        //     data = this.udpClient.Receive(ref endpoint);
                        // }

                        udpClient.Send(data, data.Length);
                        data = udpClient.Receive(ref endpoint);


                        //Console.WriteLine(id + " - " + endpoint.Address.ToString() + ":" + endpoint.Port);

                        Message answerA = Message.FromRawData(data);

                        Console.WriteLine("a {0} {1}", id, questionA.ID == answerA.ID);


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
            lock (this.globalBuilder) {
                this.globalBuilder.Append(builder);
            }
        }

        public string QueryAsync(List<string> domains) {
            globalBuilder.AppendFormat("DNS: {0}\n\n", this.ip.ToString());

            Thread[] pool = new Thread[domains.Count];
            for (int i = 0; i < domains.Count; i++) {
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