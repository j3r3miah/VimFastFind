using System;
using System.IO;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace VimFastFind {
    public class ConfigParser {
        Logger _logger = new Logger("config");

        public List<MatchRule> LoadRules(string configPath) {
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
            return rules;
        }
    }

    public class MatchRule {
        public bool Include;
        public bool Starts;
        public bool Ends;
        public string Value;

        public MatchRule(bool include, string v) {
            this.Include = include;

            if (v[0]          == '*') this.Ends   = true;
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
    }
}
