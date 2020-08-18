using System;
using System.IO;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace VimFastFind
{
    public class ConfigParser {
        Logger _logger = new Logger("config", true);

        public DirConfig LoadConfig(string configPath) {
            _logger.Trace("Loading config file: {0}", configPath);

            var rules = new List<MatchRule>();
            using (var sr = new StreamReader(configPath)) {
                string line;
                while ((line = sr.ReadLine()) != null) {
                    line = Regex.Replace(line, @"^\s*#.*", "");
                    if (String.IsNullOrWhiteSpace(line)) continue;

                    string[] s = line.Split(new char[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                    if (s[0] != "include" && s[0] != "exclude")
                        throw new Exception("Invalid config line: " + line);

                    var rule = new MatchRule(s[0] == "include", s[1]);
                    _logger.Trace("  " + rule.ToString());
                    rules.Add(rule);
                }
            }
            return new DirConfig(configPath, Path.GetDirectoryName(configPath), rules);
        }
    }

    public class DirConfig {
        public string ConfigPath { get; private set; }
        public string ScanDir { get; private set;  }
        public List<MatchRule> Rules { get; private set; }

        public DirConfig(string configPath, string scanDir, List<MatchRule> rules) {
            ConfigPath = configPath;
            ScanDir = scanDir;
            Rules = rules;
        }

        public override int GetHashCode() {
            int res = 0x1AB43C32;
            res = res * 31 + ScanDir.GetHashCode();
            foreach (var rule in Rules)
                res = res * 31 + (rule == null ? 0 : rule.GetHashCode());
            return res;
        }

        public override bool Equals(object obj) {
            var o = obj as DirConfig;
            if (o == null) return false;
            if (ScanDir != o.ScanDir) return false;
            if (Rules.Count != o.Rules.Count) return false;
            for (int i = 0; i < Rules.Count; i++) {
                if (Rules[i].Equals(o.Rules[i])) continue;
                return false;
            }
            return true;
        }

        public override string ToString() {
            return ConfigPath;
        }
    }

    public class MatchRule {
        public bool Include;
        public bool Starts;
        public bool Ends;
        public string Value;

        public MatchRule(bool include, string v) {
            this.Include = include;

            if (v[0] == '*') this.Ends = true;
            if (v[v.Length-1] == '*') this.Starts = true;

            if (v == "*")
                this.Value = "";
            else if (this.Starts && this.Ends)
                this.Value = v.Substring(1, v.Length-2);
            else if (this.Starts)
                this.Value = v.Substring(0, v.Length-1);
            else if (this.Ends)
                this.Value = v.Substring(1);
            else
                this.Value = v;
        }

        public bool Match(string e) {
            if (Starts && !Ends) return e.StartsWith(Value);
            if (Ends && !Starts) return e.EndsWith(Value);
            if (Ends && Starts) return e.IndexOf(Value) != -1;
            return e == Value;
        }

        public override string ToString() {
            return string.Format("{0} {1}{2}{3}",
                                 Include ? "include" : "exclude",
                                 Ends ? "*" : "",
                                 Value,
                                 Starts ? "*" : "");
        }

        public override bool Equals(object obj) {
            var o = obj as MatchRule;
            if (o == null) return false;
            return Include == o.Include
                && Starts == o.Starts
                && Ends == o.Ends
                && Value == o.Value;
        }

        public override int GetHashCode() {
            int res = 0x3D7EF6AB;
            res = res * 31 + Include.GetHashCode();
            res = res * 31 + Starts.GetHashCode();
            res = res * 31 + Ends.GetHashCode();
            res = res * 31 + Value.GetHashCode();
            return res;
        }
    }
}
