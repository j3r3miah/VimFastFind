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

namespace VimFastFind
{
    class Client {
        static Dictionary<DirConfig, Matcher> __matchercache = new Dictionary<DirConfig, Matcher>();

        TcpClient _client;
        Dictionary<DirConfig, Matcher> _matchers;
        Logger _logger;

        public Client(TcpClient client) {
            _client = client;
            _matchers = new Dictionary<DirConfig, Matcher>();
            _logger = new Logger("server");
            (new Thread(ev_client) { IsBackground = true }).Start();
        }

        void _UpdateConfig(List<DirConfig> configs) {
            var newMatchers = new Dictionary<DirConfig, Matcher>();
            var needInit = new List<Matcher>();

            foreach (DirConfig config in configs) {
                if (_matchers.ContainsKey(config)) {
                    newMatchers[config] = _matchers[config];
                    continue;
                }

                lock (__matchercache) {
                    Matcher matcher;
                    if (!__matchercache.TryGetValue(config, out matcher)) {
                        Logger.TraceFrom("cache", "created matcher: " + config.Name);
                        __matchercache[config] = matcher = new Matcher(config);
                        needInit.Add(matcher);
                    } else {
                        Logger.TraceFrom("cache", "cache hit: " + config.Name);
                        matcher.Ref();
                    }
                    Logger.TraceFrom("cache", "size: " + __matchercache.Count);
                    newMatchers[config] = matcher;
                }
            }

            ThreadPool.QueueUserWorkItem(delegate {
                foreach (var matcher in needInit) {
                    try {
                        matcher.Go();
                    } catch {
                        // matcher was disposed when client disconnected early
                    }
                }
            });

            foreach (var kvp in _matchers) {
                if (!newMatchers.ContainsKey(kvp.Key)) {
                    if (kvp.Value.Free())
                        __matchercache.Remove(kvp.Key);
                }
            }

            _matchers = newMatchers;
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
                                    _UpdateConfig(new ConfigParser().LoadConfig(s[1]));

                                } else if (s[0] == "grep" && s[1] == "match") {
                                    s = line.Split(new char[] { ' ', '\t' }, 3, StringSplitOptions.RemoveEmptyEntries);

                                    var combinedMatches = new TopN<string>(200);
                                    foreach (Matcher matcher in _matchers.Values) {
                                        foreach (var match in matcher.GrepMatch(line.Substring(line.IndexOf("match")+6), 200)) {
                                            combinedMatches.Add(match.Score, match.Item);
                                        }
                                    }

                                    StringBuilder sb = new StringBuilder();
                                    foreach (var match in combinedMatches) {
                                        sb.Append(match.Item);
                                        sb.Append("\n");
                                    }
                                    wtr.Write(sb.ToString());
                                    wtr.Write("\n");

                                } else if (s[0] == "find" && s[1] == "match") {
                                    s = line.Split(new char[] { ' ', '\t' }, 3, StringSplitOptions.RemoveEmptyEntries);

                                    var combinedMatches = new TopN<string>(200);
                                    foreach (Matcher matcher in _matchers.Values) {
                                        foreach (var match in matcher.PathMatch(line.Substring(line.IndexOf("match")+6).ToLowerInvariant(), 200)) {
                                            combinedMatches.Add(match.Score, match.Item);
                                        }
                                    }

                                    StringBuilder sb = new StringBuilder();
                                    foreach (var match in combinedMatches) {
                                        sb.Append(match.Item);
                                        sb.Append("\n");
                                    }
                                    wtr.Write(sb.ToString());
                                    wtr.Write("\n");

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
                    foreach (var kvp in _matchers) {
                        if (kvp.Value.Free())
                            __matchercache.Remove(kvp.Key);
                    }
                    _matchers = null;
                    Logger.TraceFrom("cache", "size: " + __matchercache.Count);
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
