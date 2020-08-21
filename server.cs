using System;
using System.Collections.Generic;
using System.IO.MemoryMappedFiles;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Net;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Text;
using System.Threading;

namespace VimFastFind {
    class Client {
        static Dictionary<DirConfig, Matcher> __matchercache = new Dictionary<DirConfig, Matcher>();

        Logger _logger = new Logger("server");

        TcpClient _client;
        DirConfig _config;
        Matcher _matcher;

        public Client(TcpClient client) {
            _client = client;
            (new Thread(ev_client) { IsBackground = true }).Start();
        }

        void _UpdateConfig(DirConfig config) {
            if (_config != null && _config.Equals(config)) return;
            _config = config;

            bool ownMatchers = false;

            if (_matcher != null) {
                if (_matcher.Free()) __matchercache.Remove(_matcher.Config);
                _matcher = null;
            }
            lock (__matchercache) {
                if (!__matchercache.TryGetValue(config, out _matcher)) {
                    _logger.Trace("created matcher: " + config.ScanDirectory);
                    __matchercache[config] = _matcher = new Matcher(config);
                    ownMatchers = true;
                } else {
                    _logger.Trace("matcher cache hit: " + config.ScanDirectory);
                    _matcher.Ref();
                }
                _logger.Trace("__matchercache size: " + __matchercache.Count);
            }

            if (ownMatchers) {
                ThreadPool.QueueUserWorkItem(delegate {
                    _matcher.Go();
                });
            }
        }

        void ev_client() {
            try {
                _logger.Trace("listening");
                using (Stream stream = _client.GetStream()) {
                    using (StreamWriter wtr = new StreamWriter(stream, Encoding.ASCII)) {
                        using (StreamReader rdr = new StreamReader(stream)) {

                            while (true) {
                                string line = rdr.ReadLine();
                                if (line == null) return;
                                // _logger.Trace("got cmd {0}", line);

                                line = Regex.Replace(line, @"^\s*#.*", "");
                                if (String.IsNullOrWhiteSpace(line)) continue;
                                string[] s = line.Split(new char[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);

                                if (s[0] == "config") {
                                    var config = new ConfigParser().LoadConfig(s[1])[0];
                                    _UpdateConfig(config);

                                } else if (s[0] == "grep" && s[1] == "match") {
                                    s = line.Split(new char[] { ' ', '\t' }, 3, StringSplitOptions.RemoveEmptyEntries);
                                    StringBuilder sb = new StringBuilder();
                                    int i = 0;
                                    foreach (string m in _matcher.GrepMatch(line.Substring(line.IndexOf("match")+6), 200)) {
                                        sb.Append(m);
                                        sb.Append("\n");
                                        i++;
                                    }
                                    wtr.Write(sb.ToString());
                                    wtr.Write("\n");

                                } else if (s[0] == "find" && s[1] == "match") {
                                    s = line.Split(new char[] { ' ', '\t' }, 3, StringSplitOptions.RemoveEmptyEntries);
                                    StringBuilder sb = new StringBuilder();
                                    int i = 0;
                                    foreach (string m in _matcher.PathMatch(line.Substring(line.IndexOf("match")+6).ToLowerInvariant(), 200)) {
                                        sb.Append(m);
                                        sb.Append("\n");
                                        i++;
                                    }
                                    wtr.Write(sb.ToString());
                                    wtr.Write("\n"); // empty line at the end

                                } else if (s[0] == "nop") {
                                    wtr.Write("nop\n");
                                } else if (s[0] == "quit") {
                                    return;
                                } else {
                                }

                                // _logger.Trace("done cmd {0}", line);
                                wtr.Flush();
                            }
                        }
                    }
                }
            } catch (Exception ex) {
                Console.WriteLine("got exception {0}", ex.ToString());
            } finally {
                if (_client != null) {
                    try { _client.Close(); } catch { }
                    _client = null;
                }
                lock (__matchercache) {
                    if (_matcher != null) {
                        if (_matcher.Free()) __matchercache.Remove(_matcher.Config);
                        _matcher = null;
                    }
                }
            }
        }
    }

    public class Server {
        static int Port = 20398;

        static void Usage() {
            Console.WriteLine();
            Console.WriteLine("usage: VFFServer [-port=PORTNUMBER]");
            Console.WriteLine();
            Console.WriteLine("       Default port is 20398");
            Console.WriteLine();
            Environment.Exit(1);
        }

        public static void Main(string[] args) {
            if (args.Length != 0) {
                if (args[0].StartsWith("-port=")) {
                    Port = Convert.ToInt32(args[0].Substring(6));
                } else {
                    Usage();
                    return;
                }
            }

            TcpListener listener = new TcpListener(IPAddress.Any, Port);
            listener.Start();

            while (true) {
                try {
                    TcpClient client = listener.AcceptTcpClient();
                    new Client(client);
                } catch { }
            }
        }
    }
}
