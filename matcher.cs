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
    public abstract class Matcher : IDisposable {
        protected string _dir;
        protected List<string> _paths = new List<string>();
        DirectoryWatcher _fswatcher;

        public List<string> Paths { get { return _paths; } }

        public bool IsFileOk(string name, bool onlyexclude=false) {
            foreach (var mr in this.Config.Rules) {
                if (!onlyexclude && mr.Include) {
                    if (mr.Match(name))
                        return true;
                } else if (!mr.Include) {
                    if (mr.Match(name))
                        return false;
                }
            }
            return onlyexclude;
        }

        public string TrimPath(string fullpath) {
            if (_dir == fullpath) return "";
            return fullpath.Substring(_dir.Length+1);
        }

        public DirConfig Config { get; private set; }
        public Matcher(DirConfig config) {
            this.Config = config;
        }

        public void Go(List<string> paths) {
            _dir = this.Config.ScanDir;
            _dir = _dir.Trim();
            while (_dir.Length > 0 && _dir[_dir.Length-1] == Path.DirectorySeparatorChar)
                _dir = _dir.Substring(0, _dir.Length-1);

            Logger.Trace("watching {0}", _dir);

            _fswatcher = new DirectoryWatcher(_dir);
            _fswatcher.EnableWatchingContents = true;
            _fswatcher.Initialize();
            _fswatcher.SubdirectoryChanged += ev_SubdirChanged;

            if (paths != null) {
                _paths = paths;
            } else {
                System.Diagnostics.Stopwatch sw = new System.Diagnostics.Stopwatch();
                Logger.Trace("starting initial scan of {0}", this.Config.ScanDir);
                sw.Start();
                foreach (DirectoryEntry entry in FastDirectoryScanner.RecursiveScan(_dir, skipdir => !IsFileOk(TrimPath(skipdir), true))) {
                    if (entry.IsFile) {
                        string tp = TrimPath(entry.FullPath);
                        if (IsFileOk(tp)) _paths.Add(tp);
                    }
                }
                sw.Stop();
                Logger.Trace("[{0}ms] {1} paths found on initial scan of {2}", sw.ElapsedMilliseconds, _paths.Count, this.Config.ScanDir);
            }
            OnPathsInited();
        }

        void ev_SubdirChanged(DirectoryWatcher source, string fulldirpath)
        {
            string dirpath = TrimPath(fulldirpath);
            if (dirpath.Length > 0 && dirpath[dirpath.Length-1] != Path.DirectorySeparatorChar)
                dirpath += Path.DirectorySeparatorChar;

            if (!Directory.Exists(fulldirpath)) {
                // Logger.Trace("subdir removed: {0}", dirpath);
                lock (_paths) {
                    int i = 0;
                    while (i < _paths.Count) {
                        string f = _paths[i++];
                        if (f.StartsWith(dirpath)) {
                            _paths.RemoveAt(--i);
                            OnPathRemoved(f);
                        }
                    }
                }
            } else {
                // Logger.Trace("subdir changed: {0}", dirpath);
                HashSet<string> filesindir = new HashSet<string>(Directory.GetFiles(fulldirpath).Where(x => IsFileOk(x)).Select(x => TrimPath(x)));
                lock (_paths) {
                    int i = 0;
                    while (i < _paths.Count) {
                        string path = _paths[i++];
                        string dir = Path.GetDirectoryName(path);
                        if (dir.Length > 0 && dir[dir.Length-1] != Path.DirectorySeparatorChar)
                            dir += Path.DirectorySeparatorChar;
                        if (dir == dirpath) {
                            if (filesindir.Contains(path)) {
                                OnPathChanged(path);
                            } else {
                                _paths.RemoveAt(--i);
                                OnPathRemoved(path);
                            }
                            filesindir.Remove(path);
                        }
                    }
                    foreach (string f in filesindir) {
                        _paths.Add(f);
                        OnPathAdded(f);
                    }
                }
            }
        }

        void ev_FileChanged(DirectoryWatcher source, string fullpath)
        {
            if (!IsFileOk(fullpath)) return;

            // Logger.Trace("filechnaged: {0}", fullpath);
            if (File.Exists(fullpath)) {
                lock (_paths) {
                    string f = TrimPath(fullpath);
                    if (_paths.Contains(f)) {
                        OnPathChanged(f);
                    } else {
                        _paths.Add(f);
                        OnPathAdded(f);
                    }
                }
            } else {
                lock (_paths) {
                    string f = TrimPath(fullpath);
                    _paths.Remove(f);
                    OnPathRemoved(f);
                }
            }
        }

        protected virtual void OnPathsInited() { }
        protected virtual void OnPathAdded(string path) { }
        protected virtual void OnPathRemoved(string path) { }
        protected virtual void OnPathChanged(string path) { }
        protected virtual void OnPathRenamed(string p1, string p2) { }

        protected abstract bool DoMatch(string path, string needle, out int score, ref object obj, List<string> outs);

        protected abstract Logger Logger { get; }

        public TopN<string> Match(string s, int count) {
            TopN<string> matches = new TopN<string>(count);

            System.Diagnostics.Stopwatch sw = new System.Diagnostics.Stopwatch();
            sw.Start();

            LockFreeQueue<string> queue = new LockFreeQueue<string>();
            int queuecount;
            lock (_paths) {
                int i = 0;
                while (i < _paths.Count)
                    queue.Enqueue(_paths[i++]);
                queuecount = _paths.Count;
            }

            ManualResetEvent mre = new ManualResetEvent(false);
            WaitCallback work = delegate {
                string path;
                List<string> outs = new List<string>();
                object obj = null;
                int score;
                while (queue.Dequeue(out path)) {
                    if (DoMatch(path, s, out score, ref obj, outs)) {
                        lock (matches) {
                            foreach (string o in outs)
                                matches.Add(score, o);
                        }
                    }
                    if (Interlocked.Decrement(ref queuecount) == 0)
                        mre.Set();
                    else
                        outs.Clear();
                }
            };

            if (queuecount != 0) {
                int cpu = 0;
                while (cpu++ < Environment.ProcessorCount)
                    ThreadPool.QueueUserWorkItem(work);
                mre.WaitOne();
            }

            // Logger.Trace("{0}ms elapsed", sw.ElapsedMilliseconds);
            return matches;
        }

        public virtual void Dispose() {
            if (_fswatcher != null) {
                try { _fswatcher.Dispose(); } catch { }
                _fswatcher = null;
            }
        }

        int _refcnt = 1;
        public void Ref() {
            Interlocked.Increment(ref _refcnt);
        }
        public bool Free() {
            if (Interlocked.Decrement(ref _refcnt) == 0) {
                Dispose();
                return true;
            }
            return false;
        }
    }

    public class PathMatcher : Matcher {
        Logger _logger = new Logger("pathmatch");
        protected override Logger Logger { get { return _logger; } }

        public PathMatcher(DirConfig config) : base(config) { }
        protected override void OnPathsInited() {
            _paths.Sort();
        }
        protected override void OnPathAdded(string path) {
            _paths.Sort();
        }

        protected override bool DoMatch(string path, string needle, out int score, ref object obj, List<string> outs) {
            int i = needle.Length-1;
            int j = path.Length-1;
            score = 0;
            bool match = false;
            while (i >= 0 && j >= 0) {
                if (Char.ToLowerInvariant(path[j]) == needle[i]) {
                    i--;
                    score++;
                    if (match)
                        score++;
                    match = true;
                } else
                    match = false;
                j--;
            }

            if (i == -1) {
                if (j >= 0)
                    if (path[j] == '/' || path[j] == '\\')
                        score++;

                outs.Add(path.Replace("\\", "/"));
                return true;
            }

            return false;
        }
    }

    public class GrepMatcher : Matcher {
        static LockFreeQueue<KeyValuePair<GrepMatcher, string>> __incomingfiles = new LockFreeQueue<KeyValuePair<GrepMatcher, string>>();
        static AutoResetEvent __queuetrigger = new AutoResetEvent(false);

        bool dead;

        Dictionary<string, string> _contents = new Dictionary<string, string>();

        Logger _logger = new Logger("grepmatch");
        protected override Logger Logger { get { return _logger; } }

        static GrepMatcher() {
            (new Thread(ev_read) { IsBackground = true }).Start();
        }

        public GrepMatcher(DirConfig config) : base(config) { }

        static void ev_read() {
            while (true) {
                KeyValuePair<GrepMatcher, string> kvp;
                while (__incomingfiles.Dequeue(out kvp)) {
                    if (kvp.Key.dead)
                        continue;
                    string file = Path.Combine(kvp.Key._dir, kvp.Value);
                    try {
                        if (!File.Exists(file)) continue;
                        using (StreamReader r = new StreamReader(file)) {
                            string v = r.ReadToEnd();
                            lock (kvp.Key._contents) {
                                kvp.Key._contents[kvp.Value] = v;
                            }
                        }

                    } catch (ArgumentException) {
                        // skipping because this is just a blank file

                    } catch (IOException e) {
                        try {
                            var fi = new FileInfo(file);
                            if (fi.Length != 0) {
                                Console.WriteLine("IO exception opening {0} for grepping: {1} ", kvp.Value, e);
                                __incomingfiles.Enqueue(kvp);
                            }
                        } catch { }

                    } catch (Exception e) {
                        Console.WriteLine("exception opening {0} for grepping: {1} ", kvp.Value, e);
                    }
                }
                __queuetrigger.WaitOne();
            }
        }

        protected override void OnPathsInited() {
            foreach (string f in _paths) {
                // Logger.Trace("adding to incoming file {0}", Path.Combine(this._dir, f));
                __incomingfiles.Enqueue(new KeyValuePair<GrepMatcher, string>(this, f));
            }
            __queuetrigger.Set();
        }
        protected override void OnPathAdded(string path) {
            // Logger.Trace("adding to incoming file {0}", Path.Combine(this._dir, path));
            __incomingfiles.Enqueue(new KeyValuePair<GrepMatcher, string>(this, path));
            __queuetrigger.Set();
        }
        protected override void OnPathRemoved(string path) {
            // Logger.Trace("removing file {0}", Path.Combine(this._dir, path));
            lock (_contents) {
                _contents.Remove(path);
            }
        }
        protected override void OnPathChanged(string path) {
            // Logger.Trace("changed file {0}", Path.Combine(this._dir, path));
            __incomingfiles.Enqueue(new KeyValuePair<GrepMatcher, string>(this, path));
            __queuetrigger.Set();
        }
        protected override void OnPathRenamed(string p1, string p2) {
            lock (_contents) {
                if (_contents.ContainsKey(p1)) {
                    _contents[p2] = _contents[p1];
                    _contents.Remove(p1);
                }
            }
        }
        protected override bool DoMatch(string path, string needle, out int score, ref object obj, List<string> outs) {
            // Logger.Trace("matching {0} against {1}", path, needle);
            string contents;
            lock (_contents) {
                if (!_contents.TryGetValue(path, out contents)) {
                    Logger.Trace("{0} not found", path);
                    score = 0;
                    return false;
                }
            }

            score = 0;
            bool ret = false;

            int idx = 0;
            while (true) {
                idx = contents.IndexOf(needle, idx, StringComparison.Ordinal);
                if (idx == -1) break;

                int oidx = idx;
                int eidx = idx;
                while (eidx < contents.Length && contents[eidx] != '\n') eidx++;
                if (eidx != contents.Length) eidx++;

                while (idx > 0 && contents[idx] != '\n') idx--;
                if (contents[idx] == '\n') idx++;

                outs.Add(path.Replace("\\", "/") + "(" + (oidx+1) + "):" + contents.Substring(idx, eidx-idx-1));
                score = 100;
                idx = eidx;
                ret = true;
                if (idx+1 >= contents.Length) break;
            }
            return ret;
        }

        public override void Dispose() {
            Logger.Trace("disposing grep");
            base.Dispose();
            dead = true;
        }
    }
}
