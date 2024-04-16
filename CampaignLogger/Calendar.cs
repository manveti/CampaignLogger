using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;

namespace CampaignLogger {
    public abstract class Calendar {
        private static readonly SortedDictionary<string, Type> calendars = new SortedDictionary<string, Type>() {
            // lower-case here; proper capitalization in calendar_names below
            ["greyhawk"] = typeof(GreyhawkCalendar),
            ["eberron"] = typeof(EberronCalendar),
            ["campaign date"] = typeof(CampaignDateCalendar),
            ["freeform"] = typeof(FreeformCalendar)
        };
        private static readonly string[] calendar_names = new[] {
            // priority order (make sure Freeform is last, as it will match anything)
            "Greyhawk", "Eberron", "Campaign Date", "Freeform"
        };

        public static Calendar get_calendar(string name) {  //TODO: string params
            name = name.ToLower();
            if (calendars.ContainsKey(name)) {
                return Activator.CreateInstance(calendars[name]) as Calendar;  //TODO: ", params"
            }
            return null;
        }

        public static Calendar get_calendar_from_timestamp(string ts) {
            foreach (string calName in calendar_names) {
                bool? valid = calendars[calName.ToLower()].InvokeMember(
                    "static_validate_timestamp",
                    BindingFlags.InvokeMethod | BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy,
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
            foreach (string calName in calendar_names) {
                bool? valid = calendars[calName.ToLower()].InvokeMember(
                    "static_validate_interval",
                    BindingFlags.InvokeMethod | BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy,
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

        public virtual bool validate_timestamp(string s) => this.parse_timestamp(s) is not null;

        public virtual bool validate_interval(string s) => this.parse_interval(s) is not null;

        public abstract CalendarTimestamp parse_timestamp(string s);

        public abstract CalendarInterval parse_interval(string s);

        public virtual CalendarTimestamp parse_event_timestamp(EventTimestamp t) {
            if (t.timestamp is null) {
                return null;
            }
            CalendarTimestamp result = this.parse_timestamp(t.timestamp);
            if (result is null) {
                return null;
            }
            if (t.delta is not null) {
                CalendarInterval delta = this.parse_interval(t.delta);
                if (delta is null) {
                    return null;
                }
                result.add(delta);
            }
            return result;
        }

        public virtual string format_timestamp(EventTimestamp t) => this.parse_event_timestamp(t)?.format() ?? "";

        public abstract string format_interval(CalendarInterval delta);

        public virtual int compare_timestamps(EventTimestamp t1, EventTimestamp t2) {
            CalendarTimestamp c1 = this.parse_event_timestamp(t1);
            CalendarTimestamp c2 = this.parse_event_timestamp(t2);
            if (c1 is null) {
                return (c2 is null ? 0 : -1);
            }
            if (c2 is null) {
                return 1;
            }
            CalendarInterval diff = c1.subtract(c2);
            if (diff is null) {
                return 0;
            }
            return diff.value;
        }

        public int compare_timestamps(EventTimestamp t1, string t2) => this.compare_timestamps(t1, new EventTimestamp(t2, null));

        public virtual string subtract_timestamp(EventTimestamp t1, EventTimestamp t2) {
            CalendarTimestamp c1 = this.parse_event_timestamp(t1);
            if (c1 is null) {
                return null;
            }
            CalendarTimestamp c2 = this.parse_event_timestamp(t2);
            if (c2 is null) {
                return null;
            }
            CalendarInterval diff = c1.subtract(c2);
            if (diff is null) {
                return null;
            }
            return this.format_interval(diff);
        }

        public string subtract_timestamp(EventTimestamp t1, string t2) => this.subtract_timestamp(t1, new EventTimestamp(t2, null));
    }

    public class EventTimestamp {
        public readonly string timestamp;
        public readonly string delta;

        public EventTimestamp(string timestamp, string delta) {
            this.timestamp = timestamp;
            this.delta = delta;
        }
    }

    public abstract class CalendarTimestamp {
        protected int value;

        public CalendarTimestamp(int value) {
            this.value = value;
        }

        public abstract string format();

        public virtual CalendarInterval subtract(CalendarTimestamp other) => new CalendarInterval(this.value - other.value);

        public virtual void add(CalendarInterval delta) => this.value += delta.value;
    }

    public class CalendarInterval {
        public int value;

        public CalendarInterval(int value) {
            this.value = value;
        }
    }

    public class FreeformTimestamp : CalendarTimestamp {
        public string text;

        public FreeformTimestamp(string text) : base(0) {
            this.text = text;
        }

        public override string format() => this.text;

        public override CalendarInterval subtract(CalendarTimestamp other) => null;

        public override void add(CalendarInterval delta) {
            FreeformInterval ffDelta = delta as FreeformInterval;
            if (ffDelta is null) {
                return;
            }
            this.text = $"{ffDelta.text} after {this.text}";
        }
    }

    public class FreeformInterval : CalendarInterval {
        public string text;

        public FreeformInterval(string text) : base(0) {
            this.text = text;
        }
    }

    public class FreeformCalendar : Calendar {
        public static bool static_validate_timestamp(string s) => s.Length > 0;

        public static bool static_validate_interval(string s) => s.Length > 0;

        public override string default_timestamp() => "campaign start";

        public override bool validate_timestamp(string s) => static_validate_timestamp(s);
        public override bool validate_interval(string s) => static_validate_interval(s);

        public override CalendarTimestamp parse_timestamp(string s) => new FreeformTimestamp(s);
        public override CalendarInterval parse_interval(string s) => new FreeformInterval(s);

        public override string format_interval(CalendarInterval delta) {
            FreeformInterval ffDelta = delta as FreeformInterval;
            if (delta is null) {
                return null;
            }
            return ffDelta.text;
        }
    }

    public class ClockTime {
        public const string PATTERN = (
            @"((?<hour>\d{1,2}):(?<minute>\d{2})(\s*(?<meridiem>am|pm))?)|" +
            @"((?<hour>\d{1,2})\s*(?<meridiem>am|pm))|" +
            @"(?<time_freeform>.+)"
        );

        protected readonly int hour_minutes;
        public int value;
        protected bool has_meridiam;
        protected string freeform;

        public ClockTime(int hourMinutes, int value, bool hasMeridiam, string freeform) {
            this.hour_minutes = hourMinutes;
            this.value = value;
            this.has_meridiam = hasMeridiam;
            this.freeform = freeform;
        }

        public static ClockTime parse(int hourMinutes, Match match) {
            if (match.Groups["hour"].Success) {
                int hour = int.Parse(match.Groups["hour"].Value), minute = 0;
                bool has_meridiem = match.Groups["meridiem"].Success;
                if (match.Groups["minute"].Success) {
                    minute = int.Parse(match.Groups["minute"].Value);
                }
                if (has_meridiem) {
                    if (hour == 12) {
                        // adjust 12 to 0 so that 12am => 00:00 and 12pm => 12:00
                        hour = 0;
                    }
                    if (match.Groups["meridiem"].Value.ToLower() == "pm") {
                        hour += 12;
                    }
                }
                return new ClockTime(hourMinutes, (hour * hourMinutes) + minute, has_meridiem, null);
            }
            if (match.Groups["time_freeform"].Success) {
                return new ClockTime(hourMinutes, 0, false, match.Groups["time_freeform"].Value);
            }
            return null;
        }

        public string format() {
            if (this.freeform is not null) {
                return this.freeform;
            }
            int hour = this.value / this.hour_minutes;
            int minute = this.value % this.hour_minutes;
            string meridiam = "";
            if (this.has_meridiam) {
                meridiam = (hour < 12 ? "am" : "pm");
                if (hour == 0) {
                    hour = 12;
                }
                if (hour > 12) {
                    hour -= 12;
                }
            }
            return $"{hour}:{minute:D2}{meridiam}";
        }
    }

    public abstract class DayCalendar : Calendar {
        protected const string TIME_INTERVAL = (
            @"((?<hours>\d+):(?<minutes>\d{2}))|" +
            @"((?<hours>\d+)\s+h(our(s?))?(,?\s+(?<minutes>\d+)\s+m(in(ute)?(s?))?)?)|" +
            @"((?<minutes>\d+)\s+min(ute)?(s?))"
        );
        protected static readonly Regex INTERVAL_EXP = new Regex(
            $"^((?<days>\\d+)\\s+day(s?)(,?\\s+({TIME_INTERVAL}))?)|({TIME_INTERVAL})$",
            RegexOptions.Compiled | RegexOptions.ExplicitCapture | RegexOptions.IgnoreCase
        );

        protected readonly int day_hours;
        protected readonly int day_minutes;
        protected readonly int hour_minutes;

        public DayCalendar(int dayHours, int hourMinutes) {
            this.day_hours = dayHours;
            this.day_minutes = dayHours * hourMinutes;
            this.hour_minutes = hourMinutes;
        }

        public static bool static_validate_interval(string s) => INTERVAL_EXP.IsMatch(s);

        public override CalendarInterval parse_interval(string s) {
            Match match = INTERVAL_EXP.Match(s);
            if (!match.Success) {
                return null;
            }
            int days = (match.Groups["days"].Success ? int.Parse(match.Groups["days"].Value) : 0);
            int hours = (match.Groups["hours"].Success ? int.Parse(match.Groups["hours"].Value) : 0);
            int minutes = (match.Groups["minutes"].Success ? int.Parse(match.Groups["minutes"].Value) : 0);
            if ((days == 0) && (hours == 0) && (minutes == 0)) {
                return null;
            }
            return new CalendarInterval((days * this.day_minutes) + (hours * this.hour_minutes) + minutes);
        }

        public override string format_interval(CalendarInterval delta) {
            if (delta is null) {
                return null;
            }
            if (delta.value == 0) {
                return "now";
            }
            int days = delta.value / this.day_minutes;
            int minutes = delta.value % this.day_minutes;
            int hours = minutes / this.hour_minutes;
            minutes %= this.hour_minutes;
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

    public class CampaignDateTimestamp : CalendarTimestamp {
        protected readonly int day_minutes;
        protected bool is_relative;
        protected ClockTime time;

        public CampaignDateTimestamp(int dayHours, int hourMinutes, Match match) : base(0) {
            this.day_minutes = dayHours * hourMinutes;
            this.is_relative = match.Groups["relative"].Success;
            this.value = int.Parse(match.Groups["date"].Value) * this.day_minutes;
            this.time = ClockTime.parse(hourMinutes, match);
            if (this.time is not null) {
                this.value += this.time.value;
            }
        }

        public override string format() {
            string relative = (this.is_relative ? "+" : "");
            int date = this.value / this.day_minutes;
            string time = this.time?.format() ?? "";
            if (time != "") {
                time = ", " + time;
            }
            return $"d{relative}{date}{time}";
        }

        public override void add(CalendarInterval delta) {
            this.value += delta.value;
            if (this.time is not null) {
                this.time.value = this.value % this.day_minutes;
            }
        }
    }

    public class CampaignDateCalendar : DayCalendar {
        protected static readonly Regex TIMESTAMP_EXP = new Regex(
            $"^d(?<relative>[+])?(?<date>\\d+)(,?\\s+({ClockTime.PATTERN}))?$",
            RegexOptions.Compiled | RegexOptions.ExplicitCapture | RegexOptions.IgnoreCase
        );
        protected const int DAY_HOURS = 24;
        protected const int HOUR_MINUTES = 60;

        public CampaignDateCalendar() : base(DAY_HOURS, HOUR_MINUTES) { }

        public static bool static_validate_timestamp(string s) => TIMESTAMP_EXP.IsMatch(s);

        public override string default_timestamp() => "d0";

        public override bool validate_timestamp(string s) => static_validate_timestamp(s);
        public override bool validate_interval(string s) => static_validate_interval(s);

        public override CalendarTimestamp parse_timestamp(string s) {
            Match match = TIMESTAMP_EXP.Match(s);
            if (!match.Success) {
                return null;
            }
            return new CampaignDateTimestamp(this.day_hours, this.hour_minutes, match);
        }
    }

    public class CalendarMonth {
        public readonly string name;
        public readonly int days;
        public readonly bool is_virtual;

        public CalendarMonth(string name, int days, bool isVirtual = false) {
            this.name = name;
            this.days = days;
            this.is_virtual = isVirtual;
        }
    }

    public class MonthTimestamp : CalendarTimestamp {
        protected readonly string template;
        protected readonly IList<CalendarMonth> months;
        protected readonly int month_count;
        protected readonly int year_minutes;
        protected readonly int day_minutes;
        protected ClockTime time;

        public MonthTimestamp(string template, IList<CalendarMonth> months, int dayHours, int hourMinutes, Match match) : base(0) {
            this.template = template;
            this.months = months;
            this.day_minutes = dayHours * hourMinutes;
            int yearDays = 0;
            int monthCount = 0;
            foreach (CalendarMonth month in months) {
                yearDays += month.days;
                if (!month.is_virtual) {
                    monthCount += 1;
                }
            }
            this.year_minutes = yearDays * this.day_minutes;
            this.month_count = monthCount;
            int year = int.Parse(match.Groups["year"].Value);
            int day = int.Parse(match.Groups["day"].Value) - 1;
            if (match.Groups["monthname"].Success) {
                string monthName = match.Groups["monthname"].Value.ToLower();
                foreach (CalendarMonth monthSpec in this.months) {
                    if (monthSpec.name.ToLower() == monthName) {
                        break;
                    }
                    day += monthSpec.days;
                }
            }
            else {
                int prevMonths = int.Parse(match.Groups["month"].Value) - 1;
                for (int i = 0; i < prevMonths; i++) {
                    day += this.months[i].days;
                }
            }
            this.value = (year * this.year_minutes) + (day * this.day_minutes);
            this.time = ClockTime.parse(hourMinutes, match);
            if (this.time is not null) {
                this.value += this.time.value;
            }
        }

        public override string format() {
            int year = this.value / this.year_minutes;
            int minute = this.value % this.year_minutes;
            int day = minute / this.day_minutes + 1;
            // 0-based month: we're just using it as an array index
            int month = 0;
            foreach (CalendarMonth monthSpec in this.months) {
                if (day <= monthSpec.days) {
                    break;
                }
                month += 1;
                day -= monthSpec.days;
            }
            string date = string.Format(this.template, year, this.months[month].name, day);
            string time = this.time?.format() ?? "";
            if (time != "") {
                time = ", " + time;
            }
            return date + time;
        }

        public override void add(CalendarInterval delta) {
            if (delta is MonthInterval interval) {
                int months = interval.months;
                // trim months to [0..month_count); convert excess to years
                int years = months / this.month_count;
                months %= this.month_count;
                if (months < 0) {
                    months += this.month_count;
                    years -= 1;
                }
                this.value += years * this.year_minutes;
                if (months > 0) {
                    // figure out which month we're currently in...
                    int day = (this.value % this.year_minutes) / this.day_minutes;
                    int curMonth = 0;
                    foreach (CalendarMonth monthSpec in this.months) {
                        if (day <= monthSpec.days) {
                            break;
                        }
                        curMonth += 1;
                        day -= monthSpec.days;
                    }
                    // ...and advance the appropriate number of months
                    while (months > 0) {
                        this.value += this.months[curMonth].days * this.day_minutes;
                        curMonth = (curMonth + 1) % this.months.Count;
                        if (!this.months[curMonth].is_virtual) {
                            // virtual months (e.g. out-of-bad festival days) don't count
                            months -= 1;
                        }
                    }
                }
            }
            this.value += delta.value;
            if (this.time is not null) {
                this.time.value = this.value % this.day_minutes;
            }
        }
    }

    public class MonthInterval : CalendarInterval {
        public int months;

        public MonthInterval(int value, int months = 0) : base(value) {
            this.months = months;
        }
    }

    public abstract class MonthCalendar : Calendar {
        protected const string DATE_INTERVAL = (
            @"((?<years>\d+)\s+y(ear(s?))?(,?\s+(?<months>\d+)\s+mo(nth(s?))?)?(,?\s+(?<days>\d+)\s+d(ay(s?))?)?)|" +
            @"((?<months>\d+)\s+mo(nth(s?))?(,?\s+(?<days>\d+)\s+d(ay(s?))?)?)|" +
            @"((?<days>\d+)\s+d(ay(s?))?)"
        );
        protected const string TIME_INTERVAL = (
            @"((?<hours>\d+):(?<minutes>\d{2}))|" +
            @"((?<hours>\d+)\s+h(our(s?))?(,?\s+(?<minutes>\d+)\s+m(in(ute)?(s?))?)?)|" +
            @"((?<minutes>\d+)\s+min(ute)?(s?))"
        );
        protected static readonly Regex INTERVAL_EXP = new Regex(
            $"^(({DATE_INTERVAL})(,?\\s+({TIME_INTERVAL}))?)|({TIME_INTERVAL})$",
            RegexOptions.Compiled | RegexOptions.ExplicitCapture | RegexOptions.IgnoreCase
        );

        protected readonly string template;
        protected readonly IList<CalendarMonth> months;
        protected readonly int year_minutes;
        protected readonly int day_hours;
        protected readonly int day_minutes;
        protected readonly int hour_minutes;

        public MonthCalendar(string template, IList<CalendarMonth> months, int dayHours, int hourMinutes) {
            this.template = template;
            this.months = months;
            this.day_hours = dayHours;
            this.day_minutes = dayHours * hourMinutes;
            int yearDays = 0;
            foreach (CalendarMonth month in months) {
                yearDays += month.days;
            }
            this.year_minutes = yearDays * this.day_minutes;
            this.hour_minutes = hourMinutes;
        }

        public static bool static_validate_interval(string s) => INTERVAL_EXP.IsMatch(s);

        public override CalendarInterval parse_interval(string s) {
            Match match = INTERVAL_EXP.Match(s);
            if (!match.Success) {
                return null;
            }
            int years = (match.Groups["years"].Success ? int.Parse(match.Groups["years"].Value) : 0);
            int months = (match.Groups["months"].Success ? int.Parse(match.Groups["months"].Value) : 0);
            int days = (match.Groups["days"].Success ? int.Parse(match.Groups["days"].Value) : 0);
            int hours = (match.Groups["hours"].Success ? int.Parse(match.Groups["hours"].Value) : 0);
            int minutes = (match.Groups["minutes"].Success ? int.Parse(match.Groups["minutes"].Value) : 0);
            if ((years == 0) && (months == 0) && (days == 0) && (hours == 0) && (minutes == 0)) {
                return null;
            }
            int value = (years * this.year_minutes) + (days * this.day_minutes) + (hours * this.hour_minutes) + minutes;
            return new MonthInterval(value, months);
        }

        public override string format_interval(CalendarInterval delta) {
            if (delta is null) {
                return null;
            }
            int months = 0;
            if (delta is MonthInterval interval) {
                months = interval.months;
            }
            if ((delta.value == 0) && (months == 0)) {
                return "now";
            }
            int years = delta.value / this.year_minutes;
            int minutes = delta.value % this.year_minutes;
            int days = minutes / this.day_minutes;
            minutes %= this.day_minutes;
            int hours = minutes / this.hour_minutes;
            minutes %= this.hour_minutes;
            List<string> chunks = new List<string>(5);
            if (years != 0){
                chunks.Add($"{years}y");
            }
            if (months > 0) {
                chunks.Add($"{months}mo");
            }
            if (days > 0) {
                chunks.Add($"{days}d");
            }
            if (hours > 0) {
                chunks.Add($"{hours}h");
            }
            if(minutes > 0) {
                chunks.Add($"{minutes}m");
            }
            return string.Join(", ", chunks);
        }
    }

    public class AlignedMonthTimestamp : MonthTimestamp {
        protected readonly IList<string> days;

        public AlignedMonthTimestamp(
            IList<string> days, string template, IList<CalendarMonth> months, int dayHours, int hourMinutes, Match match
        ) : base(template, months, dayHours, hourMinutes, match) {
            this.days = days;
        }

        public override string format() {
            string result = base.format();
            // 0-based day and month: we're just using these as array indices
            int day = (this.value % this.year_minutes) / this.day_minutes;
            int month = 0;
            foreach (CalendarMonth monthSpec in this.months) {
                if (day < monthSpec.days) {  // < rather than <= because 0-based
                    break;
                }
                month += 1;
                day -= monthSpec.days;
            }
            if (!this.months[month].is_virtual) {
                result = $"{this.days[day % this.days.Count]}, {result}";
            }
            return result;
        }
    }

    public class GreyhawkCalendar : MonthCalendar {
        protected const string TEMPLATE = "{2} {1}, {0} CY";
        protected static readonly CalendarMonth[] MONTHS = new[] {
            new CalendarMonth("Needfest", 7, true),
            new CalendarMonth("Fireseek", 28),
            new CalendarMonth("Readying", 28),
            new CalendarMonth("Coldeven", 28),
            new CalendarMonth("Growfest", 7, true),
            new CalendarMonth("Planting", 28),
            new CalendarMonth("Flocktime", 28),
            new CalendarMonth("Wealsun", 28),
            new CalendarMonth("Richfest", 7, true),
            new CalendarMonth("Reaping", 28),
            new CalendarMonth("Goodmonth", 28),
            new CalendarMonth("Harvester", 28),
            new CalendarMonth("Brewfest", 7, true),
            new CalendarMonth("Patchwall", 28),
            new CalendarMonth("Ready'reat", 28),
            new CalendarMonth("Sunsebb", 28),
        };
        protected static readonly string[] DAYS = new[] { "Starday", "Sunday", "Moonday", "Godsday", "Waterday", "Earthday", "Freeday" };
        protected static readonly string WEEKDAY_PATTERN = "(((" + string.Join(")|(", DAYS) + @")),\s+)?";
        protected const string MONTH_NAME_PATTERN = (
            "(Needfest)|(Fireseek)|(Readying)|(Coldeven)|(Growfest)|(Planting)|(Flocktime)|(Wealsun)|" +
            "(Richfest)|(Reaping)|(Goodmonth)|(Harvester)|(Brewfest)|(Patchwall)|(Ready'reat)|(Sunsebb)"
        );
        protected static readonly string MONTH_PATTERN = @"((?<month>\d{1,2})|" + $"(?<monthname>{MONTH_NAME_PATTERN}))";
        protected static readonly string DATE_PATTERN = WEEKDAY_PATTERN + @"(?<day>\d{1,2})\s+" + MONTH_PATTERN + @",\s+(?<year>\d+)\s*CY";
        protected static readonly Regex TIMESTAMP_EXP = new Regex(
            $"^{DATE_PATTERN}(,?\\s+({ClockTime.PATTERN}))?$",
            RegexOptions.Compiled | RegexOptions.ExplicitCapture | RegexOptions.IgnoreCase
        );
        protected const int DAY_HOURS = 24;
        protected const int HOUR_MINUTES = 60;

        public GreyhawkCalendar() : base(TEMPLATE, MONTHS, DAY_HOURS, HOUR_MINUTES) { }

        public static bool static_validate_timestamp(string s) => TIMESTAMP_EXP.IsMatch(s);

        public override string default_timestamp() => "1 Needfest, 591 CY";

        public override bool validate_timestamp(string s) => static_validate_timestamp(s);
        public override bool validate_interval(string s) => static_validate_interval(s);

        public override CalendarTimestamp parse_timestamp(string s) {
            Match match = TIMESTAMP_EXP.Match(s);
            if (!match.Success) {
                return null;
            }
            return new AlignedMonthTimestamp(DAYS, this.template, this.months, this.day_hours, this.hour_minutes, match);
        }
    }

    public class EberronCalendar : MonthCalendar {
        protected const string TEMPLATE = "{2} {1}, {0} YK";
        protected static readonly CalendarMonth[] MONTHS = new[] {
            new CalendarMonth("Zarantyr", 28),
            new CalendarMonth("Olarune", 28),
            new CalendarMonth("Therendor", 28),
            new CalendarMonth("Eyre", 28),
            new CalendarMonth("Dravago", 28),
            new CalendarMonth("Nymm", 28),
            new CalendarMonth("Lharvion", 28),
            new CalendarMonth("Barrakas", 28),
            new CalendarMonth("Rhaan", 28),
            new CalendarMonth("Sypheros", 28),
            new CalendarMonth("Aryth", 28),
            new CalendarMonth("Vult", 28),
        };
        protected static readonly string[] DAYS = new[] { "Sul", "Mol", "Zol", "Wir", "Zor", "Far", "Sar" };
        protected static readonly string WEEKDAY_PATTERN = "(((" + string.Join(")|(", DAYS) + @")),\s+)?";
        protected const string MONTH_NAME_PATTERN = (
            "(Zarantyr)|(Olarune)|(Therendor)|(Eyre)|(Dravago)|(Nymm)|(Lharvion)|(Barrakas)|(Rhaan)|(Sypheros)|(Aryth)|(Vult)"
        );
        protected static readonly string MONTH_PATTERN = @"((?<month>\d{1,2})|" + $"(?<monthname>{MONTH_NAME_PATTERN}))";
        protected static readonly string DATE_PATTERN = WEEKDAY_PATTERN + @"(?<day>\d{1,2})\s+" + MONTH_PATTERN + @",\s+(?<year>\d+)\s*YK";
        protected static readonly Regex TIMESTAMP_EXP = new Regex(
            $"^{DATE_PATTERN}(,?\\s+({ClockTime.PATTERN}))?$",
            RegexOptions.Compiled | RegexOptions.ExplicitCapture | RegexOptions.IgnoreCase
        );
        protected const int DAY_HOURS = 24;
        protected const int HOUR_MINUTES = 60;

        public EberronCalendar() : base(TEMPLATE, MONTHS, DAY_HOURS, HOUR_MINUTES) { }

        public static bool static_validate_timestamp(string s) => TIMESTAMP_EXP.IsMatch(s);

        public override string default_timestamp() => "Sul, 1 Zarantyr, 998 YK";

        public override bool validate_timestamp(string s) => static_validate_timestamp(s);
        public override bool validate_interval(string s) => static_validate_interval(s);

        public override CalendarTimestamp parse_timestamp(string s) {
            Match match = TIMESTAMP_EXP.Match(s);
            if (!match.Success) {
                return null;
            }
            return new AlignedMonthTimestamp(DAYS, this.template, this.months, this.day_hours, this.hour_minutes, match);
        }
    }

    //TODO:
    //forgotten realms:
    //  regular months 30 days; *festivals* 7 days; shieldmeet is leap year festival, only exists in years that are multiples of 4
    //    Hammer, *Midwinter*, Alturiak, Ches, Tarsakh, *Greengrass*, Mirtul, Kythorn, Flamerule, *Midsummer*,
    //    *Shieldmeet*, Eleasis, Eleint, *Highharvestide*, Marpenoth, Uktar, *Feast of the Moon*, Nightal
    //  tendays don't have named days
    //  "{day} {month}, {year} DR"; default "1 Hammer, 1491 DR"
    //gregorian:
    //  leap year in february if year%4==0 and (year%100!=0 or year%1000==0)
    //  ...
    //julian?
}
