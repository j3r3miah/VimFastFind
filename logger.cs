using System;

namespace VimFastFind {
#if DEBUG
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
#else
    public class Logger {
        public Logger(string name) {}
        public void Trace(string fmt, params object[] args) {}
    }
#endif
}
