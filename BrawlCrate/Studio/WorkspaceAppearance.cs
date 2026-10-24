using System;
using System.IO;
using System.Configuration;
using System.Drawing;
using System.Collections.Generic;
using System.Windows.Forms;

namespace BrawlCrate.Studio
{
    // Presentation preferences never enter a fighter document or a native pose.
    internal static class WorkspaceAppearance
    {
        internal static readonly int[] TextSizes = { 100, 125, 150, 175, 200 };
        private static int _percent = ReadPercent();
        private static readonly Dictionary<string,Font> Fonts = new Dictionary<string,Font>();
        internal static int TextPercent => _percent;
        internal static float Factor => _percent / 100f;
        internal static Font Font
        {
            get { return GetFont(false,false); }
        }
        internal static Font HeadingFont => GetFont(true,true);
        internal static Font BoldFont => GetFont(false,true);
        private static Font GetFont(bool heading,bool bold)
        {
            string key=_percent+":"+heading+":"+bold;
            if(!Fonts.TryGetValue(key,out var font))Fonts.Add(key,font=new Font("Segoe UI",(heading?11f:9f)*Factor,bold?FontStyle.Bold:FontStyle.Regular));
            return font;
        }
        internal static int Pixels(int logical) => (int)Math.Ceiling(logical * Factor);
        private static int ReadPercent()
        {
            if (Environment.GetEnvironmentVariable("FIGHTERPORT_CAPTURE_CONTROL") != null) return 100;
            int value = Properties.Settings.Default.WorkspaceTextPercent;
            return Array.IndexOf(TextSizes, value) >= 0 ? value : 100;
        }
        internal static string SetTextPercent(int percent)
        {
            if (Array.IndexOf(TextSizes, percent) < 0) throw new ArgumentOutOfRangeException(nameof(percent));
            if (_percent == percent) return null;
            _percent = percent;
            // Cache fonts for the five presentation sizes for this process.
            if (Environment.GetEnvironmentVariable("FIGHTERPORT_CAPTURE_CONTROL") == null)
            {
                Properties.Settings.Default.WorkspaceTextPercent = percent;
                try { Properties.Settings.Default.Save(); }
                catch(Exception error) when(error is ConfigurationErrorsException || error is IOException || error is UnauthorizedAccessException)
                { return "Workspace text changed for this session. Could not save the preference: "+error.Message; }
            }
            return null;
        }
        internal static void SizeDialog(Form dialog)
        {
            float scale = Font.Size / dialog.Font.Size;
            dialog.AutoScaleMode = AutoScaleMode.None;
            dialog.Font = Font;
            dialog.Scale(new SizeF(scale, scale));
            var area = Screen.FromControl(dialog.Owner ?? dialog).WorkingArea;
            dialog.AutoScroll = true;
            dialog.ClientSize = new Size(Math.Min(dialog.ClientSize.Width, area.Width - 48),
                Math.Min(dialog.ClientSize.Height, area.Height - 80));
        }
    }

    // Preserve ordinary Button accessibility, keyboard and disabled behavior;
    // paint disabled captions with readable contrast on the dark workspace.
    internal sealed class WorkspaceButton : Button
    {
        protected override void OnGotFocus(EventArgs args) { base.OnGotFocus(args); Invalidate(); }
        protected override void OnLostFocus(EventArgs args) { base.OnLostFocus(args); Invalidate(); }
        protected override void OnPaint(PaintEventArgs args)
        {
            Color surface = Enabled && (Focused || ClientRectangle.Contains(PointToClient(Cursor.Position)))
                ? Color.FromArgb(49, 60, 76) : Color.FromArgb(32, 34, 40);
            using (var brush = new SolidBrush(surface)) args.Graphics.FillRectangle(brush, ClientRectangle);
            using (var pen = new Pen(Color.FromArgb(115, 124, 140)))
                args.Graphics.DrawRectangle(pen, 0, 0, Math.Max(0, Width - 1), Math.Max(0, Height - 1));
            TextRenderer.DrawText(args.Graphics, Text, Font, Rectangle.Inflate(ClientRectangle, -6, -2),
                Enabled ? Color.FromArgb(233, 237, 244) : Color.FromArgb(164, 173, 188),
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
            if (Focused) ControlPaint.DrawFocusRectangle(args.Graphics, Rectangle.Inflate(ClientRectangle, -4, -4));
        }
    }
}
