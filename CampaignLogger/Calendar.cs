using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;

namespace CampaignLogger {
    public abstract class Calendar {
        private static readonly SortedDictionary<string, Type> calendars = new SortedDictionary<string, Type>() {
            // priority order (make sure Freeform is last, as it will match anything)
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
                bool? valid = calendars[calName].InvokeMember(
                    "static_validate_timestamp",
                    BindingFlags.InvokeMethod | BindingFlags.Public | BindingFlags.Static,
                    null,
                    null,
                    new object[] { ts }
                ) as bool?;
                if (valid == true) {
                    return get_calendar(calName);
                }
            }
            return null;
        }

        public static Calendar get_get_calendar_from_interval(string interval) {
            foreach (string calName in calendars.Keys) {
                bool? valid = calendars[calName].InvokeMember(
                    "static_validate_interval",
                    BindingFlags.InvokeMethod | BindingFlags.Public | BindingFlags.Static,
                    null,
                    null,
                    new object[] { interval }
                ) as bool?;
                if (valid == true) {
                    return get_calendar(calName);
                }
            }
            return null;
        }

        // no such thing as abstract static methods, so we just have to be careful to implement these on all subclasses:
        // public abstract static bool static_validate_timestamp(string s);
        // public abstract static bool static_validate_interval(string s);

        public abstract string default_timestamp();

        public abstract bool validate_timestamp(string s);

        public abstract bool validate_interval(string s);

        public virtual string format_timestamp(EventTimestamp t) {
            if (t.delta is null) {
                return t.timestamp;
            }
            return this.format_relative_timestamp(t);
        }

        protected abstract string format_relative_timestamp(EventTimestamp t);

        public abstract int compare_timestamps(EventTimestamp t1, EventTimestamp t2);

        public int compare_timestamps(EventTimestamp t1, string t2) {
            return this.compare_timestamps(t1, new EventTimestamp(t2, null));
        }

        public virtual string subtract_timestamp(EventTimestamp t1, EventTimestamp t2) => null;

        public string subtract_timestamp(EventTimestamp t1, string t2) {
            return this.subtract_timestamp(t1, new EventTimestamp(t2, null));
        }
    }

    public class EventTimestamp {
        public readonly string timestamp;
        public readonly string delta;

        public EventTimestamp(string timestamp, string delta) {
            this.timestamp = timestamp;
            this.delta = delta;
        }
    }

    public class FreeformCalendar : Calendar {
        public static bool static_validate_timestamp(string s) {
            return s.Length > 0;
        }

        public static bool static_validate_interval(string s) {
            return s.Length > 0;
        }

        public override string default_timestamp() {
            return "campaign start";
        }

        public override bool validate_timestamp(string s) => static_validate_timestamp(s);
        public override bool validate_interval(string s) => static_validate_interval(s);

        protected override string format_relative_timestamp(EventTimestamp t) {
            return $"{t.delta} after {t.timestamp}";
        }

        public override int compare_timestamps(EventTimestamp t1, EventTimestamp t2) {
            // can't compare free-form timestamps, so just consider them all equal
            return 0;
        }
    }

    //TODO: campaign date, eberron, fr, julian
}
