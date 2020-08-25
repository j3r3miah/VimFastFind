using System;
using System.IO;
using System.Collections.Generic;

namespace VimFastFind {
    public class Logger {
#if DEBUG
        static StreamWriter __fw = null;

        static Dictionary<string, bool> __enabled = new Dictionary<string, bool> {
            ["cache"] = true,
            ["config"] = false,
            ["server"] = false,
            ["pathmatch"] = true,
            ["grepmatch"] = true,
        };
#else
        static StreamWriter __fw = new StreamWriter(
                Path.Combine(Path.GetTempPath(), "vff.log"));

        static Dictionary<string, bool> __enabled = new Dictionary<string, bool> {
            // all enabled
        };
#endif

        string _prefix;
        bool _doLogging;

        public Logger(string name) {
            _prefix = string.Format("[{0}] ", name);
            _doLogging = !__enabled.ContainsKey(name) || __enabled[name];
        }

        public void Trace(string fmt) {
            if (!_doLogging) return;
            __WriteLine(_prefix + fmt);
        }

        public void Trace(string fmt, params object[] args) {
            if (!_doLogging) return;
            __WriteLine(_prefix + fmt, args);
        }

        public static void TraceFrom(string name, string fmt) {
            if (!__enabled.ContainsKey(name) || __enabled[name]) {
                var prefix = string.Format("[{0}] ", name);
                __WriteLine(prefix + fmt);
            }
        }

        public static void TraceFrom(string name, string fmt, params object[] args) {
            if (!__enabled.ContainsKey(name) || __enabled[name]) {
                var prefix = string.Format("[{0}] ", name);
                __WriteLine(prefix + fmt, args);
            }
        }

        static void __WriteLine(string cmd, params object[] args) {
            if (__fw != null) {
                __fw.WriteLine(String.Format(cmd, args));
                __fw.Flush();
            } else {
                Console.WriteLine(cmd, args);
            }
        }

    }
}
