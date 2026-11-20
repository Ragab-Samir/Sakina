// سكينة (Sakina) - رفيق أذكار خفيف يعمل في System Tray
// C# 5 / .NET Framework 4.x (موجود في ويندوز 10 و11 افتراضياً) - لا يحتاج تثبيت أي شيء.
// البناء: شغّل build.bat

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.IO;
using System.Media;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;
using Microsoft.Win32;
using WTimer = System.Windows.Forms.Timer;

namespace Sakina
{
    // ------------------------------------------------------------------ ويندوز
    static class Native
    {
        [StructLayout(LayoutKind.Sequential)]
        struct LASTINPUTINFO { public uint cbSize; public uint dwTime; }

        [DllImport("user32.dll")] static extern bool GetLastInputInfo(ref LASTINPUTINFO p);
        [DllImport("shell32.dll")] static extern int SHQueryUserNotificationState(out int state);
        [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int val, int size);
        [DllImport("kernel32.dll")] static extern bool SetProcessWorkingSetSize(IntPtr h, IntPtr min, IntPtr max);
        [DllImport("user32.dll")] public static extern bool DestroyIcon(IntPtr h);

        public static double IdleSeconds()
        {
            try
            {
                LASTINPUTINFO i = new LASTINPUTINFO();
                i.cbSize = (uint)Marshal.SizeOf(typeof(LASTINPUTINFO));
                if (!GetLastInputInfo(ref i)) return 0;
                uint now = unchecked((uint)Environment.TickCount);
                return unchecked(now - i.dwTime) / 1000.0;
            }
            catch { return 0; }
        }

        // تطبيق بملء الشاشة / لعبة / عرض تقديمي
        public static bool IsBusyFullscreen()
        {
            try
            {
                int st;
                int hr = SHQueryUserNotificationState(out st);
                return hr == 0 && (st == 2 || st == 3 || st == 4);
            }
            catch { return false; }
        }

        public static void RoundCorners(IntPtr hwnd)
        {
            try { int pref = 2; DwmSetWindowAttribute(hwnd, 33, ref pref, 4); } catch { }
        }

        // تقليل استهلاك الرام بعد كل نشاط
        public static void TrimMemory()
        {
            try
            {
                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();
                using (Process p = Process.GetCurrentProcess())
                    SetProcessWorkingSetSize(p.Handle, (IntPtr)(-1), (IntPtr)(-1));
            }
            catch { }
        }

        public static float Scale()
        {
            try { using (Graphics g = Graphics.FromHwnd(IntPtr.Zero)) return g.DpiX / 96f; }
            catch { return 1f; }
        }

        public static void SetAutostart(bool on)
        {
            try
            {
                using (RegistryKey k = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", true))
                {
                    if (k == null) return;
                    if (on) k.SetValue("Sakina", "\"" + Application.ExecutablePath + "\"");
                    else k.DeleteValue("Sakina", false);
                }
            }
            catch { }
        }

        public static GraphicsPath Round(Rectangle r, int rad)
        {
            GraphicsPath p = new GraphicsPath();
            int d = rad * 2;
            p.AddArc(r.X, r.Y, d, d, 180, 90);
            p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            p.CloseFigure();
            return p;
        }

        public static void Crescent(Graphics g, RectangleF r, Color c)
        {
            using (GraphicsPath p1 = new GraphicsPath())
            using (GraphicsPath p2 = new GraphicsPath())
            {
                p1.AddEllipse(r);
                p2.AddEllipse(r.X + r.Width * 0.30f, r.Y + r.Height * 0.07f, r.Width * 0.86f, r.Height * 0.86f);
                using (Region rg = new Region(p1))
                using (SolidBrush b = new SolidBrush(c))
                {
                    rg.Exclude(p2);
                    g.FillRegion(b, rg);
                }
            }
        }

        public static Color H(string hex) { return ColorTranslator.FromHtml(hex); }
    }

    // ------------------------------------------------------------------ الثيم
    class Theme
    {
        public Color C1, C2, Fg, Accent, OnAccent, Sub, Bg, Ctl, Line;

        public static Theme Get(string name)
        {
            if (name == "light")
            {
                Theme l = new Theme();
                l.C1 = Native.H("#FBF8EF"); l.C2 = Native.H("#EFE8D4");
                l.Fg = Native.H("#1E2B27"); l.Accent = Native.H("#8F6F1E"); l.OnAccent = Native.H("#FFFFFF");
                l.Sub = Native.H("#5E6C66"); l.Bg = Native.H("#F6F2E6"); l.Ctl = Native.H("#FFFFFF"); l.Line = Native.H("#DDD5BE");
                return l;
            }
            Theme d = new Theme();
            d.C1 = Native.H("#10231F"); d.C2 = Native.H("#1B3B34");
            d.Fg = Native.H("#F3EEDC"); d.Accent = Native.H("#D6B66B"); d.OnAccent = Native.H("#10231F");
            d.Sub = Native.H("#9DB5AC"); d.Bg = Native.H("#0D1B18"); d.Ctl = Native.H("#16302A"); d.Line = Native.H("#24453D");
            return d;
        }
    }

    // ------------------------------------------------------------------ الإعدادات
    class Settings
    {
        public bool Paused;
        public int IntervalMin = 30;
        public bool AutoPause = true;
        public bool Pomodoro = true;
        public int PomodoroMin = 45;
        public string Theme = "dark";
        public int OpacityPct = 92;
        public string Position = "bottom-right";
        public int DurationSec = 8;
        public bool Sound = false;
        public bool Autostart = false;
        public List<string[]> Custom = new List<string[]>();   // {cat, text}

        static string Dir
        {
            get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Sakina"); }
        }

        public Settings Clone()
        {
            Settings s = (Settings)MemberwiseClone();
            s.Custom = new List<string[]>(Custom);
            return s;
        }

        static int I(Dictionary<string, string> d, string k, int def, int min, int max)
        {
            string v; int n;
            if (d.TryGetValue(k, out v) && int.TryParse(v, out n)) return Math.Max(min, Math.Min(max, n));
            return def;
        }
        static bool B(Dictionary<string, string> d, string k, bool def)
        {
            string v;
            if (d.TryGetValue(k, out v)) return v == "1";
            return def;
        }

        public static Settings Load()
        {
            Settings s = new Settings();
            try
            {
                string f = Path.Combine(Dir, "settings.ini");
                if (File.Exists(f))
                {
                    Dictionary<string, string> d = new Dictionary<string, string>();
                    foreach (string line in File.ReadAllLines(f, Encoding.UTF8))
                    {
                        int i = line.IndexOf('=');
                        if (i > 0) d[line.Substring(0, i).Trim()] = line.Substring(i + 1).Trim();
                    }
                    s.Paused = B(d, "paused", false);
                    s.IntervalMin = I(d, "interval", 30, 1, 720);
                    s.AutoPause = B(d, "autopause", true);
                    s.Pomodoro = B(d, "pomodoro", true);
                    s.PomodoroMin = I(d, "pomodoro_min", 45, 10, 180);
                    string v;
                    if (d.TryGetValue("theme", out v) && (v == "light" || v == "dark")) s.Theme = v;
                    s.OpacityPct = I(d, "opacity", 92, 30, 100);
                    if (d.TryGetValue("position", out v)) s.Position = v;
                    s.DurationSec = I(d, "duration", 8, 3, 60);
                    s.Sound = B(d, "sound", false);
                    s.Autostart = B(d, "autostart", false);
                }
                string cf = Path.Combine(Dir, "custom.txt");
                if (File.Exists(cf))
                {
                    foreach (string line in File.ReadAllLines(cf, Encoding.UTF8))
                    {
                        int i = line.IndexOf('|');
                        if (i > 0 && i < line.Length - 1)
                            s.Custom.Add(new string[] { line.Substring(0, i), line.Substring(i + 1) });
                    }
                }
            }
            catch { }
            return s;
        }

        public void Save()
        {
            try
            {
                Directory.CreateDirectory(Dir);
                StringBuilder sb = new StringBuilder();
                sb.AppendLine("paused=" + (Paused ? "1" : "0"));
                sb.AppendLine("interval=" + IntervalMin);
                sb.AppendLine("autopause=" + (AutoPause ? "1" : "0"));
                sb.AppendLine("pomodoro=" + (Pomodoro ? "1" : "0"));
                sb.AppendLine("pomodoro_min=" + PomodoroMin);
                sb.AppendLine("theme=" + Theme);
                sb.AppendLine("opacity=" + OpacityPct);
                sb.AppendLine("position=" + Position);
                sb.AppendLine("duration=" + DurationSec);
                sb.AppendLine("sound=" + (Sound ? "1" : "0"));
                sb.AppendLine("autostart=" + (Autostart ? "1" : "0"));
                File.WriteAllText(Path.Combine(Dir, "settings.ini"), sb.ToString(), Encoding.UTF8);

                StringBuilder cb = new StringBuilder();
                foreach (string[] c in Custom)
                    cb.AppendLine(c[0] + "|" + c[1].Replace("\r", " ").Replace("\n", " "));
                File.WriteAllText(Path.Combine(Dir, "custom.txt"), cb.ToString(), Encoding.UTF8);
            }
            catch { }
        }
    }

    // ------------------------------------------------------------------ المكتبة
    static class Library
    {
        static readonly Random Rnd = new Random();

        static readonly string[][] Builtin = new string[][]
        {
            new string[]{"morning", "أَصْبَحْنَا وَأَصْبَحَ الْمُلْكُ لِلَّهِ، وَالْحَمْدُ لِلَّهِ، لَا إِلَهَ إِلَّا اللَّهُ وَحْدَهُ لَا شَرِيكَ لَهُ"},
            new string[]{"morning", "اللَّهُمَّ بِكَ أَصْبَحْنَا، وَبِكَ أَمْسَيْنَا، وَبِكَ نَحْيَا، وَبِكَ نَمُوتُ، وَإِلَيْكَ النُّشُورُ"},
            new string[]{"morning", "سُبْحَانَ اللَّهِ وَبِحَمْدِهِ"},
            new string[]{"morning", "اللَّهُمَّ إِنِّي أَسْأَلُكَ عِلْمًا نَافِعًا وَرِزْقًا طَيِّبًا وَعَمَلًا مُتَقَبَّلًا"},
            new string[]{"evening", "أَمْسَيْنَا وَأَمْسَى الْمُلْكُ لِلَّهِ، وَالْحَمْدُ لِلَّهِ، لَا إِلَهَ إِلَّا اللَّهُ وَحْدَهُ لَا شَرِيكَ لَهُ"},
            new string[]{"evening", "أَعُوذُ بِكَلِمَاتِ اللَّهِ التَّامَّاتِ مِنْ شَرِّ مَا خَلَقَ"},
            new string[]{"evening", "بِسْمِ اللَّهِ الَّذِي لَا يَضُرُّ مَعَ اسْمِهِ شَيْءٌ فِي الْأَرْضِ وَلَا فِي السَّمَاءِ وَهُوَ السَّمِيعُ الْعَلِيمُ"},
            new string[]{"friday", "اللَّهُمَّ صَلِّ وَسَلِّمْ عَلَى نَبِيِّنَا مُحَمَّدٍ ﷺ، وَأَكْثِرْ مِنَ الصَّلَاةِ عَلَيْهِ يَوْمَ الْجُمُعَةِ"},
            new string[]{"friday", "تذكير لطيف: لا تنسَ قراءة سورة الكهف يوم الجمعة"},
            new string[]{"friday", "في يوم الجمعة ساعة إجابة، فأكثر من الدعاء"},
            new string[]{"general", "لَا حَوْلَ وَلَا قُوَّةَ إِلَّا بِاللَّهِ"},
            new string[]{"general", "أَسْتَغْفِرُ اللَّهَ وَأَتُوبُ إِلَيْهِ"},
            new string[]{"general", "سُبْحَانَ اللَّهِ وَبِحَمْدِهِ، سُبْحَانَ اللَّهِ الْعَظِيمِ"},
            new string[]{"general", "سُبْحَانَ اللَّهِ، وَالْحَمْدُ لِلَّهِ، وَلَا إِلَهَ إِلَّا اللَّهُ، وَاللَّهُ أَكْبَرُ"},
            new string[]{"general", "رَبِّ اشْرَحْ لِي صَدْرِي وَيَسِّرْ لِي أَمْرِي"},
            new string[]{"general", "اللَّهُمَّ أَعِنِّي عَلَى ذِكْرِكَ وَشُكْرِكَ وَحُسْنِ عِبَادَتِكَ"},
            new string[]{"general", "رَبَّنَا آتِنَا فِي الدُّنْيَا حَسَنَةً وَفِي الْآخِرَةِ حَسَنَةً وَقِنَا عَذَابَ النَّارِ"},
            new string[]{"general", "يَا مُقَلِّبَ الْقُلُوبِ ثَبِّتْ قَلْبِي عَلَى دِينِكَ"},
            new string[]{"general", "حَسْبِيَ اللَّهُ لَا إِلَهَ إِلَّا هُوَ عَلَيْهِ تَوَكَّلْتُ وَهُوَ رَبُّ الْعَرْشِ الْعَظِيمِ"},
            new string[]{"general", "لَا إِلَهَ إِلَّا أَنْتَ سُبْحَانَكَ إِنِّي كُنْتُ مِنَ الظَّالِمِينَ"},
            new string[]{"general", "اللَّهُمَّ صَلِّ وَسَلِّمْ عَلَى نَبِيِّنَا مُحَمَّدٍ ﷺ"},
        };

        static readonly string[] PomoDuas = new string[]
        {
            "رَبِّ زِدْنِي عِلْمًا",
            "رَبِّ اشْرَحْ لِي صَدْرِي وَيَسِّرْ لِي أَمْرِي",
            "اللَّهُمَّ إِنِّي أَسْأَلُكَ عِلْمًا نَافِعًا وَرِزْقًا طَيِّبًا وَعَمَلًا مُتَقَبَّلًا",
        };

        public const string PomoTip = "أرح عينيك: انظر إلى شيء بعيد لعشرين ثانية، وعدّل جلستك، ومدّد ظهرك قليلاً.";

        public static string TitleOf(string cat)
        {
            switch (cat)
            {
                case "morning": return "أذكار الصباح";
                case "evening": return "أذكار المساء";
                case "friday": return "سنن يوم الجمعة";
                default: return "لحظة سكينة";
            }
        }

        public static string PomoDua() { return PomoDuas[Rnd.Next(PomoDuas.Length)]; }

        public static string[] Pick(Settings s, string last)
        {
            DateTime n = DateTime.Now;
            bool fri = n.DayOfWeek == DayOfWeek.Friday;
            bool morn = n.Hour >= 4 && n.Hour < 11;
            bool eve = n.Hour >= 15 && n.Hour < 20;

            List<string[]> all = new List<string[]>(Builtin);
            all.AddRange(s.Custom);
            List<string[]> general = new List<string[]>();
            List<string[]> specific = new List<string[]>();
            foreach (string[] it in all)
            {
                if (it[0] == "general") general.Add(it);
                else if ((it[0] == "friday" && fri) || (it[0] == "morning" && morn) || (it[0] == "evening" && eve))
                    specific.Add(it);
            }
            List<string[]> pool = (specific.Count > 0 && Rnd.Next(100) < 60) ? specific : general;
            if (pool.Count == 0) pool = all;
            List<string[]> fresh = pool.FindAll(delegate (string[] x) { return x[1] != last; });
            if (fresh.Count > 0) pool = fresh;
            return pool[Rnd.Next(pool.Count)];
        }
    }

    // ------------------------------------------------------------------ نغمة هادئة
    static class Chime
    {
        static SoundPlayer player;

        public static void Play()
        {
            try
            {
                int rate = 22050; double dur = 1.0;
                int n = (int)(rate * dur);
                MemoryStream ms = new MemoryStream();
                BinaryWriter w = new BinaryWriter(ms);
                w.Write(Encoding.ASCII.GetBytes("RIFF")); w.Write(36 + n * 2);
                w.Write(Encoding.ASCII.GetBytes("WAVEfmt ")); w.Write(16);
                w.Write((short)1); w.Write((short)1); w.Write(rate); w.Write(rate * 2); w.Write((short)2); w.Write((short)16);
                w.Write(Encoding.ASCII.GetBytes("data")); w.Write(n * 2);
                for (int i = 0; i < n; i++)
                {
                    double t = i / (double)rate;
                    double env = Math.Exp(-4.0 * t) * Math.Min(1.0, t * 40);
                    double v = 0.5 * Math.Sin(2 * Math.PI * 660 * t) + 0.25 * Math.Sin(2 * Math.PI * 990 * t);
                    w.Write((short)(v * env * 3200));
                }
                w.Flush();
                ms.Position = 0;
                if (player != null) player.Dispose();
                player = new SoundPlayer(ms);
                player.Play();
            }
            catch { }
        }
    }

    // ------------------------------------------------------------------ البطاقة العائمة
    class CardForm : Form
    {
        readonly string title, body, extra;
        readonly Theme th;
        readonly float sc;
        readonly Font fTitle, fBody, fExtra;
        readonly StringFormat fmt;
        readonly int pad, rad;
        readonly double target;
        readonly bool sound;
        const int InMs = 350, OutMs = 450;
        int holdMs, outStart, endAt;
        DateTime t0;
        WTimer anim;
        bool hover;
        double lastOp = -1;
        Rectangle titleRect, lineRect, bodyRect, extraRect, barRect;

        static string FindFont(string[] names, string fallback)
        {
            try
            {
                using (InstalledFontCollection ifc = new InstalledFontCollection())
                {
                    foreach (string n in names)
                        foreach (FontFamily f in ifc.Families)
                            if (string.Equals(f.Name, n, StringComparison.OrdinalIgnoreCase)) return f.Name;
                }
            }
            catch { }
            return fallback;
        }

        public CardForm(Settings s, string title, string body, string extra)
        {
            this.title = title; this.body = body; this.extra = extra;
            th = Theme.Get(s.Theme);
            sc = Native.Scale();
            pad = (int)(22 * sc);
            rad = (int)(10 * sc);
            target = s.OpacityPct / 100.0;
            sound = s.Sound;
            holdMs = s.DurationSec * 1000;
            outStart = InMs + holdMs;
            endAt = outStart + OutMs;

            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual;
            DoubleBuffered = true;
            BackColor = th.C1;
            Opacity = 0;

            string bodyName = FindFont(new string[] { "Amiri", "Sakkal Majalla", "Traditional Arabic" }, "Segoe UI");
            float bodySize = bodyName == "Segoe UI" ? 14f : (bodyName == "Amiri" ? 16f : 20f);
            fTitle = new Font("Segoe UI", 10f, FontStyle.Bold);
            fBody = new Font(bodyName, bodySize, FontStyle.Regular);
            fExtra = new Font("Segoe UI", 9.5f, FontStyle.Regular);
            fmt = new StringFormat(StringFormat.GenericTypographic);
            fmt.FormatFlags |= StringFormatFlags.DirectionRightToLeft;
            fmt.Alignment = StringAlignment.Near;       // Near = اليمين في الاتجاه RTL
            fmt.LineAlignment = StringAlignment.Near;

            int width = (int)(410 * sc);
            int innerW = width - 2 * pad;
            int y = pad;
            using (Graphics g = Graphics.FromHwnd(IntPtr.Zero))
            {
                g.TextRenderingHint = TextRenderingHint.AntiAlias;
                int titleH = (int)Math.Ceiling(g.MeasureString(title, fTitle, innerW, fmt).Height);
                titleRect = new Rectangle(pad, y, innerW, titleH);
                y += titleH + (int)(8 * sc);
                lineRect = new Rectangle(pad, y, innerW, Math.Max(1, (int)sc));
                y += lineRect.Height + (int)(14 * sc);
                int bodyH = (int)Math.Ceiling(g.MeasureString(body, fBody, innerW, fmt).Height) + (int)(4 * sc);
                bodyRect = new Rectangle(pad, y, innerW, bodyH);
                y += bodyH;
                if (!string.IsNullOrEmpty(extra))
                {
                    y += (int)(10 * sc);
                    int exH = (int)Math.Ceiling(g.MeasureString(extra, fExtra, innerW, fmt).Height);
                    extraRect = new Rectangle(pad, y, innerW, exH);
                    y += exH;
                }
            }
            y += pad;
            ClientSize = new Size(width, y);
            barRect = new Rectangle(pad, y - (int)(4 * sc), innerW, Math.Max(2, (int)(2 * sc)));

            Rectangle wa = Screen.PrimaryScreen.WorkingArea;
            int m = (int)(20 * sc);
            int x = s.Position.EndsWith("right") ? wa.Right - Width - m : wa.Left + m;
            int yy = s.Position.StartsWith("bottom") ? wa.Bottom - Height - m : wa.Top + m;
            Location = new Point(x, yy);
        }

        protected override bool ShowWithoutActivation { get { return true; } }

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.ExStyle |= 0x08000000 | 0x00000080 | 0x00000008;   // NOACTIVATE | TOOLWINDOW | TOPMOST
                return cp;
            }
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == 0x0021) { m.Result = (IntPtr)3; return; }   // MA_NOACTIVATE
            base.WndProc(ref m);
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            Native.RoundCorners(Handle);
            try
            {
                using (GraphicsPath p = Native.Round(new Rectangle(0, 0, Width, Height), rad))
                    Region = new Region(p);
            }
            catch { }
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            if (sound) Chime.Play();
            t0 = DateTime.UtcNow;
            anim = new WTimer();
            anim.Interval = 33;
            anim.Tick += OnAnim;
            anim.Start();
        }

        void OnAnim(object sender, EventArgs e)
        {
            int t = (int)(DateTime.UtcNow - t0).TotalMilliseconds;
            if (hover && t >= InMs && t < outStart)
            {
                t0 = t0.AddMilliseconds(anim.Interval);   // المؤشر فوق البطاقة: لا تختفِ
                t = (int)(DateTime.UtcNow - t0).TotalMilliseconds;
            }
            if (t >= endAt) { anim.Stop(); Close(); return; }
            double op;
            if (t < InMs) op = target * t / InMs;
            else if (t < outStart) op = target;
            else op = target * (1.0 - (t - outStart) / (double)OutMs);
            op = Math.Max(0, Math.Min(1, op));
            if (Math.Abs(op - lastOp) > 0.004) { Opacity = op; lastOp = op; }
            Invalidate(barRect);
        }

        protected override void OnMouseEnter(EventArgs e) { hover = true; base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hover = false; base.OnMouseLeave(e); }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            int t = (int)(DateTime.UtcNow - t0).TotalMilliseconds;
            if (t < outStart) { outStart = Math.Max(t, 0); endAt = outStart + OutMs; }
            base.OnMouseDown(e);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.AntiAlias;
            Rectangle rc = ClientRectangle;

            using (LinearGradientBrush bg = new LinearGradientBrush(rc, th.C1, th.C2, 125f))
                g.FillRectangle(bg, rc);

            Rectangle br = new Rectangle(0, 0, rc.Width - 1, rc.Height - 1);
            using (GraphicsPath bp = Native.Round(br, rad))
            using (Pen pen = new Pen(Color.FromArgb(90, th.Accent), 1f))
                g.DrawPath(pen, bp);

            float cs = 16 * sc;
            Native.Crescent(g, new RectangleF(pad, titleRect.Y + (titleRect.Height - cs) / 2f, cs, cs), th.Accent);

            using (SolidBrush ab = new SolidBrush(th.Accent))
                g.DrawString(title, fTitle, ab, titleRect, fmt);

            using (LinearGradientBrush lb = new LinearGradientBrush(lineRect, Color.FromArgb(0, th.Accent), Color.FromArgb(170, th.Accent), 0f))
                g.FillRectangle(lb, lineRect);

            using (SolidBrush fb = new SolidBrush(th.Fg))
                g.DrawString(body, fBody, fb, bodyRect, fmt);

            if (!string.IsNullOrEmpty(extra))
                using (SolidBrush sb = new SolidBrush(th.Sub))
                    g.DrawString(extra, fExtra, sb, extraRect, fmt);

            // شريط تقدم رفيع يوضح الوقت المتبقي
            int t = t0 == default(DateTime) ? 0 : (int)(DateTime.UtcNow - t0).TotalMilliseconds;
            double frac = 1.0 - Math.Max(0, Math.Min(1, (t - InMs) / (double)Math.Max(1, outStart - InMs)));
            if (t < InMs) frac = 1;
            int w = (int)(barRect.Width * frac);
            if (w > 0)
                using (SolidBrush pb = new SolidBrush(Color.FromArgb(110, th.Accent)))
                    g.FillRectangle(pb, barRect.Right - w, barRect.Y, w, barRect.Height);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (anim != null) { anim.Stop(); anim.Dispose(); }
                fTitle.Dispose(); fBody.Dispose(); fExtra.Dispose(); fmt.Dispose();
            }
            base.Dispose(disposing);
        }
    }

    // ------------------------------------------------------------------ مفتاح تبديل (Toggle)
    class Toggle : Control
    {
        bool on;
        readonly Theme th;
        public event EventHandler CheckedChanged;

        public Toggle(Theme th, float sc)
        {
            this.th = th;
            Size = new Size((int)(44 * sc), (int)(24 * sc));
            Cursor = Cursors.Hand;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
        }

        public bool Checked
        {
            get { return on; }
            set
            {
                if (on == value) return;
                on = value; Invalidate();
                if (CheckedChanged != null) CheckedChanged(this, EventArgs.Empty);
            }
        }

        protected override void OnClick(EventArgs e) { Checked = !on; base.OnClick(e); }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.Clear(Parent != null ? Parent.BackColor : th.Bg);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            Rectangle r = new Rectangle(0, 0, Width - 1, Height - 1);
            using (GraphicsPath p = Native.Round(r, r.Height / 2))
            using (SolidBrush b = new SolidBrush(on ? th.Accent : th.Line))
                g.FillPath(b, p);
            int d = Height - 8;
            int x = on ? Width - d - 4 : 4;
            using (SolidBrush k = new SolidBrush(on ? th.OnAccent : th.Sub))
                g.FillEllipse(k, x, 4, d, d);
        }
    }

    // ------------------------------------------------------------------ نافذة الإعدادات
    class SettingsForm : Form
    {
        readonly App app;
        readonly Theme th;
        readonly float sc;
        readonly Settings cfg;
        readonly Panel[] pages = new Panel[3];
        readonly Label[] tabs = new Label[3];
        Panel underline;
        NumericUpDown nInterval, nPomo, nDur;
        Toggle tAuto, tPomo, tStart, tSound;
        ComboBox cbTheme, cbPos, cbCat;
        TrackBar tbOp;
        ListBox lb;
        TextBox tx;
        List<string[]> custom;

        static readonly string[] PosKeys = { "bottom-right", "bottom-left", "top-right", "top-left" };
        static readonly string[] PosNames = { "أسفل اليمين", "أسفل اليسار", "أعلى اليمين", "أعلى اليسار" };
        static readonly string[] CatKeys = { "general", "morning", "evening", "friday" };
        static readonly string[] CatNames = { "أدعية وأذكار عامة", "أذكار الصباح", "أذكار المساء", "سنن يوم الجمعة" };

        int S(int v) { return (int)Math.Round(v * sc); }

        public SettingsForm(App app)
        {
            this.app = app;
            cfg = app.Cfg;
            th = Theme.Get(cfg.Theme);
            sc = Native.Scale();
            custom = new List<string[]>(cfg.Custom);

            Text = "سكينة — الإعدادات";
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false; MinimizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = th.Bg;
            ForeColor = th.Fg;
            Font = new Font("Segoe UI", 10f);
            ClientSize = new Size(S(480), S(600));
            RightToLeft = RightToLeft.No;

            BuildHeader();
            BuildTabs();
            BuildPage1();
            BuildPage2();
            BuildPage3();
            BuildFooter();
            SelectTab(0);
        }

        // --- أدوات مساعدة
        Label Lbl(Control parent, string text, float size, Color color, FontStyle st)
        {
            Label l = new Label();
            l.Text = text; l.Font = new Font("Segoe UI", size, st); l.ForeColor = color; l.BackColor = Color.Transparent;
            l.RightToLeft = RightToLeft.Yes;
            l.AutoSize = false;
            l.Size = l.GetPreferredSize(Size.Empty);
            parent.Controls.Add(l);
            return l;
        }

        Button Btn(string text, bool primary)
        {
            Button b = new Button();
            b.Text = text; b.FlatStyle = FlatStyle.Flat; b.Cursor = Cursors.Hand;
            b.FlatAppearance.BorderSize = primary ? 0 : 1;
            b.FlatAppearance.BorderColor = th.Line;
            b.BackColor = primary ? th.Accent : th.Ctl;
            b.ForeColor = primary ? th.OnAccent : th.Fg;
            b.Font = new Font("Segoe UI", 10f, FontStyle.Bold);
            b.Size = new Size(S(110), S(36));
            b.UseVisualStyleBackColor = false;
            return b;
        }

        NumericUpDown Num(int min, int max, int val)
        {
            NumericUpDown n = new NumericUpDown();
            n.Minimum = min; n.Maximum = max; n.Value = Math.Max(min, Math.Min(max, val));
            n.BackColor = th.Ctl; n.ForeColor = th.Fg; n.BorderStyle = BorderStyle.FixedSingle;
            n.TextAlign = HorizontalAlignment.Center;
            n.Width = S(84);
            return n;
        }

        ComboBox Combo(string[] items, int sel, int width)
        {
            ComboBox c = new ComboBox();
            c.DropDownStyle = ComboBoxStyle.DropDownList; c.FlatStyle = FlatStyle.Flat;
            c.BackColor = th.Ctl; c.ForeColor = th.Fg; c.RightToLeft = RightToLeft.Yes;
            c.Items.AddRange(items);
            c.SelectedIndex = Math.Max(0, sel);
            c.Width = S(width);
            return c;
        }

        Toggle Tog(bool val)
        {
            Toggle t = new Toggle(th, sc);
            t.Checked = val;
            return t;
        }

        Panel NewPage()
        {
            Panel p = new Panel();
            p.BackColor = th.Bg;
            p.Bounds = new Rectangle(0, S(132), ClientSize.Width, S(404));
            Controls.Add(p);
            return p;
        }

        int Row(Panel pg, int y, string title, string desc, Control ctl, bool last)
        {
            int rowH = desc != null ? S(64) : S(52);
            int right = pg.Width - S(24);
            Label t = Lbl(pg, title, 10.5f, th.Fg, FontStyle.Regular);
            int ty = desc != null ? y + S(10) : y + (rowH - t.Height) / 2;
            t.Location = new Point(right - t.Width, ty);
            if (desc != null)
            {
                Label d = Lbl(pg, desc, 8.5f, th.Sub, FontStyle.Regular);
                d.Location = new Point(right - d.Width, ty + t.Height + S(2));
            }
            if (ctl != null)
            {
                pg.Controls.Add(ctl);
                ctl.Location = new Point(S(24), y + (rowH - ctl.Height) / 2);
            }
            if (!last)
            {
                Panel ln = new Panel();
                ln.BackColor = th.Line;
                ln.Bounds = new Rectangle(S(24), y + rowH - 1, pg.Width - S(48), 1);
                pg.Controls.Add(ln);
            }
            return y + rowH;
        }

        // --- الواجهة
        void BuildHeader()
        {
            Panel h = new Panel();
            h.Bounds = new Rectangle(0, 0, ClientSize.Width, S(88));
            h.Paint += delegate (object s, PaintEventArgs e)
            {
                Graphics g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                using (LinearGradientBrush b = new LinearGradientBrush(h.ClientRectangle, Native.H("#10231F"), Native.H("#1E4339"), 20f))
                    g.FillRectangle(b, h.ClientRectangle);
                Native.Crescent(g, new RectangleF(h.Width - S(24) - S(34), S(18), S(34), S(34)), Native.H("#D6B66B"));
                StringFormat f = new StringFormat(StringFormat.GenericTypographic);
                f.FormatFlags |= StringFormatFlags.DirectionRightToLeft;
                g.TextRenderingHint = TextRenderingHint.AntiAlias;
                using (Font f1 = new Font("Segoe UI", 18f, FontStyle.Bold))
                using (Font f2 = new Font("Segoe UI", 9.5f))
                using (SolidBrush w = new SolidBrush(Native.H("#F3EEDC")))
                using (SolidBrush m = new SolidBrush(Native.H("#9DB5AC")))
                {
                    g.DrawString("سكينة", f1, w, new RectangleF(0, S(14), h.Width - S(24) - S(46), S(40)), f);
                    g.DrawString("رفيق أذكار هادئ لا يقطع تركيزك", f2, m, new RectangleF(0, S(54), h.Width - S(24), S(24)), f);
                }
                f.Dispose();
            };
            Controls.Add(h);
        }

        void BuildTabs()
        {
            string[] names = { "التذكير", "المظهر", "المكتبة" };
            int w = ClientSize.Width / 3;
            Panel bar = new Panel();
            bar.BackColor = th.Bg;
            bar.Bounds = new Rectangle(0, S(88), ClientSize.Width, S(44));
            Controls.Add(bar);
            for (int i = 0; i < 3; i++)
            {
                int idx = i;
                Label l = new Label();
                l.Text = names[i]; l.Font = new Font("Segoe UI", 10.5f, FontStyle.Bold);
                l.TextAlign = ContentAlignment.MiddleCenter; l.Cursor = Cursors.Hand;
                l.Bounds = new Rectangle(ClientSize.Width - (i + 1) * w, 0, w, S(42));
                l.BackColor = th.Bg;
                l.Click += delegate { SelectTab(idx); };
                bar.Controls.Add(l);
                tabs[i] = l;
            }
            underline = new Panel();
            underline.BackColor = th.Accent;
            underline.Height = S(3);
            underline.Width = w - S(40);
            bar.Controls.Add(underline);
            underline.BringToFront();
            Panel sep = new Panel();
            sep.BackColor = th.Line;
            sep.Bounds = new Rectangle(0, S(43), ClientSize.Width, 1);
            bar.Controls.Add(sep);
        }

        void SelectTab(int i)
        {
            for (int k = 0; k < 3; k++)
            {
                if (pages[k] != null) pages[k].Visible = (k == i);
                tabs[k].ForeColor = (k == i) ? th.Accent : th.Sub;
            }
            underline.Location = new Point(tabs[i].Left + S(20), S(40));
        }

        void BuildPage1()
        {
            Panel p = pages[0] = NewPage();
            nInterval = Num(1, 720, cfg.IntervalMin);
            tAuto = Tog(cfg.AutoPause);
            tPomo = Tog(cfg.Pomodoro);
            nPomo = Num(10, 180, cfg.PomodoroMin);
            tStart = Tog(cfg.Autostart);
            int y = S(6);
            y = Row(p, y, "التذكير كل (دقيقة)", "فترة هادئة بين كل ذكر وآخر", nInterval, false);
            y = Row(p, y, "الوضع الذكي", "تأجيل التذكير مع الألعاب والفيديو بملء الشاشة", tAuto, false);
            y = Row(p, y, "ربط التذكير ببومودورو", "دعاء مع تذكير براحة العين بعد عمل متواصل", tPomo, false);
            y = Row(p, y, "مدة العمل المتواصل (دقيقة)", null, nPomo, false);
            y = Row(p, y, "التشغيل مع بدء ويندوز", null, tStart, true);
        }

        void BuildPage2()
        {
            Panel p = pages[1] = NewPage();
            cbTheme = Combo(new string[] { "داكن", "فاتح" }, cfg.Theme == "light" ? 1 : 0, 110);
            tbOp = new TrackBar();
            tbOp.Minimum = 30; tbOp.Maximum = 100; tbOp.Value = Math.Max(30, Math.Min(100, cfg.OpacityPct));
            tbOp.TickStyle = TickStyle.None; tbOp.Width = S(170); tbOp.BackColor = th.Bg; tbOp.AutoSize = false; tbOp.Height = S(30);
            cbPos = Combo(PosNames, Array.IndexOf(PosKeys, cfg.Position), 130);
            nDur = Num(3, 60, cfg.DurationSec);
            tSound = Tog(cfg.Sound);
            int y = S(6);
            y = Row(p, y, "الثيم", null, cbTheme, false);
            y = Row(p, y, "شفافية البطاقة", null, tbOp, false);
            y = Row(p, y, "مكان البطاقة على الشاشة", null, cbPos, false);
            y = Row(p, y, "مدة الظهور (ثانية)", null, nDur, false);
            y = Row(p, y, "نغمة هادئة جداً", "وإلا يكتفي بالتنبيه البصري", tSound, true);
            Button pv = Btn("معاينة", false);
            pv.Location = new Point(p.Width - S(24) - pv.Width, y + S(14));
            pv.Click += delegate
            {
                Settings t = Collect();
                new CardForm(t, "معاينة", "سُبْحَانَ اللَّهِ وَبِحَمْدِهِ", null).Show();
            };
            p.Controls.Add(pv);
        }

        void BuildPage3()
        {
            Panel p = pages[2] = NewPage();
            int right = p.Width - S(24);
            Label hd = Lbl(p, "أذكارك وأدعيتك الشخصية (تُضاف إلى المكتبة الجاهزة)", 9.5f, th.Sub, FontStyle.Regular);
            hd.Location = new Point(right - hd.Width, S(10));

            lb = new ListBox();
            lb.BorderStyle = BorderStyle.None; lb.BackColor = th.Ctl; lb.ForeColor = th.Fg;
            lb.RightToLeft = RightToLeft.Yes; lb.Font = new Font("Segoe UI", 10f);
            lb.Bounds = new Rectangle(S(24), S(38), p.Width - S(48), S(140));
            p.Controls.Add(lb);
            RefreshList();

            Button del = Btn("حذف المحدد", false);
            del.Location = new Point(S(24), S(184));
            del.Click += delegate
            {
                int i = lb.SelectedIndex;
                if (i >= 0 && i < custom.Count) { custom.RemoveAt(i); RefreshList(); }
            };
            p.Controls.Add(del);

            cbCat = Combo(CatNames, 0, 170);
            cbCat.Location = new Point(right - cbCat.Width, S(232));
            p.Controls.Add(cbCat);

            tx = new TextBox();
            tx.Multiline = true; tx.BorderStyle = BorderStyle.FixedSingle;
            tx.BackColor = th.Ctl; tx.ForeColor = th.Fg; tx.RightToLeft = RightToLeft.Yes;
            tx.Font = new Font("Segoe UI", 11f);
            tx.Bounds = new Rectangle(S(24), S(266), p.Width - S(48), S(64));
            p.Controls.Add(tx);

            Button add = Btn("إضافة", true);
            add.Location = new Point(right - add.Width, S(340));
            add.Click += delegate
            {
                string t = tx.Text.Trim();
                if (t.Length == 0) return;
                custom.Add(new string[] { CatKeys[Math.Max(0, cbCat.SelectedIndex)], t });
                tx.Text = "";
                RefreshList();
            };
            p.Controls.Add(add);
        }

        void RefreshList()
        {
            lb.Items.Clear();
            foreach (string[] c in custom)
            {
                int k = Array.IndexOf(CatKeys, c[0]);
                lb.Items.Add(c[1] + "  [" + CatNames[Math.Max(0, k)] + "]");
            }
        }

        void BuildFooter()
        {
            Panel f = new Panel();
            f.BackColor = th.Bg;
            f.Bounds = new Rectangle(0, S(536), ClientSize.Width, S(64));
            Controls.Add(f);
            Panel sep = new Panel();
            sep.BackColor = th.Line; sep.Bounds = new Rectangle(0, 0, f.Width, 1);
            f.Controls.Add(sep);
            Button save = Btn("حفظ", true);
            save.Location = new Point(f.Width - S(24) - save.Width, S(14));
            save.Click += delegate
            {
                app.ApplySettings(Collect());
                Close();
            };
            Button cancel = Btn("إلغاء", false);
            cancel.Location = new Point(save.Left - S(10) - cancel.Width, S(14));
            cancel.Click += delegate { Close(); };
            f.Controls.Add(save);
            f.Controls.Add(cancel);
        }

        Settings Collect()
        {
            Settings s = cfg.Clone();
            s.IntervalMin = (int)nInterval.Value;
            s.AutoPause = tAuto.Checked;
            s.Pomodoro = tPomo.Checked;
            s.PomodoroMin = (int)nPomo.Value;
            s.Autostart = tStart.Checked;
            s.Theme = cbTheme.SelectedIndex == 1 ? "light" : "dark";
            s.OpacityPct = tbOp.Value;
            s.Position = PosKeys[Math.Max(0, cbPos.SelectedIndex)];
            s.DurationSec = (int)nDur.Value;
            s.Sound = tSound.Checked;
            s.Custom = new List<string[]>(custom);
            return s;
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            base.OnFormClosed(e);
            app.SettingsClosed();
        }
    }

    // ------------------------------------------------------------------ التطبيق
    class App : ApplicationContext
    {
        public Settings Cfg;
        readonly NotifyIcon tray;
        readonly WTimer tick;
        readonly ToolStripMenuItem pauseItem;
        CardForm card;
        SettingsForm settings;
        int workSecs;
        DateTime lastReminder = DateTime.Now;
        string lastText;

        public App()
        {
            Cfg = Settings.Load();

            ContextMenuStrip menu = new ContextMenuStrip();
            menu.RightToLeft = RightToLeft.Yes;
            menu.ShowImageMargin = false;
            menu.Font = new Font("Segoe UI", 10f);
            menu.Items.Add("الإعدادات", null, delegate { OpenSettings(); });
            menu.Items.Add("اعرض ذكراً الآن", null, delegate { ShowDhikr(); });
            pauseItem = new ToolStripMenuItem("إيقاف مؤقت");
            pauseItem.Checked = Cfg.Paused;
            pauseItem.Click += delegate
            {
                Cfg.Paused = !Cfg.Paused;
                pauseItem.Checked = Cfg.Paused;
                Cfg.Save();
            };
            menu.Items.Add(pauseItem);
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("خروج", null, delegate { Quit(); });

            tray = new NotifyIcon();
            tray.Text = "سكينة";
            tray.Icon = BuildIcon();
            tray.ContextMenuStrip = menu;
            tray.MouseClick += delegate (object s, MouseEventArgs e)
            {
                if (e.Button == MouseButtons.Left) OpenSettings();
            };
            tray.Visible = true;

            tick = new WTimer();
            tick.Interval = 5000;      // كل 5 ثوانٍ فقط: استهلاك معالج شبه صفري
            tick.Tick += OnTick;
            tick.Start();

            Native.TrimMemory();
        }

        static Icon BuildIcon()
        {
            using (Bitmap bmp = new Bitmap(32, 32))
            {
                using (Graphics g = Graphics.FromImage(bmp))
                {
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    using (SolidBrush b = new SolidBrush(Native.H("#14302A"))) g.FillEllipse(b, 1, 1, 30, 30);
                    using (Pen p = new Pen(Native.H("#D6B66B"), 2f)) g.DrawEllipse(p, 2, 2, 27, 27);
                    Native.Crescent(g, new RectangleF(8, 7, 17, 17), Native.H("#D6B66B"));
                }
                IntPtr h = bmp.GetHicon();
                try { return (Icon)Icon.FromHandle(h).Clone(); }
                finally { Native.DestroyIcon(h); }
            }
        }

        void OnTick(object sender, EventArgs e)
        {
            if (Cfg.Paused) return;
            double idle = Native.IdleSeconds();
            bool busy = Cfg.AutoPause && Native.IsBusyFullscreen();

            if (idle > 300) workSecs = 0;
            else if (idle < 120 && !busy) workSecs += 5;
            if (busy) return;

            DateTime now = DateTime.Now;
            if (Cfg.Pomodoro && workSecs >= Cfg.PomodoroMin * 60)
            {
                Show("استراحة قصيرة", Library.PomoDua(), Library.PomoTip);
                workSecs = 0;
                lastReminder = now;
            }
            else if ((now - lastReminder).TotalMinutes >= Cfg.IntervalMin && idle < 600)
            {
                ShowDhikr();
                lastReminder = now;
            }
        }

        void ShowDhikr()
        {
            string[] it = Library.Pick(Cfg, lastText);
            lastText = it[1];
            Show(Library.TitleOf(it[0]), it[1], null);
        }

        void Show(string title, string body, string extra)
        {
            try
            {
                if (card != null && !card.IsDisposed) card.Close();
                card = new CardForm(Cfg, title, body, extra);
                card.FormClosed += delegate { card = null; Native.TrimMemory(); };
                card.Show();
            }
            catch { }
        }

        void OpenSettings()
        {
            if (settings != null && !settings.IsDisposed) { settings.Activate(); return; }
            settings = new SettingsForm(this);
            settings.Show();
            settings.Activate();
        }

        public void SettingsClosed() { settings = null; Native.TrimMemory(); }

        public void ApplySettings(Settings s)
        {
            Cfg = s;
            Cfg.Save();
            Native.SetAutostart(Cfg.Autostart);
            pauseItem.Checked = Cfg.Paused;
        }

        void Quit()
        {
            tick.Stop();
            tray.Visible = false;
            tray.Dispose();
            ExitThread();
        }
    }

    static class Program
    {
        [STAThread]
        static void Main()
        {
            bool created;
            using (System.Threading.Mutex m = new System.Threading.Mutex(true, "Sakina_SingleInstance_Mutex", out created))
            {
                if (!created) return;
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.Run(new App());
                GC.KeepAlive(m);
            }
        }
    }
}
