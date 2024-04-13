using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;

namespace CampaignLogger {
    public abstract class Calendar {
        private static readonly SortedDictionary<string, Type> calendars = new SortedDictionary<string, Type>() {
            // priority order (make sure Freeform is last, as it will match anything)
            ["Campaign Date"] = typeof(CampaignDateCalendar),
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

    public class CampaignDateCalendar : Calendar {
        protected const string TIME_PATTERN = (
            @"((?<hour>\d{1,2}):(?<minute>\d{2})(\s*(?<meridiem>am|pm))?)|" +
            @"((?<hour>\d{1,2})\s*(?<meridiem>am|pm))|" +
            @"(?<time_freeform>.+)"
        );
        protected static readonly Regex TIMESTAMP_EXP = new Regex(
            $"^d(?<relative>[+])(?<date>\\d+)(,?\\s+({TIME_PATTERN}))?$",
            RegexOptions.Compiled | RegexOptions.ExplicitCapture | RegexOptions.IgnoreCase
        );
        protected const string TIME_INTERVAL = (
            @"((?<hours>\d{2})(?<minutes>\d{2}))|" +
            @"((?<hours>\d+)(:(?<minutes>\d{2}))?)|" +
            @"((?<hours>\d+)\s+h(our(s?))?(,?\s+(?<minutes>\d+)\s+m(in(ute)?(s?))?)?)|" +
            @"((?<minutes>\d+)\s+min(ute)?(s?))"
        );
        protected static readonly Regex INTERVAL_EXP = new Regex(
            $"^((?<days>\\d+)\\s+day(s?)(,?\\s+({TIME_INTERVAL}))?)|({TIME_INTERVAL})$",
            RegexOptions.Compiled | RegexOptions.ExplicitCapture | RegexOptions.IgnoreCase
        );

        public static bool static_validate_timestamp(string s) {
            return TIMESTAMP_EXP.IsMatch(s);
        }

        public static bool static_validate_interval(string s) {
            return INTERVAL_EXP.IsMatch(s);
        }

        public override string default_timestamp() {
            return "d0";
        }

        public override bool validate_timestamp(string s) => static_validate_timestamp(s);
        public override bool validate_interval(string s) => static_validate_interval(s);

        protected class CampaignDate {
            public bool is_relative;
            public int date;
            public int hour;
            public int minute;
            public bool has_meridiem;
            public string time_freeform;

            public CampaignDate(Match match) {
                this.is_relative = match.Groups["relative"].Success;
                this.date = int.Parse(match.Groups["date"].Value);
                this.hour = (match.Groups["hour"].Success ? int.Parse(match.Groups["hour"].Value) : -1);
                this.minute = (match.Groups["minute"].Success ? int.Parse(match.Groups["minute"].Value) : -1);
                this.has_meridiem = false;
                if (match.Groups["meridiem"].Success) {
                    this.has_meridiem = true;
                    this.hour += 12;
                }
                this.time_freeform = null;
                if (match.Groups["time_freeform"].Success) {
                    this.time_freeform = match.Groups["time_freeform"].Value;
                }
            }

            public string format() {
                string relative = (this.is_relative ? "+" : "");
                string time = this.time_freeform ?? "";
                if (this.hour >= 0) {
                    int hour = this.hour;
                    string meridiem = "";
                    if (this.has_meridiem) {
                        meridiem = (hour <= 12 ? "am" : "pm");
                    }
                    if (meridiem == "pm") {
                        hour -= 12;
                    }
                    time = (this.minute >= 0 ? $"{hour}:{this.minute}{meridiem}" : $"{hour}{meridiem}");
                }
                if (time != "") {
                    time = ", " + time;
                }
                return $"d{relative}{this.date}{time}";
            }
        }

        protected static CampaignDate parse_timestamp(EventTimestamp t) {
            Match timestamp = TIMESTAMP_EXP.Match(t.timestamp);
            if (!timestamp.Success) {
                return null;
            }
            CampaignDate date = new CampaignDate(timestamp);
            if (t.delta is null) {
                return date;
            }
            Match interval = INTERVAL_EXP.Match(t.delta);
            if (!interval.Success) {
                return null;
            }
            int days = (interval.Groups["days"].Success ? int.Parse(interval.Groups["days"].Value) : 0);
            int hours = (interval.Groups["hours"].Success ? int.Parse(interval.Groups["hours"].Value) : 0);
            int minutes = (interval.Groups["minutes"].Success ? int.Parse(interval.Groups["minutes"].Value) : 0);
            if ((days != 0) || (hours != 0) || (minutes != 0)) {
                if (date.minute >= 0) {
                    date.minute += minutes;
                }
                else {
                    hours += minutes / 60;
                }
                if (date.hour >= 0) {
                    date.hour += hours;
                }
                else {
                    days += hours / 24;
                }
                date.date += days;
            }
            // normalize date
            if (date.minute >= 60) {
                date.hour += date.minute / 60;
                date.minute %= 60;
            }
            if (date.hour >= 24) {
                date.date += date.hour / 24;
                date.hour %= 24;
            }
            return date;
        }

        protected override string format_relative_timestamp(EventTimestamp t) {
            CampaignDate date = parse_timestamp(t);
            if (date is null) {
                return t.timestamp;
            }
            return date.format();
        }

        public override int compare_timestamps(EventTimestamp t1, EventTimestamp t2) {
            CampaignDate d1 = parse_timestamp(t1), d2 = parse_timestamp(t2);
            if (d1 is null) {
                return (d2 is null ? 0 : -1);
            }
            if (d2 is null) {
                return 1;
            }
            int result = d1.date - d2.date;
            if (result != 0) {
                return result;
            }
            if ((d1.hour >= 0) && (d2.hour >= 0)) {
                result = d1.hour - d2.hour;
                if (result != 0) {
                    return result;
                }
                if ((d1.minute >= 0) && (d2.minute >= 0)) {
                    result = d1.minute - d2.minute;
                }
            }
            return result;
        }

        public override string subtract_timestamp(EventTimestamp t1, EventTimestamp t2) {
            CampaignDate d1 = parse_timestamp(t1), d2 = parse_timestamp(t2);
            if ((d1 is null) || (d2 is null)) {
                return null;
            }
            int days = d1.date - d2.date;
            if ((d1.hour < 0) || (d2.hour < 0)) {
                if (days == 0) {
                    return "today";
                }
                string s = (days == 1 ? "" : "s");
                return $"{days} day{s}";
            }
            int hours = d1.hour - d2.hour;
            int minutes = 0;
            if ((d1.minute >= 0) && (d2.minute >= 0)) {
                minutes = d1.minute - d2.minute;
            }
            while (minutes < 0) {
                minutes += 60;
                hours -= 1;
            }
            while (hours < 0) {
                hours += 24;
                days -= 1;
            }
            if ((days == 0) && (hours == 0) && (minutes == 0)) {
                return "now";
            }
            List<string> chunks = new List<string>(3);
            if (days != 0) {
                string s = (days == 1 ? "" : "s");
                chunks.Add($"{days} day{s}");
            }
            if (hours > 0) {
                chunks.Add($"{hours}h");
            }
            if (minutes > 0) {
                chunks.Add($"{minutes}m");
            }
            return string.Join(", ", chunks);
        }
    }

    //TODO: eberron, fr, julian
}
