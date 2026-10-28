using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using HancomLanguageSwitch;

class CoreTests
{
    static int assertions;
    static void Assert(bool condition, string message) { assertions++; if (!condition) throw new Exception(message); }
    static void Reject(Action action, string message) { bool rejected = false; try { action(); } catch { rejected = true; } Assert(rejected, message); }
    static string Read(string p) { return File.ReadAllText(p, Encoding.UTF8); }
    static int Main(string[] args)
    {
        try {
            string root = Path.GetFullPath(Path.Combine("tests", "runs", Guid.NewGuid().ToString("N")));
            Directory.CreateDirectory(root);
            string config = Path.Combine(root, "ThemeInfo.xml");
            string original = "<?xml version=\"1.0\" encoding=\"utf-8\"?><root><!--preserve me--><Default><ThemeInfo><CultureName value=\"ko-KR\" /></ThemeInfo></Default><Hwp><ThemeInfo><UxXmlName value=\"Hancom2016\" /></ThemeInfo></Hwp><Hword><ThemeInfo><UxXmlName value=\"Hancom2016\" /></ThemeInfo></Hword><Other value=\"custom\" /></root>";
            File.WriteAllText(config, original, new UTF8Encoding(false));
            var engine = new LanguageSwitch(config, args[0], Path.Combine(root, "backup"));
            engine.CheckClosed = () => {};
            Assert(engine.GetCulture("Hwp") == "ko-KR (inherited)", "Initial inherited language");
            Assert(engine.Apply(new[] { "Hwp" }, "en-US"), "English apply should change language");
            Assert(engine.GetCulture("Hwp") == "en-US", "HWP English preference");
            Assert(engine.GetCulture("Hword") == "ko-KR (inherited)", "Hanword must be preserved");
            XmlDocument d = LanguageSwitch.ReadXml(config);
            Assert(((XmlElement)d.SelectSingleNode("/root/Default/ThemeInfo/CultureName")).GetAttribute("value") == "ko-KR", "Global language must be preserved");
            Assert(Read(config).Contains("preserve me") && Read(config).Contains("Hancom2016"), "Preserve comments and UI theme");
            Assert(Read(Path.Combine(root, "backup", "ThemeInfo.original.xml")) == original, "Original backup must be exact");
            Assert(!engine.Apply(new[] { "Hwp" }, "en-US"), "Repeated apply should be idempotent");
            // Changes made later by the user must survive language rollback.
            d.DocumentElement.SetAttribute("userChangedLater", "yes"); d.Save(config);
            Assert(engine.Apply(new[] { "Hwp", "Hword" }, "ko-KR"), "Both applications to Korean");
            Assert(engine.Apply(new[] { "Hwp", "Hword" }, "en-US"), "Both applications to English");
            Assert(engine.Restore(new[] { "Hwp", "Hword" }), "Restore original language nodes");
            d = LanguageSwitch.ReadXml(config);
            Assert(d.SelectSingleNode("/root/Hwp/ThemeInfo/CultureName") == null, "Remove originally missing override");
            Assert(d.SelectSingleNode("/root/Hword/ThemeInfo/CultureName") == null, "Remove originally missing Hanword override");
            Assert(d.DocumentElement.GetAttribute("userChangedLater") == "yes", "Restore must preserve later unrelated settings");
            Assert(!engine.Restore(new[] { "Hwp" }), "Repeated restore should be idempotent");
            byte[] before = File.ReadAllBytes(config);
            engine.CheckClosed = () => { throw new InvalidOperationException("App busy"); };
            Reject(() => engine.Apply(new[] { "Hwp" }, "en-US"), "Running application must block changes");
            Assert(before.SequenceEqual(File.ReadAllBytes(config)), "Busy check must leave file untouched");
            engine.CheckClosed = () => {};
            Reject(() => engine.Apply(new[] { "Hwp" }, "fr-FR"), "Reject unsupported language");
            Reject(() => engine.Apply(new[] { "../Hwp" }, "en-US"), "Reject arbitrary application names");
            var missing = new LanguageSwitch(config, Path.Combine(root, "missing"), Path.Combine(root, "backup"));
            missing.CheckClosed = () => {};
            Reject(() => missing.Apply(new[] { "Hwp" }, "en-US"), "Missing language resource must block changes");
            Assert(before.SequenceEqual(File.ReadAllBytes(config)), "Invalid input must leave file untouched");
            File.AppendAllText(Path.Combine(root, "backup", "ThemeInfo.original.xml"), "corrupt");
            Reject(() => engine.Restore(new[] { "Hwp" }), "Corrupt backup must be refused");
            // Existing per-app overrides must return to their exact original meaning.
            string second = Path.Combine(root, "second"); Directory.CreateDirectory(second);
            string cfg2 = Path.Combine(second, "ThemeInfo.xml");
            File.WriteAllText(cfg2, "<root><Default><ThemeInfo><CultureName value=\"ko-KR\" /></ThemeInfo></Default><Hwp><ThemeInfo><CultureName value=\"ko-KR\" custom=\"retain\" /></ThemeInfo></Hwp></root>");
            var e2 = new LanguageSwitch(cfg2, args[0], Path.Combine(second, "backup")); e2.CheckClosed = () => {};
            e2.Apply(new[] { "Hwp" }, "en-US"); e2.Restore(new[] { "Hwp" });
            var node = (XmlElement)LanguageSwitch.ReadXml(cfg2).SelectSingleNode("/root/Hwp/ThemeInfo/CultureName");
            Assert(node.GetAttribute("value") == "ko-KR" && node.GetAttribute("custom") == "retain", "Restore original override attributes");
            // Detect a concurrent write before the atomic replacement.
            int closedChecks = 0;
            e2.CheckClosed = () => { if (++closedChecks == 2) File.AppendAllText(cfg2, "<!--concurrent-->"); };
            Reject(() => e2.Apply(new[] { "Hwp" }, "en-US"), "Concurrent configuration change must block replacement");
            Assert(Read(cfg2).Contains("concurrent") && e2.GetCulture("Hwp") == "ko-KR", "Concurrent user update must survive");
            Assert(!Directory.GetFiles(second, "*.tmp").Any(), "Temporary files cleaned after failure");
            File.WriteAllText(cfg2, "<!DOCTYPE root [<!ENTITY xxe SYSTEM 'file:///C:/Windows/win.ini'>]><root>&xxe;</root>");
            Reject(() => LanguageSwitch.ReadXml(cfg2), "External entities must be prohibited");
            File.WriteAllText(cfg2, "<root><broken>");
            Reject(() => e2.Apply(new[] { "Hwp" }, "en-US"), "Malformed XML must be refused");
            Console.WriteLine("PASS: " + assertions + " assertions. Fixtures: " + root); return 0;
        } catch (Exception e) { Console.Error.WriteLine(e); return 1; }
    }
}
