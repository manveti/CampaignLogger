using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CampaignLoggerTests {
    [TestClass]
    public class CalendarTests {
        [TestMethod]
        [DataRow("1 Needfest, 591 CY", "Greyhawk")]
        [DataRow("Waterday, 12 Wealsun, 1000 CY", "Greyhawk")]
        [DataRow("Sul, 1 Zarantyr, 998 YK", "Eberron")]
        [DataRow("10 Dravago, 1040 YK", "Eberron")]
        [DataRow("d0", "Campaign Date")]
        [DataRow("d+876, morning", "Campaign Date")]
        [DataRow("Midday on the eve of the 83rd anniversary of the fall of the Pumpkin Tyrant", "Freeform")]
        [DataRow("Anything at all", "Freeform")]
        public void test_get_calendar_from_timestamp(string timestamp, string expectedName) {
            CampaignLogger.Calendar calendar = CampaignLogger.Calendar.get_calendar_from_timestamp(timestamp);
            CampaignLogger.Calendar expected = CampaignLogger.Calendar.get_calendar(expectedName);
            Assert.AreEqual(expected.GetType(), calendar.GetType());
        }
    }

    public abstract class CalendarTestBase {
        protected abstract CampaignLogger.Calendar get_calendar();

        protected void test_parse_timestamp_helper(string timestamp, string expected) {
            CampaignLogger.Calendar calendar = this.get_calendar();
            CampaignLogger.CalendarTimestamp result = calendar.parse_timestamp(timestamp);
            Assert.AreEqual(expected, result.format());
        }

        protected void test_parse_interval_helper(string interval, string expected) {
            CampaignLogger.Calendar calendar = this.get_calendar();
            CampaignLogger.CalendarInterval result = calendar.parse_interval(interval);
            Assert.AreEqual(expected, calendar.format_interval(result));
        }

        protected void test_parse_event_timestamp_helper(string timestamp, string interval, string expected) {
            CampaignLogger.Calendar calendar = this.get_calendar();
            CampaignLogger.EventTimestamp eventTimestamp = new CampaignLogger.EventTimestamp(timestamp, interval);
            CampaignLogger.CalendarTimestamp result = calendar.parse_event_timestamp(eventTimestamp);
            Assert.AreEqual(expected, result.format());
        }

        protected void test_compare_timestamps_helper(string t1, string t2, int expected) {
            CampaignLogger.Calendar calendar = this.get_calendar();
            CampaignLogger.EventTimestamp et1 = new CampaignLogger.EventTimestamp(t1, null);
            CampaignLogger.EventTimestamp et2 = new CampaignLogger.EventTimestamp(t2, null);
            int result = calendar.compare_timestamps(et1, et2);
            Assert.AreEqual(System.Math.Sign(expected), System.Math.Sign(result));
        }

        protected void test_subtract_timestamp_helper(string t1, string t2, string expected) {
            CampaignLogger.Calendar calendar = this.get_calendar();
            CampaignLogger.EventTimestamp et1 = new CampaignLogger.EventTimestamp(t1, null);
            CampaignLogger.EventTimestamp et2 = new CampaignLogger.EventTimestamp(t2, null);
            string result = calendar.subtract_timestamp(et1, et2);
            Assert.AreEqual(expected, result);
        }
    }

    [TestClass]
    public class FreeformCalendarTests : CalendarTestBase {
        protected override CampaignLogger.Calendar get_calendar() => new CampaignLogger.FreeformCalendar();

        [TestMethod]
        [DataRow("Midday on the eve of the 83rd anniversary of the fall of the Pumpkin Tyrant")]
        [DataRow("Anything at all")]
        public void test_parse_timestamp(string timestamp) {
            this.test_parse_timestamp_helper(timestamp, timestamp);
        }

        [TestMethod]
        [DataRow("3 days")]
        [DataRow("7 fortnights")]
        [DataRow("Anything at all")]
        public void test_parse_interval(string interval) {
            this.test_parse_interval_helper(interval, interval);
        }

        [TestMethod]
        [DataRow("Some important day", "3 days", "3 days after Some important day")]
        [DataRow("Anything at all", "Anything else", "Anything else after Anything at all")]
        public void parse_event_timestamp(string timestamp, string interval, string expected) {
            this.test_parse_event_timestamp_helper(timestamp, interval, expected);
        }

        [TestMethod]
        [DataRow("Some important day", "3 days after Some important day", 0)]
        [DataRow("Any timestamp at all", "Any other timestamp", 0)]
        public void test_compare_timestamps(string t1, string t2, int expected) {
            this.test_compare_timestamps_helper(t1, t2, expected);
        }

        [TestMethod]
        [DataRow("Some important day", "3 days after Some important day", null)]
        [DataRow("Any timestamp at all", "Any other timestamp", null)]
        public void test_subtract_timestamp(string t1, string t2, string expected) {
            this.test_subtract_timestamp_helper(t1, t2, expected);
        }
    }

    [TestClass]
    public class CampaignDateCalendarTests : CalendarTestBase {
        protected override CampaignLogger.Calendar get_calendar() => new CampaignLogger.CampaignDateCalendar();

        [TestMethod]
        [DataRow("d0", "d0")]
        [DataRow("D0", "d0")]
        [DataRow("d+876, morning", "d+876, morning")]
        [DataRow("D+876, morning", "d+876, morning")]
        [DataRow("d+123, 4:56", "d+123, 4:56")]
        public void test_parse_timestamp(string timestamp, string expected) {
            this.test_parse_timestamp_helper(timestamp, expected);
        }

        [TestMethod]
        [DataRow("1 day", "1 day")]
        [DataRow("3 days", "3 days")]
        [DataRow("4 hours", "4h")]
        [DataRow("1:23", "1h, 23m")]
        [DataRow("10 days, 20:30", "10 days, 20h, 30m")]
        [DataRow("10 days, 20 hours, 30 minutes", "10 days, 20h, 30m")]
        [DataRow("1 year", null)]
        [DataRow("1 month", null)]
        [DataRow("7 fortnights", null)]
        [DataRow("Anything at all", null)]
        public void test_parse_interval(string interval, string expected) {
            this.test_parse_interval_helper(interval, expected);
        }

        [TestMethod]
        [DataRow("d0", "1 day", "d1")]
        [DataRow("d+876, morning", "3 days", "d+879, morning")]
        [DataRow("d+876, morning", "1 hour", "d+876, morning")]
        [DataRow("d+123, 4:56", "10 days, 20 hours, 30 minutes", "d+134, 1:26")]
        public void parse_event_timestamp(string timestamp, string interval, string expected) {
            this.test_parse_event_timestamp_helper(timestamp, interval, expected);
        }

        [TestMethod]
        [DataRow("d0", "D0", 0)]
        [DataRow("d1", "d0", 1)]
        [DataRow("d+876, morning", "d+879, morning", -1)]
        [DataRow("d+123, 4:56", "d+123, 2:34", 1)]
        public void test_compare_timestamps(string t1, string t2, int expected) {
            this.test_compare_timestamps_helper(t1, t2, expected);
            this.test_compare_timestamps_helper(t2, t1, -expected);
        }

        [TestMethod]
        [DataRow("d0", "D0", "now")]
        [DataRow("d1", "d0", "1 day")]
        [DataRow("d+879, morning", "d+876, morning", "3 days")]
        [DataRow("d+123, 4:56", "d+123, 2:34", "2h, 22m")]
        public void test_subtract_timestamp(string t1, string t2, string expected) {
            this.test_subtract_timestamp_helper(t1, t2, expected);
        }
    }

    [TestClass]
    public class GreyhawkCalendarTests : CalendarTestBase {
        protected override CampaignLogger.Calendar get_calendar() => new CampaignLogger.GreyhawkCalendar();

        [TestMethod]
        [DataRow("1 Needfest, 591 CY", "1 Needfest, 591 CY")]
        [DataRow("1 needfest, 591 cy", "1 Needfest, 591 CY")]
        [DataRow("1 Needfest, 591 CY, morning", "1 Needfest, 591 CY, morning")]
        [DataRow("1 Needfest, 591 CY, 7:03", "1 Needfest, 591 CY, 7:03")]
        [DataRow("1 Needfest, 591 CY, 7:03am", "1 Needfest, 591 CY, 7:03am")]
        [DataRow("1 Needfest, 591 CY, 7:03AM", "1 Needfest, 591 CY, 7:03am")]
        [DataRow("Waterday, 12 Wealsun, 1000 CY", "Waterday, 12 Wealsun, 1000 CY")]
        [DataRow("waterday, 12 wealsun, 1000 cy", "Waterday, 12 Wealsun, 1000 CY")]
        [DataRow("12 Wealsun, 1000 CY", "Waterday, 12 Wealsun, 1000 CY")]
        public void test_parse_timestamp(string timestamp, string expected) {
            this.test_parse_timestamp_helper(timestamp, expected);
        }

        [TestMethod]
        [DataRow("1 day", "1d")]
        [DataRow("3 days", "3d")]
        [DataRow("4 hours", "4h")]
        [DataRow("1:23", "1h, 23m")]
        [DataRow("10 days, 20:30", "10d, 20h, 30m")]
        [DataRow("10 days, 20 hours, 30 minutes", "10d, 20h, 30m")]
        [DataRow("1 year", "1y")]
        [DataRow("2 years", "2y")]
        [DataRow("364 days", "1y")]
        [DataRow("366 days", "1y, 2d")]
        [DataRow("1 month", "1mo")]
        [DataRow("2 months", "2mo")]
        [DataRow("16 years, 3 months, 2 days, 4:56", "16y, 3mo, 2d, 4h, 56m")]
        [DataRow("16 years, 3 months, 2 days, 4 hours, 56 minutes", "16y, 3mo, 2d, 4h, 56m")]
        [DataRow("7 fortnights", null)]
        [DataRow("Anything at all", null)]
        public void test_parse_interval(string interval, string expected) {
            this.test_parse_interval_helper(interval, expected);
        }

        [TestMethod]
        [DataRow("1 Needfest, 591 CY", "1 day", "2 Needfest, 591 CY")]
        [DataRow("1 Needfest, 591 CY, morning", "2 days", "3 Needfest, 591 CY, morning")]
        [DataRow("1 Needfest, 591 CY, 7:03", "3 days", "4 Needfest, 591 CY, 7:03")]
        [DataRow("1 Needfest, 591 CY, 7:03am", "4 days", "5 Needfest, 591 CY, 7:03am")]
        [DataRow("1 Needfest, 591 CY, 7:03", "1 day, 7 hours", "2 Needfest, 591 CY, 14:03")]
        [DataRow("1 Needfest, 591 CY, 7:03am", "1 day, 7 hours", "2 Needfest, 591 CY, 2:03pm")]
        [DataRow("1 Needfest, 591 CY", "1 year, 1 month, 1 day", "Sunday, 2 Fireseek, 592 CY")]
        [DataRow("4 Flocktime, 1000 CY", "28 days", "Godsday, 4 Wealsun, 1000 CY")]
        [DataRow("4 Flocktime, 1000 CY", "1 month", "Godsday, 4 Wealsun, 1000 CY")]
        [DataRow("4 Wealsun, 1000 CY", "28 days", "4 Richfest, 1000 CY")]
        [DataRow("4 Wealsun, 1000 CY", "1 month", "Godsday, 4 Reaping, 1000 CY")]
        [DataRow("4 Flocktime, 1000 CY", "1 year, 1 month, 1 day", "Waterday, 5 Wealsun, 1001 CY")]
        public void parse_event_timestamp(string timestamp, string interval, string expected) {
            this.test_parse_event_timestamp_helper(timestamp, interval, expected);
        }

        [TestMethod]
        [DataRow("1 Needfest, 591 CY", "1 Needfest, 591 cy", 0)]
        [DataRow("2 Needfest, 591 CY", "1 Needfest, 591 CY", 1)]
        [DataRow("1 Needfest, 591 CY, morning", "3 Needfest, 591 CY, morning", -1)]
        [DataRow("1 Needfest, 591 CY, 7:03am", "1 Needfest, 591 CY, 19:03", -1)]
        [DataRow("1 Needfest, 591 CY, 7:03pm", "1 Needfest, 591 CY, 19:03", 0)]
        [DataRow("1 Needfest, 592 CY", "2 Needfest, 591 cy", 1)]
        public void test_compare_timestamps(string t1, string t2, int expected) {
            this.test_compare_timestamps_helper(t1, t2, expected);
            this.test_compare_timestamps_helper(t2, t1, -expected);
        }

        [TestMethod]
        [DataRow("1 Needfest, 591 CY", "1 Needfest, 591 cy", "now")]
        [DataRow("2 Needfest, 591 CY", "1 Needfest, 591 CY", "1d")]
        [DataRow("3 Needfest, 591 CY, morning", "1 Needfest, 591 CY, morning", "2d")]
        [DataRow("1 Needfest, 591 CY, 19:03", "1 Needfest, 591 CY, 7:03am", "12h")]
        [DataRow("1 Needfest, 591 CY, 7:03pm", "1 Needfest, 591 CY, 19:03", "now")]
        [DataRow("1 Needfest, 592 CY", "2 Needfest, 591 cy", "363d")]
        public void test_subtract_timestamp(string t1, string t2, string expected) {
            this.test_subtract_timestamp_helper(t1, t2, expected);
        }
    }

    [TestClass]
    public class EberronCalendarTests : CalendarTestBase {
        protected override CampaignLogger.Calendar get_calendar() => new CampaignLogger.EberronCalendar();

        [TestMethod]
        [DataRow("Sul, 1 Zarantyr, 998 YK", "Sul, 1 Zarantyr, 998 YK")]
        [DataRow("sul, 1 zarantyr, 998 yk", "Sul, 1 Zarantyr, 998 YK")]
        [DataRow("10 Dravago, 1040 YK", "Zol, 10 Dravago, 1040 YK")]
        [DataRow("10 dravago, 1040 yk", "Zol, 10 Dravago, 1040 YK")]
        [DataRow("Zol, 10 Dravago, 1040 YK", "Zol, 10 Dravago, 1040 YK")]
        public void test_parse_timestamp(string timestamp, string expected) {
            this.test_parse_timestamp_helper(timestamp, expected);
        }

        [TestMethod]
        [DataRow("1 day", "1d")]
        [DataRow("3 days", "3d")]
        [DataRow("4 hours", "4h")]
        [DataRow("1:23", "1h, 23m")]
        [DataRow("10 days, 20:30", "10d, 20h, 30m")]
        [DataRow("10 days, 20 hours, 30 minutes", "10d, 20h, 30m")]
        [DataRow("1 year", "1y")]
        [DataRow("2 years", "2y")]
        [DataRow("336 days", "1y")]
        [DataRow("338 days", "1y, 2d")]
        [DataRow("1 month", "1mo")]
        [DataRow("2 months", "2mo")]
        [DataRow("16 years, 3 months, 2 days, 4:56", "16y, 3mo, 2d, 4h, 56m")]
        [DataRow("16 years, 3 months, 2 days, 4 hours, 56 minutes", "16y, 3mo, 2d, 4h, 56m")]
        [DataRow("7 fortnights", null)]
        [DataRow("Anything at all", null)]
        public void test_parse_interval(string interval, string expected) {
            this.test_parse_interval_helper(interval, expected);
        }

        [TestMethod]
        [DataRow("1 Zarantyr, 998 YK", "1 day", "Mol, 2 Zarantyr, 998 YK")]
        [DataRow("1 Zarantyr, 998 YK, morning", "2 days", "Zol, 3 Zarantyr, 998 YK, morning")]
        [DataRow("1 Zarantyr, 998 YK, 7:03", "3 days", "Wir, 4 Zarantyr, 998 YK, 7:03")]
        [DataRow("1 Zarantyr, 998 YK, 7:03pm", "4 days", "Zor, 5 Zarantyr, 998 YK, 7:03pm")]
        [DataRow("1 Zarantyr, 998 YK, 7:03", "1 day, 7 hours", "Mol, 2 Zarantyr, 998 YK, 14:03")]
        [DataRow("1 Zarantyr, 998 YK, 7:03am", "1 day, 7 hours", "Mol, 2 Zarantyr, 998 YK, 2:03pm")]
        [DataRow("1 Zarantyr, 998 YK, 7:03pm", "1 day, 7 hours", "Zol, 3 Zarantyr, 998 YK, 2:03am")]
        [DataRow("1 Zarantyr, 998 YK", "1 year, 1 month, 1 day", "Mol, 2 Olarune, 999 YK")]
        [DataRow("1 Zarantyr, 998 YK, 12:34", "28 days", "Sul, 1 Olarune, 998 YK, 12:34")]
        [DataRow("1 Zarantyr, 998 YK, 12:34", "1 month", "Sul, 1 Olarune, 998 YK, 12:34")]
        public void parse_event_timestamp(string timestamp, string interval, string expected) {
            this.test_parse_event_timestamp_helper(timestamp, interval, expected);
        }

        [TestMethod]
        [DataRow("1 Zarantyr, 998 YK", "1 Zarantyr, 998 yk", 0)]
        [DataRow("Mol, 2 Zarantyr, 998 YK", "1 Zarantyr, 998 YK", 1)]
        [DataRow("1 Zarantyr, 998 YK, morning", "Zol, 3 Zarantyr, 998 YK, morning", -1)]
        [DataRow("1 Zarantyr, 998 YK, 7:03am", "1 Zarantyr, 998 YK, 19:03", -1)]
        [DataRow("1 Zarantyr, 998 YK, 7:03pm", "1 Zarantyr, 998 YK, 19:03", 0)]
        [DataRow("1 Zarantyr, 999 YK", "2 Zarantyr, 998 YK", 1)]
        public void test_compare_timestamps(string t1, string t2, int expected) {
            this.test_compare_timestamps_helper(t1, t2, expected);
            this.test_compare_timestamps_helper(t2, t1, -expected);
        }

        [TestMethod]
        [DataRow("1 Zarantyr, 998 YK", "1 Zarantyr, 998 yk", "now")]
        [DataRow("Mol, 2 Zarantyr, 998 YK", "1 Zarantyr, 998 YK", "1d")]
        [DataRow("Zol, 3 Zarantyr, 998 YK, morning", "1 Zarantyr, 998 YK, morning", "2d")]
        [DataRow("1 Zarantyr, 998 YK, 19:03", "1 Zarantyr, 998 YK, 7:03am", "12h")]
        [DataRow("1 Zarantyr, 998 YK, 7:03pm", "1 Zarantyr, 998 YK, 19:03", "now")]
        [DataRow("1 Zarantyr, 999 YK", "2 Zarantyr, 998 YK", "335d")]
        public void test_subtract_timestamp(string t1, string t2, string expected) {
            this.test_subtract_timestamp_helper(t1, t2, expected);
        }
    }
}
