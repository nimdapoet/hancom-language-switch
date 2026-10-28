using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Xml;

namespace HancomLanguageSwitch
{
    public sealed class LanguageSwitch
    {
        public readonly string ConfigPath;
        public readonly string BinPath;
        public readonly string BackupDirectory;
        public Action CheckClosed = EnsureHancomClosed;
        static readonly string[] AllowedApps = { "Hwp", "Hword" };

        public LanguageSwitch(string configPath, string binPath, string backupDirectory)
        {
            ConfigPath = Path.GetFullPath(configPath);
            BinPath = Path.GetFullPath(binPath);
            BackupDirectory = Path.GetFullPath(backupDirectory);
        }

        public static void EnsureHancomClosed()
        {
            foreach (string name in new[] { "Hwp", "Hword", "HCell", "HShow", "HConfig", "Hpdf", "HncOffice", "HncStart" })
                foreach (Process process in Process.GetProcessesByName(name))
                    using (process)
                        throw new InvalidOperationException("Save your documents and close " + name + " before changing language. No application will be closed automatically.");
        }

        public static XmlDocument ReadXml(string path)
        {
            var settings = new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null };
            var doc = new XmlDocument { PreserveWhitespace = true, XmlResolver = null };
            using (XmlReader reader = XmlReader.Create(path, settings)) doc.Load(reader);
            if (doc.DocumentElement == null || doc.DocumentElement.Name != "root")
                throw new InvalidDataException("Expected a Hancom ThemeInfo.xml document with a <root> element.");
            return doc;
        }

        static void ValidateApps(string[] apps)
        {
            if (apps == null || apps.Length == 0 || apps.Distinct().Count() != apps.Length || apps.Any(a => !AllowedApps.Contains(a)))
                throw new ArgumentException("Select Hangul (Hwp), Hanword (Hword), or both.");
        }

        public void ValidateResources(string[] apps, string culture)
        {
            ValidateApps(apps);
            if (culture != "en-US" && culture != "ko-KR") throw new ArgumentException("Only en-US and ko-KR are supported.");
            foreach (string app in apps)
            {
                string exe = Path.Combine(BinPath, app + ".exe");
                string resource = Path.Combine(BinPath, app + ".String." + culture + ".dll");
                if (!File.Exists(exe) || !File.Exists(resource))
                    throw new FileNotFoundException("The selected application or its " + culture + " resources are missing: " + resource);
                var a = FileVersionInfo.GetVersionInfo(exe);
                var b = FileVersionInfo.GetVersionInfo(resource);
                if (a.FileMajorPart != 12 || a.FileVersion != b.FileVersion)
                    throw new InvalidOperationException("This tool requires Hancom 2022 (12.x) and matching language resource versions for " + app + ".");
            }
        }

        void ValidateConfig()
        {
            if (!File.Exists(ConfigPath)) throw new FileNotFoundException("Run Hancom Office 2022 once to create its user configuration: " + ConfigPath);
            if (!string.Equals(Path.GetFileName(ConfigPath), "ThemeInfo.xml", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Only ThemeInfo.xml can be changed.");
            // Refuse the installation's shared ThemeInfo.xml. Test fixtures may use another directory.
            if (ConfigPath.StartsWith(BinPath + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Use the per-user ThemeInfo.xml, not the installation configuration.");
            RejectReparsePoints(ConfigPath);
            RejectReparsePoints(BackupDirectory);
            ReadXml(ConfigPath);
        }

        static void RejectReparsePoints(string path)
        {
            for (string p = path; !string.IsNullOrEmpty(p); p = Path.GetDirectoryName(p))
                if ((File.Exists(p) || Directory.Exists(p)) && (File.GetAttributes(p) & FileAttributes.ReparsePoint) != 0)
                    throw new IOException("Linked files or folders are not supported: " + p);
        }

        public string GetCulture(string app)
        {
            ValidateApps(new[] { app });
            XmlDocument doc = ReadXml(ConfigPath);
            XmlElement node = doc.SelectSingleNode("/root/" + app + "/ThemeInfo/CultureName") as XmlElement;
            if (node != null && node.HasAttribute("value")) return node.GetAttribute("value");
            node = doc.SelectSingleNode("/root/Default/ThemeInfo/CultureName") as XmlElement;
            return node == null ? "(application default)" : node.GetAttribute("value") + " (inherited)";
        }

        static XmlElement EnsureElement(XmlDocument doc, XmlElement parent, string name)
        {
            XmlElement element = parent.SelectSingleNode(name) as XmlElement;
            if (element == null) { element = doc.CreateElement(name); parent.AppendChild(element); }
            return element;
        }

        static void SetCulture(XmlDocument doc, string app, string culture)
        {
            XmlElement appNode = EnsureElement(doc, doc.DocumentElement, app);
            XmlElement theme = EnsureElement(doc, appNode, "ThemeInfo");
            EnsureElement(doc, theme, "CultureName").SetAttribute("value", culture);
        }

        static string Hash(byte[] bytes)
        {
            using (SHA256 sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", "");
        }

        string BackupPath { get { return Path.Combine(BackupDirectory, "ThemeInfo.original.xml"); } }
        string ManifestPath { get { return Path.Combine(BackupDirectory, "backup.xml"); } }

        void CreateOrValidateBackup(byte[] original)
        {
            Directory.CreateDirectory(BackupDirectory);
            if (File.Exists(BackupPath) || File.Exists(ManifestPath)) { LoadBackup(); return; }
            File.WriteAllBytes(BackupPath, original);
            var doc = new XmlDocument();
            XmlElement root = doc.CreateElement("root"); doc.AppendChild(root);
            root.SetAttribute("configPath", ConfigPath);
            root.SetAttribute("sha256", Hash(original));
            root.SetAttribute("createdUtc", DateTime.UtcNow.ToString("o"));
            doc.Save(ManifestPath);
        }

        XmlDocument LoadBackup()
        {
            if (!File.Exists(BackupPath) || !File.Exists(ManifestPath)) throw new FileNotFoundException("No complete original backup is available.");
            RejectReparsePoints(BackupDirectory);
            RejectReparsePoints(BackupPath);
            RejectReparsePoints(ManifestPath);
            XmlDocument manifest = ReadXml(ManifestPath);
            if (!string.Equals(manifest.DocumentElement.GetAttribute("configPath"), ConfigPath, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("The backup belongs to another configuration file.");
            byte[] bytes = File.ReadAllBytes(BackupPath);
            if (Hash(bytes) != manifest.DocumentElement.GetAttribute("sha256")) throw new InvalidDataException("The original backup failed its integrity check.");
            return ReadXml(BackupPath);
        }

        void WriteChange(XmlDocument doc, byte[] before)
        {
            // Keep a full snapshot before each operation. Revert merges language settings only.
            string snapshot = Path.Combine(BackupDirectory, "ThemeInfo.before-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fffffff") + "-" + Guid.NewGuid().ToString("N") + ".xml");
            File.WriteAllBytes(snapshot, before);
            string temp = ConfigPath + ".language-" + Guid.NewGuid().ToString("N") + ".tmp";
            try {
                using (XmlWriter writer = XmlWriter.Create(temp, new XmlWriterSettings { Encoding = new UTF8Encoding(false), Indent = false })) doc.Save(writer);
                ReadXml(temp);
                CheckClosed();
                if (!File.ReadAllBytes(ConfigPath).SequenceEqual(before))
                    throw new IOException("Hancom's configuration changed during the operation. No change was applied; please retry after closing Hancom.");
                // Same-volume atomic replacement; never truncate the live XML in place.
                File.Replace(temp, ConfigPath, null);
            }
            finally { if (File.Exists(temp)) File.Delete(temp); }
        }

        public bool Apply(string[] apps, string culture)
        {
            ValidateResources(apps, culture);
            CheckClosed();
            ValidateConfig();
            byte[] before = File.ReadAllBytes(ConfigPath);
            XmlDocument doc = ReadXml(ConfigPath);
            bool changed = false;
            foreach (string app in apps) {
                var node = doc.SelectSingleNode("/root/" + app + "/ThemeInfo/CultureName") as XmlElement;
                if (node == null || node.GetAttribute("value") != culture) { SetCulture(doc, app, culture); changed = true; }
            }
            if (!changed) return false;
            CreateOrValidateBackup(before);
            WriteChange(doc, before);
            return true;
        }

        public bool Restore(string[] apps)
        {
            ValidateApps(apps);
            CheckClosed();
            ValidateConfig();
            XmlDocument original = LoadBackup();
            byte[] before = File.ReadAllBytes(ConfigPath);
            XmlDocument current = ReadXml(ConfigPath);
            bool changed = false;
            foreach (string app in apps) {
                string xpath = "/root/" + app + "/ThemeInfo/CultureName";
                XmlElement old = original.SelectSingleNode(xpath) as XmlElement;
                XmlElement now = current.SelectSingleNode(xpath) as XmlElement;
                if (old == null) {
                    if (now != null) { now.ParentNode.RemoveChild(now); changed = true; }
                } else if (now == null || now.OuterXml != old.OuterXml) {
                    XmlElement theme = EnsureElement(current, EnsureElement(current, current.DocumentElement, app), "ThemeInfo");
                    if (now != null) theme.RemoveChild(now);
                    theme.AppendChild(current.ImportNode(old, true)); changed = true;
                }
            }
            if (changed) WriteChange(current, before);
            return changed;
        }
    }
}
