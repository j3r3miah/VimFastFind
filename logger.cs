using System;

namespace VimFastFind {
#if DEBUG
    public class Logger {
        string _prefix;
        bool _doLogging;

        public Logger(string name) : this(name, true) {}

        public Logger(string name, bool doLogging) {
            _prefix = string.Format("[{0}] ", name);
            _doLogging = doLogging;
        }

        public void Trace(string fmt) {
            if (_doLogging)
                Console.WriteLine(_prefix + fmt);
        }

        public void Trace(string fmt, params object[] args) {
            if (_doLogging)
                Console.WriteLine(_prefix + fmt, args);
        }
    }
#else
    public class Logger {
        public Logger(string name) {}
        public Logger(string name, bool doLogging) {}
        public void Trace(string fmt, params object[] args) {}
    }
#endif
}
