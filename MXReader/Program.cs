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
        // private static string DNSIPAddress = "8.8.8.8";
        // private static string DNSIPAddress = "153.19.105.120";

        private static string dns;

        private static string reportFile;

        private static string domainListFile;

        static void Main(string[] args) {
            List<string> domains = new List<string>();
            // string domains = new List<string>();


            foreach (var item in args) {
                if (item.StartsWith("--dns=")) {
                    dns = item.Substring(6);
                }
                else if (item.StartsWith("--domains=")) {
                    domainListFile = item.Substring(10);
                }
                else if (item.StartsWith("--report=")) {
                    reportFile = item.Substring(9);
                }
                else if (item == "--help" || item == "-h") {
                    Console.WriteLine(
                        "Useage: MXReader [options] domain1 domain2 ... domainN\n" +
                        "Options arguments\n" +
                        "  --dns=IP\t\tip of DNS server\n" +
                        "  --domains=path\tpath to file with list of domains\n" +
                        "  --report=path\t\tpath to report file. If the file exist it will be overwriten\n" +
                        "  -h|--help\t\tprint this help message"
                    );
                    return;
                }
                else if (item.StartsWith("-")) {
                    Console.WriteLine("Unknown parameter. Please use -h for help.");
                    return;
                }
                else {
                    domains.Add(item);
                }
            }

            if (!string.IsNullOrWhiteSpace(domainListFile)) {
                if (!File.Exists(domainListFile)) {
                    Console.WriteLine("domains file does not exist.");
                    return;
                }

                string fileContent = File.ReadAllText(domainListFile);
                domains.AddRange(
                    fileContent.Split(Array.Empty<char>(), StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                );
            }

            if (!IPAddress.TryParse(dns, out IPAddress ip)){
                Console.WriteLine("Invalid DNS IP address.");
                return;
            }

            MXReader reader = new (ip);
            reader.Connect();

            // domains = new List<string>{
            //     "gmail.com",
            //     "hotmail.com",
            //     "aol.com",
            //     "yahoo.com"
            // };

            // string report = reader.Query(domains);
            string report = reader.QueryAsync(domains);

            Console.WriteLine(report);

            File.WriteAllText(reportFile, report);
        }
    }
}
