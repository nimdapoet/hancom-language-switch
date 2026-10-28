using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using Microsoft.Win32;

namespace HancomLanguageSwitch
{
    static class Program
    {
        public static string FindBin()
        {
            using (RegistryKey root = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry32))
            using (RegistryKey key = root.OpenSubKey(@"SOFTWARE\HNC\Shared\Common12.0")) {
                string value = key == null ? null : key.GetValue("Common Path") as string;
                if (value != null && File.Exists(Path.Combine(value, "Hwp.exe"))) return value;
            }
            string fallback = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), @"Hnc\Office 2022\HOffice120\Bin");
            return fallback;
        }

        [STAThread]
        static int Main(string[] args)
        {
            try {
                string config = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), @"HNC\User\Common\120\ThemeInfo.xml");
                string backup = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"HancomLanguageSwitch\2022");
                LanguageSwitch engine = new LanguageSwitch(config, FindBin(), backup);
                // Headless modes are for reproducible verification; the default opens the GUI.
                if (args.Length > 0) {
                    string[] apps = new[] { "Hwp" };
                    if (args.Length > 1) apps = args[1].Split(',');
                    if (args[0] == "--english") engine.Apply(apps, "en-US");
                    else if (args[0] == "--korean") engine.Apply(apps, "ko-KR");
                    else if (args[0] == "--restore") engine.Restore(apps);
                    else if (args[0] != "--status") throw new ArgumentException("Supported: --status, --english, --korean, --restore; optional second argument Hwp,Hword.");
                    foreach (string app in apps) Console.WriteLine(app + ": " + engine.GetCulture(app));
                    return 0;
                }
                Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false);
                Application.Run(new SwitchForm(engine)); return 0;
            } catch (Exception e) {
                if (args.Length > 0) Console.Error.WriteLine(e.Message);
                else MessageBox.Show(e.Message, "Hancom Language Switch", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return 1;
            }
        }
    }

    sealed class SwitchForm : Form
    {
        readonly LanguageSwitch engine;
        readonly CheckBox hwp = new CheckBox { Text = "Hangul / HWP (한글)", Checked = true, AutoSize = true };
        readonly CheckBox hword = new CheckBox { Text = "Hanword (한워드)", AutoSize = true };
        readonly Label status = new Label { AutoSize = true, MaximumSize = new Size(570, 0) };
        readonly Label result = new Label { AutoSize = true, MaximumSize = new Size(570, 0), ForeColor = Color.FromArgb(25, 90, 125) };
        public SwitchForm(LanguageSwitch engine)
        {
            this.engine = engine;
            Text = "Hancom 2022 — Language Switch"; Font = new Font("Segoe UI", 10);
            AutoScaleMode = AutoScaleMode.Dpi; ClientSize = new Size(650, 590);
            FormBorderStyle = FormBorderStyle.FixedDialog; MaximizeBox = false; StartPosition = FormStartPosition.CenterScreen;
            var layout = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, Padding = new Padding(22), AutoScroll = true };
            Controls.Add(layout);
            layout.Controls.Add(new Label { Text = "English interface for Hancom 2022", Font = new Font("Segoe UI", 17, FontStyle.Bold), AutoSize = true, Margin = new Padding(0, 0, 0, 12) });
            layout.Controls.Add(new Label { Text = "Uses the language resources already installed with Hancom.\nClose all Hancom applications before applying a change.", AutoSize = true, Margin = new Padding(0, 0, 0, 14) });
            layout.Controls.Add(hwp); layout.Controls.Add(hword);
            layout.Controls.Add(status);
            var buttons = new FlowLayoutPanel { AutoSize = true, Margin = new Padding(0, 16, 0, 10) };
            buttons.Controls.Add(Button("Use English", () => Apply("en-US")));
            buttons.Controls.Add(Button("Use Korean", () => Apply("ko-KR")));
            buttons.Controls.Add(Button("Restore original", Restore)); layout.Controls.Add(buttons);
            var launches = new FlowLayoutPanel { AutoSize = true, Margin = new Padding(0, 0, 0, 14) };
            launches.Controls.Add(Button("Open Hangul", () => Launch("Hwp")));
            launches.Controls.Add(Button("Open Hanword", () => Launch("Hword"))); layout.Controls.Add(launches);
            layout.Controls.Add(result);
            layout.Controls.Add(new Label { Text = "Backup: " + engine.BackupDirectory + "\n\nThis is an independent utility, not an official Hancom language pack.\nSome dialogs may remain Korean. Your document files are not edited.", AutoSize = true, MaximumSize = new Size(570, 0), ForeColor = Color.DimGray, Margin = new Padding(0, 12, 0, 0) });
            RefreshStatus();
        }
        Button Button(string text, Action action)
        {
            var b = new Button { Text = text, AutoSize = true, Padding = new Padding(10, 4, 10, 4) };
            b.Click += (s,e) => {
                try { action(); RefreshStatus(); }
                catch (Exception ex) { result.Text = ex.Message; result.ForeColor = Color.Firebrick; }
            };
            return b;
        }
        string[] SelectedApps()
        {
            return new[] { hwp.Checked ? "Hwp" : null, hword.Checked ? "Hword" : null }.Where(a => a != null).ToArray();
        }
        void Apply(string culture)
        {
            bool changed = engine.Apply(SelectedApps(), culture);
            result.ForeColor = Color.FromArgb(25,90,125);
            result.Text = changed ? "Language preference saved. Open the selected application to verify its interface." : "The selected language preference is already set.";
        }
        void Restore()
        {
            bool changed = engine.Restore(SelectedApps());
            result.ForeColor = Color.FromArgb(25,90,125);
            result.Text = changed ? "Original language preference restored. Other settings were preserved." : "Original language preference is already in effect.";
        }
        void RefreshStatus()
        {
            try { status.Text = "Current preference:\nHangul: " + engine.GetCulture("Hwp") + "\nHanword: " + engine.GetCulture("Hword"); }
            catch (Exception e) { status.Text = e.Message; }
        }
        void Launch(string app)
        {
            engine.ValidateResources(new[] { app }, "en-US");
            Process.Start(new ProcessStartInfo(Path.Combine(engine.BinPath, app + ".exe")) { WorkingDirectory = engine.BinPath, UseShellExecute = false });
        }
    }
}
