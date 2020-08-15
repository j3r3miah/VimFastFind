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
        static Dictionary<string, PathMatcher> __pathmatchercache = new Dictionary<string, PathMatcher>();
        static Dictionary<string, GrepMatcher> __grepmatchercache = new Dictionary<string, GrepMatcher>();

        Logger _logger = new Logger("server");

        PathMatcher _pathmatcher;
        GrepMatcher _grepmatcher;
        bool _ownspath;
        bool _ownsgrep;

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
                                _logger.Trace("got cmd {0}", line);

                                line = Regex.Replace(line, @"^\s*#.*", "");
                                if (String.IsNullOrWhiteSpace(line)) continue;
                                string[] s = line.Split(new char[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);


                                if (s[0] == "init") {
                                    s = line.Split(new char[] { ' ', '\t' }, 2, StringSplitOptions.RemoveEmptyEntries);
                                    lock (__pathmatchercache) {
                                        if (!__pathmatchercache.TryGetValue(s[1], out _pathmatcher)) {
                                            __pathmatchercache[s[1]] = _pathmatcher = new PathMatcher(s[1]);
                                            _ownspath = true;
                                        } else {
                                            _pathmatcher.Ref();
                                        }
                                    }

                                    lock (__grepmatchercache) {
                                        if (!__grepmatchercache.TryGetValue(s[1], out _grepmatcher)) {
                                            __grepmatchercache[s[1]] = _grepmatcher = new GrepMatcher(s[1]);
                                            _ownsgrep = true;
                                        } else {
                                            _grepmatcher.Ref();
                                        }
                                    }

                                } else if (s[0] == "go") {
                                    if (_ownspath) _pathmatcher.Go(null);
                                    if (_ownsgrep) _grepmatcher.Go(_pathmatcher.Paths);

                                } else if (s[0] == "config") {
                                    var rules = new ConfigParser().LoadRules(s[1]);
                                    if (_ownspath) _pathmatcher.Rules = rules;
                                    if (_ownsgrep) _grepmatcher.Rules = rules;

                                } else if (s[0] == "grep" && s[1] == "match") {
                                    s = line.Split(new char[] { ' ', '\t' }, 3, StringSplitOptions.RemoveEmptyEntries);
                                    _logger.Trace("find! {0}", line);
                                    StringBuilder sb = new StringBuilder();
                                    int i = 0;
                                    foreach (string m in _grepmatcher.Match(line.Substring(line.IndexOf("match")+6), 200)) {
                                        sb.Append(m);
                                        sb.Append("\n");
                                        i++;
                                    }
                                    _logger.Trace(sb.ToString());
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
                                _logger.Trace("done cmd {0}", line);
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
                        if (_pathmatcher.Free()) __pathmatchercache.Remove(_pathmatcher.InitDir);
                        _pathmatcher = null;
                    }
                }
                lock (__grepmatchercache) {
                    if (_grepmatcher != null) {
                        if (_grepmatcher.Free()) __grepmatchercache.Remove(_grepmatcher.InitDir);
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
            ThreadPool.QueueUserWorkItem(delegate { });
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
