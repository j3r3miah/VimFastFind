using System;

namespace VimFastFind {
    public class Logger {
        string _prefix;

        public Logger(string name) {
            _prefix = string.Format("[{0}] ", name);
        }

        public void Trace(string fmt) {
            Console.WriteLine(_prefix + fmt);
        }

        public void Trace(string fmt, params object[] args) {
            Console.WriteLine(_prefix + fmt, args);
        }
    }
}
