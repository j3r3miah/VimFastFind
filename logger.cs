using System;
using System.Collections.Generic;

namespace VimFastFind {
#if DEBUG
    public class Logger {
        static Dictionary<string, bool> __enabled = new Dictionary<string, bool> {
            ["config"] = true,
            ["server"] = false,
            ["pathmatch"] = true,
            ["grepmatch"] = true,
        };

        string _prefix;
        bool _doLogging;

        public Logger(string name) {
            _prefix = string.Format("[{0}] ", name);
            _doLogging = !__enabled.ContainsKey(name) || __enabled[name];
        }

        public void Trace(string fmt) {
            if (_doLogging)
                Console.WriteLine(_prefix + fmt);
        }

        public void Trace(string fmt, params object[] args) {
            if (_doLogging)
                Console.WriteLine(_prefix + fmt, args);
        }

        public static void TraceFrom(string name, string fmt) {
            if (!__enabled.ContainsKey(name) || __enabled[name]) {
                var prefix = string.Format("[{0}] ", name);
                Console.WriteLine(prefix + fmt);
            }
        }

        public static void TraceFrom(string name, string fmt, params object[] args) {
            if (!__enabled.ContainsKey(name) || __enabled[name]) {
                var prefix = string.Format("[{0}] ", name);
                Console.WriteLine(prefix + fmt, args);
            }
        }
    }
#else
    public class Logger {
        public Logger(string name) {}
        public Logger(string name, bool doLogging) {}
        public void Trace(string fmt, params object[] args) {}
        public static void TraceFrom(string name, string fmt, params object[] args) {}
    }
#endif
}
