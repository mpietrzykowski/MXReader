/*
Author: Marcin Pietrzykowski
*/

using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace MXReader {
    //MX record reader form DNS
    public sealed class MXReader {

        #region Fields

        private const int DNS_PORT = 53;

        private const int TIMEOUT = 10000;

        private readonly string dns;

        private readonly IPAddress ip;

        private readonly StringBuilder report;

        private readonly object reportLock = new ();

        private readonly ManualResetEvent signal = new (false);

        private int taskCount;

        #endregion

        #region Constructors

        public MXReader(IPAddress ip) {
            this.ip = ip;
            this.dns = ip.ToString();
            this.report = new StringBuilder();
        }

        #endregion

        #region Methods

        public string Query(IList<string> domains) {
            //Do tej metody pewnie zrobiłbym methodę Connet i Close z wyłuskaniem UdpClient ale 
            //że to metoda tylko do celów testowych to korzystam Query Worker...
            this.report.Clear();

            foreach (var item in domains) {
                QueryWorker((string)item);
            }

            return this.report.ToString();
        }

        public string QueryAsync(IList<string> domains) {
            this.report.Clear();

            this.taskCount = domains.Count;
            this.signal.Reset();

            for (int i = 0; i < domains.Count; i++) {
                ThreadPool.QueueUserWorkItem(QueryWorker, domains[i]);
            }

            this.signal.WaitOne();

            return this.report.ToString();
        }

        private void QueryWorker(object o) {
            try {
                using (UdpClient udpClient = new()) {
                    udpClient.Client.SendTimeout = TIMEOUT;
                    udpClient.Client.ReceiveTimeout = TIMEOUT;

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
            catch (SocketException se) {
                Console.WriteLine("Warning! Socket Error: " + se.ErrorCode + " for domain " + o);
            }
            catch (Exception ex){
                Console.WriteLine("Warning! Something wrong happen during reading MX record for domain: " + o + ". " + ex.Message);
            }
            finally {
                if (Interlocked.Decrement(ref this.taskCount) == 0) {
                    this.signal.Set();
                }
            }
        }
    
        #endregion
    }
}