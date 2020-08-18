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
        static readonly TimeSpan MATCHER_TTL = TimeSpan.FromMinutes(10);

        static Dictionary<DirConfig, PathMatcher> __pathmatchercache = new Dictionary<DirConfig, PathMatcher>();
        static Dictionary<DirConfig, GrepMatcher> __grepmatchercache = new Dictionary<DirConfig, GrepMatcher>();

        static Logger _logger = new Logger("server");

        PathMatcher _pathmatcher;
        GrepMatcher _grepmatcher;

        TcpClient _client;

        public Client(TcpClient client) {
            _client = client;
            (new Thread(ev_client) { IsBackground = true }).Start();
        }

        static PathMatcher __GetOrCreatePathMatcher(DirConfig config) {
            PathMatcher ret = null;
            return ret;
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

                                    // free old path matcher, if there is one
                                    if (_pathmatcher != null) {
                                        _pathmatcher.Free();
                                        _pathmatcher = null;
                                    }
                                    // get or create path matcher for new config
                                    lock (__pathmatchercache) {
                                        if (__pathmatchercache.TryGetValue(config, out _pathmatcher)) {
                                            if (_pathmatcher.IsDisposed) {
                                                _logger.Trace("pathmatcher cache disposed: " + config);
                                                __pathmatchercache.Remove(config);
                                                _pathmatcher = null;
                                            } else {
                                                _logger.Trace("pathmatcher cache hit: " + config);
                                                _pathmatcher.Ref();
                                            }
                                        }
                                        if (_pathmatcher == null) {
                                            __pathmatchercache[config] = _pathmatcher = new PathMatcher(config, MATCHER_TTL);
                                            _logger.Trace("created pathmatcher: " + config);
                                            _pathmatcher.Go(null);
                                        }
                                        _logger.Trace("__pathmatchercache size: " + __pathmatchercache.Count);
                                        _logger.Trace("keys = " + string.Join("\n                ", __pathmatchercache.Keys));
                                    }

                                    // free old grep matcher, if there is one
                                    if (_grepmatcher != null) {
                                        _grepmatcher.Free();
                                        _grepmatcher = null;
                                    }
                                    // get or create grep matcher for new config
                                    lock (__grepmatchercache) {
                                        if (__grepmatchercache.TryGetValue(config, out _grepmatcher)) {
                                            if (_grepmatcher.IsDisposed) {
                                                __grepmatchercache.Remove(config);
                                                _grepmatcher = null;
                                            } else {
                                                // _logger.Trace("grepmatcher cache hit: " + config);
                                                _grepmatcher.Ref();
                                            }
                                        }
                                        if (_grepmatcher == null) {
                                            __grepmatchercache[config] = _grepmatcher = new GrepMatcher(config, MATCHER_TTL);
                                            // _logger.Trace("created grepmatcher: " + config);
                                            _grepmatcher.Go(_pathmatcher.Paths);
                                        }
                                        // _logger.Trace("__grepmatchercache size: " + __grepmatchercache.Count);
                                    }

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
