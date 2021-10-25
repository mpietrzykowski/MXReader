using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace MXReader {
    class Program {
        
        // private static string DNSIPAddress = "89.228.4.126";
        private static string DNSIPAddress = "8.8.8.8";
        // private static string DNSIPAddress = "153.19.105.120";

        private static string dns;

        private static string reportFile;

        private static string domainListFile;

        static void Main(string[] args) {
            List<string> domains = new List<string>();
            foreach (var item in args) {
                if (item.StartsWith("-dns=")) {
                    dns = item.Substring(5);
                }
                else if (item.StartsWith("-domains=")) {
                    domainListFile = item.Substring(9);
                }
                else if (item.StartsWith("-report=")) {
                    reportFile = item.Substring(8);
                }
                else {
                    domains.Add(item);
                }
            }


            MXReader reader = new (DNSIPAddress);
            reader.Connect();

            // string[] domains = new string[]{
            //     "gmail.com",
            //     "hotmail.com",
            //     "aol.com",
            //     "yahoo.com"
            // };

            //Console.WriteLine(reader.Query(domains));
            Console.WriteLine(reader.QueryAsync(domains));
        }
    }
}
