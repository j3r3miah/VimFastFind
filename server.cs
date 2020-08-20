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
        // TODO hash of config :: pair(path + grep matcher)
        static Dictionary<DirConfig, PathMatcher> __pathmatchercache = new Dictionary<DirConfig, PathMatcher>();
        static Dictionary<DirConfig, GrepMatcher> __grepmatchercache = new Dictionary<DirConfig, GrepMatcher>();

        Logger _logger = new Logger("server");

        DirConfig _config;
        PathMatcher _pathmatcher;
        GrepMatcher _grepmatcher;

        TcpClient _client;

        public Client(TcpClient client) {
            _client = client;
            (new Thread(ev_client) { IsBackground = true }).Start();
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
                                    var config = new ConfigParser().LoadConfig(s[1]);
                                    if (_config != null && _config.Equals(config)) continue;
                                    _config = config;

                                    bool ownMatchers = false;

                                    if (_pathmatcher != null) {
                                        if (_pathmatcher.Free()) __pathmatchercache.Remove(_pathmatcher.Config);
                                        _pathmatcher = null;
                                    }
                                    lock (__pathmatchercache) {
                                        if (!__pathmatchercache.TryGetValue(config, out _pathmatcher)) {
                                            _logger.Trace("created pathmatcher: " + config.ScanDirectory);
                                            __pathmatchercache[config] = _pathmatcher = new PathMatcher(config);
                                            ownMatchers = true;
                                        } else {
                                            _logger.Trace("pathmatcher cache hit: " + config.ScanDirectory);
                                            _pathmatcher.Ref();
                                        }
                                        _logger.Trace("__pathmatchercache size: " + __pathmatchercache.Count);
                                    }

                                    if (_grepmatcher != null) {
                                        if (_grepmatcher.Free()) __grepmatchercache.Remove(_grepmatcher.Config);
                                        _grepmatcher = null;
                                    }
                                    lock (__grepmatchercache) {
                                        if (!__grepmatchercache.TryGetValue(config, out _grepmatcher)) {
                                            _logger.Trace("created grepmatcher: " + config.ScanDirectory);
                                            __grepmatchercache[config] = _grepmatcher = new GrepMatcher(config);
                                        } else {
                                            _logger.Trace("grepmatcher cache hit: " + config.ScanDirectory);
                                            _grepmatcher.Ref();
                                        }
                                        _logger.Trace("__grepmatchercache size: " + __grepmatchercache.Count);
                                    }

                                    if (ownMatchers) {
                                        ThreadPool.QueueUserWorkItem(delegate {
                                            _pathmatcher.Go(null);
                                            // TODO read files as we're scanning paths
                                            _grepmatcher.Go(_pathmatcher.Paths);
                                        });
                                    }

                                } else if (s[0] == "grep" && s[1] == "match") {
                                    s = line.Split(new char[] { ' ', '\t' }, 3, StringSplitOptions.RemoveEmptyEntries);
                                    StringBuilder sb = new StringBuilder();
                                    int i = 0;
                                    foreach (string m in _grepmatcher.Match(line.Substring(line.IndexOf("match")+6), 200)) {
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
                                    foreach (string m in _pathmatcher.Match(line.Substring(line.IndexOf("match")+6).ToLowerInvariant(), 200)) {
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
                lock (__pathmatchercache) {
                    if (_pathmatcher != null) {
                        if (_pathmatcher.Free()) __pathmatchercache.Remove(_pathmatcher.Config);
                        _pathmatcher = null;
                    }
                }
                lock (__grepmatchercache) {
                    if (_grepmatcher != null) {
                        if (_grepmatcher.Free()) __grepmatchercache.Remove(_grepmatcher.Config);
                        _grepmatcher = null;
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
