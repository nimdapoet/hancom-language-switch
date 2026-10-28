using System;
using System.Drawing;
using System.Windows.Forms;
using HancomLanguageSwitch;
class Preview
{
    [STAThread]
    static void Main(string[] args)
    {
        Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false);
        using (var form = new SwitchForm(new LanguageSwitch(args[0], args[1], args[2])))
        using (var bitmap = new Bitmap(form.Width, form.Height)) {
            form.StartPosition = FormStartPosition.Manual; form.Location = new Point(-30000, -30000); form.ShowInTaskbar = false;
            form.Show(); Application.DoEvents(); form.PerformLayout();
            form.DrawToBitmap(bitmap, new Rectangle(0, 0, form.Width, form.Height));
            bitmap.Save(args[3], System.Drawing.Imaging.ImageFormat.Png);
        }
    }
}
