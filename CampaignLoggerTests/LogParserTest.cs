using System.Collections.Generic;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CampaignLoggerTests {
    [TestClass]
    public class LogParserTests {
        [TestMethod]
        [DataRow("", null, new string[] { "" })]
        [DataRow("test data", null, new string[] { "test data" })]
        [DataRow("Bob, Joe, and Enid", null, new string[] { "Bob, Joe, and Enid" })]
        [DataRow("Bob, Joe, and Enid", ",", new string[] { "Bob", "Joe", "and Enid" })]
        [DataRow("thing one (sub 1.1; sub 1.2); thing two", null, new string[] { "thing one (sub 1.1; sub 1.2)", "thing two" })]
        [DataRow("a[b;\";fake];\"c;[two; levels; deep];]; d", null, new string[] { "a[b;\";fake];\"c;[two; levels; deep];]", "d" })]
        [DataRow("@{Ernie, the Terror of Detroit}, Jeff", ",", new string[] { "@{Ernie, the Terror of Detroit}", "Jeff" })]
        public void test_split_line(string s, string delim, string[] expected) {
            List<string> result;
            if (delim is null) {
                result = new List<string>(CampaignLogger.LogParser.split_line(s));
            }
            else {
                result = new List<string>(CampaignLogger.LogParser.split_line(s, delim));
            }
            string resStr = string.Join("\", \"", result);
            if (result.Count > 0) {
                resStr = "\"" + resStr + "\"";
            }
            string expStr = string.Join("\", \"", expected);
            if (expected.Length > 0) {
                expStr = "\"" + expStr + "\"";
            }
            CollectionAssert.AreEqual(expected, result, $"[{resStr}] != [{expStr}]");
        }

        [TestMethod]
        [DataRow("", 1, new string[] { "" })]
        [DataRow("foo; bar; baz", 1, new string[] { "foo", "bar; baz" })]
        [DataRow("foo; bar; baz", 2, new string[] { "foo", "bar", "baz" })]
        [DataRow("foo; bar; baz", -1, new string[] { "foo", "bar", "baz" })]
        [DataRow("foo (f1; f2); bar (br1; br2); baz (bz1, bz2)", 1, new string[] { "foo (f1; f2)", "bar (br1; br2); baz (bz1, bz2)" })]
        public void test_split_line_count(string s, int count, string[] expected) {
            List<string> result = new List<string>(CampaignLogger.LogParser.split_line(s, count: count));
            string resStr = string.Join("\", \"", result);
            if (result.Count > 0) {
                resStr = "\"" + resStr + "\"";
            }
            string expStr = string.Join("\", \"", expected);
            if (expected.Length > 0) {
                expStr = "\"" + expStr + "\"";
            }
            CollectionAssert.AreEqual(expected, result, $"[{resStr}] != [{expStr}]");
        }
    }
}
