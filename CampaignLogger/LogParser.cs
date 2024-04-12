using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace CampaignLogger {
    public class LogParser {
        private const string QUOTE = "\"";
        private static readonly Regex DEFAULT_SPLIT_EXP = new Regex(@"\s*[.;]\s*", RegexOptions.Compiled | RegexOptions.ExplicitCapture);
        private static readonly Regex SECTION_OPENER_EXP = new Regex(
            @"\s*(?<opener>[""([{])\s*", RegexOptions.Compiled | RegexOptions.ExplicitCapture
        );
        private static readonly Dictionary<string, Regex> SECTION_CLOSER_EXPS = new Dictionary<string, Regex>() {
            [QUOTE] = new Regex(@"\s*(?<closer>[""])\s*", RegexOptions.Compiled | RegexOptions.ExplicitCapture),
            ["("] = new Regex(@"\s*((?<opener>[""(])|(?<closer>[)]))\s*", RegexOptions.Compiled | RegexOptions.ExplicitCapture),
            ["["] = new Regex(@"\s*((?<opener>[""[])|(?<closer>[]]))\s*", RegexOptions.Compiled | RegexOptions.ExplicitCapture),
            ["{"] = new Regex(@"\s*((?<opener>[""{])|(?<closer>[}]))\s*", RegexOptions.Compiled | RegexOptions.ExplicitCapture),
        };

        public static IEnumerable<string> split_line(string s, Regex delim = null, int count = -1) {
            if (s.Length <= 0) {
                yield return "";
            }
            if (delim is null) {
                delim = DEFAULT_SPLIT_EXP;
            }
            int offset = 0, searchOffset = 0;
            while (offset < s.Length) {
                Match delimMatch = delim.Match(s, searchOffset);
                if (!delimMatch.Success) {
                    // we're in the last chunk; yield it and we're done
                    yield return s[offset..];
                    break;
                }
                Match sectionMatch = SECTION_OPENER_EXP.Match(s, searchOffset, delimMatch.Index - searchOffset);
                if (!sectionMatch.Success) {
                    // no subsection in this chunk; yield it and move to next chunk
                    yield return s[offset..delimMatch.Index];
                    offset = delimMatch.Index + delimMatch.Length;
                    searchOffset = offset;
                    if (count > 0) {
                        count -= 1;
                        if (count <= 0) {
                            if (offset < s.Length) {
                                yield return s[offset..];
                            }
                            break;
                        }
                    }
                    continue;
                }
                // there's a quoted/parenthetical/etc. subsection in this chunk; skip over it and keep looking for delimiter
                searchOffset = sectionMatch.Index + sectionMatch.Length;
                string opener = sectionMatch.Groups["opener"].Value;
                int openCount = 1;
                Regex closerExp = SECTION_CLOSER_EXPS[opener];
                Match closerMatch = closerExp.Match(s, searchOffset);
                while (closerMatch.Success) {
                    searchOffset = closerMatch.Index + closerMatch.Length;
                    if (closerMatch.Groups["closer"].Success) {
                        // got a closer for this subsection (e.g. a '}' for a '{' section); decrement open count until no outstanding openers
                        openCount -= 1;
                        if (openCount <= 0) {
                            break;
                        }
                        closerMatch = closerExp.Match(s, searchOffset);
                    }
                    else if (closerMatch.Groups["opener"].Value == QUOTE) {
                        // got a quote inside a non-quote subsection; skip over quoted subsection then resume looking for closer
                        openCount += 1;
                        closerMatch = SECTION_CLOSER_EXPS[QUOTE].Match(s, searchOffset);
                    }
                    else {
                        // got another opener for this kind of subsection; increment open count
                        openCount += 1;
                        closerMatch = closerExp.Match(s, searchOffset);
                    }
                }
            }
        }

        public static IEnumerable<string> split_line(string s, string delim, int count = -1) {
            return split_line(s, new Regex($"\\s*{delim}\\s*", RegexOptions.Compiled | RegexOptions.ExplicitCapture), count);
        }
    }
}
