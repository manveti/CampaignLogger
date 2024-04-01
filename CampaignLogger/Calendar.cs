using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace CampaignLogger {
    public abstract class Calendar {
        private static readonly SortedDictionary<string, Type> calendars = new SortedDictionary<string, Type>() {
            // priority order
            ["Freeform"] = typeof(FreeformCalendar)
        };

        public static Calendar get_calendar(string name) {  //TODO: string params
            Dictionary<string, Type> lowerCalendars = new Dictionary<string, Type>();
            foreach (string calName in calendars.Keys) {
                lowerCalendars[calName.ToLower()] = calendars[calName];
            }
            name = name.ToLower();
            if (lowerCalendars.ContainsKey(name)) {
                return Activator.CreateInstance(lowerCalendars[name]) as Calendar;  //TODO: ", params"
            }
            return null;
        }

        public static Calendar get_calendar_from_timestamp(string ts) {
            foreach (string calName in calendars.Keys) {
                string timestamp = calendars[calName].InvokeMember(
                    "static_match_timestamp",
                    BindingFlags.InvokeMethod | BindingFlags.Public | BindingFlags.Static,
                    null,
                    null,
                    new object[] { ts }
                ) as string;
                if (timestamp is not null) {
                    return get_calendar(calName);
                }
            }
            return null;
        }

        // no such thing as abstract static methods, so we just have to be careful to implement this on all subclasses:
        // public abstract static string static_match_timestamp(string s);

        public abstract string default_timestamp();

        public abstract string match_timestamp(string s);

        //TODO: parse interval, add(timestamp, interval), compare(timestamp, timestamp)
    }

    public class FreeformCalendar : Calendar {
        private static readonly Regex TIMESTAMP_EXP = new Regex(
            @"^(?<timestamp>.+?)(\s+[(]continued[)])?:$", RegexOptions.Compiled | RegexOptions.ExplicitCapture
        );

        public static string static_match_timestamp(string s) {
            Match match = TIMESTAMP_EXP.Match(s);
            if (match.Success) {
                return match.Groups["timestamp"].Value;
            }
            return null;
        }

        public override string default_timestamp() {
            return "campaign start";
        }

        public override string match_timestamp(string s) => static_match_timestamp(s);

        //TODO: ...
    }

    //TODO: campaign date, eberron, fr, julian
}
